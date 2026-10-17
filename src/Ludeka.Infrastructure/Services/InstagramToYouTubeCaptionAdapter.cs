using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Infrastructure.Services;

/// <summary>
/// Motor híbrido en 3 fases para transformar el copy de un Reel de Instagram al formato de YouTube Short (INC-150):
/// 1. Cruce determinista de menciones @handle contra el directorio de Ludeka (Publisher, Store, Creator, MonitoredSocialAccount).
/// 2. Adaptación asistida con Google Gemini Flash (con fallback determinista resiliente).
/// 3. Cierre editorial determinista con enlace a ludeka.es y enlaces a las fichas de Ludeka detectadas.
/// </summary>
public class InstagramToYouTubeCaptionAdapter : IInstagramToYouTubeCaptionAdapter
{
    private static readonly Regex MentionRegex = new(@"(?<![a-zA-Z0-9._%+-])@([a-zA-Z0-9._]{2,40})", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IPublisherRepository _publisherRepository;
    private readonly IStoreRepository _storeRepository;
    private readonly ICreatorRepository _creatorRepository;
    private readonly IMonitoredAccountRepository _monitoredAccountRepository;
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _geminiOptions;
    private readonly InstagramOptions _instagramOptions;
    private readonly ILogger<InstagramToYouTubeCaptionAdapter> _logger;

    public InstagramToYouTubeCaptionAdapter(
        IPublisherRepository publisherRepository,
        IStoreRepository storeRepository,
        ICreatorRepository creatorRepository,
        IMonitoredAccountRepository monitoredAccountRepository,
        HttpClient httpClient,
        IOptions<GeminiOptions> geminiOptions,
        IOptions<InstagramOptions> instagramOptions,
        ILogger<InstagramToYouTubeCaptionAdapter> logger)
    {
        _publisherRepository = publisherRepository ?? throw new ArgumentNullException(nameof(publisherRepository));
        _storeRepository = storeRepository ?? throw new ArgumentNullException(nameof(storeRepository));
        _creatorRepository = creatorRepository ?? throw new ArgumentNullException(nameof(creatorRepository));
        _monitoredAccountRepository = monitoredAccountRepository ?? throw new ArgumentNullException(nameof(monitoredAccountRepository));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _geminiOptions = geminiOptions?.Value ?? new GeminiOptions();
        _instagramOptions = instagramOptions?.Value ?? new InstagramOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<YouTubeAdaptedMetadataDto> AdaptCaptionForYouTubeShortAsync(string? instagramCaption, CancellationToken ct = default)
    {
        var rawCaption = instagramCaption?.Trim() ?? string.Empty;
        var baseUrl = string.IsNullOrWhiteSpace(_instagramOptions.PublicBaseUrl)
            ? "https://ludeka.es"
            : _instagramOptions.PublicBaseUrl.TrimEnd('/');

        // FASE 1: Extraer menciones @handle de Instagram y cruzarlas con el directorio de Ludeka
        var resolvedMentions = await ResolveDirectoryMentionsAsync(rawCaption, baseUrl, ct);

        // FASE 2: Adaptación con Google Gemini Flash (o Fallback determinista si no está disponible)
        string adaptedTitle;
        string adaptedBody;

        var geminiResult = await TryAdaptWithGeminiAsync(rawCaption, resolvedMentions, ct);
        if (geminiResult.HasValue)
        {
            adaptedTitle = NormalizeShortTitle(geminiResult.Value.Title, rawCaption);
            adaptedBody = ApplyDeterministicMentionSafeguards(geminiResult.Value.CleanDescription, resolvedMentions);
        }
        else
        {
            adaptedTitle = BuildDeterministicTitle(rawCaption);
            adaptedBody = BuildDeterministicBody(rawCaption, resolvedMentions);
        }

        // FASE 3: Ensamblar el bloque de cierre determinista con enlace a ludeka.es
        var finalDescription = AppendLudekaDeterministicFooter(adaptedBody, resolvedMentions, baseUrl);

        return new YouTubeAdaptedMetadataDto(
            Title: adaptedTitle,
            Description: finalDescription,
            ResolvedMentions: resolvedMentions);
    }

    private async Task<IReadOnlyList<ResolvedMentionDto>> ResolveDirectoryMentionsAsync(
        string caption,
        string publicBaseUrl,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(caption))
            return [];

        var matches = MentionRegex.Matches(caption);
        if (matches.Count == 0)
            return [];

        var uniqueHandles = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in matches)
        {
            var rawHandle = match.Groups[1].Value.TrimEnd('.');
            if (rawHandle.Length >= 2 && seen.Add(rawHandle))
            {
                uniqueHandles.Add(rawHandle);
            }
        }

        if (uniqueHandles.Count == 0)
            return [];

        var publishers = await _publisherRepository.GetAllAsync(ct);
        var stores = await _storeRepository.GetAllAsync(ct);
        var creators = await _creatorRepository.GetAllAsync(ct);
        var monitoredAccounts = await _monitoredAccountRepository.GetAllAsync(platform: null, type: null, ct: ct);

        var resolvedList = new List<ResolvedMentionDto>();

        foreach (var igHandle in uniqueHandles)
        {
            var normalizedIg = NormalizeHandle(igHandle);

            // 1. Buscar en Editoriales (Publishers)
            var publisher = publishers.FirstOrDefault(p => MatchesEntityInstagram(p.SocialLinks, p.Slug, normalizedIg));
            if (publisher != null)
            {
                resolvedList.Add(BuildResolvedMention(
                    igHandle,
                    publisher.Name,
                    publisher.GetYouTubeLink(),
                    $"{publicBaseUrl}/editoriales/{publisher.Slug}",
                    monitoredAccounts));
                continue;
            }

            // 2. Buscar en Tiendas (Stores)
            var store = stores.FirstOrDefault(s => MatchesEntityInstagram(s.SocialLinks, s.Slug, normalizedIg));
            if (store != null)
            {
                resolvedList.Add(BuildResolvedMention(
                    igHandle,
                    store.Name,
                    store.GetYouTubeLink(),
                    $"{publicBaseUrl}/tiendas/{store.Slug}",
                    monitoredAccounts));
                continue;
            }

            // 3. Buscar en Creadores (Creators)
            var creator = creators.FirstOrDefault(c => MatchesEntityInstagram(c.SocialLinks, c.Slug, normalizedIg));
            if (creator != null)
            {
                resolvedList.Add(BuildResolvedMention(
                    igHandle,
                    creator.Name,
                    creator.GetYouTubeLink(),
                    $"{publicBaseUrl}/creadores/{creator.Slug}",
                    monitoredAccounts));
                continue;
            }

            // 4. Buscar en Cuentas Sociales Monitorizadas (MonitoredSocialAccounts)
            var monitoredIg = monitoredAccounts.FirstOrDefault(m =>
                m.Platform == SocialPlatform.Instagram &&
                (NormalizeHandle(m.HandleOrChannelId) == normalizedIg ||
                 ExtractHandleFromUrl(m.ProfileUrl) == normalizedIg));

            if (monitoredIg != null)
            {
                var monitoredYt = monitoredAccounts.FirstOrDefault(m =>
                    m.Platform == SocialPlatform.YouTube &&
                    string.Equals(m.Name, monitoredIg.Name, StringComparison.OrdinalIgnoreCase));

                string? ytHandle = monitoredYt != null
                    ? ExtractYouTubeHandle(monitoredYt.HandleOrChannelId, monitoredYt.ProfileUrl)
                    : null;

                string? ytUrl = monitoredYt?.ProfileUrl;
                var replacement = !string.IsNullOrWhiteSpace(ytHandle)
                    ? $"{monitoredIg.Name} ({ytHandle})"
                    : monitoredIg.Name;

                resolvedList.Add(new ResolvedMentionDto(
                    InstagramHandle: $"@{igHandle}",
                    OfficialName: monitoredIg.Name,
                    YouTubeHandle: ytHandle,
                    YouTubeChannelUrl: ytUrl,
                    LudekaProfileUrl: null,
                    ReplacementText: replacement));
            }
        }

        return resolvedList;
    }

