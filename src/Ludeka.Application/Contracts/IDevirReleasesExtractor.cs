using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Extractor especializado para los próximos lanzamientos de Devir Iberia.
/// </summary>
public interface IDevirReleasesExtractor
{
    /// <summary>
    /// Descarga y parsea la página de próximos lanzamientos de Devir Iberia.
    /// </summary>
    Task<IReadOnlyList<EditorialReleaseItem>> ExtractReleasesAsync(CancellationToken ct = default);

    /// <summary>
    /// Parsea el HTML de la página de próximos lanzamientos de Devir, descartando meses anteriores al mes de referencia (por defecto el mes actual en UTC).
    /// </summary>
    IReadOnlyList<EditorialReleaseItem> ParseHtml(string html, DateOnly? referenceDate = null);

    /// <summary>
    /// Descarga y parsea la galería y metadatos de producto de una ficha oficial de Devir Iberia.
    /// </summary>
    Task<DevirProductGalleryDto?> ExtractProductGalleryAsync(string productUrl, CancellationToken ct = default);

    /// <summary>
    /// Parsea el HTML de una ficha de producto de Devir Iberia extrayendo la galería oficial (face3d, components1, backflat, frontflat) y metadatos.
    /// </summary>
    DevirProductGalleryDto? ParseProductGalleryHtml(string html);

    /// <summary>
    /// Descarga y parsea una página del catálogo general de juegos de mesa de Devir Iberia.
    /// </summary>
    Task<DevirCatalogPageResultDto> ExtractCatalogPageAsync(int page = 1, CancellationToken ct = default);

    /// <summary>
    /// Parsea el HTML de una página del catálogo de Devir Iberia extrayendo los productos listados y si hay página siguiente.
    /// </summary>
    DevirCatalogPageResultDto ParseCatalogPageHtml(string html);
}
