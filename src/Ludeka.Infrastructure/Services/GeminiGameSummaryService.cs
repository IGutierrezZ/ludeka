using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Infrastructure.Services;

/// <summary>
/// Proveedor oficial de síntesis editorial inteligente que conecta con la API REST de Google Gemini
/// e integra autoselección de modelos y un generador heurístico determinista como respaldo transparente (Zero-Crash Fallback).
/// </summary>
public class GeminiGameSummaryService : IAiGameSummaryService
{
    private const string DenialMessage =
        "Se requiere el permiso de moderación 'CanEditGames' para ejecutar la carga nocturna de síntesis con IA.";

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<GeminiGameSummaryService> _logger;
    private readonly ISessionPermissionGuard? _permissionGuard;
    private string? _lastApiError;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GeminiGameSummaryService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        IGameRepository gameRepository,
        ILogger<GeminiGameSummaryService> logger,
        ISessionPermissionGuard? permissionGuard = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? new GeminiOptions();
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _permissionGuard = permissionGuard;
    }

    /// <summary>
    /// Revalida sesión y permiso releyendo el <c>AppUser</c> actual (INC-46, W1) para la carga por
    /// lotes del panel; la síntesis individual la invocan servicios ya guardados o el ciclo nocturno.
    /// </summary>
    private Task RequirePermissionAsync(CancellationToken ct)
        => _permissionGuard is null
            ? Task.CompletedTask
            : _permissionGuard.RequireAsync(ModeratorPermission.CanEditGames, DenialMessage, ct);

    public async Task<AiGameSummaryDto> GenerateSummaryAsync(Game game, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(game);

        // 1. Conmutación a simulación heurística si está configurado explícitamente en modo simulación
        if (_options.ShouldSimulate)
        {
            _logger.LogInformation("Gemini está en modo simulado o sin API Key. Empleando generador heurístico editorial para '{Title}'.", game.SpanishTitle);
            return HeuristicGameSummaryGenerator.Generate(game, "Heurística Editorial");
        }

        // En modo producción (Simulate = false), es obligatorio disponer de ApiKey y respuesta válida
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("Gemini: ApiKey vacía en modo producción para '{Title}'. No se generará texto heurístico inventado.", game.SpanishTitle);
            throw new InvalidOperationException($"No se puede generar la síntesis de IA para '{game.SpanishTitle}': Gemini ApiKey no configurada.");
        }

        // 2. Ejecución con Google Gemini API (con autoselección y autorrecuperación de modelo)
        try
        {
            var summaryFromGemini = await CallGeminiApiAsync(game, ct);
            if (summaryFromGemini != null)
            {
                return summaryFromGemini;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al invocar Google Gemini API para '{Title}'.", game.SpanishTitle);
            throw new HttpRequestException($"Error al invocar Google Gemini API para '{game.SpanishTitle}': {ex.Message}", ex);
        }

        string failureReason = !string.IsNullOrWhiteSpace(_lastApiError)
            ? _lastApiError
            : "Google Gemini API devolvió una respuesta vacía o no estructurada";

        throw new InvalidOperationException($"No se pudo generar la síntesis para '{game.SpanishTitle}': {failureReason}");
    }

    public async Task<AiGameSummaryDto> EnsureSummaryForGameAsync(Guid gameId, CancellationToken ct = default)
    {
        var game = await _gameRepository.GetByIdAsync(gameId, ct)
            ?? throw new InvalidOperationException($"No se encontró el juego con ID {gameId}");

        if (game.AiSummary != null)
        {
            return new AiGameSummaryDto(
                game.Id,
                game.SpanishTitle,
                game.AiSummary.ScalabilitySummary,
                game.AiSummary.AgeSummary,
                game.AiSummary.FootprintSummary,
                game.AiSummary.GeneralVerdict,
                game.AiSummary.Model,
                game.AiSummary.GeneratedAt
            );
        }

        var generated = await GenerateSummaryAsync(game, ct);
        var aiSummaryVo = new AiGameSummary(
            generated.GeneralVerdict,
            generated.ScalabilitySummary,
            generated.AgeSummary,
            generated.FootprintSummary,
            generated.Model,
            generated.GeneratedAt ?? DateTime.UtcNow
        );

        game.SetAiSummary(aiSummaryVo);
        await _gameRepository.UpdateAsync(game, ct);

        return generated;
    }

    public async Task<AiBatchProcessingResultDto> ProcessPendingSummariesBatchAsync(int batchSize = 20, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        if (batchSize < 1) batchSize = 20;

        var gamesWithoutSummary = await _gameRepository.GetGamesWithoutAiSummaryAsync(batchSize, ct);
        if (gamesWithoutSummary.Count == 0)
        {
            return new AiBatchProcessingResultDto(0, 0, 0, []);
        }

        int successCount = 0;
        int failedCount = 0;
        var summarizedTitles = new List<string>();

        foreach (var game in gamesWithoutSummary)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var summary = await GenerateSummaryAsync(game, ct);
                var aiSummaryVo = new AiGameSummary(
                    summary.GeneralVerdict,
                    summary.ScalabilitySummary,
                    summary.AgeSummary,
                    summary.FootprintSummary,
                    summary.Model,
                    summary.GeneratedAt ?? DateTime.UtcNow
                );

                game.SetAiSummary(aiSummaryVo);
                await _gameRepository.UpdateAsync(game, ct);

                summarizedTitles.Add(game.SpanishTitle);
                successCount++;
                _logger.LogInformation("Carga nocturna IA: generada síntesis para '{Title}' ({Model}).", game.SpanishTitle, summary.Model);
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogWarning(ex, "Carga nocturna IA: error al sintetizar el juego '{Title}' (ID {Id}).", game.SpanishTitle, game.Id);
            }
        }

        return new AiBatchProcessingResultDto(
            ProcessedCount: gamesWithoutSummary.Count,
            SuccessCount: successCount,
            FailedCount: failedCount,
            SummarizedTitles: summarizedTitles
        );
    }

    public async Task<AiBatchResultDto> GenerateBatchSummariesAsync(
        IReadOnlyList<AiGameBatchInputDto> games,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(games);

        if (games.Count == 0)
        {
            return new AiBatchResultDto(Success: true, QuotaExhausted: false, Summaries: new Dictionary<int, AiGameSummaryDto>(), ErrorMessage: null);
        }

        // 1. Conmutación a simulación si está configurado o no hay ApiKey
        if (_options.ShouldSimulate || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogInformation("Gemini está en modo simulado o sin ApiKey. Generando síntesis heurística en lote para {Count} juegos.", games.Count);
            var heuristicDict = games.ToDictionary(g => g.BggId, g => HeuristicGameSummaryGenerator.Generate(g, "Heurística Editorial"));
            return new AiBatchResultDto(Success: true, QuotaExhausted: false, Summaries: heuristicDict, ErrorMessage: null);
        }

        // 2. Ejecución con Google Gemini API
        try
        {
            return await CallGeminiBatchApiAsync(games, ct);
        }
        catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning("Google Gemini API: Límite de cuota alcanzado (HTTP 429 Too Many Requests). Pausando lote de IA.");
            return new AiBatchResultDto(Success: false, QuotaExhausted: true, Summaries: new Dictionary<int, AiGameSummaryDto>(), ErrorMessage: "Cuota de Gemini agotada (HTTP 429).");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al invocar Google Gemini API en lote: {Message}", ex.Message);
            return new AiBatchResultDto(Success: false, QuotaExhausted: false, Summaries: new Dictionary<int, AiGameSummaryDto>(), ErrorMessage: ex.Message);
        }
    }

    private async Task<AiGameSummaryDto?> CallGeminiApiAsync(Game game, CancellationToken ct)
    {
        string prompt = BuildPrompt(game);
        string effectiveModel = _options.GetEffectiveModel();

        var result = await TryGenerateWithModelAsync(game, prompt, effectiveModel, ct);
        if (result != null)
        {
            return result;
        }

        // Autorrecuperación: si el modelo configurado no era el predeterminado y falló (ej. 404 o 503),
        // reintentar automáticamente con el modelo canónico recomendado
        if (!effectiveModel.Equals(GeminiOptions.DefaultModel, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Reintentando llamada a Gemini con el modelo predeterminado {DefaultModel}", GeminiOptions.DefaultModel);
            var defaultResult = await TryGenerateWithModelAsync(game, prompt, GeminiOptions.DefaultModel, ct);
            if (defaultResult != null)
            {
                return defaultResult;
            }
        }

        // Fallback de segundo nivel ante posible sobrecarga (HTTP 503) del modelo principal
        const string fallbackStableModel = "gemini-2.5-flash";
        if (!effectiveModel.Equals(fallbackStableModel, StringComparison.OrdinalIgnoreCase) &&
            !GeminiOptions.DefaultModel.Equals(fallbackStableModel, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Reintentando llamada a Gemini con el modelo de respaldo {FallbackModel}", fallbackStableModel);
            return await TryGenerateWithModelAsync(game, prompt, fallbackStableModel, ct);
        }

        return null;
    }

    private async Task<AiGameSummaryDto?> TryGenerateWithModelAsync(Game game, string prompt, string model, CancellationToken ct)
    {
        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.2
            }
        };

        string jsonPayload = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        string requestUri = $"{_options.BaseUrl.TrimEnd('/')}/models/{model}:generateContent?key={_options.ApiKey}";

        using var response = await _httpClient.PostAsync(requestUri, content, ct);
        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(ct);
            _lastApiError = ExtractErrorMessage(errorBody, response.StatusCode, model);
            _logger.LogWarning("Google Gemini API ({Model}) respondió con código {StatusCode}: {Error}", model, response.StatusCode, errorBody);
            return null;
        }

        string responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);

        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.GetArrayLength() == 0)
        {
            _logger.LogWarning("Respuesta de Gemini ({Model}) sin candidatos válidos.", model);
            return null;
        }

        var candidate = candidates[0];
        if (!candidate.TryGetProperty("content", out var contentProp) ||
            !contentProp.TryGetProperty("parts", out var parts) ||
            parts.GetArrayLength() == 0)
        {
            _logger.LogWarning("Respuesta de Gemini ({Model}) sin partes de contenido.", model);
            return null;
        }

        string rawStructuredJson = parts[0].GetProperty("text").GetString() ?? string.Empty;
        var parsed = JsonSerializer.Deserialize<GeminiStructuredResponse>(rawStructuredJson, JsonOptions);

        if (parsed == null ||
            string.IsNullOrWhiteSpace(parsed.GeneralVerdict) ||
            string.IsNullOrWhiteSpace(parsed.ScalabilitySummary))
        {
            _logger.LogWarning("No se pudo parsear la respuesta JSON estructurada de Gemini ({Model}).", model);
            return null;
        }

        return new AiGameSummaryDto(
            GameId: game.Id,
            GameTitle: game.SpanishTitle,
            ScalabilitySummary: parsed.ScalabilitySummary.Trim(),
            AgeSummary: parsed.AgeSummary?.Trim() ?? $"{game.Age.CommunityAge}+ años según comunidad.",
            FootprintSummary: parsed.FootprintSummary?.Trim() ?? "Mesa de comedor estándar.",
            GeneralVerdict: parsed.GeneralVerdict.Trim(),
            Model: $"Google Gemini ({model})",
            GeneratedAt: DateTime.UtcNow
        );
    }

    private static string ExtractErrorMessage(string errorBody, System.Net.HttpStatusCode statusCode, string model)
    {
        try
        {
            using var doc = JsonDocument.Parse(errorBody);
            if (doc.RootElement.TryGetProperty("error", out var errorEl) &&
                errorEl.TryGetProperty("message", out var msgEl))
            {
                var msg = msgEl.GetString();
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    return $"Google Gemini ({model}) HTTP {(int)statusCode}: {msg.Trim()}";
                }
            }
        }
        catch
        {
            // Ignorar y caer al formato por defecto
        }

        string snippet = errorBody.Length > 200 ? errorBody[..200] + "..." : errorBody;
        return $"Google Gemini ({model}) HTTP {(int)statusCode}: {snippet}";
    }

    private static string BuildPrompt(Game game)
    {
        return $"""
            Eres un crítico y analista experto de juegos de mesa para Ludeka («Juegos, sorteos, eventos y opiniones de verdad. Bienvenido a tu mesa»).
            Genera un resumen editorial objetivo, conciso y fundamentado en español neutro para la ficha del juego:

            DATOS TÉCNICOS:
            - Título en español: {game.SpanishTitle}
            - Título original: {game.OriginalTitle}
            - Autor: {(string.IsNullOrWhiteSpace(game.Designer) ? "Desconocido" : game.Designer)}
            - Editorial: {(string.IsNullOrWhiteSpace(game.Publisher) ? "Desconocida" : game.Publisher)}
            - Año: {game.YearPublished}
            - Estilo: {game.Style}
            - Confrontación: {game.Confrontation}
            - Escalabilidad ideal según consenso: {game.CalculateIdealPlayerCountText()}
            - Edades: Caja oficial {game.Age.BoxAge}+ | Recomendada por comunidad {game.Age.CommunityAge}+
            - Despliegue en mesa: {game.Footprint}
            - Duración: {game.Duration.MinMinutes}-{game.Duration.MaxMinutes} minutos (~{game.Duration.EstimatedPerPlayerMinutes} min por jugador)
            - Puntuación BGG: {game.BggRating:0.0}/10
            - Sinopsis original: {(string.IsNullOrWhiteSpace(game.Description) ? "Sin sinopsis" : game.Description)}

            INSTRUCCIONES DE FORMATO:
            Devuelve estrictamente un JSON válido con estas 4 propiedades textuales exactas en español:
            1. "generalVerdict": Reseña objetiva (2-3 oraciones). Tono sobrio y profesional. Sin frases publicitarias vacías.
            2. "scalabilitySummary": A qué número de jugadores brilla según el consenso, fluidez y entreturno.
            3. "ageSummary": Comparativa de edad de caja vs real y accesibilidad familiar con niños.
            4. "footprintSummary": Huella física en mesa (comedor, cafetería, monstruo) y ritmo de partida.
            """;
    }

    private class GeminiStructuredResponse
    {
        [JsonPropertyName("generalVerdict")]
        public string? GeneralVerdict { get; set; }

        [JsonPropertyName("scalabilitySummary")]
        public string? ScalabilitySummary { get; set; }

        [JsonPropertyName("ageSummary")]
        public string? AgeSummary { get; set; }

        [JsonPropertyName("footprintSummary")]
        public string? FootprintSummary { get; set; }
    }

    private async Task<AiBatchResultDto> CallGeminiBatchApiAsync(IReadOnlyList<AiGameBatchInputDto> games, CancellationToken ct)
    {
        string prompt = BuildBatchPrompt(games);
        string effectiveModel = _options.GetEffectiveModel();

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.2
            }
        };

        string jsonPayload = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        string requestUri = $"{_options.BaseUrl.TrimEnd('/')}/models/{effectiveModel}:generateContent?key={_options.ApiKey}";

        using var response = await _httpClient.PostAsync(requestUri, content, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            string errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Google Gemini API ({Model}) respondió con código HTTP 429 Too Many Requests: {ErrorBody}", effectiveModel, errorBody);
            return new AiBatchResultDto(Success: false, QuotaExhausted: true, Summaries: new Dictionary<int, AiGameSummaryDto>(), ErrorMessage: $"HTTP 429 Too Many Requests: {errorBody}");
        }

        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Google Gemini API ({Model}) respondió con código {StatusCode}: {ErrorBody}", effectiveModel, response.StatusCode, errorBody);
            if (errorBody.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase))
            {
                return new AiBatchResultDto(Success: false, QuotaExhausted: true, Summaries: new Dictionary<int, AiGameSummaryDto>(), ErrorMessage: $"RESOURCE_EXHAUSTED: {errorBody}");
            }

            return new AiBatchResultDto(Success: false, QuotaExhausted: false, Summaries: new Dictionary<int, AiGameSummaryDto>(), ErrorMessage: $"HTTP {response.StatusCode}: {errorBody}");
        }

        string responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);

        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            return new AiBatchResultDto(Success: false, QuotaExhausted: false, Summaries: new Dictionary<int, AiGameSummaryDto>(), ErrorMessage: "Gemini devolvió respuesta sin candidatos.");
        }

        var candidate = candidates[0];
        if (!candidate.TryGetProperty("content", out var contentProp) ||
            !contentProp.TryGetProperty("parts", out var parts) ||
            parts.GetArrayLength() == 0)
        {
            return new AiBatchResultDto(Success: false, QuotaExhausted: false, Summaries: new Dictionary<int, AiGameSummaryDto>(), ErrorMessage: "Gemini devolvió respuesta sin partes de contenido.");
        }

        string rawStructuredJson = parts[0].GetProperty("text").GetString() ?? string.Empty;
        var parsedList = JsonSerializer.Deserialize<List<GeminiStructuredBatchItemResponse>>(rawStructuredJson, JsonOptions);

        if (parsedList == null || parsedList.Count == 0)
        {
            return new AiBatchResultDto(Success: false, QuotaExhausted: false, Summaries: new Dictionary<int, AiGameSummaryDto>(), ErrorMessage: "No se pudo parsear el array JSON estructurado del lote.");
        }

        var summaries = new Dictionary<int, AiGameSummaryDto>();
        var gamesDict = games.ToDictionary(g => g.BggId);

        foreach (var item in parsedList)
        {
            if (gamesDict.TryGetValue(item.BggId, out var inputGame))
            {
                summaries[item.BggId] = new AiGameSummaryDto(
                    GameId: Guid.Empty,
                    GameTitle: inputGame.SpanishTitle,
                    ScalabilitySummary: item.ScalabilitySummary?.Trim() ?? "Escalabilidad según consenso.",
                    AgeSummary: item.AgeSummary?.Trim() ?? $"{inputGame.MinAge}+ años.",
                    FootprintSummary: item.FootprintSummary?.Trim() ?? "Mesa de comedor estándar.",
                    GeneralVerdict: item.GeneralVerdict?.Trim() ?? "Síntesis editorial.",
                    Model: $"Google Gemini ({effectiveModel}) [Batch]",
                    GeneratedAt: DateTime.UtcNow
                );
            }
        }

        // Fallback heurístico transparente para cualquier juego del lote que Gemini haya omitido
        foreach (var game in games)
        {
            if (!summaries.ContainsKey(game.BggId))
            {
                summaries[game.BggId] = HeuristicGameSummaryGenerator.Generate(game, "Heurística Editorial (Fallback Batch)");
            }
        }

        return new AiBatchResultDto(Success: true, QuotaExhausted: false, Summaries: summaries, ErrorMessage: null);
    }

    private static string BuildBatchPrompt(IReadOnlyList<AiGameBatchInputDto> games)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Eres un crítico y analista experto de juegos de mesa para Ludeka («Juegos, sorteos, eventos y opiniones de verdad. Bienvenido a tu mesa»).");
        sb.AppendLine("Genera un resumen editorial objetivo, conciso y fundamentado en español neutro para cada uno de los siguientes juegos:");
        sb.AppendLine();

        foreach (var g in games)
        {
            sb.AppendLine($"[JUEGO BGG #{g.BggId}]");
            sb.AppendLine($"- Título: {g.SpanishTitle} (Original: {g.OriginalTitle})");
            sb.AppendLine($"- Autor: {(string.IsNullOrWhiteSpace(g.Designer) ? "Desconocido" : g.Designer)} | Editorial: {(string.IsNullOrWhiteSpace(g.Publisher) ? "Desconocida" : g.Publisher)} | Año: {g.YearPublished}");
            sb.AppendLine($"- Jugadores: {g.MinPlayers}-{g.MaxPlayers} | Edad: {g.MinAge}+ | Valoración BGG: {g.Rating:0.0}/10");
            string desc = string.IsNullOrWhiteSpace(g.Description) ? "Sin descripción" : (g.Description.Length > 300 ? g.Description[..300] + "..." : g.Description);
            sb.AppendLine($"- Sinopsis: {desc}");
            sb.AppendLine();
        }

        sb.AppendLine("INSTRUCCIONES DE FORMATO:");
        sb.AppendLine("Devuelve estrictamente un JSON válido con un array donde cada elemento tenga exactamente estas 5 propiedades:");
        sb.AppendLine("1. \"bggId\": Número entero con el identificador BGG del juego correspondiente.");
        sb.AppendLine("2. \"generalVerdict\": Reseña objetiva (2-3 oraciones). Tono sobrio y profesional. Sin frases publicitarias.");
        sb.AppendLine("3. \"scalabilitySummary\": A qué número de jugadores brilla, fluidez y entreturno.");
        sb.AppendLine("4. \"ageSummary\": Accesibilidad de edad real y experiencia familiar.");
        sb.AppendLine("5. \"footprintSummary\": Huella física en mesa (comedor, cafetería, monstruo) y ritmo.");

        return sb.ToString();
    }

    private class GeminiStructuredBatchItemResponse
    {
        [JsonPropertyName("bggId")]
        public int BggId { get; set; }

        [JsonPropertyName("generalVerdict")]
        public string? GeneralVerdict { get; set; }

        [JsonPropertyName("scalabilitySummary")]
        public string? ScalabilitySummary { get; set; }

        [JsonPropertyName("ageSummary")]
        public string? AgeSummary { get; set; }

        [JsonPropertyName("footprintSummary")]
        public string? FootprintSummary { get; set; }
    }
}
