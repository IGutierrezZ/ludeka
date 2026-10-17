using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Infrastructure.YouTube;

/// <summary>
/// Cliente OAuth 2.0 para la subida desatendida de YouTube Shorts en el canal oficial de Ludeka
/// mediante el protocolo Resumable Upload de YouTube Data API v3 (INC-150).
/// </summary>
public class YouTubeShortsUploadClient : IYouTubeShortsUploadClient
{
    private readonly HttpClient _httpClient;
    private readonly YouTubeOptions _youTubeOptions;
    private readonly AuthenticationOptions _authOptions;
    private readonly ILogger<YouTubeShortsUploadClient> _logger;

    public YouTubeShortsUploadClient(
        HttpClient httpClient,
        IOptions<YouTubeOptions> youTubeOptions,
        IOptions<AuthenticationOptions> authOptions,
        ILogger<YouTubeShortsUploadClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _youTubeOptions = youTubeOptions?.Value ?? new YouTubeOptions();
        _authOptions = authOptions?.Value ?? new AuthenticationOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsConfigured =>
        !_youTubeOptions.Simulate &&
        !string.IsNullOrWhiteSpace(ResolveClientId()) &&
        !string.IsNullOrWhiteSpace(ResolveClientSecret()) &&
        !string.IsNullOrWhiteSpace(_youTubeOptions.OAuthRefreshToken);

    public async Task<YouTubeShortUploadResultDto> UploadShortFromUrlAsync(
        string mediaUrl,
        string title,
        string description,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl))
            throw new ArgumentException("La URL del vídeo de origen no puede estar vacía.", nameof(mediaUrl));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título del Short no puede estar vacío.", nameof(title));

        if (!IsConfigured)
        {
            var simVideoId = $"sim_yt_{Guid.NewGuid().ToString("N")[..10]}";
            _logger.LogInformation("YouTubeShortsUploadClient en modo simulado: Subiendo Short '{Title}' -> {VideoId}", title, simVideoId);
            return new YouTubeShortUploadResultDto(
                VideoId: simVideoId,
                ShortUrl: $"https://www.youtube.com/shorts/{simVideoId}");
        }

        // 1. Obtener Access Token fresco intercambiando el Refresh Token OAuth 2.0
        var accessToken = await RefreshAccessTokenAsync(ct);

