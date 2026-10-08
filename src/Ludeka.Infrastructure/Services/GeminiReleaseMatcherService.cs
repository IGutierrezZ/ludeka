using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Infrastructure.Services;

/// <summary>
/// Implementación de IReleaseAiMatcherService que consulta a Google Gemini para deducir el título original BGG,
/// su ID y razonamiento, con fallback heurístico determinista para pruebas y entornos offline.
/// </summary>
public class GeminiReleaseMatcherService : IReleaseAiMatcherService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiReleaseMatcherService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GeminiReleaseMatcherService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiReleaseMatcherService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? new GeminiOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AiReleaseMatchResultDto> SuggestMatchAsync(
        string rawTitle,
        string publisher,
        decimal? estimatedPvp,
        string? notes,
        CancellationToken ct = default)
    {
        var cleanTitle = CleanTitle(rawTitle);

        if (string.IsNullOrWhiteSpace(cleanTitle))
        {
            return new AiReleaseMatchResultDto(
                SuggestedBggId: null,
                SuggestedTitle: null,
                Reasoning: "No se proporcionó un título de producto reconocible.");
        }

        // Si hay API key y no estamos en modo simulación forzado, intentar Gemini
        if (!_options.ShouldSimulate && !string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            try
            {
                var geminiResult = await CallGeminiMatcherApiAsync(cleanTitle, publisher, estimatedPvp, notes, ct).ConfigureAwait(false);
                if (geminiResult != null && (!string.IsNullOrWhiteSpace(geminiResult.SuggestedTitle) || geminiResult.SuggestedBggId.HasValue))
                {
                    return geminiResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al invocar Google Gemini para deducir correspondencia BGG de '{Title}'. Aplicando fallback heurístico.", cleanTitle);
            }
        }

        // Fallback heurístico determinista (diccionario de traducciones conocidas + normalización)
        return GenerateHeuristicMatch(cleanTitle, publisher, estimatedPvp, notes);
    }

    private async Task<AiReleaseMatchResultDto?> CallGeminiMatcherApiAsync(
        string title,
        string publisher,
        decimal? pvp,
        string? notes,
        CancellationToken ct)
    {
        var effectiveModel = _options.GetEffectiveModel();

        var prompt = $@"Eres un experto en juegos de mesa modernos y en la base de datos de BoardGameGeek (BGG).
Una editorial española ({publisher}) ha anunciado el siguiente lanzamiento o preventa:
- Título publicado en tienda: ""{title}""
- Precio estimado: {(pvp.HasValue ? $"{pvp.Value}€" : "Desconocido")}
- Notas contextuales: ""{notes ?? "Sin notas"}""

Tu tarea:
1. Deducir el título canónico original en inglés/internacional registrado en BGG.
2. Si conoces con certeza el BGG ID numérico oficial del juego, inclúyelo en ""suggestedBggId"". Si tienes dudas o no existe, pon null.
3. Explicar brevemente en 1-2 frases en castellano peninsular la correspondencia editorial (editorial original, diseñador o equivalencia de título).

Responde ÚNICAMENTE con un JSON válido con esta estructura:
{{
  ""suggestedBggId"": 367209,
  ""suggestedTitle"": ""Galactic Cruise"",
  ""reasoning"": ""Edición en castellano de Maldito Games del juego 'Galactic Cruise' (2024), diseñado por Dennis Northcott y T.K. King.""
}}";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                responseMimeType = "application/json"
            }
        };

        var jsonPayload = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var requestUri = $"{_options.BaseUrl.TrimEnd('/')}/models/{effectiveModel}:generateContent?key={_options.ApiKey}";

        using var response = await _httpClient.PostAsync(requestUri, content, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            _logger.LogWarning("Gemini API devolvió código {Code} al inferir BGG: {Error}", response.StatusCode, err);
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(responseJson);

        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return null;

        var candidate = candidates[0];
        if (!candidate.TryGetProperty("content", out var contentProp) ||
            !contentProp.TryGetProperty("parts", out var parts) ||
            parts.GetArrayLength() == 0)
            return null;

        var rawJson = parts[0].GetProperty("text").GetString();
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;

        var parsed = JsonSerializer.Deserialize<GeminiMatcherResponseDto>(rawJson, JsonOptions);
        if (parsed == null)
            return null;

        return new AiReleaseMatchResultDto(
            SuggestedBggId: parsed.SuggestedBggId,
            SuggestedTitle: !string.IsNullOrWhiteSpace(parsed.SuggestedTitle) ? parsed.SuggestedTitle.Trim() : title,
            Reasoning: parsed.Reasoning ?? $"Sugerido por Gemini ({effectiveModel})");
    }

    private static AiReleaseMatchResultDto GenerateHeuristicMatch(
        string title,
        string publisher,
        decimal? estimatedPvp,
        string? notes)
    {
        var normalized = NormalizeKey(title);

        // Diccionario determinista de equivalencias frecuentes entre títulos españoles de Maldito/Devir y BGG
        if (KnownEquivalences.TryGetValue(normalized, out var known))
        {
            return new AiReleaseMatchResultDto(
                SuggestedBggId: known.BggId,
                SuggestedTitle: known.CanonicalTitle,
                Reasoning: known.Reasoning);
        }

        // Si contiene sufijos de expansión conocidos
        if (normalized.Contains("expansion", StringComparison.OrdinalIgnoreCase))
        {
            var baseName = Regex.Replace(title, @"(?i)\b(expansión|expansion|set de expansión)\b.*", "").Trim();
            if (!string.IsNullOrWhiteSpace(baseName))
            {
                return new AiReleaseMatchResultDto(
                    SuggestedBggId: null,
                    SuggestedTitle: baseName,
                    Reasoning: $"Expansión para el juego '{baseName}' editada por {publisher}.");
            }
        }

        return new AiReleaseMatchResultDto(
            SuggestedBggId: null,
            SuggestedTitle: title,
            Reasoning: $"Propuesta preliminar de IA por correspondencia nominal con '{title}' ({publisher}).");
    }

    private static string CleanTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var clean = Regex.Replace(title, @"(?i)\s*\((?:edici[oó]n en espa[nñ]ol|castellano|espa[nñ]ol|preventa)\)", "").Trim();
        return clean;
    }

    private static string NormalizeKey(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var norm = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in norm)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return Regex.Replace(sb.ToString().ToLowerInvariant(), @"[^a-z0-9]", "");
    }

    private static readonly Dictionary<string, (int? BggId, string CanonicalTitle, string Reasoning)> KnownEquivalences = new()
    {
        ["crucerogalactico"] = (367209, "Galactic Cruise", "Edición en castellano de Maldito Games para 'Galactic Cruise' (2024), diseñado por Dennis Northcott y T.K. King."),
        ["los12trabajosdehercules"] = (374945, "12 Labours of Hercules", "Traducción oficial al castellano del juego solitario '12 Labours of Hercules'."),
        ["12trabajosdehercules"] = (374945, "12 Labours of Hercules", "Traducción oficial al castellano de '12 Labours of Hercules' publicada por Maldito Games."),
        ["elvalledelosmolinos"] = (416560, "Windmill Valley", "Edición en español de Maldito Games para 'Windmill Valley' del autor Dani Garcia."),
        ["valledelosmolinos"] = (416560, "Windmill Valley", "Edición en español de 'Windmill Valley' (Board&Dice / Maldito Games)."),
        ["thehanginggardens"] = (32679, "The Hanging Gardens", "Lanzamiento oficial de Devir para 'The Hanging Gardens'."),
        ["hanginggardens"] = (32679, "The Hanging Gardens", "Lanzamiento de Devir para 'The Hanging Gardens'."),
        ["lacrimosa"] = (348450, "Lacrimosa", "Juego publicado por Devir con título idéntico en BoardGameGeek."),
        ["amphipolis"] = (396860, "Amphipolis", "Publicación de Maldito Games con título canónico 'Amphipolis'."),
        ["diogenes"] = (421882, "Diogenes", "Juego de cartas 'Diogenes' publicado por Maldito Games."),
        ["theredcathedral"] = (284678, "The Red Cathedral", "Juego de Devir ('The Red Cathedral')."),
        ["lacatedralroja"] = (284678, "The Red Cathedral", "Traducción en español de 'The Red Cathedral' de Devir."),
        ["daggerheart"] = (null, "Daggerheart", "Juego de mesa / rol de Darrington Press distribuido por Devir."),
        ["enelabismo"] = (null, "In the Abyss", "Título de Devir con posible referencia internacional 'In the Abyss'.")
    };

    private class GeminiMatcherResponseDto
    {
        public int? SuggestedBggId { get; set; }
        public string? SuggestedTitle { get; set; }
        public string? Reasoning { get; set; }
    }
}
