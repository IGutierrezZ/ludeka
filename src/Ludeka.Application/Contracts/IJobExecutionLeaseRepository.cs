using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Adquisición y transición de <see cref="Ludeka.Core.Entities.JobExecutionLease"/> (INC-47, R5,
/// diseño §7.3/§7.4, decisión D4). La clasificación de violación de unicidad es específica del
/// proveedor (<c>PostgresException.SqlState == "23505"</c> / <c>SqliteException.SqliteErrorCode
/// == 19</c>) y vive en la implementación de <c>Ludeka.Infrastructure</c>:
/// <see cref="TryAcquireAsync"/> nunca propaga una excepción de proveedor hacia
/// <c>Ludeka.Application</c>, que se mantiene agnóstica.
/// </summary>
public interface IJobExecutionLeaseRepository
{
    /// <summary>Intenta reservar la ventana <paramref name="windowKey"/> de
    /// <paramref name="jobName"/> mediante <c>INSERT</c> primero (diseño §7.3). Si la fila ya
    /// existe, decide entre devolver el desenlace ya conocido (completada, o viva en manos de
    /// otra ejecución) o tomar el control por intercambio condicional de una concesión huérfana
    /// (latido caducado) o retomable (<c>Failed</c>).</summary>
    Task<LeaseAcquisition> TryAcquireAsync(
        string jobName, string windowKey, string? hostIdentifier, CancellationToken ct = default);

    /// <summary>Actualiza el latido de una concesión en curso.</summary>
    Task TouchAsync(Guid leaseId, CancellationToken ct = default);

    /// <summary>Marca la concesión como completada con éxito (código de salida 0, aun con
    /// fallos parciales dentro del lote).</summary>
    Task MarkCompletedAsync(
        Guid leaseId, int processedCount, int failedCount, long durationMs, CancellationToken ct = default);

    /// <summary>Marca la concesión como fallida (código de salida distinto de cero): retomable
    /// por una ejecución posterior mediante el mismo intercambio condicional de
    /// <see cref="TryAcquireAsync"/>.</summary>
    Task MarkFailedAsync(
        Guid leaseId, string? errorMessage, int processedCount, int failedCount, long durationMs,
        CancellationToken ct = default);
}
