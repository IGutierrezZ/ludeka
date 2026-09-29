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

    public async Task<SocialAiAnalysisResultDto> AnalyzeMultimodalAsync(
        string? text,
        byte[]? basesImageBytes,
        string? basesImageMimeType,
        byte[]? coverImageBytes = null,
        string? coverImageMimeType = null,
        string? authorOrChannel = null,
        CancellationToken ct = default)
    {
        var cleanText = text?.Trim() ?? string.Empty;

        // Si no hay ninguna imagen, delegar a AnalyzeTextAsync
        if ((basesImageBytes == null || basesImageBytes.Length == 0) &&
            (coverImageBytes == null || coverImageBytes.Length == 0))
        {
            return await AnalyzeTextAsync(cleanText, authorOrChannel, ct);
        }

        // Si hay ApiKey y no está en modo simulado, intentar Gemini API Multimodal
        if (!_options.ShouldSimulate && !string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            try
            {
                var geminiResult = await CallGeminiMultimodalApiAsync(
                    cleanText,
                    basesImageBytes,
                    basesImageMimeType,
                    coverImageBytes,
                    coverImageMimeType,
                    authorOrChannel,
                    ct);

                if (geminiResult != null)
                {
                    return geminiResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al invocar Google Gemini Vision para análisis multimodal. Usando fallback heurístico.");
            }
        }

        // Fallback heurístico multimodal local
        return GenerateHeuristicMultimodal(cleanText, authorOrChannel, basesImageBytes != null || coverImageBytes != null);
    }

    private async Task<SocialAiAnalysisResultDto?> CallGeminiApiAsync(string text, string? authorOrChannel, CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;
        var prompt = $@"
Eres el asistente de ingesta social y catalogación de Ludeka, la plataforma comunitaria de juegos de mesa en español.
Fecha actual de referencia para el análisis: {nowUtc:yyyy-MM-dd} (año en curso: {nowUtc.Year}).
Analiza la siguiente publicación social de Instagram, YouTube o web del sector lúdico:

Autor/Canal de la publicación: {authorOrChannel ?? "Desconocido"}
Texto de la publicación:
""""""
{text}
""""""

Instrucciones para fechas:
- Si la publicación indica un día y mes sin año explícito (ej. 'hasta el 2 de octubre', '5 de noviembre'), asume el año en curso ({nowUtc.Year}) o el siguiente año ({nowUtc.Year + 1}) si la fecha en el año actual ya hubiese vencido respecto a la fecha actual ({nowUtc:yyyy-MM-dd}).
- NUNCA inventes o asumas años pasados (como 2024 o anteriores) a menos que figuren expresamente en el texto.
- Devuelve la fecha siempre en formato ISO UTC completo YYYY-MM-DDTHH:mm:ssZ. Si es una fecha límite de sorteo sin hora especificada, asigna las 23:59:59Z.

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
            Notes: parsed.Notes,
            CropBoundingBox: null,
            TerritorialScope: parsed.TerritorialScope ?? parsed.Location);
    }

    private async Task<SocialAiAnalysisResultDto?> CallGeminiMultimodalApiAsync(
        string? text,
        byte[]? basesImageBytes,
        string? basesImageMimeType,
        byte[]? coverImageBytes,
        string? coverImageMimeType,
        string? authorOrChannel,
        CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;
        var prompt = $@"
Eres el asistente de ingesta social y catalogación de Ludeka, la plataforma comunitaria de juegos de mesa en español.
Fecha actual de referencia para el análisis: {nowUtc:yyyy-MM-dd} (año en curso: {nowUtc.Year}).
Analiza la siguiente publicación social de Instagram, YouTube o web del sector lúdico, prestando especial atención a las imágenes adjuntas (captura de bases del sorteo, cartel o portada del post):

Autor/Canal de la publicación: {authorOrChannel ?? "Desconocido"}
Texto adicional proporcionado por el usuario:
""""""
{text ?? string.Empty}
""""""

Instrucciones prioritarias:
1. Realiza OCR sobre la captura de pantalla de bases o texto si está adjunta. Extrae las condiciones, fecha límite, ámbito territorial y premios.
2. Si es un sorteo, extrae organizador, colaboradores (@cuentas), premio (juego o accesorio), fecha límite exacta y ámbito geográfico (ej. 'Península', 'España', 'Baleares y Canarias', 'Internacional').
3. Si el texto o la imagen de bases indica un día y mes sin año explícito (ej. 'hasta el 2 de octubre', 'el 5 de noviembre'), asume el año en curso ({nowUtc.Year}) o el siguiente año ({nowUtc.Year + 1}) si la fecha en el año actual ya hubiese vencido respecto a la fecha actual ({nowUtc:yyyy-MM-dd}). NUNCA infieras o asumas años pasados (como 2024 o anteriores) salvo que figuren expresamente en la publicación. Si es fecha límite sin hora especificada, asigna las 23:59:59Z.
4. Si hay una imagen del cartel o post, devuelve en 'cropBoundingBox' las 4 coordenadas normalizadas [ymin, xmin, ymax, xmax] (valores enteros entre 0 y 1000) que aíslan la imagen principal del cartel/premio, descartando la barra superior de estado del teléfono (hora, batería) y la barra inferior de navegación de Instagram.

Devuelve EXCLUSIVAMENTE un objeto JSON válido con los siguientes campos:
{{
  ""detectedType"": ""Giveaway"" | ""WeeklyRelease"" | ""BoardGameEvent"" | ""MediaItem"",
  ""title"": ""Título conciso en español"",
  ""organizerOrAuthor"": ""Editorial, organizador o creador responsable"",
  ""collaborator"": ""Colaborador o cuenta asociada si existe, o null"",
  ""suggestedGameTitle"": ""Título del juego de mesa principal o premio, o null"",
  ""eventOrReleaseDateIso"": ""YYYY-MM-DDTHH:mm:ssZ (fecha límite de sorteo, fecha de inicio de evento o fecha de lanzamiento) o null"",
  ""eventEndDateIso"": ""YYYY-MM-DDTHH:mm:ssZ o null"",
  ""location"": ""Ámbito territorial si es sorteo (ej. Península, España) o ciudad si es evento, o null"",
  ""territorialScope"": ""Península"" | ""España"" | ""Internacional"" | null,
  ""estimatedPvp"": número decimal si se indica precio, o null,
  ""mediaCategory"": ""Tutorial"" | ""Playthrough"" | ""Review"" | null,
  ""playerCountBadge"": ""Ej. 'Partida a 2' o null"",
  ""notes"": ""Resumen o bases breves del contenido"",
  ""cropBoundingBox"": [ymin, xmin, ymax, xmax] o null
}}";

        var effectiveModel = string.IsNullOrWhiteSpace(_options.Model)
            ? GeminiOptions.DefaultModel
            : _options.Model;

        var partsList = new System.Collections.Generic.List<object>
        {
            new { text = prompt }
        };

        if (basesImageBytes != null && basesImageBytes.Length > 0)
        {
            partsList.Add(new
            {
                inlineData = new
                {
                    mimeType = !string.IsNullOrWhiteSpace(basesImageMimeType) ? basesImageMimeType : "image/jpeg",
                    data = Convert.ToBase64String(basesImageBytes)
                }
            });
        }

        if (coverImageBytes != null && coverImageBytes.Length > 0)
        {
            partsList.Add(new
            {
                inlineData = new
                {
                    mimeType = !string.IsNullOrWhiteSpace(coverImageMimeType) ? coverImageMimeType : "image/jpeg",
                    data = Convert.ToBase64String(coverImageBytes)
                }
            });
        }

        var requestBody = new
        {
            contents = new[]
            {
                new { parts = partsList }
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
            _logger.LogWarning("Gemini Vision API devolvió código {Code}: {Error}", response.StatusCode, err);
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

        NormalizedBoundingBoxDto? boundingBox = null;
        if (parsed.CropBoundingBox != null && parsed.CropBoundingBox.Length == 4)
        {
            var b = new NormalizedBoundingBoxDto(
                parsed.CropBoundingBox[0],
                parsed.CropBoundingBox[1],
                parsed.CropBoundingBox[2],
                parsed.CropBoundingBox[3]);
            if (b.IsValid)
            {
                boundingBox = b;
            }
        }

        return new SocialAiAnalysisResultDto(
            DetectedType: type,
            Title: !string.IsNullOrWhiteSpace(parsed.Title) ? parsed.Title : "Sorteo Asistido",
            OrganizerOrAuthor: !string.IsNullOrWhiteSpace(parsed.OrganizerOrAuthor) ? parsed.OrganizerOrAuthor : authorOrChannel ?? "Editorial / Creador",
            Collaborator: parsed.Collaborator,
            SuggestedGameTitle: parsed.SuggestedGameTitle,
            EventOrReleaseDate: startDate,
            EventEndDate: endDate,
            Location: parsed.Location,
            EstimatedPvp: parsed.EstimatedPvp,
            MediaCategory: category,
            PlayerCountBadge: parsed.PlayerCountBadge,
            Notes: parsed.Notes,
            CropBoundingBox: boundingBox,
            TerritorialScope: parsed.TerritorialScope ?? parsed.Location);
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

        string? territorialScope = null;
        if (lower.Contains("península") || lower.Contains("peninsula"))
            territorialScope = "Península";
        else if (lower.Contains("españa") || lower.Contains("espana"))
            territorialScope = "España";
        else if (lower.Contains("internacional"))
            territorialScope = "Internacional";

        return new SocialAiAnalysisResultDto(
            DetectedType: type,
            Title: title,
            OrganizerOrAuthor: author,
            Collaborator: collaborator,
            SuggestedGameTitle: gameTitle,
            EventOrReleaseDate: startDate,
            EventEndDate: endDate,
            Location: location ?? territorialScope,
            EstimatedPvp: null,
            MediaCategory: mediaCategory,
            PlayerCountBadge: playerCountBadge,
            Notes: "Detección heurística de patrones editoriales en español",
            CropBoundingBox: null,
            TerritorialScope: territorialScope);
    }

    public static SocialAiAnalysisResultDto GenerateHeuristicMultimodal(string? text, string? authorOrChannel, bool hasImages)
    {
        var clean = !string.IsNullOrWhiteSpace(text) ? text : "¡Sorteo activo! Bases y participación";
        var res = GenerateHeuristic(clean, authorOrChannel);

        if (hasImages)
        {
            var defaultBox = new NormalizedBoundingBoxDto(45, 0, 945, 1000);
            return res with
            {
                DetectedType = SocialSubmissionType.Giveaway,
                CropBoundingBox = defaultBox,
                TerritorialScope = res.TerritorialScope ?? "Península"
            };
        }

        return res;
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
        public string? TerritorialScope { get; set; }
        public decimal? EstimatedPvp { get; set; }
        public string? MediaCategory { get; set; }
        public string? PlayerCountBadge { get; set; }
        public string? Notes { get; set; }
        public int[]? CropBoundingBox { get; set; }
    }
}