    private static ResolvedMentionDto BuildResolvedMention(
        string igHandle,
        string officialName,
        SocialNetworkLink? ytLink,
        string ludekaProfileUrl,
        IReadOnlyList<MonitoredSocialAccount> monitoredAccounts)
    {
        string? ytHandle = null;
        string? ytUrl = ytLink?.Url;

        if (ytLink != null)
        {
            ytHandle = ExtractYouTubeHandle(ytLink.Handle, ytLink.Url);
        }
        else
        {
            var monitoredYt = monitoredAccounts.FirstOrDefault(m =>
                m.Platform == SocialPlatform.YouTube &&
                string.Equals(m.Name, officialName, StringComparison.OrdinalIgnoreCase));

            if (monitoredYt != null)
            {
                ytHandle = ExtractYouTubeHandle(monitoredYt.HandleOrChannelId, monitoredYt.ProfileUrl);
                ytUrl = monitoredYt.ProfileUrl;
            }
        }

        var replacement = !string.IsNullOrWhiteSpace(ytHandle)
            ? $"{officialName} ({ytHandle})"
            : officialName;

        return new ResolvedMentionDto(
            InstagramHandle: $"@{igHandle}",
            OfficialName: officialName,
            YouTubeHandle: ytHandle,
            YouTubeChannelUrl: ytUrl,
            LudekaProfileUrl: ludekaProfileUrl,
            ReplacementText: replacement);
    }

