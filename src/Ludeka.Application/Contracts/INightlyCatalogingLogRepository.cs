using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Contrato para el repositorio de persistencia de bitácora de ejecuciones nocturnas.
/// </summary>
public interface INightlyCatalogingLogRepository
{
    Task AddAsync(NightlyCatalogingExecutionLog log, CancellationToken ct = default);
    Task UpdateAsync(NightlyCatalogingExecutionLog log, CancellationToken ct = default);
    Task<IReadOnlyList<NightlyCatalogingExecutionLog>> GetRecentLogsAsync(int limit = 20, CancellationToken ct = default);
    Task<NightlyCatalogingExecutionLog?> GetLatestLogAsync(CancellationToken ct = default);

    /// <summary>
    /// Marca como fallidos los registros de ejecución que quedaron en estado 'Running'
    /// iniciados antes o en el instante especificado (por caídas, reinicios o cortes abruptos).
    /// </summary>
    Task<int> FailStaleRunningLogsAsync(DateTimeOffset startedBefore, CancellationToken ct = default);
}
