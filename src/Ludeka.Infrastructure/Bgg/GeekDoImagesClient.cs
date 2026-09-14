using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Infrastructure.Bgg;

/// <summary>
/// Cliente oficial para consultar la API interna de imágenes comunitarias de GeekDo (BoardGameGeek).
/// Permite recuperar las 3 fotos comunitarias más votadas (carátula frontal, trasera y componentes en mesa).
/// </summary>
public class GeekDoImagesClient : IGeekDoImagesClient
{
    private readonly HttpClient _httpClient;
    private readonly BggMassIngestionOptions _options;
    private readonly ILogger<GeekDoImagesClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GeekDoImagesClient(
        HttpClient httpClient,
        IOptions<BggMassIngestionOptions> options,
        ILogger<GeekDoImagesClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? new BggMassIngestionOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GeekDoGalleryImagesDto> GetTopVotedImagesAsync(int bggId, CancellationToken ct = default)
    {
        if (bggId <= 0) return new GeekDoGalleryImagesDto(null, null, null);

        // Modo Simulado para desarrollo local y tests sin conexión
        if (_options.Simulate)
        {
            return new GeekDoGalleryImagesDto(
                FrontCoverUrl: $"https://cf.geekdo-images.com/simulated/{bggId}/front.jpg",
                BackCoverUrl: $"https://cf.geekdo-images.com/simulated/{bggId}/back.jpg",
                TableOrGameplayUrl: $"https://cf.geekdo-images.com/simulated/{bggId}/table.jpg"
            );
        }

        string url = $"https://api.geekdo.com/api/images?ajax=1&gallery=all&objectid={bggId}&objecttype=thing";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Ludeka/1.0 (El Letterboxd de los juegos de mesa; contacto@ludeka.com)");
            request.Headers.Add("Accept", "application/json");

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("GeekDo API devolvió estado HTTP {Status} para el juego #{BggId}.", response.StatusCode, bggId);
                return new GeekDoGalleryImagesDto(null, null, null);
            }

            string json = await response.Content.ReadAsStringAsync(ct);
            return ParseGeekDoImagesJson(json, bggId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar las fotos de GeekDo para el juego #{BggId}: {Message}", bggId, ex.Message);
            return new GeekDoGalleryImagesDto(null, null, null);
        }
    }

