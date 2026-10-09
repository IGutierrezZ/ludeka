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
    bool IsReprint = false,
    bool IsMonthOnly = false,
    string? TableImageUrl = null,
    string? BackCoverImageUrl = null,
    int? BggId = null);

/// <summary>
/// Representa la galería y datos de producto extraídos de la ficha de un juego en Devir Iberia.
/// </summary>
public record DevirProductGalleryDto(
    string? CoverImageUrl,
    string? TableImageUrl,
    string? BackCoverImageUrl,
    string? FrontFlatImageUrl,
    string? Ean = null,
    decimal? Pvp = null);

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

/// <summary>
/// Representa una entrada de producto en el catálogo general de juegos de mesa de Devir Iberia.
/// </summary>
public record DevirCatalogItemDto(
    string ProductUrl,
    string? Title = null,
    string? Ean = null,
    string? CoverImageUrl = null);

/// <summary>
/// Resultado del raspado de una página del catálogo general de Devir Iberia.
/// </summary>
public record DevirCatalogPageResultDto(
    IReadOnlyList<DevirCatalogItemDto> Items,
    bool HasNextPage,
    bool Success = true);

/// <summary>
/// Representa la galería y datos de producto extraídos de la ficha de un juego en Maldito Games.
/// </summary>
public record MalditoProductGalleryDto(
    string? CoverImageUrl,
    string? TableImageUrl,
    string? BackCoverImageUrl,
    string? FrontFlatImageUrl,
    string? Ean = null,
    decimal? Pvp = null);

/// <summary>
/// Representa una entrada de producto en el catálogo general de Maldito Games.
/// </summary>
public record MalditoCatalogItemDto(
    string ProductUrl,
    string? Title = null,
    string? Ean = null,
    string? CoverImageUrl = null);

/// <summary>
/// Resultado del raspado de una página del catálogo general de Maldito Games.
/// </summary>
public record MalditoCatalogPageResultDto(
    IReadOnlyList<MalditoCatalogItemDto> Items,
    bool HasNextPage,
    bool Success = true);

/// <summary>
/// Representa la galería y datos de producto extraídos de la ficha de un juego en Arrakis Games.
/// </summary>
public record ArrakisProductGalleryDto(
    string? CoverImageUrl,
    string? TableImageUrl,
    string? BackCoverImageUrl,
    string? FrontFlatImageUrl,
    string? Ean = null,
    decimal? Pvp = null,
    int? BggId = null,
    string? BggUrl = null,
    string? Title = null,
    string? StatusText = null);

/// <summary>
/// Representa una entrada de producto en el catálogo general de Arrakis Games.
/// </summary>
public record ArrakisCatalogItemDto(
    string ProductUrl,
    string? Title = null,
    string? Ean = null,
    string? CoverImageUrl = null,
    int? BggId = null,
    decimal? Pvp = null);