    private async Task<(string Title, string CleanDescription)?> TryAdaptWithGeminiAsync(
        string rawCaption,
        IReadOnlyList<ResolvedMentionDto> resolvedMentions,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawCaption) ||
            _geminiOptions.ShouldSimulate ||
            string.IsNullOrWhiteSpace(_geminiOptions.ApiKey))
        {
            return null;
        }

        var mentionsContext = new StringBuilder();
        if (resolvedMentions.Count > 0)
        {
            mentionsContext.AppendLine("Equivalencias oficiales resueltas en el directorio de Ludeka (DEBES sustituir cada mención de Instagram por su texto de reemplazo exacto):");
            foreach (var m in resolvedMentions)
            {
                mentionsContext.AppendLine($"- {m.InstagramHandle} => {m.ReplacementText}");
            }
        }
        else
        {
            mentionsContext.AppendLine("No hay menciones pre-resueltas en el directorio.");
        }

        var prompt = $@"
Eres el editor del canal de YouTube de Ludeka, la plataforma de juegos de mesa en español.
Vamos a publicar como YouTube Short un Reel procedente de nuestra cuenta de Instagram.

Texto original del Reel en Instagram:
""""""
{rawCaption}
""""""

{mentionsContext}

Instrucciones de adaptación a YouTube Shorts:
1. Genera un 'title' (título gancho en español) claro, atractivo y directo sobre el juego o tema del Reel, de máximo 85 caracteres (sin incluir #Shorts, ya que se añadirá después).
2. Genera 'cleanDescription' adaptando el texto original para YouTube:
   - Sustituye las menciones de Instagram listadas arriba exactamente por su texto de reemplazo oficial.
   - Si hay otras menciones '@cuenta' en el texto que NO estén en la lista de arriba, transfórmalas en el nombre comercial legible de esa tienda, editorial o autor quitando la '@' (ej. '@maldito_games' -> 'Maldito Games'), para no enlazar por error a canales equivocados en YouTube.
   - Elimina frases exclusivas de Instagram como 'link en bio' o 'enlace en la biografía'.
   - Conserva el tono cercano y apasionado por los juegos de mesa.

Devuelve EXCLUSIVAMENTE un objeto JSON válido con este esquema:
{{
  ""title"": ""Título atractivo para YouTube Short"",
  ""cleanDescription"": ""Descripción limpia y adaptada para YouTube""
}}";

        var models = _geminiOptions.GetModelCandidateChain();
        foreach (var model in models)
        {
            try
            {
                var requestBody = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    },
                    generationConfig = new
                    {
                        responseMimeType = "application/json",
                        temperature = 0.2
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(requestBody, JsonOptions);
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var requestUri = $"{_geminiOptions.BaseUrl.TrimEnd('/')}/models/{model}:generateContent?key={_geminiOptions.ApiKey}";

                using var response = await _httpClient.PostAsync(requestUri, content, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Gemini API ({Model}) devolvió {StatusCode} al adaptar Reel para YouTube Short.", model, response.StatusCode);
                    continue;
                }

                var responseJson = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(responseJson);

                if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                    continue;

                var rawJson = candidates[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (string.IsNullOrWhiteSpace(rawJson))
                    continue;

                var parsed = JsonSerializer.Deserialize<GeminiShortAdaptationDto>(rawJson, JsonOptions);
                if (parsed != null && !string.IsNullOrWhiteSpace(parsed.Title) && !string.IsNullOrWhiteSpace(parsed.CleanDescription))
                {
                    return (parsed.Title.Trim(), parsed.CleanDescription.Trim());
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Error al invocar Gemini ({Model}) para adaptar Reel a YouTube Short. Probando siguiente o fallback.", model);
            }
        }

        return null;
    }

    public static string BuildDeterministicTitle(string? rawCaption)
    {
        if (string.IsNullOrWhiteSpace(rawCaption))
            return "Juegos de mesa en tu mesa | Ludeka #Shorts";

        var lines = rawCaption
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !l.StartsWith('#'))
            .ToList();

        var firstLine = lines.Count > 0 ? lines[0] : rawCaption;

        // Limpiar hashtags y menciones @ en el título
        firstLine = Regex.Replace(firstLine, @"#\S+", string.Empty).Trim();
        firstLine = MentionRegex.Replace(firstLine, m => HumanizeUnknownHandle(m.Groups[1].Value)).Trim();

        if (string.IsNullOrWhiteSpace(firstLine))
            firstLine = "Juegos de mesa en Ludeka";

        return NormalizeShortTitle(firstLine, rawCaption);
    }

    public static string NormalizeShortTitle(string candidateTitle, string? fallbackCaption = null)
    {
        var clean = (candidateTitle ?? string.Empty).Trim();
        clean = Regex.Replace(clean, @"\s+#shorts\b", string.Empty, RegexOptions.IgnoreCase).Trim();

        if (string.IsNullOrWhiteSpace(clean))
        {
            clean = "Juegos de mesa en Ludeka";
        }

        const string suffix = " #Shorts";
        int maxBaseLength = 100 - suffix.Length; // 92 chars

        if (clean.Length > maxBaseLength)
        {
            clean = clean[..(maxBaseLength - 1)].TrimEnd() + "…";
        }

        return clean + suffix;
    }

    public static string BuildDeterministicBody(string? rawCaption, IReadOnlyList<ResolvedMentionDto> resolvedMentions)
    {
        if (string.IsNullOrWhiteSpace(rawCaption))
            return "¡Nuevo vídeo de juegos de mesa en Ludeka!";

        var text = rawCaption.Trim();

        // 1. Limpiar giros exclusivos de Instagram ("link en bio", "enlace en la bio")
        text = Regex.Replace(
            text,
            @"(?:tienes\s+el\s+)?(?:link|enlace)\s+en\s+(?:nuestra\s+|la\s+|mi\s+)?(?:bio|biograf[ií]a|perfil)\.?",
            string.Empty,
            RegexOptions.IgnoreCase).Trim();

        // 2. Aplicar reemplazos del directorio y humanizar @handles desconocidos
        return ApplyDeterministicMentionSafeguards(text, resolvedMentions);
    }

    public static string ApplyDeterministicMentionSafeguards(string text, IReadOnlyList<ResolvedMentionDto> resolvedMentions)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var resolvedMap = resolvedMentions.ToDictionary(
            m => m.InstagramHandle.TrimStart('@').ToLowerInvariant(),
            m => m,
            StringComparer.OrdinalIgnoreCase);

        var allowedYtHandles = new HashSet<string>(
            resolvedMentions
                .Where(m => !string.IsNullOrWhiteSpace(m.YouTubeHandle))
                .Select(m => m.YouTubeHandle!.TrimStart('@').ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        return MentionRegex.Replace(text, match =>
        {
            var handle = match.Groups[1].Value.TrimEnd('.');
            var trailingDots = match.Groups[1].Value[handle.Length..];

            if (allowedYtHandles.Contains(handle.ToLowerInvariant()))
            {
                return match.Value;
            }

            if (resolvedMap.TryGetValue(handle.ToLowerInvariant(), out var resolved))
            {
                return resolved.ReplacementText + trailingDots;
            }

            return HumanizeUnknownHandle(handle) + trailingDots;
        });
    }

    public static string AppendLudekaDeterministicFooter(
        string cleanBody,
        IReadOnlyList<ResolvedMentionDto> resolvedMentions,
        string publicBaseUrl = "https://ludeka.es")
    {
        var baseUrl = string.IsNullOrWhiteSpace(publicBaseUrl) ? "https://ludeka.es" : publicBaseUrl.TrimEnd('/');
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(cleanBody))
        {
            sb.AppendLine(cleanBody.Trim());
            sb.AppendLine();
        }

        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━");

        var mentionsWithProfile = resolvedMentions
            .Where(m => !string.IsNullOrWhiteSpace(m.LudekaProfileUrl))
            .DistinctBy(m => m.LudekaProfileUrl, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (mentionsWithProfile.Count > 0)
        {
            sb.AppendLine("🏷️ Mencionados en este vídeo:");
            foreach (var item in mentionsWithProfile)
            {
                sb.AppendLine($"• {item.OfficialName}: {item.LudekaProfileUrl}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("🎲 Descubre más juegos de mesa, compara precios en tiendas, sorteos y eventos en tu mesa:");
        sb.AppendLine($"👉 {baseUrl}");
        sb.AppendLine();
        sb.Append("#Shorts #JuegosDeMesa #BoardGames #Ludeka");

        return sb.ToString();
    }

    public static string HumanizeUnknownHandle(string rawHandle)
    {
        var clean = rawHandle.Trim().TrimStart('@').TrimEnd('.');
        if (string.IsNullOrWhiteSpace(clean))
            return string.Empty;

        var spaced = clean.Replace('_', ' ').Replace('.', ' ');
        spaced = Regex.Replace(spaced, @"\s+", " ").Trim();

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced.ToLowerInvariant());
    }

    private static bool MatchesEntityInstagram(IEnumerable<SocialNetworkLink> links, string entitySlug, string normalizedIgHandle)
    {
        if (NormalizeHandle(entitySlug) == normalizedIgHandle)
            return true;

        foreach (var link in links)
        {
            if (link.Platform != SocialPlatform.Instagram)
                continue;

            if (!string.IsNullOrWhiteSpace(link.Handle) && NormalizeHandle(link.Handle) == normalizedIgHandle)
                return true;

            if (ExtractHandleFromUrl(link.Url) == normalizedIgHandle)
                return true;
        }

        return false;
    }

    private static string NormalizeHandle(string? handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
            return string.Empty;

        return handle.Trim().TrimStart('@').TrimEnd('/').ToLowerInvariant();
    }

    private static string ExtractHandleFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        if (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0)
            {
                return NormalizeHandle(segments[^1]);
            }
        }

        return NormalizeHandle(url);
    }

    private static string? ExtractYouTubeHandle(string? handle, string? url)
    {
        if (!string.IsNullOrWhiteSpace(handle))
        {
            var trimmed = handle.Trim();
            if (trimmed.StartsWith("UC", StringComparison.Ordinal) && trimmed.Length >= 20)
            {
                // Es un Channel ID técnico (UC...), no un @handle legible
            }
            else
            {
                return trimmed.StartsWith('@') ? trimmed : $"@{trimmed}";
            }
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            var match = Regex.Match(url, @"youtube\.com/@([a-zA-Z0-9._-]+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return $"@{match.Groups[1].Value}";
            }
        }

        return null;
    }

    private sealed class GeminiShortAdaptationDto
    {
        public string? Title { get; set; }
        public string? CleanDescription { get; set; }
    }
}
