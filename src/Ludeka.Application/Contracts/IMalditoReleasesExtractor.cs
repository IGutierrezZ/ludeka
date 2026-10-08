using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Extractor especializado para los próximos lanzamientos y preventas de Maldito Games.
/// </summary>
public interface IMalditoReleasesExtractor
{
    /// <summary>
    /// Descarga y parsea la información de futuros lanzamientos de Maldito Games.
    /// </summary>
    Task<IReadOnlyList<EditorialReleaseItem>> ExtractReleasesAsync(CancellationToken ct = default);

    /// <summary>
    /// Parsea el HTML de Maldito Games (portada y/o catálogo).
    /// </summary>
    IReadOnlyList<EditorialReleaseItem> ParseHtml(string homeHtml, string? catalogHtml = null);

    /// <summary>
    /// Extrae la galería y datos detallados de una ficha de producto de Maldito Games.
    /// </summary>
    Task<MalditoProductGalleryDto?> ExtractProductGalleryAsync(string productUrl, CancellationToken ct = default);

    /// <summary>
    /// Extrae una página de productos del catálogo general de Maldito Games.
    /// </summary>
    Task<MalditoCatalogPageResultDto> ExtractCatalogPageAsync(int page = 1, CancellationToken ct = default);
}

