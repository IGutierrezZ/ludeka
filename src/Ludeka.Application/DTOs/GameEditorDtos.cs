using System;
using System.Collections.Generic;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Application.DTOs;

/// <summary>
/// Comando para actualizar los metadatos y parámetros de catálogo de un juego existente.
/// </summary>
public record UpdateGameDetailsCommand(
    Guid GameId,
    string SpanishTitle,
    string OriginalTitle,
    string Designer,
    string Publisher,
    int YearPublished,
    string? Description,
    int MinPlayers,
    int MaxPlayers,
    int MinDurationMinutes,
    int MaxDurationMinutes,
    int EstimatedPerPlayerMinutes,
    int BoxAge,
    int CommunityAge,
    ConfrontationType Confrontation,
    GameStyle Style,
    bool IsOfficialSolo,
    LanguageDependence Language,
    TableFootprint Footprint,
    string? CoverImageUrl,
    string? BackCoverImageUrl = null,
    string? TableImageUrl = null,
    Guid? AssociatedReportId = null,
    string? ResolutionNotes = null,
    IReadOnlyList<SleeveItem>? Sleeves = null,
    string? Ean = null
);

/// <summary>
/// Resultado del procesamiento y almacenamiento de una imagen de portada de juego.
/// </summary>
public record GameImageUploadResult(
    bool Success,
    string? RelativePath,
    string? ErrorMessage
);

/// <summary>
/// DTO de auditoría editorial para reflejar el historial de cambios de una ficha.
/// </summary>
public record GameEditLogDto(
    Guid Id,
    Guid GameId,
    string EditorUserId,
    string EditorName,
    string SummaryOfChanges,
    Guid? AssociatedReportId,
    DateTimeOffset EditedAt
);
