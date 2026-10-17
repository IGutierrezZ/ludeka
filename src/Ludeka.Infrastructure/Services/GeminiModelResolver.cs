using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Infrastructure.Services;

/// <summary>
/// Resolutor dinámico que consulta el catálogo oficial de Google Gemini (<c>GET /v1beta/models</c>),
/// filtra los modelos generativos estables de las gamas <c>flash-lite</c> y <c>flash</c> (&gt;= 3.5),
/// los ordena del más barato al más caro (y de versión más reciente a más antigua dentro de cada gama)
/// y gestiona penalizaciones temporales en memoria ante errores HTTP 404/429/503.
/// </summary>
public sealed partial class GeminiModelResolver : IGeminiModelResolver
{
    private static readonly TimeSpan CacheSuccessTtl = TimeSpan.FromHours(12);
    private static readonly TimeSpan CacheFailureRetryTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan TransientCooldownDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan HardExclusionDuration = TimeSpan.FromHours(6);
    private const decimal MinSupportedModelVersion = 3.5m;

    [GeneratedRegex(@"^gemini-(?<version>\d+(?:\.\d+)?)-(?<tier>flash-lite|flash)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EligibleModelRegex();

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiModelResolver> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly ConcurrentDictionary<string, ModelPenaltyEntry> _penalties = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<string>? _cachedDiscoveredModels;
    private DateTimeOffset _cacheExpiresAt = DateTimeOffset.MinValue;

    public GeminiModelResolver(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiModelResolver> logger,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? new GeminiOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<string>> GetCandidateModelsAsync(CancellationToken ct = default)
    {
        if (_options.ShouldSimulate)
        {
            return _options.GetModelCandidateChain();
        }

        var baseModels = await GetOrRefreshBaseModelsAsync(ct).ConfigureAwait(false);
        return ApplyOverridesAndPenalties(baseModels);
    }

    public void ReportModelFailure(string model, HttpStatusCode? statusCode)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return;
        }

        var cleanModel = NormalizeModelId(model);
        var now = _timeProvider.GetUtcNow();

        if (statusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            _penalties[cleanModel] = new ModelPenaltyEntry(IsExcluded: true, ExpiresAt: now.Add(HardExclusionDuration));
            _logger.LogWarning("GeminiModelResolver: modelo {Model} excluido temporalmente tras devolver HTTP {StatusCode}.", cleanModel, statusCode);
            return;
        }

        if (statusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable or HttpStatusCode.InternalServerError or HttpStatusCode.GatewayTimeout)
        {
            _penalties[cleanModel] = new ModelPenaltyEntry(IsExcluded: false, ExpiresAt: now.Add(TransientCooldownDuration));
            _logger.LogInformation("GeminiModelResolver: modelo {Model} puesto en cooldown temporal tras HTTP {StatusCode}.", cleanModel, statusCode);
        }
    }

    public void ReportModelSuccess(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return;
        }

        _penalties.TryRemove(NormalizeModelId(model), out _);
    }

