using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

public class RssBlogFeedCollector : ISocialChannelCollector
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RssBlogFeedCollector> _logger;

    private static readonly XNamespace AtomNs = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace ContentNs = "http://purl.org/rss/1.0/modules/content/";
    private static readonly XNamespace MediaNs = "http://search.yahoo.com/mrss/";

    public RssBlogFeedCollector(HttpClient httpClient, ILogger<RssBlogFeedCollector> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool CanHandle(SocialPlatform platform) =>
        platform == SocialPlatform.RssFeed || platform == SocialPlatform.Website;

    public async Task<IReadOnlyList<DiscoveredSocialPostDto>> CollectRecentPostsAsync(
        MonitoredSocialAccount account,
        int maxItems = 5,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var feedUrl = await ResolveFeedUrlAsync(account, ct);
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            _logger.LogWarning("No se pudo resolver la URL del feed RSS/Blog para '{Name}' ({Url}).", account.Name, account.ProfileUrl);
            return [];
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, feedUrl);
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36 Ludeka/1.0");

            using var response = await _httpClient.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Respuesta HTTP {StatusCode} al obtener feed RSS: {Url}", response.StatusCode, feedUrl);
                return [];
            }

            var xml = await response.Content.ReadAsStringAsync(ct);
            return ParseFeedXml(xml, account.Name, account.Platform, maxItems);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Error al descargar o procesar feed RSS {Url}: {Message}", feedUrl, ex.Message);
            throw;
        }
    }

    public static IReadOnlyList<DiscoveredSocialPostDto> ParseFeedXml(
        string xmlContent,
        string fallbackAuthor,
        SocialPlatform platform,
        int maxItems = 5)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
            return [];

        var doc = XDocument.Parse(xmlContent);

        // Detectar si es Atom o RSS 2.0
        if (doc.Root != null && doc.Root.Name.LocalName.Equals("feed", StringComparison.OrdinalIgnoreCase))
        {
            return ParseAtomFeed(doc, fallbackAuthor, platform, maxItems);
        }

        return ParseRss2Feed(doc, fallbackAuthor, platform, maxItems);
    }

    private static IReadOnlyList<DiscoveredSocialPostDto> ParseRss2Feed(
        XDocument doc,
        string fallbackAuthor,
        SocialPlatform platform,
        int maxItems)
    {
        var items = doc.Descendants("item").Take(maxItems);
        var list = new List<DiscoveredSocialPostDto>();

        foreach (var item in items)
        {
            var title = item.Element("title")?.Value?.Trim() ?? "Noticia editorial";
            var link = item.Element("link")?.Value?.Trim();

            if (string.IsNullOrWhiteSpace(link))
                continue;

            var rawDesc = item.Element(ContentNs + "encoded")?.Value
                ?? item.Element("description")?.Value
                ?? string.Empty;

            var cleanDesc = WebUtility.HtmlDecode(Regex.Replace(rawDesc, @"<[^>]+>", string.Empty)).Trim();

            // Extraer imagen de enclosure o media:content o del contenido HTML
            var thumbnail = item.Element("enclosure")?.Attribute("url")?.Value
                ?? item.Element(MediaNs + "content")?.Attribute("url")?.Value
                ?? item.Element(MediaNs + "thumbnail")?.Attribute("url")?.Value;

            if (string.IsNullOrWhiteSpace(thumbnail))
            {
                var imgMatch = Regex.Match(rawDesc, @"<img[^>]+src=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                if (imgMatch.Success)
                {
                    thumbnail = imgMatch.Groups[1].Value;
                }
            }

            var author = item.Element("author")?.Value
                ?? item.Element("creator")?.Value
                ?? fallbackAuthor;

            var publishedAt = DateTimeOffset.UtcNow;
            var pubStr = item.Element("pubDate")?.Value;
            if (!string.IsNullOrWhiteSpace(pubStr) && DateTimeOffset.TryParse(pubStr, out var parsedDate))
            {
                publishedAt = parsedDate;
            }

            list.Add(new DiscoveredSocialPostDto(
                sourceUrl: link,
                title: title,
                authorOrChannel: author,
                description: cleanDesc,
                thumbnailUrl: thumbnail,
                publishedAt: publishedAt,
                isVideo: false,
                platform: platform));
        }

        return list;
    }

    private static IReadOnlyList<DiscoveredSocialPostDto> ParseAtomFeed(
        XDocument doc,
        string fallbackAuthor,
        SocialPlatform platform,
        int maxItems)
    {
        var entries = doc.Descendants(AtomNs + "entry").Take(maxItems);
        var list = new List<DiscoveredSocialPostDto>();

        foreach (var entry in entries)
        {
            var title = entry.Element(AtomNs + "title")?.Value?.Trim() ?? "Noticia editorial";
            var link = entry.Element(AtomNs + "link")?.Attribute("href")?.Value?.Trim();

            if (string.IsNullOrWhiteSpace(link))
                continue;

            var rawDesc = entry.Element(AtomNs + "content")?.Value
                ?? entry.Element(AtomNs + "summary")?.Value
                ?? string.Empty;

            var cleanDesc = WebUtility.HtmlDecode(Regex.Replace(rawDesc, @"<[^>]+>", string.Empty)).Trim();
            var author = entry.Element(AtomNs + "author")?.Element(AtomNs + "name")?.Value ?? fallbackAuthor;

            string? thumbnail = null;
            var imgMatch = Regex.Match(rawDesc, @"<img[^>]+src=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (imgMatch.Success)
            {
                thumbnail = imgMatch.Groups[1].Value;
            }

            var publishedAt = DateTimeOffset.UtcNow;
            var pubStr = entry.Element(AtomNs + "published")?.Value ?? entry.Element(AtomNs + "updated")?.Value;
            if (!string.IsNullOrWhiteSpace(pubStr) && DateTimeOffset.TryParse(pubStr, out var parsedDate))
            {
                publishedAt = parsedDate;
            }

            list.Add(new DiscoveredSocialPostDto(
                sourceUrl: link,
                title: title,
                authorOrChannel: author,
                description: cleanDesc,
                thumbnailUrl: thumbnail,
                publishedAt: publishedAt,
                isVideo: false,
                platform: platform));
        }

        return list;
    }

    private async Task<string?> ResolveFeedUrlAsync(MonitoredSocialAccount account, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(account.ResolvedFeedUrl))
            return account.ResolvedFeedUrl;

        var target = !string.IsNullOrWhiteSpace(account.ProfileUrl)
            ? account.ProfileUrl.Trim()
            : account.HandleOrChannelId.Trim();

        // 1. Si la URL ya termina explícitamente en /feed, .xml o /rss
        if (target.EndsWith("/feed", StringComparison.OrdinalIgnoreCase) ||
            target.EndsWith("/feed/", StringComparison.OrdinalIgnoreCase) ||
            target.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
            target.EndsWith("/rss", StringComparison.OrdinalIgnoreCase))
        {
            account.SetResolvedFeedUrl(target);
            return target;
        }

        // 2. Comprobar si el HTML de la web contiene enlace <link rel="alternate" type="application/rss+xml" ...>
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, target);
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");

            using var res = await _httpClient.SendAsync(req, ct);
            if (res.IsSuccessStatusCode)
            {
                var html = await res.Content.ReadAsStringAsync(ct);
                var feedLinkMatch = Regex.Match(
                    html,
                    @"<link[^>]+type=[""']application/(?:rss\+xml|atom\+xml)[""'][^>]+href=[""']([^""']+)[""']",
                    RegexOptions.IgnoreCase);

                if (!feedLinkMatch.Success)
                {
                    feedLinkMatch = Regex.Match(
                        html,
                        @"<link[^>]+href=[""']([^""']+)[""'][^>]+type=[""']application/(?:rss\+xml|atom\+xml)[""']",
                        RegexOptions.IgnoreCase);
                }

                if (feedLinkMatch.Success)
                {
                    var href = feedLinkMatch.Groups[1].Value;
                    var resolved = new Uri(new Uri(target), href).ToString();
                    account.SetResolvedFeedUrl(resolved);
                    return resolved;
                }
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "No se pudo autodescubrir feed en {Url}: {Message}", target, ex.Message);
        }

        // 3. Fallback común: probar agregando /feed/ al dominio base
        var fallbackUrl = target.TrimEnd('/') + "/feed/";
        account.SetResolvedFeedUrl(fallbackUrl);
        return fallbackUrl;
    }
}
