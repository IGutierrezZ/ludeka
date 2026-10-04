using System;

namespace Ludeka.Application.DTOs;

/// <summary>
/// Información de la edición en español extraída analíticamente desde las versiones de BGG XMLAPI2.
/// </summary>
public record BggSpanishVersionInfoDto(
    string Title,
    string? Publisher,
    int? YearPublished,
    string? Ean,
    string? ProductCode
);

/// <summary>
/// Resultado del barrido de catálogo para actualizar títulos en español y EAN desde versiones locales.
/// </summary>
public record BggVersionCatalogSweepResultDto(
    int EvaluatedCount,
    int UpdatedTitlesCount,
    int UpdatedEansCount,
    int SkippedCount,
    int FailedCount,
    int LastBggIdProcessed,
    bool HasMore,
    string Message
);
