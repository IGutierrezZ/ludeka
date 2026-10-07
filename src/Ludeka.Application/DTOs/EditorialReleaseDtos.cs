using System;
using System.Collections.Generic;

namespace Ludeka.Application.DTOs;

/// <summary>
/// Representa un lanzamiento editorial extraído de fuentes oficiales (Devir, Maldito Games, etc.).
/// </summary>
public record EditorialReleaseItem(
    string Title,
    string Publisher,
    DateOnly? ReleaseDate = null,
    string? TargetDateText = null,
    decimal? EstimatedPvp = null,
    string? Ean = null,
    string? CoverImageUrl = null,
    string? Notes = null,
    string? SourceUrl = null,
    bool IsReprint = false);

/// <summary>
/// Resultado del proceso de sincronización para una editorial concreta.
/// </summary>
public record EditorialSyncResultDto(
    string Publisher,
    bool Success,
    int ItemsFound,
    int ReleasesCreated,
    int ReleasesUpdated,
    int GamesLinked,
    int GamesImported,
    string? ErrorMessage = null);

/// <summary>
/// Resumen global de la sincronización de todas las editoriales configuradas.
/// </summary>
public record EditorialSyncSummaryDto(
    int TotalFound,
    int CreatedCount,
    int UpdatedCount,
    int GamesLinkedCount,
    int GamesImportedFromBggCount,
    IReadOnlyList<EditorialSyncResultDto> PublisherResults,
    IReadOnlyList<string> Errors);
