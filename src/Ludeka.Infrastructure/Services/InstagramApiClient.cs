using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Infrastructure.Services;

public class InstagramApiClient : IInstagramApiClient
{
    private readonly HttpClient _httpClient;
    private readonly InstagramOptions _options;
    private readonly ILogger<InstagramApiClient> _logger;

    public InstagramApiClient(
        HttpClient httpClient,
        IOptions<InstagramOptions> options,
        ILogger<InstagramApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? new InstagramOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> CreateMediaContainerAsync(string imageUrl, string caption, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new ArgumentException("La URL de la imagen no puede estar vacía.", nameof(imageUrl));

        if (!_options.IsConfigured)
        {
            _logger.LogInformation("InstagramApiClient en modo simulado: Creando contenedor para imagen '{ImageUrl}'", imageUrl);
            var simCreationId = $"sim_container_{Guid.NewGuid().ToString("N")[..12]}";
            return simCreationId;
        }

        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/{_options.InstagramAccountId}/media";

        var postData = new Dictionary<string, string>
        {
            ["image_url"] = imageUrl,
            ["caption"] = caption ?? string.Empty,
            ["access_token"] = _options.AccessToken!
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(postData)
        };

        using var response = await _httpClient.SendAsync(request, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error al crear contenedor en Instagram API. Código: {StatusCode}, Respuesta: {Response}", response.StatusCode, responseContent);
            var errorMessage = ExtractMetaErrorMessage(responseContent) ?? $"Error {response.StatusCode} al crear contenedor de Instagram.";
            throw new HttpRequestException(errorMessage, null, response.StatusCode);
        }

        using var doc = JsonDocument.Parse(responseContent);
        if (doc.RootElement.TryGetProperty("id", out var idProp))
        {
            return idProp.GetString() ?? throw new InvalidOperationException("No se recibió el ID del contenedor de Instagram.");
        }

        throw new InvalidOperationException($"Respuesta inesperada de Instagram API: {responseContent}");
    }

    public async Task<string> PublishMediaAsync(string creationId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(creationId))
            throw new ArgumentException("El ID de creación del contenedor no puede estar vacío.", nameof(creationId));

        if (!_options.IsConfigured)
        {
            _logger.LogInformation("InstagramApiClient en modo simulado: Publicando contenedor '{CreationId}'", creationId);
            var simMediaId = $"sim_media_{Guid.NewGuid().ToString("N")[..12]}";
            return simMediaId;
        }

        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/{_options.InstagramAccountId}/media_publish";

        var postData = new Dictionary<string, string>
        {
            ["creation_id"] = creationId,
            ["access_token"] = _options.AccessToken!
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(postData)
        };

        using var response = await _httpClient.SendAsync(request, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error al publicar contenedor en Instagram API. Código: {StatusCode}, Respuesta: {Response}", response.StatusCode, responseContent);
            var errorMessage = ExtractMetaErrorMessage(responseContent) ?? $"Error {response.StatusCode} al publicar en Instagram.";
            throw new HttpRequestException(errorMessage, null, response.StatusCode);
        }

        using var doc = JsonDocument.Parse(responseContent);
        if (doc.RootElement.TryGetProperty("id", out var idProp))
        {
            return idProp.GetString() ?? throw new InvalidOperationException("No se recibió el ID del post publicado en Instagram.");
        }

        throw new InvalidOperationException($"Respuesta inesperada de Instagram API: {responseContent}");
    }

    public async Task<string?> GetPermalinkAsync(string mediaId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(mediaId))
            return null;

        if (!_options.IsConfigured)
        {
            return $"https://www.instagram.com/p/{mediaId}/";
        }

        try
        {
            var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/{mediaId}?fields=permalink&access_token={_options.AccessToken}";
            using var response = await _httpClient.GetAsync(endpoint, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseContent);
                if (doc.RootElement.TryGetProperty("permalink", out var permalinkProp))
                {
                    return permalinkProp.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo recuperar el permalink exacto para el media ID '{MediaId}'", mediaId);
        }

        return $"https://www.instagram.com/p/{mediaId}/";
    }