    public static GeekDoGalleryImagesDto ParseGeekDoImagesJson(string json, int bggId)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new GeekDoGalleryImagesDto(null, null, null);
        }

        try
        {
            var root = JsonSerializer.Deserialize<GeekDoImagesApiResponse>(json, JsonOptions);
            if (root?.Images == null || root.Images.Count == 0)
            {
                return new GeekDoGalleryImagesDto(null, null, null);
            }

            var candidates = root.Images;

            // 1. Portada frontal: boxartfront, front, box
            var frontCandidate = candidates
                .Where(i => IsFrontImage(i))
                .OrderByDescending(i => i.NumPositive)
                .FirstOrDefault();

            // 2. Contraportada / Trasera: boxartback, boxback, back
            var backCandidate = candidates
                .Where(i => IsBackImage(i))
                .OrderByDescending(i => i.NumPositive)
                .FirstOrDefault();

            // 3. Foto en mesa / componentes: gameplay, creative, components, in play
            var tableCandidate = candidates
                .Where(i => IsTableImage(i))
                .OrderByDescending(i => i.NumPositive)
                .FirstOrDefault();

            // Fallback para portada frontal si no tiene etiqueta explícita: primera imagen con más votos
            if (frontCandidate == null && candidates.Count > 0)
            {
                frontCandidate = candidates.OrderByDescending(i => i.NumPositive).First();
            }

            // Fallback para mesa si no hay etiqueta explícita pero hay más fotos distintas a portada y contraportada
            if (tableCandidate == null)
            {
                tableCandidate = candidates
                    .Where(i => i.ImageId != frontCandidate?.ImageId && i.ImageId != backCandidate?.ImageId)
                    .OrderByDescending(i => i.NumPositive)
                    .FirstOrDefault();
            }

            return new GeekDoGalleryImagesDto(
                FrontCoverUrl: ExtractBestUrl(frontCandidate),
                BackCoverUrl: ExtractBestUrl(backCandidate),
                TableOrGameplayUrl: ExtractBestUrl(tableCandidate)
            );
        }
        catch
        {
            return new GeekDoGalleryImagesDto(null, null, null);
        }
    }

    private static bool IsFrontImage(GeekDoImageItem item)
    {
        string type = (item.CanonicalType ?? string.Empty).ToLowerInvariant();
        string caption = (item.Caption ?? string.Empty).ToLowerInvariant();
        string name = (item.Name ?? string.Empty).ToLowerInvariant();

        return type.Contains("boxartfront") ||
               type.Contains("front") ||
               caption.Contains("box front") ||
               caption.Contains("cover") ||
               name.Contains("front") ||
               name.Contains("cover");
    }

    private static bool IsBackImage(GeekDoImageItem item)
    {
        string type = (item.CanonicalType ?? string.Empty).ToLowerInvariant();
        string caption = (item.Caption ?? string.Empty).ToLowerInvariant();
        string name = (item.Name ?? string.Empty).ToLowerInvariant();

        return type.Contains("boxartback") ||
               type.Contains("boxback") ||
               type.Contains("back") ||
               caption.Contains("box back") ||
               caption.Contains("back cover") ||
               name.Contains("back");
    }

    private static bool IsTableImage(GeekDoImageItem item)
    {
        string type = (item.CanonicalType ?? string.Empty).ToLowerInvariant();
        string caption = (item.Caption ?? string.Empty).ToLowerInvariant();
        string name = (item.Name ?? string.Empty).ToLowerInvariant();

        return type.Contains("gameplay") ||
               type.Contains("creative") ||
               type.Contains("components") ||
               caption.Contains("in play") ||
               caption.Contains("table") ||
               caption.Contains("components") ||
               name.Contains("play") ||
               name.Contains("components");
    }

    private static string? ExtractBestUrl(GeekDoImageItem? item)
    {
        if (item == null) return null;

        if (item.Images?.Original?.Src != null && !string.IsNullOrWhiteSpace(item.Images.Original.Src))
            return NormalizeUrl(item.Images.Original.Src);

        if (item.Images?.Large?.Src != null && !string.IsNullOrWhiteSpace(item.Images.Large.Src))
            return NormalizeUrl(item.Images.Large.Src);

        if (!string.IsNullOrWhiteSpace(item.ImageUrl))
            return NormalizeUrl(item.ImageUrl);

        if (item.Images?.Thumb?.Src != null && !string.IsNullOrWhiteSpace(item.Images.Thumb.Src))
            return NormalizeUrl(item.Images.Thumb.Src);

        return null;
    }

    private static string NormalizeUrl(string url)
    {
        url = url.Trim();
        if (url.StartsWith("//"))
        {
            return "https:" + url;
        }
        return url;
    }

    private class GeekDoImagesApiResponse
    {
        [JsonPropertyName("images")]
        public List<GeekDoImageItem>? Images { get; set; }
    }

    public class GeekDoImageItem
    {
        [JsonPropertyName("imageid")]
        public string? ImageId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("caption")]
        public string? Caption { get; set; }

        [JsonPropertyName("numpositive")]
        public int NumPositive { get; set; }

        [JsonPropertyName("imageurl")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("canonicaltype")]
        public string? CanonicalType { get; set; }

        [JsonPropertyName("images")]
        public GeekDoImageVariations? Images { get; set; }
    }

    public class GeekDoImageVariations
    {
        [JsonPropertyName("thumb")]
        public GeekDoImageRef? Thumb { get; set; }

        [JsonPropertyName("large")]
        public GeekDoImageRef? Large { get; set; }

        [JsonPropertyName("original")]
        public GeekDoImageRef? Original { get; set; }
    }

    public class GeekDoImageRef
    {
        [JsonPropertyName("src")]
        public string? Src { get; set; }
    }
}
