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
}
