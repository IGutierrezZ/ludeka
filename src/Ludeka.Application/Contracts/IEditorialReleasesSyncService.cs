using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Servicio orquestador para la sincronización periódica de novedades y lanzamientos editoriales.
/// </summary>
public interface IEditorialReleasesSyncService
{
    /// <summary>
    /// Sincroniza todas las editoriales configuradas (Devir, Maldito Games, etc.).
    /// </summary>
    Task<EditorialSyncSummaryDto> SyncAllEditorialReleasesAsync(CancellationToken ct = default);

    /// <summary>
    /// Sincroniza los lanzamientos de una editorial específica.
    /// </summary>
    Task<EditorialSyncResultDto> SyncPublisherReleasesAsync(string publisher, CancellationToken ct = default);
}
