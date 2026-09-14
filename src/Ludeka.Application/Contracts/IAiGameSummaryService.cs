using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Contrato para el servicio de generación y persistencia de síntesis inteligentes de juegos con IA o motor de respaldo.
/// </summary>
public interface IAiGameSummaryService
{
    /// <summary>
    /// Genera la síntesis estructurada para la entidad de juego proporcionada.
    /// </summary>
    Task<AiGameSummaryDto> GenerateSummaryAsync(Game game, CancellationToken ct = default);

    /// <summary>
    /// Asegura que el juego con el identificador dado posea un resumen de IA generado y persistido en el catálogo.
    /// </summary>
    Task<AiGameSummaryDto> EnsureSummaryForGameAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Procesa por lotes (carga nocturna o bajo demanda) los juegos del catálogo que aún no dispongan de síntesis de IA.
    /// </summary>
    Task<AiBatchProcessingResultDto> ProcessPendingSummariesBatchAsync(int batchSize = 20, CancellationToken ct = default);

    /// <summary>
    /// Genera la síntesis estructurada para una lista de juegos agrupados en una única petición a Gemini Flash (Batching).
    /// </summary>
    Task<AiBatchResultDto> GenerateBatchSummariesAsync(IReadOnlyList<AiGameBatchInputDto> games, CancellationToken ct = default);
}

