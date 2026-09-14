using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Infrastructure.Services;

public class GeminiSocialAnalysisService : ISocialAiAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiSocialAnalysisService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GeminiSocialAnalysisService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiSocialAnalysisService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? new GeminiOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SocialAiAnalysisResultDto> AnalyzeTextAsync(string text, string? authorOrChannel = null, CancellationToken ct = default)
    {
        var cleanText = text?.Trim() ?? string.Empty;

        // 1. Si no hay texto, retornar heurística básica
        if (string.IsNullOrWhiteSpace(cleanText))
        {
            return GenerateHeuristic(cleanText, authorOrChannel);
        }

        // 2. Si hay ApiKey y no está en modo simulado, intentar Gemini API
        if (!_options.ShouldSimulate && !string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            try
            {
                var geminiResult = await CallGeminiApiAsync(cleanText, authorOrChannel, ct);
                if (geminiResult != null)
                {
                    return geminiResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al invocar Google Gemini para análisis de texto social. Usando fallback heurístico.");
            }
        }

        // 3. Fallback heurístico inteligente local
        return GenerateHeuristic(cleanText, authorOrChannel);
    }

    private async Task<SocialAiAnalysisResultDto?> CallGeminiApiAsync(string text, string? authorOrChannel, CancellationToken ct)
    {
        var prompt = $@"
Eres el asistente de ingesta social y catalogación de Ludeka, la plataforma comunitaria de juegos de mesa en español.
Analiza la siguiente publicación social de Instagram, YouTube o web del sector lúdico:

Autor/Canal de la publicación: {authorOrChannel ?? "Desconocido"}
Texto de la publicación:
""""""
{text}
""""""

Devuelve EXCLUSIVAMENTE un objeto JSON válido con los siguientes campos:
{{
  ""detectedType"": ""Giveaway"" | ""WeeklyRelease"" | ""BoardGameEvent"" | ""MediaItem"",
  ""title"": ""Título conciso en español"",
  ""organizerOrAuthor"": ""Editorial, organizador o creador responsable"",
  ""collaborator"": ""Colaborador o cuenta asociada si existe, o null"",
  ""suggestedGameTitle"": ""Título del juego de mesa principal, o null"",
  ""eventOrReleaseDateIso"": ""YYYY-MM-DDTHH:mm:ssZ (fecha límite de sorteo, fecha de inicio de evento o fecha de lanzamiento) o null"",
  ""eventEndDateIso"": ""YYYY-MM-DDTHH:mm:ssZ (fecha de fin si el evento dura varios días) o null"",
  ""location"": ""Ciudad o recinto si es un evento, o null"",
  ""estimatedPvp"": número decimal si se indica precio, o null,
  ""mediaCategory"": ""Tutorial"" | ""Playthrough"" | ""Review"" | null,
  ""playerCountBadge"": ""Ej. 'Partida a 2' o null"",
  ""notes"": ""Resumen o bases breves del contenido""
}}";

        var effectiveModel = string.IsNullOrWhiteSpace(_options.Model)
            ? GeminiOptions.DefaultModel
            : _options.Model;

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.1
            }
        };

        var jsonPayload = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var requestUri = $"{_options.BaseUrl.TrimEnd('/')}/models/{effectiveModel}:generateContent?key={_options.ApiKey}";

        using var response = await _httpClient.PostAsync(requestUri, content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Gemini API devolvió código {Code}: {Error}", response.StatusCode, err);
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
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

        var parsed = JsonSerializer.Deserialize<GeminiAnalysisResponseDto>(rawJson, JsonOptions);
        if (parsed == null)
            return null;

        var type = Enum.TryParse<SocialSubmissionType>(parsed.DetectedType, true, out var t)
            ? t
            : SocialSubmissionType.Giveaway;

        DateTimeOffset? startDate = null;
        if (!string.IsNullOrWhiteSpace(parsed.EventOrReleaseDateIso) &&
            DateTimeOffset.TryParse(parsed.EventOrReleaseDateIso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d1))
        {
            startDate = d1;
        }

        DateTimeOffset? endDate = null;
        if (!string.IsNullOrWhiteSpace(parsed.EventEndDateIso) &&
            DateTimeOffset.TryParse(parsed.EventEndDateIso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d2))
        {
            endDate = d2;
        }

        MediaCategory? category = null;
        if (!string.IsNullOrWhiteSpace(parsed.MediaCategory) &&
            Enum.TryParse<MediaCategory>(parsed.MediaCategory, true, out var cat))
        {
            category = cat;
        }

        return new SocialAiAnalysisResultDto(
            DetectedType: type,
            Title: !string.IsNullOrWhiteSpace(parsed.Title) ? parsed.Title : "Publicación Asistida",
            OrganizerOrAuthor: !string.IsNullOrWhiteSpace(parsed.OrganizerOrAuthor) ? parsed.OrganizerOrAuthor : authorOrChannel ?? "Editorial / Creador",
            Collaborator: parsed.Collaborator,
            SuggestedGameTitle: parsed.SuggestedGameTitle,
            EventOrReleaseDate: startDate,
            EventEndDate: endDate,
            Location: parsed.Location,
            EstimatedPvp: parsed.EstimatedPvp,
            MediaCategory: category,
            PlayerCountBadge: parsed.PlayerCountBadge,
            Notes: parsed.Notes);
    }

    public static SocialAiAnalysisResultDto GenerateHeuristic(string text, string? authorOrChannel)
    {
        var lower = text.ToLowerInvariant();
        var author = !string.IsNullOrWhiteSpace(authorOrChannel) ? authorOrChannel.Trim() : "Comunidad";

        // 1. Detección de Tipo
        SocialSubmissionType type;
        if (lower.Contains("sorteo") || lower.Contains("sorteamos") || lower.Contains("participa") || lower.Contains("bases del sorteo") || lower.Contains("regalamos"))
        {
            type = SocialSubmissionType.Giveaway;
        }
        else if (lower.Contains("feria") || lower.Contains("festival") || lower.Contains("jornadas") || lower.Contains("convención") || lower.Contains("convencion") || lower.Contains("interocio") || lower.Contains("essen"))
        {
            type = SocialSubmissionType.BoardGameEvent;
        }
        else if (lower.Contains("novedad") || lower.Contains("ya en tiendas") || lower.Contains("lanzamiento") || lower.Contains("a la venta") || lower.Contains("reimpresión") || lower.Contains("reimpresion"))
        {
            type = SocialSubmissionType.WeeklyRelease;
        }
        else if (lower.Contains("tutorial") || lower.Contains("cómo jugar") || lower.Contains("como jugar") || lower.Contains("partida") || lower.Contains("gameplay") || lower.Contains("reseña") || lower.Contains("unboxing"))
        {
            type = SocialSubmissionType.MediaItem;
        }
        else
        {
            type = SocialSubmissionType.Giveaway;
        }

        // 2. Extracción de juego sugerido
        string? gameTitle = null;
        var gameMatch = Regex.Match(text, @"(?:sorteo\s+(?:de\s+)?|partida\s+(?:a\s+)?|tutorial\s+(?:de\s+)?|novedad:\s*|juego\s+)?[""«]([^""»]+)[""»]", RegexOptions.IgnoreCase);
        if (gameMatch.Success)
        {
            gameTitle = gameMatch.Groups[1].Value.Trim();
        }
        else
        {
            var wordMatch = Regex.Match(text, @"(?:sorteo\s+(?:de\s+)?|partida\s+(?:a\s+)?|tutorial\s+(?:de\s+)?)([A-ZÁÉÍÓÚ][a-zA-ZáéíóúñÑ0-9\s]{2,25})", RegexOptions.Compiled);
            if (wordMatch.Success)
            {
                gameTitle = wordMatch.Groups[1].Value.Trim();
            }
        }

        // 3. Extracción de colaborador
        string? collaborator = null;
        var collabHandleMatch = Regex.Match(text, @"(?:en\s+colaboraci[oó]n\s+con|junto\s+a)\s+@([A-Za-z0-9_.-]+)", RegexOptions.IgnoreCase);
        if (collabHandleMatch.Success)
        {
            collaborator = collabHandleMatch.Groups[1].Value.Trim();
        }
        else
        {
            var collabMatch = Regex.Match(text, @"(?:en\s+colaboraci[oó]n\s+con|junto\s+a)\s+([A-Za-z0-9_ÁÉÍÓÚáéíóúñÑ.-]+(?:\s+[A-Za-z0-9_ÁÉÍÓÚáéíóúñÑ.-]+)?)", RegexOptions.IgnoreCase);
            if (collabMatch.Success)
            {
                collaborator = collabMatch.Groups[1].Value.Trim();
            }
        }

        // 4. Extracción de fechas
        DateTimeOffset? startDate = null;
        DateTimeOffset? endDate = null;

        var rangeMatch = Regex.Match(text, @"del\s+(\d{1,2})\s+al\s+(\d{1,2})\s+de\s+(enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|octubre|noviembre|diciembre)", RegexOptions.IgnoreCase);
        if (rangeMatch.Success)
        {
            var day1 = int.Parse(rangeMatch.Groups[1].Value);
            var day2 = int.Parse(rangeMatch.Groups[2].Value);
            var month = ParseSpanishMonth(rangeMatch.Groups[3].Value);
            var year = DateTime.UtcNow.Year;
            if (month < DateTime.UtcNow.Month) year++;

            startDate = new DateTimeOffset(year, month, day1, 10, 0, 0, TimeSpan.Zero);
            endDate = new DateTimeOffset(year, month, day2, 20, 0, 0, TimeSpan.Zero);
        }
        else
        {
            var singleDateMatch = Regex.Match(text, @"(?:hasta\s+el|el\s+pr[oó]ximo|el|fecha\s+l[ií]mite:?)\s+(\d{1,2})\s+de\s+(enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|octubre|noviembre|diciembre)", RegexOptions.IgnoreCase);
            if (singleDateMatch.Success)
            {
                var day = int.Parse(singleDateMatch.Groups[1].Value);
                var month = ParseSpanishMonth(singleDateMatch.Groups[2].Value);
                var year = DateTime.UtcNow.Year;
                if (month < DateTime.UtcNow.Month) year++;

                startDate = new DateTimeOffset(year, month, day, 23, 59, 59, TimeSpan.Zero);
            }
        }

        // 5. Categoría y badge de medios
        MediaCategory? mediaCategory = null;
        string? playerCountBadge = null;

        if (type == SocialSubmissionType.MediaItem)
        {
            if (lower.Contains("tutorial") || lower.Contains("cómo jugar") || lower.Contains("como jugar"))
                mediaCategory = MediaCategory.Tutorial;
            else if (lower.Contains("partida") || lower.Contains("gameplay"))
            {
                mediaCategory = MediaCategory.Gameplay;
                if (lower.Contains("a 2") || lower.Contains("a dos") || lower.Contains("para 2"))
                    playerCountBadge = "Partida a 2";
                else if (lower.Contains("solitario") || lower.Contains("en solitario"))
                    playerCountBadge = "En Solitario";
                else if (lower.Contains("a 3"))
                    playerCountBadge = "Partida a 3";
                else if (lower.Contains("a 4"))
                    playerCountBadge = "Partida a 4";
            }
            else
                mediaCategory = MediaCategory.ReviewOpinion;
        }

        // 6. Ubicación si es evento
        string? location = null;
        if (type == SocialSubmissionType.BoardGameEvent)
        {
            if (lower.Contains("madrid") || lower.Contains("ifema")) location = "Madrid";
            else if (lower.Contains("córdoba") || lower.Contains("cordoba")) location = "Córdoba";
            else if (lower.Contains("barcelona")) location = "Barcelona";
            else if (lower.Contains("valencia")) location = "Valencia";
            else location = "España";
        }

        // 7. Título construido
        var title = type switch
        {
            SocialSubmissionType.Giveaway => !string.IsNullOrWhiteSpace(gameTitle)
                ? $"Sorteo de {gameTitle}"
                : $"Sorteo Lúdico de {author}",
            SocialSubmissionType.BoardGameEvent => !string.IsNullOrWhiteSpace(gameTitle)
                ? $"Jornadas {gameTitle}"
                : $"Evento Lúdico {location ?? "Comunitario"}",
            SocialSubmissionType.WeeklyRelease => !string.IsNullOrWhiteSpace(gameTitle)
                ? $"Lanzamiento: {gameTitle}"
                : $"Novedad editorial de {author}",
            SocialSubmissionType.MediaItem => !string.IsNullOrWhiteSpace(gameTitle)
                ? $"{mediaCategory?.ToString() ?? "Vídeo"}: {gameTitle}"
                : $"Vídeo de {author}",
            _ => "Publicación detectada"
        };

        return new SocialAiAnalysisResultDto(
            DetectedType: type,
            Title: title,
            OrganizerOrAuthor: author,
            Collaborator: collaborator,
            SuggestedGameTitle: gameTitle,
            EventOrReleaseDate: startDate,
            EventEndDate: endDate,
            Location: location,
            EstimatedPvp: null,
            MediaCategory: mediaCategory,
            PlayerCountBadge: playerCountBadge,
            Notes: "Detección heurística de patrones editoriales en español");
    }

    private static int ParseSpanishMonth(string monthName)
    {
        return monthName.ToLowerInvariant() switch
        {
            "enero" => 1,
            "febrero" => 2,
            "marzo" => 3,
            "abril" => 4,
            "mayo" => 5,
            "junio" => 6,
            "julio" => 7,
            "agosto" => 8,
            "septiembre" => 9,
            "octubre" => 10,
            "noviembre" => 11,
            "diciembre" => 12,
            _ => DateTime.UtcNow.Month
        };
    }

    private class GeminiAnalysisResponseDto
    {
        public string? DetectedType { get; set; }
        public string? Title { get; set; }
        public string? OrganizerOrAuthor { get; set; }
        public string? Collaborator { get; set; }
        public string? SuggestedGameTitle { get; set; }
        public string? EventOrReleaseDateIso { get; set; }
        public string? EventEndDateIso { get; set; }
        public string? Location { get; set; }
        public decimal? EstimatedPvp { get; set; }
        public string? MediaCategory { get; set; }
        public string? PlayerCountBadge { get; set; }
        public string? Notes { get; set; }
    }
}