    public async Task<IReadOnlyList<InstagramAccountMediaDto>> GetAccountReelsAsync(int limit = 25, CancellationToken ct = default)
    {
        var effectiveLimit = Math.Clamp(limit, 1, 100);

        if (!_options.IsConfigured)
        {
            _logger.LogInformation("InstagramApiClient en modo simulado: Devolviendo Reels simulados de la cuenta de Ludeka.");
            return GenerateSimulatedAccountReels(effectiveLimit);
        }

        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/{_options.InstagramAccountId}/media" +
                       $"?fields=id,media_type,media_product_type,media_url,permalink,caption,timestamp" +
                       $"&limit={effectiveLimit}&access_token={_options.AccessToken}";

        using var response = await _httpClient.GetAsync(endpoint, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error al consultar Reels en Instagram Graph API. Código: {StatusCode}, Respuesta: {Response}", response.StatusCode, responseContent);
            var errorMessage = ExtractMetaErrorMessage(responseContent) ?? $"Error {response.StatusCode} al consultar media de Instagram.";
            throw new HttpRequestException(errorMessage, null, response.StatusCode);
        }

        using var doc = JsonDocument.Parse(responseContent);
        if (!doc.RootElement.TryGetProperty("data", out var dataArray) || dataArray.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<InstagramAccountMediaDto>();

        foreach (var item in dataArray.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            var mediaType = item.TryGetProperty("media_type", out var mtProp) ? mtProp.GetString() : null;
            var productType = item.TryGetProperty("media_product_type", out var mptProp) ? mptProp.GetString() : null;
            var mediaUrl = item.TryGetProperty("media_url", out var urlProp) ? urlProp.GetString() : null;
            var permalink = item.TryGetProperty("permalink", out var linkProp) ? linkProp.GetString() : null;
            var caption = item.TryGetProperty("caption", out var capProp) ? capProp.GetString() : null;
            var timestampRaw = item.TryGetProperty("timestamp", out var tsProp) ? tsProp.GetString() : null;

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(mediaUrl))
                continue;

            bool isReelOrVideo = string.Equals(productType, "REELS", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(mediaType, "VIDEO", StringComparison.OrdinalIgnoreCase);

            if (!isReelOrVideo)
                continue;

            var publishedAt = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(timestampRaw) &&
                DateTimeOffset.TryParse(timestampRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedTs))
            {
                publishedAt = parsedTs;
            }

            results.Add(new InstagramAccountMediaDto(
                Id: id,
                MediaType: mediaType ?? "VIDEO",
                MediaProductType: productType ?? "REELS",
                MediaUrl: mediaUrl,
                Permalink: !string.IsNullOrWhiteSpace(permalink) ? permalink : $"https://www.instagram.com/reel/{id}/",
                Caption: caption,
                Timestamp: publishedAt));
        }

        return results;
    }

    private static IReadOnlyList<InstagramAccountMediaDto> GenerateSimulatedAccountReels(int limit)
    {
        var baseTime = new DateTimeOffset(2026, 10, 10, 18, 0, 0, TimeSpan.Zero);
        var simulated = new List<InstagramAccountMediaDto>
        {
            new(
                Id: "sim_reel_101",
                MediaType: "VIDEO",
                MediaProductType: "REELS",
                MediaUrl: "https://cdn.ludeka.es/sim/reels/reel-101.mp4",
                Permalink: "https://www.instagram.com/reel/sim_reel_101/",
                Caption: "¡Vistazo rápido a The White Castle de @deviriberia! Un eurogame intenso que ya tienes en tiendas como @zacatrus. Tienes el link en bio. #juegosdemesa #thewhitecastle",
                Timestamp: baseTime.AddDays(-4)),
            new(
                Id: "sim_reel_102",
                MediaType: "VIDEO",
                MediaProductType: "REELS",
                MediaUrl: "https://cdn.ludeka.es/sim/reels/reel-102.mp4",
                Permalink: "https://www.instagram.com/reel/sim_reel_102/",
                Caption: "¿Cómo escala Harmonies a 2 jugadores? Lo probamos en mesa gracias a @asmodee_es y os contamos nuestro veredicto en 90 segundos. #harmonies #boardgames",
                Timestamp: baseTime.AddDays(-3)),
            new(
                Id: "sim_reel_103",
                MediaType: "VIDEO",
                MediaProductType: "REELS",
                MediaUrl: "https://cdn.ludeka.es/sim/reels/reel-103.mp4",
                Permalink: "https://www.instagram.com/reel/sim_reel_103/",
                Caption: "3 juegos de caja pequeña imprescindibles de @malditogames y @tranjisgames para llevar de viaje este fin de semana. #juegosdemesa",
                Timestamp: baseTime.AddDays(-2)),
            new(
                Id: "sim_reel_104",
                MediaType: "VIDEO",
                MediaProductType: "REELS",
                MediaUrl: "https://cdn.ludeka.es/sim/reels/reel-104.mp4",
                Permalink: "https://www.instagram.com/reel/sim_reel_104/",
                Caption: "Abrimos la edición en español de Arcs junto a @deviriberia. ¿Merece la pena la campaña? Enlace en la biografía. #arcs #colewehrle",
                Timestamp: baseTime.AddDays(-1)),
            new(
                Id: "sim_reel_105",
                MediaType: "VIDEO",
                MediaProductType: "REELS",
                MediaUrl: "https://cdn.ludeka.es/sim/reels/reel-105.mp4",
                Permalink: "https://www.instagram.com/reel/sim_reel_105/",
                Caption: "Resumen de ofertas y sorteos activos de la semana en @zacatrus y @dungeonmarvels. ¡No te pierdas ninguno en tu mesa! #sorteos #ofertasjuegos",
                Timestamp: baseTime)
        };

        return simulated.Count > limit ? simulated.GetRange(0, limit) : simulated;
    }

    private static string? ExtractMetaErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var errorObj) &&
                errorObj.TryGetProperty("message", out var messageProp))
            {
                return messageProp.GetString();
            }
        }
        catch
        {
            // Ignorar errores de parsing si no es JSON
        }

        return null;
    }
}
