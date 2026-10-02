using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Infrastructure.Bgg;

public class BggXmlApiClient : IBggClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly RateLimiter _rateLimiter;
    private readonly bool _ownsHttpClient;
    private readonly BggOptions _options;
    private readonly IBggRawSnapshotRepository? _snapshotRepo;
    private readonly ILogger<BggXmlApiClient>? _logger;

    public BggXmlApiClient(
        HttpClient? httpClient = null,
        IOptions<BggOptions>? options = null,
        IBggRawSnapshotRepository? snapshotRepo = null,
        ILogger<BggXmlApiClient>? logger = null)
    {
        _ownsHttpClient = httpClient == null;
        _httpClient = httpClient ?? new HttpClient();
        _options = options?.Value ?? new BggOptions();
        _snapshotRepo = snapshotRepo;
        _logger = logger;

        var bearerToken = _options.BearerToken;
        if (!string.IsNullOrWhiteSpace(bearerToken) && _httpClient.DefaultRequestHeaders.Authorization == null)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken.Trim());
        }

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        }

        // Limitar a máximo 2 peticiones por segundo para cortesía hacia los servidores de BGG
        _rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 2,
            QueueLimit = 10,
            TokensPerPeriod = 2,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true
        });
    }

    public Task<string?> FetchRawThingXmlAsync(int bggId, CancellationToken ct = default)
    {
        if (bggId <= 0) return Task.FromResult<string?>(null);
        return FetchRawThingsXmlAsync([bggId], ct);
    }

    public async Task<string?> FetchRawThingsXmlAsync(IEnumerable<int> bggIds, CancellationToken ct = default)
    {
        if (bggIds == null) return null;

        var validIds = bggIds.Where(id => id > 0).Distinct().Take(20).ToList();
        if (validIds.Count == 0) return null;

        string idsParam = string.Join(",", validIds);
        string url = $"https://boardgamegeek.com/xmlapi2/thing?id={idsParam}&stats=1";

        int maxRetries = 3;
        int delayMs = 1500;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            using var lease = await _rateLimiter.AcquireAsync(1, ct);
            if (!lease.IsAcquired)
            {
                await Task.Delay(500, ct);
                continue;
            }

            try
            {
                using var response = await _httpClient.GetAsync(url, ct);

                // Si BGG responde 202 (Accepted), la petición ha sido encolada para procesamiento interno
                if (response.StatusCode == HttpStatusCode.Accepted)
                {
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(delayMs * attempt, ct);
                        continue;
                    }
                    return null;
                }

                if (response.StatusCode == (HttpStatusCode)429 || response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    if (attempt < maxRetries)
                    {
                        int waitSec = BggResilienceAndAuthHandler.ExtractRetryAfterSeconds(response) ?? ((delayMs * 2 * attempt) / 1000);
                        await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, waitSec)), ct);
                        continue;
                    }
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new HttpRequestException("401 Unauthorized: BGG requiere autenticación mediante Application Token (Bearer). Consulta https://boardgamegeek.com/applications y configura 'Bgg:ApiToken'.", null, HttpStatusCode.Unauthorized);
                }

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                string xmlContent = await response.Content.ReadAsStringAsync(ct);
                if (_snapshotRepo != null && !string.IsNullOrWhiteSpace(xmlContent))
                {
                    await TryPersistSnapshotsAsync(validIds, xmlContent, ct);
                }

                return xmlContent;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw;
            }
            catch (HttpRequestException) when (attempt < maxRetries)
            {
                await Task.Delay(delayMs, ct);
            }
            catch (Exception)
            {
                return null;
            }
        }

        return null;
    }

    private async Task TryPersistSnapshotsAsync(IEnumerable<int> requestedIds, string xmlContent, CancellationToken ct)
    {
        if (_snapshotRepo == null || string.IsNullOrWhiteSpace(xmlContent)) return;

        try
        {
            var doc = XDocument.Parse(xmlContent);
            var items = doc.Root?.Elements("item")?.ToList();
            if (items == null) return;

            var foundIds = new HashSet<int>();

            foreach (var item in items)
            {
                if (int.TryParse(item.Attribute("id")?.Value, out int bggId) && bggId > 0)
                {
                    foundIds.Add(bggId);
                    string rawJson = BggXmlToJsonConverter.ConvertToJson(item);
                    var snapshot = new BggRawSnapshot(bggId, rawJson, apiVersion: 2, fetchedAt: DateTimeOffset.UtcNow);
                    await _snapshotRepo.UpsertAsync(snapshot, ct);
                }
            }

            // Registrar snapshots vacíos de control para IDs solicitados que BGG no devolvió (eliminados/privados)
            foreach (var reqId in requestedIds)
            {
                if (!foundIds.Contains(reqId) && reqId > 0)
                {
                    var placeholder = new BggRawSnapshot(reqId, "{\"notFound\":true}", apiVersion: 2, fetchedAt: DateTimeOffset.UtcNow);
                    await _snapshotRepo.UpsertAsync(placeholder, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error no bloqueante al auto-persistir snapshot de BGG: {Message}", ex.Message);
        }
    }

    public async Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default)
    {
        string? xmlContent = await FetchRawThingXmlAsync(bggId, ct);
        if (string.IsNullOrWhiteSpace(xmlContent)) return null;

        try
        {
            var doc = XDocument.Parse(xmlContent);
            var item = doc.Root?.Element("item");
            if (item == null) return null;

            return BggXmlParser.ParseItem(item);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public Game? ParseGameFromRawJson(string rawJson)
    {
        var item = BggJsonToXmlConverter.ConvertToItemElement(rawJson);
        if (item == null) return null;
        return BggXmlParser.ParseItem(item);
    }

    public Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(string username, CancellationToken ct = default)
    {
        return FetchUserCollectionAsync(username, null, ct);
    }

    public async Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(
        string username,
        IProgress<BggImportProgressReport>? progress,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username)) return [];

        progress?.Report(new BggImportProgressReport(BggImportPhase.Initializing, "Iniciando solicitud a BoardGameGeek..."));

        string url = $"https://boardgamegeek.com/xmlapi2/collection?username={Uri.EscapeDataString(username.Trim())}&stats=1";

        int maxRetries = Math.Max(1, _options.MaxPollingRetries);
        int timeoutSeconds = Math.Max(5, _options.PollingTimeoutSeconds);
        int initialDelaySeconds = Math.Max(1, _options.InitialPollingDelaySeconds);
        int[] delays = [initialDelaySeconds, 5, 8, 12, 15, 15];

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        int rateLimitAttempts = 0;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            if (stopwatch.Elapsed.TotalSeconds >= timeoutSeconds)
            {
                progress?.Report(new BggImportProgressReport(
                    BggImportPhase.Failed,
                    $"Tiempo límite de espera ({timeoutSeconds}s) excedido contactando con BGG.",
                    CurrentAttempt: attempt,
                    MaxAttempts: maxRetries));
                return [];
            }

            if (attempt == 1)
            {
                progress?.Report(new BggImportProgressReport(
                    BggImportPhase.RequestingBgg,
                    "Solicitando colección a BoardGameGeek...",
                    CurrentAttempt: 1,
                    MaxAttempts: maxRetries));
            }

            using var lease = await _rateLimiter.AcquireAsync(1, ct);
            if (!lease.IsAcquired)
            {
                await Task.Delay(500, ct);
                continue;
            }

            try
            {
                using var response = await _httpClient.GetAsync(url, ct);

                // 1. Si BGG responde 202 (Accepted), la colección está encolada para generarse en caché
                if (response.StatusCode == HttpStatusCode.Accepted)
                {
                    int delayIndex = Math.Min(attempt - 1, delays.Length - 1);
                    int waitSeconds = delays[delayIndex];
                    int remainingSeconds = (int)Math.Max(1, timeoutSeconds - stopwatch.Elapsed.TotalSeconds);
                    waitSeconds = Math.Min(waitSeconds, remainingSeconds);

                    progress?.Report(new BggImportProgressReport(
                        BggImportPhase.PreparingInBgg,
                        $"BGG está preparando tu colección en sus servidores (intento {attempt} de {maxRetries})...",
                        CurrentAttempt: attempt,
                        MaxAttempts: maxRetries,
                        WaitSeconds: waitSeconds));

                    if (attempt >= maxRetries || stopwatch.Elapsed.TotalSeconds + waitSeconds >= timeoutSeconds)
                    {
                        progress?.Report(new BggImportProgressReport(
                            BggImportPhase.Failed,
                            "BGG continúa preparando la colección. Puedes volver a intentarlo en unos instantes.",
                            CurrentAttempt: attempt,
                            MaxAttempts: maxRetries));
                        return [];
                    }

                    await Task.Delay(TimeSpan.FromSeconds(waitSeconds), ct);
                    continue;
                }

                // 2. Control de Rate Limit (HTTP 429 / 503)
                if (response.StatusCode == (HttpStatusCode)429 || response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    int waitSeconds = BggResilienceAndAuthHandler.ExtractRetryAfterSeconds(response) ?? (5 * (rateLimitAttempts + 1));
                    rateLimitAttempts++;

                    progress?.Report(new BggImportProgressReport(
                        BggImportPhase.RateLimitedWaiting,
                        $"BGG está temporalmente saturado (Rate limit {((int)response.StatusCode)}). Pausando {waitSeconds}s antes de reintentar...",
                        CurrentAttempt: attempt,
                        MaxAttempts: maxRetries,
                        WaitSeconds: waitSeconds));

                    if (rateLimitAttempts > _options.MaxRateLimitRetries || stopwatch.Elapsed.TotalSeconds + waitSeconds >= timeoutSeconds)
                    {
                        progress?.Report(new BggImportProgressReport(
                            BggImportPhase.Failed,
                            "Límite de peticiones de BGG alcanzado. Por favor, espera un minuto antes de reintentar.",
                            CurrentAttempt: attempt,
                            MaxAttempts: maxRetries));
                        return [];
                    }

                    await Task.Delay(TimeSpan.FromSeconds(waitSeconds), ct);
                    continue;
                }

                // 3. Autenticación fallida (HTTP 401)
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    progress?.Report(new BggImportProgressReport(
                        BggImportPhase.Failed,
                        "Error 401: BGG requiere autenticación mediante Application Token (Bearer)."));
                    throw new HttpRequestException("401 Unauthorized: BGG requiere autenticación mediante Application Token (Bearer). Consulta https://boardgamegeek.com/applications y configura 'Bgg:ApiToken'.", null, HttpStatusCode.Unauthorized);
                }

                if (!response.IsSuccessStatusCode)
                {
                    return [];
                }

                string xmlContent = await response.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(xmlContent)) return [];

                var doc = XDocument.Parse(xmlContent);

                // Comprobar si BGG devolvió 200 OK con un elemento <message> de encolado
                var messageEl = doc.Root?.Element("message");
                if (messageEl != null && messageEl.Value.Contains("accepted and will be processed", StringComparison.OrdinalIgnoreCase))
                {
                    int delayIndex = Math.Min(attempt - 1, delays.Length - 1);
                    int waitSeconds = delays[delayIndex];

                    progress?.Report(new BggImportProgressReport(
                        BggImportPhase.PreparingInBgg,
                        $"BGG está preparando tu colección en sus servidores (intento {attempt} de {maxRetries})...",
                        CurrentAttempt: attempt,
                        MaxAttempts: maxRetries,
                        WaitSeconds: waitSeconds));

                    if (attempt >= maxRetries) return [];
                    await Task.Delay(TimeSpan.FromSeconds(waitSeconds), ct);
                    continue;
                }

                var items = BggXmlParser.ParseCollection(doc);
                progress?.Report(new BggImportProgressReport(
                    BggImportPhase.ProcessingItems,
                    $"Colección descargada con éxito. Procesando {items.Count} juegos...",
                    CurrentAttempt: attempt,
                    MaxAttempts: maxRetries,
                    ItemsFound: items.Count));

                return items;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw;
            }
            catch (HttpRequestException) when (attempt < maxRetries)
            {
                await Task.Delay(TimeSpan.FromSeconds(initialDelaySeconds), ct);
            }
            catch (Exception)
            {
                return [];
            }
        }

        return [];
    }

    public async Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2) return [];

        string url = $"https://boardgamegeek.com/xmlapi2/search?query={Uri.EscapeDataString(query.Trim())}&type=boardgame";

        int maxRetries = 3;
        int delayMs = 1000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            using var lease = await _rateLimiter.AcquireAsync(1, ct);
            if (!lease.IsAcquired)
            {
                await Task.Delay(500, ct);
                continue;
            }

            try
            {
                using var response = await _httpClient.GetAsync(url, ct);

                if (response.StatusCode == (HttpStatusCode)429 || response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    if (attempt < maxRetries)
                    {
                        int waitSec = BggResilienceAndAuthHandler.ExtractRetryAfterSeconds(response) ?? ((delayMs * 2 * attempt) / 1000);
                        await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, waitSec)), ct);
                        continue;
                    }
                    return [];
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new HttpRequestException("401 Unauthorized: BGG requiere autenticación mediante Application Token (Bearer). Consulta https://boardgamegeek.com/applications y configura 'Bgg:ApiToken'.", null, HttpStatusCode.Unauthorized);
                }

                if (!response.IsSuccessStatusCode)
                {
                    return [];
                }

                string xmlContent = await response.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(xmlContent)) return [];

                var doc = XDocument.Parse(xmlContent);
                return BggXmlParser.ParseSearchResults(doc);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw;
            }
            catch (HttpRequestException) when (attempt < maxRetries)
            {
                await Task.Delay(delayMs, ct);
            }
            catch (Exception)
            {
                return [];
            }
        }

        return [];
    }

    public async Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default)
    {
        string url = "https://boardgamegeek.com/xmlapi2/hot?type=boardgame";
        int maxRetries = 3;
        int delayMs = 1500;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            using var lease = await _rateLimiter.AcquireAsync(1, ct);
            if (!lease.IsAcquired)
            {
                await Task.Delay(500, ct);
                continue;
            }

            try
            {
                using var response = await _httpClient.GetAsync(url, ct);

                if (response.StatusCode == (HttpStatusCode)429 || response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    if (attempt < maxRetries)
                    {
                        int waitSec = BggResilienceAndAuthHandler.ExtractRetryAfterSeconds(response) ?? ((delayMs * 2 * attempt) / 1000);
                        await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, waitSec)), ct);
                        continue;
                    }
                    return [];
                }

                if (!response.IsSuccessStatusCode)
                {
                    return [];
                }

                string xmlContent = await response.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(xmlContent)) return [];

                var doc = XDocument.Parse(xmlContent);
                var items = BggXmlParser.ParseHotGames(doc);
                return items.Take(limit).ToList();
            }
            catch (Exception)
            {
                if (attempt >= maxRetries) return [];
                await Task.Delay(delayMs, ct);
            }
        }

        return [];
    }

    public void Dispose()
    {
        _rateLimiter.Dispose();
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
