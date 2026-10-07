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
    /// Parsea el HTML de la página de próximos lanzamientos de Devir.
    /// </summary>
    IReadOnlyList<EditorialReleaseItem> ParseHtml(string html);
}
