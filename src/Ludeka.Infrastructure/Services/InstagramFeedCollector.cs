using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Infrastructure.Services;

public class InstagramFeedCollector : ISocialChannelCollector
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<SocialCollectorOptions> _optionsMonitor;
    private readonly ILogger<InstagramFeedCollector> _logger;

    public InstagramFeedCollector(
        HttpClient httpClient,
        IOptionsMonitor<SocialCollectorOptions> optionsMonitor,
        ILogger<InstagramFeedCollector> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool CanHandle(SocialPlatform platform) => platform == SocialPlatform.Instagram;

    public async Task<IReadOnlyList<DiscoveredSocialPostDto>> CollectRecentPostsAsync(
        MonitoredSocialAccount account,
        int maxItems = 5,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var options = _optionsMonitor.CurrentValue;

        // 1. Modo simulado para pruebas unitarias y desarrollo local offline
        if (options.Simulate)
        {
            _logger.LogInformation("InstagramFeedCollector en modo simulado para cuenta '{Name}'", account.Name);
            return GenerateSimulatedPosts(account, maxItems);
        }

        var cleanHandle = account.HandleOrChannelId.Trim().TrimStart('@');

        // 2. Si hay plantilla de RSS-Bridge configurada (ej. en Docker o servidor)
        if (!string.IsNullOrWhiteSpace(options.RssBridgeUrlTemplate))
        {
            var bridgeUrl = options.RssBridgeUrlTemplate.Replace("{username}", cleanHandle, StringComparison.OrdinalIgnoreCase);
            try
            {
                using var bridgeReq = new HttpRequestMessage(HttpMethod.Get, bridgeUrl);
                using var bridgeRes = await _httpClient.SendAsync(bridgeReq, ct);
                if (bridgeRes.IsSuccessStatusCode)
                {
                    var xml = await bridgeRes.Content.ReadAsStringAsync(ct);
                    return RssBlogFeedCollector.ParseFeedXml(xml, account.Name, SocialPlatform.Instagram, maxItems);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Error al consultar RSS-Bridge para Instagram '{Handle}': {Message}", cleanHandle, ex.Message);
            }
        }

        // 3. Consulta no invasiva a perfil público con cabecera de previsualización
        var profileUrl = !string.IsNullOrWhiteSpace(account.ProfileUrl)
            ? account.ProfileUrl
            : $"https://www.instagram.com/{cleanHandle}/";

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, profileUrl);
            req.Headers.UserAgent.ParseAdd("facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)");

            using var response = await _httpClient.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Perfil de Instagram {Url} devolvió status {StatusCode}. Se aplica enfriamiento sin bloquear el ciclo.", profileUrl, response.StatusCode);
                return [];
            }

            var html = await response.Content.ReadAsStringAsync(ct);
            return ParseInstagramProfileHtml(html, account.Name, maxItems);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Excepción al consultar perfil público de Instagram {Url}: {Message}", profileUrl, ex.Message);
            return [];
        }
    }

    public static IReadOnlyList<DiscoveredSocialPostDto> ParseInstagramProfileHtml(string html, string accountName, int maxItems = 5)
    {
        if (string.IsNullOrWhiteSpace(html))
            return [];

        // Buscar enlaces canónicos a publicaciones /p/{code}/ o /reel/{code}/
        var matches = Regex.Matches(html, @"/(?:p|reel)/([a-zA-Z0-9_-]{8,15})/?", RegexOptions.IgnoreCase);
        var seenCodes = new HashSet<string>();
        var list = new List<DiscoveredSocialPostDto>();

        foreach (Match m in matches)
        {
            if (list.Count >= maxItems)
                break;

            var code = m.Groups[1].Value;
            if (!seenCodes.Add(code))
                continue;

            var postUrl = $"https://www.instagram.com/p/{code}/";
            list.Add(new DiscoveredSocialPostDto(
                sourceUrl: postUrl,
                title: $"Publicación de {accountName} en Instagram",
                authorOrChannel: accountName,
                description: string.Empty,
                thumbnailUrl: null,
                publishedAt: DateTimeOffset.UtcNow,
                isVideo: m.Value.Contains("/reel/"),
                platform: SocialPlatform.Instagram));
        }

        return list;
    }

    private static IReadOnlyList<DiscoveredSocialPostDto> GenerateSimulatedPosts(MonitoredSocialAccount account, int maxItems)
    {
        var cleanHandle = account.HandleOrChannelId.Trim().TrimStart('@');
        var now = DateTimeOffset.UtcNow;
        var count = Math.Min(maxItems, 2);
        var list = new List<DiscoveredSocialPostDto>();

        for (int i = 1; i <= count; i++)
        {
            var code = $"sim_{cleanHandle}_{i}";
            var postUrl = $"https://www.instagram.com/p/{code}/";
            list.Add(new DiscoveredSocialPostDto(
                sourceUrl: postUrl,
                title: $"Sorteo y Novedad simulada de {account.Name} #{i}",
                authorOrChannel: account.Name,
                description: $"¡Gran sorteo exclusivo de {account.Name}! Participa mencionando a 2 amigos.",
                thumbnailUrl: "https://images.unsplash.com/photo-1610890716171-6b1bb98ffd09?w=600",
                publishedAt: now.AddHours(-i * 4),
                isVideo: false,
                platform: SocialPlatform.Instagram));
        }

        return list;
    }
}
