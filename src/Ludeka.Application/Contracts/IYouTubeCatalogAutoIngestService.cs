using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Orquestador de auto-ingesta periódica de vídeos para juegos clave del catálogo que no disponen de contenido audiovisual.
/// </summary>
public interface IYouTubeCatalogAutoIngestService
{
    /// <summary>
    /// Ejecuta el lote desatendido de auto-ingesta por ruta de sistema.
    /// </summary>
    Task<YouTubeCatalogAutoIngestResultDto> RunScheduledAutoIngestAsync(int? customLimit = null, int? maxRank = null, CancellationToken ct = default);

    /// <summary>
    /// Ejecuta el lote desatendido con revalidación de permisos de sesión de moderación.
    /// </summary>
    Task<YouTubeCatalogAutoIngestResultDto> ExecuteAutoIngestAsync(int? customLimit = null, int? maxRank = null, CancellationToken ct = default);
}
