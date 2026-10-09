using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Extractor especializado para los próximos lanzamientos, preventas y catálogo de Arrakis Games (https://arrakisgames.com/).
/// </summary>
public interface IArrakisReleasesExtractor
{
    /// <summary>
    /// Descarga y parsea la información de futuros lanzamientos y reimpresiones de Arrakis Games.
    /// Excluye explícitamente productos marcados como ya disponibles en tienda.
    /// </summary>
    Task<IReadOnlyList<EditorialReleaseItem>> ExtractReleasesAsync(CancellationToken ct = default);

    /// <summary>
    /// Parsea el HTML de la portada y/o de la sección de noticias/próximamente de Arrakis Games.
    /// </summary>
    IReadOnlyList<EditorialReleaseItem> ParseHtml(string homeHtml, string? newsHtml = null);

    /// <summary>
    /// Extrae la galería y datos detallados de una ficha de producto de Arrakis Games (EAN, PVP, BGG, fotos).
    /// </summary>
    Task<ArrakisProductGalleryDto?> ExtractProductGalleryAsync(string productUrl, CancellationToken ct = default);

    /// <summary>
    /// Extrae las entradas de productos del catálogo general de Arrakis Games (recorriendo catálogo y sitemap).
    /// </summary>
    Task<IReadOnlyList<ArrakisCatalogItemDto>> ExtractFullCatalogAsync(CancellationToken ct = default);
}
