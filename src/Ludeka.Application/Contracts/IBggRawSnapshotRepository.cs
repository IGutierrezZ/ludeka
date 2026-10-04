using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Contrato de persistencia para la tabla satélite de payloads brutos de BGG.
/// </summary>
public interface IBggRawSnapshotRepository
{
    /// <summary>
    /// Obtiene el snapshot por su identificador BGG.
    /// </summary>
    Task<BggRawSnapshot?> GetByBggIdAsync(int bggId, CancellationToken ct = default);

    /// <summary>
    /// Inserta o actualiza un snapshot de forma idempotente.
    /// </summary>
    Task UpsertAsync(BggRawSnapshot snapshot, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los identificadores de BGG de juegos del catálogo que aún carecen de snapshot satélite.
    /// </summary>
    Task<IReadOnlyList<int>> GetMissingBggIdsAsync(int limit = 50, CancellationToken ct = default);

    /// <summary>
    /// Obtiene el número total de snapshots almacenados en la base de datos.
    /// </summary>
    Task<int> GetCountAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene el total de juegos en catálogo que cuentan con identificador BGG válido.
    /// </summary>
    Task<int> GetTotalGamesWithBggIdCountAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene una lista de snapshots para análisis y descubrimiento de enlaces.
    /// </summary>
    Task<IReadOnlyList<BggRawSnapshot>> GetAllSnapshotsAsync(int limit = 500, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un bloque de snapshots con BggId estrictamente superior a lastBggId, ordenados por BggId asc.
    /// </summary>
    Task<IReadOnlyList<BggRawSnapshot>> GetSnapshotsAfterBggIdAsync(int lastBggId, int limit = 200, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los identificadores de BGG cuyos snapshots existentes carecen del nodo 'versions'.
    /// </summary>
    Task<IReadOnlyList<int>> GetBggIdsMissingVersionsAsync(int limit = 50, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<int>>([]);

    /// <summary>
    /// Cuenta cuántos snapshots almacenados contienen información de versiones.
    /// </summary>
    Task<int> GetCountWithVersionsAsync(CancellationToken ct = default)
        => Task.FromResult(0);
}

