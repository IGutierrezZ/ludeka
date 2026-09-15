using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Logging;

namespace Ludeka.Infrastructure.Services;

public class TelegramChannelCollector : ISocialChannelCollector
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramChannelCollector> _logger;

    public TelegramChannelCollector(HttpClient httpClient, ILogger<TelegramChannelCollector> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool CanHandle(SocialPlatform platform) => platform == SocialPlatform.Telegram;

    public async Task<IReadOnlyList<DiscoveredSocialPostDto>> CollectRecentPostsAsync(
        MonitoredSocialAccount account,
        int maxItems = 5,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var channelUser = ExtractChannelUsername(account.HandleOrChannelId, account.ProfileUrl);
        if (string.IsNullOrWhiteSpace(channelUser))
        {
            _logger.LogWarning("No se pudo extraer el identificador de canal de Telegram para '{Name}' ({Handle}).", account.Name, account.HandleOrChannelId);
            return [];
        }

        var publicViewUrl = $"https://t.me/s/{channelUser}";

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, publicViewUrl);
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36 Ludeka/1.0");

            using var response = await _httpClient.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Respuesta HTTP {StatusCode} al consultar vista pública de Telegram: {Url}", response.StatusCode, publicViewUrl);
                return [];
            }

            var html = await response.Content.ReadAsStringAsync(ct);
            return ParseTelegramHtml(html, channelUser, account.Name, maxItems);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Error al consultar canal público de Telegram {Url}: {Message}", publicViewUrl, ex.Message);
            throw;
        }
    }

    public static string? ExtractChannelUsername(string handleOrChannelId, string profileUrl)
    {
        var input = !string.IsNullOrWhiteSpace(handleOrChannelId) ? handleOrChannelId.Trim() : profileUrl.Trim();
        var match = Regex.Match(input, @"(?:https?://)?(?:www\.)?(?:t\.me/)?(?:s/)?@?([a-zA-Z0-9_]{3,32})", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    public static IReadOnlyList<DiscoveredSocialPostDto> ParseTelegramHtml(
        string html,
        string channelUsername,
        string fallbackAuthor,
        int maxItems = 5)
    {
        if (string.IsNullOrWhiteSpace(html))
            return [];

        var list = new List<DiscoveredSocialPostDto>();

        // Localizar bloques de mensajes data-post="canal/id"
        var messageBlocks = Regex.Matches(
            html,
            @"<div[^>]*class=[""'][^""']*tgme_widget_message[^""']*[""'][^>]*data-post=[""']([^""']+)[""'][^>]*>(.*?)</div>\s*</div>\s*</div>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // Si la expresión anidada falla, buscar data-post directamente
        var postMatches = Regex.Matches(html, @"data-post=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
        var postIds = new HashSet<string>();

        foreach (Match m in postMatches)
        {
            var dataPost = m.Groups[1].Value;
            if (!postIds.Add(dataPost))
                continue;
        }

        // Dividir el HTML por mensajes para extraer cada uno
        var chunks = Regex.Split(html, @"(?=<div[^>]*class=[""'][^""']*tgme_widget_message\b)");

        foreach (var chunk in chunks)
        {
            if (list.Count >= maxItems)
                break;

            var postMatch = Regex.Match(chunk, @"data-post=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (!postMatch.Success)
                continue;

            var dataPost = postMatch.Groups[1].Value; // ej. "deviriberia/1234"
            var sourceUrl = $"https://t.me/{dataPost}";

            // Extraer texto del mensaje
            var textMatch = Regex.Match(chunk, @"<div[^>]*class=[""'][^""']*tgme_widget_message_text[^""']*[""'][^>]*>(.*?)</div>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var rawText = textMatch.Success ? textMatch.Groups[1].Value : string.Empty;
            var cleanText = WebUtility.HtmlDecode(Regex.Replace(rawText, @"<br\s*/?>", "\n"));
            cleanText = Regex.Replace(cleanText, @"<[^>]+>", string.Empty).Trim();

            // Si el mensaje no tiene texto pero es solo una imagen o vídeo, creamos un placeholder
            var title = !string.IsNullOrWhiteSpace(cleanText)
                ? (cleanText.Length > 80 ? cleanText[..77] + "..." : cleanText)
                : $"Publicación en Telegram ({dataPost})";

            // Extraer foto
            string? photoUrl = null;
            var photoMatch = Regex.Match(chunk, @"background-image:url\('([^']+)'\)", RegexOptions.IgnoreCase);
            if (photoMatch.Success)
            {
                photoUrl = photoMatch.Groups[1].Value;
            }

            // Extraer fecha
            var dateMatch = Regex.Match(chunk, @"<time[^>]*datetime=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            var publishedAt = DateTimeOffset.UtcNow;
            if (dateMatch.Success && DateTimeOffset.TryParse(dateMatch.Groups[1].Value, out var parsedDate))
            {
                publishedAt = parsedDate;
            }

            var isVideo = chunk.Contains("tgme_widget_message_video_player", StringComparison.OrdinalIgnoreCase);

            list.Add(new DiscoveredSocialPostDto(
                sourceUrl: sourceUrl,
                title: title,
                authorOrChannel: fallbackAuthor,
                description: cleanText,
                thumbnailUrl: photoUrl,
                publishedAt: publishedAt,
                isVideo: isVideo,
                platform: SocialPlatform.Telegram));
        }

        // Ordenar los más recientes primero (en t.me/s vienen en orden cronológico ascendente)
        return list.OrderByDescending(p => p.PublishedAt).Take(maxItems).ToList();
    }
}