    private async Task<IReadOnlyList<string>> GetOrRefreshBaseModelsAsync(CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow();
        var snapshot = _cachedDiscoveredModels;
        if (snapshot is { Count: > 0 } && now < _cacheExpiresAt)
        {
            return snapshot;
        }

        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            now = _timeProvider.GetUtcNow();
            if (_cachedDiscoveredModels is { Count: > 0 } && now < _cacheExpiresAt)
            {
                return _cachedDiscoveredModels;
            }

            var discovered = await FetchAndRankModelsFromApiAsync(ct).ConfigureAwait(false);
            if (discovered.Count > 0)
            {
                _cachedDiscoveredModels = discovered;
                _cacheExpiresAt = now.Add(CacheSuccessTtl);
                _logger.LogInformation(
                    "GeminiModelResolver: catálogo dinámico actualizado con {Count} modelos ordenados por precio: {Models}",
                    discovered.Count,
                    string.Join(" -> ", discovered));
                return discovered;
            }

            var fallback = _cachedDiscoveredModels is { Count: > 0 }
                ? _cachedDiscoveredModels
                : GeminiOptions.DefaultFallbackModels;

            _cachedDiscoveredModels = fallback;
            _cacheExpiresAt = now.Add(CacheFailureRetryTtl);
            return fallback;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<IReadOnlyList<string>> FetchAndRankModelsFromApiAsync(CancellationToken ct)
    {
        try
        {
            var requestUri = $"{_options.BaseUrl.TrimEnd('/')}/models?key={_options.ApiKey}";
            using var response = await _httpClient.GetAsync(requestUri, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "GeminiModelResolver: GET /models devolvió HTTP {StatusCode}. Se utilizará la cadena de respaldo por defecto.",
                    response.StatusCode);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("models", out var modelsElement) ||
                modelsElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var rankedCandidates = new List<RankedModelCandidate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in modelsElement.EnumerateArray())
            {
                if (!SupportsGenerateContent(item))
                {
                    continue;
                }

                if (!item.TryGetProperty("name", out var nameProp))
                {
                    continue;
                }

                var rawName = nameProp.GetString();
                if (string.IsNullOrWhiteSpace(rawName))
                {
                    continue;
                }

                var modelId = NormalizeModelId(rawName);
                var match = EligibleModelRegex().Match(modelId);
                if (!match.Success)
                {
                    continue;
                }

                if (!decimal.TryParse(match.Groups["version"].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var version) ||
                    version < MinSupportedModelVersion)
                {
                    continue;
                }

                var tierGroup = match.Groups["tier"].Value;
                int costTier = tierGroup.Equals("flash-lite", StringComparison.OrdinalIgnoreCase) ? 1 : 2;

                if (seen.Add(modelId))
                {
                    rankedCandidates.Add(new RankedModelCandidate(modelId, costTier, version));
                }
            }

            return rankedCandidates
                .OrderBy(c => c.CostTier)
                .ThenByDescending(c => c.Version)
                .Select(c => c.ModelId)
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "GeminiModelResolver: error al consultar GET /models. Empleando cadena de respaldo por defecto.");
            return [];
        }
    }

    private IReadOnlyList<string> ApplyOverridesAndPenalties(IReadOnlyList<string> baseModels)
    {
        var orderedBase = new List<string>(baseModels.Count + 1);

        if (_options.HasExplicitModelOverride(out var explicitModel))
        {
            orderedBase.Add(explicitModel);
        }

        foreach (var model in baseModels)
        {
            if (!orderedBase.Exists(m => string.Equals(m, model, StringComparison.OrdinalIgnoreCase)))
            {
                orderedBase.Add(model);
            }
        }

        var now = _timeProvider.GetUtcNow();
        var healthy = new List<string>(orderedBase.Count);
        var cooledDown = new List<string>();

        foreach (var model in orderedBase)
        {
            if (_penalties.TryGetValue(model, out var penalty))
            {
                if (now >= penalty.ExpiresAt)
                {
                    _penalties.TryRemove(model, out _);
                    healthy.Add(model);
                }
                else if (!penalty.IsExcluded)
                {
                    cooledDown.Add(model);
                }
                // Si IsExcluded es true y aún no ha expirado, se omite de ambas listas
            }
            else
            {
                healthy.Add(model);
            }
        }

        healthy.AddRange(cooledDown);

        // Salvaguarda: si todos los modelos fueron excluidos por 404, devolver la lista original para no dejar la cadena vacía
        return healthy.Count > 0 ? healthy : orderedBase;
    }

    private static bool SupportsGenerateContent(JsonElement modelElement)
    {
        if (!modelElement.TryGetProperty("supportedGenerationMethods", out var methods) ||
            methods.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        foreach (var method in methods.EnumerateArray())
        {
            if (string.Equals(method.GetString(), "generateContent", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeModelId(string rawModel)
    {
        var trimmed = rawModel.Trim();
        return trimmed.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? trimmed["models/".Length..]
            : trimmed;
    }

    private readonly record struct RankedModelCandidate(string ModelId, int CostTier, decimal Version);
    private readonly record struct ModelPenaltyEntry(bool IsExcluded, DateTimeOffset ExpiresAt);
}
