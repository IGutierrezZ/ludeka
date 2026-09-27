using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Logging;

namespace Ludeka.Infrastructure.Services;

public class OpenGraphSocialMetadataExtractor : ISocialMetadataExtractor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenGraphSocialMetadataExtractor> _logger;

    private static readonly Regex YouTubeIdRegex = new(
        @"(?:youtu\.be\/|youtube\.com\/(?:embed\/|v\/|watch\?v=|watch\?.+&v=|shorts\/))([a-zA-Z0-9_-]{11})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex OgTitleRegex = new(
        @"<meta\s+(?:property|name)=[""'](?:og:title|twitter:title)[""']\s+content=(?:""([^""]*)""|'([^']*)')|<meta\s+content=(?:""([^""]*)""|'([^']*)')\s+(?:property|name)=[""'](?:og:title|twitter:title)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex OgImageRegex = new(
        @"<meta\s+(?:property|name)=[""'](?:og:image|twitter:image)[""']\s+content=(?:""([^""]*)""|'([^']*)')|<meta\s+content=(?:""([^""]*)""|'([^']*)')\s+(?:property|name)=[""'](?:og:image|twitter:image)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex OgDescriptionRegex = new(
        @"<meta\s+(?:property|name)=[""'](?:og:description|twitter:description|description)[""']\s+content=(?:""([^""]*)""|'([^']*)')|<meta\s+content=(?:""([^""]*)""|'([^']*)')\s+(?:property|name)=[""'](?:og:description|twitter:description|description)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TitleTagRegex = new(
        @"<title[^>]*>([^<]*)<\/title>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public OpenGraphSocialMetadataExtractor(HttpClient httpClient, ILogger<OpenGraphSocialMetadataExtractor> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SocialMetadataResultDto?> ExtractFromUrlAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var platform = DetectPlatform(url);

        // 1. Caso especializado: YouTube
        if (platform == SocialPlatform.YouTube)
        {
            var ytMatch = YouTubeIdRegex.Match(url);
            if (ytMatch.Success)
            {
                var videoId = ytMatch.Groups[1].Value;
                var nativeThumb = $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg";

                // Consultar oEmbed de YouTube para título y canal
                try
                {
                    var oEmbedUrl = $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(url)}&format=json";
                    using var oEmbedResponse = await _httpClient.GetAsync(oEmbedUrl, ct);
                    if (oEmbedResponse.IsSuccessStatusCode)
                    {
                        var json = await oEmbedResponse.Content.ReadAsStringAsync(ct);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
                        var author = root.TryGetProperty("author_name", out var a) ? a.GetString() : null;
                        var thumb = root.TryGetProperty("thumbnail_url", out var th) ? th.GetString() : nativeThumb;

                        return new SocialMetadataResultDto(
                            Url: url,
                            Platform: SocialPlatform.YouTube,
                            Title: title,
                            AuthorOrChannel: author,
                            Description: title,
                            ImageUrl: thumb ?? nativeThumb,
                            IsVideo: true,
                            VideoId: videoId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Fallo al consultar oEmbed de YouTube para {Url}. Usando fallback de id.", url);
                }

                return new SocialMetadataResultDto(
                    Url: url,
                    Platform: SocialPlatform.YouTube,
                    Title: null,
                    AuthorOrChannel: null,
                    Description: null,
                    ImageUrl: nativeThumb,
                    IsVideo: true,
                    VideoId: videoId);
            }
        }

        // 2. Caso genérico / Instagram / Web: Parseo OpenGraph
        var isVideo = url.Contains("/reel/", StringComparison.OrdinalIgnoreCase) ||
                      url.Contains("/tv/", StringComparison.OrdinalIgnoreCase);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            request.Headers.AcceptLanguage.ParseAdd("es-ES,es;q=0.9,en;q=0.8");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new SocialMetadataResultDto(url, platform, null, null, null, null, isVideo);
            }

            var html = await response.Content.ReadAsStringAsync(ct);

            var title = ExtractMetaValue(html, OgTitleRegex) ?? ExtractTitleTag(html);
            var image = ExtractMetaValue(html, OgImageRegex);
            var description = ExtractMetaValue(html, OgDescriptionRegex);

            // En Instagram, el título suele tener la forma "Author on Instagram: '...'"
            string? author = null;
            if (platform == SocialPlatform.Instagram)
            {
                // Si la respuesta es la pantalla vacía de login o título genérico "Instagram" sin imagen ni descripción
                if (string.Equals(title, "Instagram", StringComparison.OrdinalIgnoreCase) ||
                    (title != null && (title.StartsWith("Login", StringComparison.OrdinalIgnoreCase) || title.StartsWith("Inicia sesión", StringComparison.OrdinalIgnoreCase))) &&
                    string.IsNullOrWhiteSpace(image) && string.IsNullOrWhiteSpace(description))
                {
                    title = null;
                    author = null;
                    image = null;
                    description = null;
                    isVideo = false;
                }
                else if (!string.IsNullOrWhiteSpace(title))
                {
                    var authorMatch = Regex.Match(title, @"^(.+?)(?:\s+(?:en|on)\s+Instagram)?(?:\s*[:•]|\s*$)", RegexOptions.IgnoreCase);
                    if (authorMatch.Success)
                    {
                        author = authorMatch.Groups[1].Value.Trim();
                    }
                }
            }

            return new SocialMetadataResultDto(
                Url: url,
                Platform: platform,
                Title: title,
                AuthorOrChannel: author,
                Description: description,
                ImageUrl: image,
                IsVideo: isVideo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo extraer OpenGraph para la URL {Url}", url);
            return new SocialMetadataResultDto(url, platform, null, null, null, null, isVideo);
        }
    }

    private static string? ExtractMetaValue(string html, Regex regex)
    {
        var match = regex.Match(html);
        if (!match.Success) return null;

        for (int i = 1; i < match.Groups.Count; i++)
        {
            if (match.Groups[i].Success && !string.IsNullOrWhiteSpace(match.Groups[i].Value))
            {
                return WebUtility.HtmlDecode(match.Groups[i].Value).Trim();
            }
        }

        return null;
    }

    private static string? ExtractTitleTag(string html)
    {
        var match = TitleTagRegex.Match(html);
        if (!match.Success) return null;
        var val = match.Groups[1].Value;
        return string.IsNullOrWhiteSpace(val) ? null : WebUtility.HtmlDecode(val).Trim();
    }

    private static SocialPlatform DetectPlatform(string url)
    {
        if (url.Contains("instagram.com", StringComparison.OrdinalIgnoreCase))
            return SocialPlatform.Instagram;

        if (url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) || url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
            return SocialPlatform.YouTube;

        if (url.Contains("twitter.com", StringComparison.OrdinalIgnoreCase) || url.Contains("x.com", StringComparison.OrdinalIgnoreCase))
            return SocialPlatform.Twitter;

        if (url.Contains("tiktok.com", StringComparison.OrdinalIgnoreCase))
            return SocialPlatform.TikTok;

        return SocialPlatform.Website;
    }
}
