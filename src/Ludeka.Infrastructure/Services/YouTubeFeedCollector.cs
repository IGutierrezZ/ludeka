using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Logging;

namespace Ludeka.Infrastructure.Services;

public class YouTubeFeedCollector : ISocialChannelCollector
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<YouTubeFeedCollector> _logger;

    private static readonly XNamespace AtomNs = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace MediaNs = "http://search.yahoo.com/mrss/";
    private static readonly XNamespace YtNs = "http://www.youtube.com/xml/schemas/2015";

    public YouTubeFeedCollector(HttpClient httpClient, ILogger<YouTubeFeedCollector> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool CanHandle(SocialPlatform platform) => platform == SocialPlatform.YouTube;

    public async Task<IReadOnlyList<DiscoveredSocialPostDto>> CollectRecentPostsAsync(
        MonitoredSocialAccount account,
        int maxItems = 5,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var feedUrl = await ResolveFeedUrlAsync(account, ct);
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            _logger.LogWarning("No se pudo resolver el feed Atom de YouTube para la cuenta '{Name}' ({Handle}).", account.Name, account.HandleOrChannelId);
            return [];
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, feedUrl);
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36 Ludeka/1.0");

            using var response = await _httpClient.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Respuesta HTTP {StatusCode} al obtener feed Atom de YouTube: {Url}", response.StatusCode, feedUrl);
                return [];
            }

            var xml = await response.Content.ReadAsStringAsync(ct);
            return ParseFeedXml(xml, account.Name, maxItems);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Error al descargar o procesar feed de YouTube {Url}: {Message}", feedUrl, ex.Message);
            throw;
        }
    }

    public static IReadOnlyList<DiscoveredSocialPostDto> ParseFeedXml(string xmlContent, string fallbackAuthor, int maxItems = 5)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
            return [];

        var doc = XDocument.Parse(xmlContent);
        var entries = doc.Descendants(AtomNs + "entry").Take(maxItems);
        var list = new List<DiscoveredSocialPostDto>();

        foreach (var entry in entries)
        {
            var videoId = entry.Element(YtNs + "videoId")?.Value?.Trim();
            var title = entry.Element(AtomNs + "title")?.Value?.Trim() ?? "Vídeo de YouTube";
            var link = entry.Element(AtomNs + "link")?.Attribute("href")?.Value?.Trim();

            if (string.IsNullOrWhiteSpace(link) && !string.IsNullOrWhiteSpace(videoId))
            {
                link = $"https://www.youtube.com/watch?v={videoId}";
            }

            if (string.IsNullOrWhiteSpace(link))
                continue;

            var author = entry.Element(AtomNs + "author")?.Element(AtomNs + "name")?.Value?.Trim() ?? fallbackAuthor;
            var description = entry.Element(MediaNs + "group")?.Element(MediaNs + "description")?.Value?.Trim() ?? string.Empty;
            var thumbnail = entry.Element(MediaNs + "group")?.Element(MediaNs + "thumbnail")?.Attribute("url")?.Value?.Trim();

            if (string.IsNullOrWhiteSpace(thumbnail) && !string.IsNullOrWhiteSpace(videoId))
            {
                thumbnail = $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg";
            }

            var publishedAt = DateTimeOffset.UtcNow;
            var pubStr = entry.Element(AtomNs + "published")?.Value;
            if (!string.IsNullOrWhiteSpace(pubStr) && DateTimeOffset.TryParse(pubStr, out var parsedDate))
            {
                publishedAt = parsedDate;
            }

            list.Add(new DiscoveredSocialPostDto(
                sourceUrl: link,
                title: title,
                authorOrChannel: author,
                description: description,
                thumbnailUrl: thumbnail,
                publishedAt: publishedAt,
                isVideo: true,
                platform: SocialPlatform.YouTube));
        }

        return list;
    }

    private async Task<string?> ResolveFeedUrlAsync(MonitoredSocialAccount account, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(account.ResolvedFeedUrl))
            return account.ResolvedFeedUrl;

        var handleOrId = account.HandleOrChannelId.Trim();

        // 1. Si ya es un ID nativo de canal de YouTube (UC...)
        if (handleOrId.StartsWith("UC", StringComparison.OrdinalIgnoreCase) && handleOrId.Length >= 22)
        {
            var url = $"https://www.youtube.com/feeds/videos.xml?channel_id={handleOrId}";
            account.SetResolvedFeedUrl(url);
            return url;
        }

        // 2. Si la URL del perfil es directa a un canal con /channel/UC...
        var channelMatch = Regex.Match(account.ProfileUrl, @"/channel/(UC[a-zA-Z0-9_-]{20,24})", RegexOptions.IgnoreCase);
        if (channelMatch.Success)
        {
            var channelId = channelMatch.Groups[1].Value;
            var url = $"https://www.youtube.com/feeds/videos.xml?channel_id={channelId}";
            account.SetResolvedFeedUrl(url);
            return url;
        }

        // 3. Si es un handle (@nombre) o perfil general, resolver haciendo una petición GET ligera al perfil
        var profileUrl = account.ProfileUrl;
        if (string.IsNullOrWhiteSpace(profileUrl) || !profileUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var cleanHandle = handleOrId.StartsWith('@') ? handleOrId : $"@{handleOrId}";
            profileUrl = $"https://www.youtube.com/{cleanHandle}";
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, profileUrl);
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");

            using var res = await _httpClient.SendAsync(req, ct);
            if (res.IsSuccessStatusCode)
            {
                var html = await res.Content.ReadAsStringAsync(ct);
                var metaMatch = Regex.Match(html, @"<meta\s+itemprop=[""']channelId[""']\s+content=[""'](UC[a-zA-Z0-9_-]{20,24})[""']", RegexOptions.IgnoreCase);
                if (!metaMatch.Success)
                {
                    metaMatch = Regex.Match(html, @"[""']channelId[""']\s*:\s*[""'](UC[a-zA-Z0-9_-]{20,24})[""']", RegexOptions.IgnoreCase);
                }

                if (metaMatch.Success)
                {
                    var channelId = metaMatch.Groups[1].Value;
                    var url = $"https://www.youtube.com/feeds/videos.xml?channel_id={channelId}";
                    account.SetResolvedFeedUrl(url);
                    return url;
                }
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "No se pudo resolver el ChannelId para {Url}: {Message}", profileUrl, ex.Message);
        }

        // Fallback: Si el handle tiene formato sin arroba ni espacios, intentar feed por user
        var cleanUser = handleOrId.TrimStart('@');
        return $"https://www.youtube.com/feeds/videos.xml?user={cleanUser}";
    }
}
