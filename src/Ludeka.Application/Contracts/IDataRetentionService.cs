using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Servicio de mantenimiento para la retención y purga de entidades caducadas (sorteos, eventos, novedades) y liberación de imágenes asociadas.
/// </summary>
public interface IDataRetentionService
{
    /// <summary>
    /// Ejecuta el ciclo de purga de datos caducados según las políticas de retención configuradas.
    /// </summary>
    Task<DataRetentionResult> PurgeExpiredDataAsync(CancellationToken ct = default);
}