        // 2. Descargar el binario MP4 desde el CDN de Instagram
        using var mediaResponse = await _httpClient.GetAsync(mediaUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!mediaResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"No se pudo descargar el vídeo del Reel desde Instagram ({mediaResponse.StatusCode}).", null, mediaResponse.StatusCode);
        }

        var videoBytes = await mediaResponse.Content.ReadAsByteArrayAsync(ct);
        if (videoBytes.Length == 0)
        {
            throw new InvalidOperationException("El archivo de vídeo descargado desde Instagram está vacío.");
        }

        // 3. Iniciar sesión de subida Resumable en YouTube Data API v3
        var initiateUrl = $"{_youTubeOptions.UploadBaseUrl.TrimEnd('/')}?uploadType=resumable&part=snippet,status";
        var metadataPayload = new
        {
            snippet = new
            {
                title = title.Length > 100 ? title[..100] : title,
                description = description ?? string.Empty,
                categoryId = string.IsNullOrWhiteSpace(_youTubeOptions.DefaultCategoryId) ? "20" : _youTubeOptions.DefaultCategoryId,
                defaultLanguage = "es",
                defaultAudioLanguage = "es"
            },
            status = new
            {
                privacyStatus = string.IsNullOrWhiteSpace(_youTubeOptions.DefaultPrivacyStatus) ? "public" : _youTubeOptions.DefaultPrivacyStatus,
                selfDeclaredMadeForKids = false
            }
        };

        using var initiateReq = new HttpRequestMessage(HttpMethod.Post, initiateUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(metadataPayload), Encoding.UTF8, "application/json")
        };
        initiateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        initiateReq.Headers.Add("X-Upload-Content-Type", "video/mp4");
        initiateReq.Headers.Add("X-Upload-Content-Length", videoBytes.Length.ToString());

        using var initiateRes = await _httpClient.SendAsync(initiateReq, ct);
        var initiateBody = await initiateRes.Content.ReadAsStringAsync(ct);

        if (!initiateRes.IsSuccessStatusCode)
        {
            if (IsQuotaExceededResponse(initiateRes.StatusCode, initiateBody))
            {
                _logger.LogWarning("Cuota diaria de YouTube Data API v3 agotada (quotaExceeded) al iniciar subida de '{Title}'.", title);
                return new YouTubeShortUploadResultDto(
                    VideoId: string.Empty,
                    ShortUrl: string.Empty,
                    IsQuotaExceeded: true,
                    ErrorMessage: "Cuota diaria gratuita de YouTube Data API v3 alcanzada (quotaExceeded). Se reintentará en el siguiente ciclo diario.");
            }

            throw new HttpRequestException($"Error {initiateRes.StatusCode} al iniciar subida en YouTube: {initiateBody}", null, initiateRes.StatusCode);
        }

        var uploadSessionUri = initiateRes.Headers.Location;
        if (uploadSessionUri == null)
        {
            throw new InvalidOperationException("YouTube API no devolvió la cabecera Location para la sesión de subida resumable.");
        }

        // 4. Enviar los bytes del vídeo MP4 a la URL de sesión resumable
        using var uploadReq = new HttpRequestMessage(HttpMethod.Put, uploadSessionUri);
        uploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var byteContent = new ByteArrayContent(videoBytes);
        byteContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        uploadReq.Content = byteContent;

        using var uploadRes = await _httpClient.SendAsync(uploadReq, ct);
        var uploadBody = await uploadRes.Content.ReadAsStringAsync(ct);

        if (!uploadRes.IsSuccessStatusCode)
        {
            if (IsQuotaExceededResponse(uploadRes.StatusCode, uploadBody))
            {
                return new YouTubeShortUploadResultDto(
                    VideoId: string.Empty,
                    ShortUrl: string.Empty,
                    IsQuotaExceeded: true,
                    ErrorMessage: "Cuota diaria gratuita de YouTube Data API v3 alcanzada durante la transferencia.");
            }

            throw new HttpRequestException($"Error {uploadRes.StatusCode} al transferir vídeo a YouTube: {uploadBody}", null, uploadRes.StatusCode);
        }

        using var doc = JsonDocument.Parse(uploadBody);
        if (doc.RootElement.TryGetProperty("id", out var idProp))
        {
            var videoId = idProp.GetString();
            if (!string.IsNullOrWhiteSpace(videoId))
            {
                return new YouTubeShortUploadResultDto(
                    VideoId: videoId,
                    ShortUrl: $"https://www.youtube.com/shorts/{videoId}");
            }
        }

        throw new InvalidOperationException($"Respuesta de YouTube sin ID de vídeo: {uploadBody}");
    }

    private async Task<string> RefreshAccessTokenAsync(CancellationToken ct)
    {
        var tokenValues = new Dictionary<string, string>
        {
            ["client_id"] = ResolveClientId()!,
            ["client_secret"] = ResolveClientSecret()!,
            ["refresh_token"] = _youTubeOptions.OAuthRefreshToken!,
            ["grant_type"] = "refresh_token"
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, _youTubeOptions.TokenEndpointUrl)
        {
            Content = new FormUrlEncodedContent(tokenValues)
        };

        using var res = await _httpClient.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Error OAuth ({res.StatusCode}) al refrescar el token de YouTube: {body}", null, res.StatusCode);
        }

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("access_token", out var tokenProp))
        {
            var accessToken = tokenProp.GetString();
            if (!string.IsNullOrWhiteSpace(accessToken))
                return accessToken;
        }

        throw new InvalidOperationException("La respuesta de Google OAuth no contenía un access_token válido.");
    }

    private string? ResolveClientId()
    {
        if (!string.IsNullOrWhiteSpace(_youTubeOptions.OAuthClientId))
            return _youTubeOptions.OAuthClientId;

        return _authOptions.Providers != null &&
               _authOptions.Providers.TryGetValue("Google", out var googleProvider)
            ? googleProvider.ClientId
            : null;
    }

    private string? ResolveClientSecret()
    {
        if (!string.IsNullOrWhiteSpace(_youTubeOptions.OAuthClientSecret))
            return _youTubeOptions.OAuthClientSecret;

        return _authOptions.Providers != null &&
               _authOptions.Providers.TryGetValue("Google", out var googleProvider)
            ? googleProvider.ClientSecret
            : null;
    }

    private static bool IsQuotaExceededResponse(HttpStatusCode statusCode, string responseBody)
    {
        if (statusCode != HttpStatusCode.Forbidden && statusCode != HttpStatusCode.TooManyRequests)
            return false;

        return !string.IsNullOrWhiteSpace(responseBody) &&
               (responseBody.Contains("quotaExceeded", StringComparison.OrdinalIgnoreCase) ||
                responseBody.Contains("uploadLimitExceeded", StringComparison.OrdinalIgnoreCase));
    }
}
