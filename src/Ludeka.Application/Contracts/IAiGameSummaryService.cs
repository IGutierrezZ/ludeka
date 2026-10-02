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

    /// <summary>
    /// Genera la síntesis de aporte de una expansión respecto a su juego base (o en solitario si aún no está vinculado).
    /// </summary>
    Task<ExpansionAporteAiDto> GenerateExpansionAporteAsync(Game expansion, Game? baseGame = null, CancellationToken ct = default)
        => Task.FromResult(new ExpansionAporteAiDto(
            ExpansionId: expansion.Id,
            WhatItBringsSummary: expansion.WhatItBringsSummary ?? string.Empty,
            Necessity: expansion.ExpansionNecessity ?? Ludeka.Core.Enums.ExpansionNecessity.Situational,
            ImpactTags: (expansion.ImpactTags ?? new List<Ludeka.Core.Enums.ExpansionImpactTag>()).AsReadOnly(),
            ExtraPlayerCount: expansion.ExtraPlayerCount,
            ExtraDurationMinutes: expansion.ExtraDurationMinutes,
            Model: "DefaultFallback",
            GeneratedAt: DateTime.UtcNow));

    /// <summary>
    /// Asegura que la expansión con el identificador dado posea su aporte editorial/IA generado y persistido en el catálogo.
    /// </summary>
    Task<ExpansionAporteAiDto> EnsureExpansionAporteAsync(Guid expansionId, CancellationToken ct = default)
        => Task.FromResult(new ExpansionAporteAiDto(
            ExpansionId: expansionId,
            WhatItBringsSummary: string.Empty,
            Necessity: Ludeka.Core.Enums.ExpansionNecessity.Situational,
            ImpactTags: Array.Empty<Ludeka.Core.Enums.ExpansionImpactTag>(),
            ExtraPlayerCount: null,
            ExtraDurationMinutes: null,
            Model: "DefaultFallback",
            GeneratedAt: DateTime.UtcNow));
}

