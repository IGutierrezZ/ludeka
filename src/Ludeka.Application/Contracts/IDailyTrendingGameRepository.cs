using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Contrato de persistencia para instantáneas diarias de juegos en tendencia (BGG Hotness).
/// </summary>
public interface IDailyTrendingGameRepository
{
    /// <summary>
    /// Obtiene los juegos en tendencia para una fecha UTC concreta ordenados ascendentemente por su puesto (Rank 1..50).
    /// </summary>
    Task<IReadOnlyList<DailyTrendingGame>> GetTrendingByDateAsync(DateOnly dateUtc, CancellationToken ct = default);

    /// <summary>
    /// Obtiene las tendencias de la última fecha disponible en el sistema hasta el límite indicado.
    /// </summary>
    Task<IReadOnlyList<DailyTrendingGame>> GetLatestTrendingAsync(int limit = 50, CancellationToken ct = default);

    /// <summary>
    /// Inserta o actualiza un lote de puestos en tendencia para una fecha.
    /// Si ya existen registros para la misma fecha y puesto o BggId, actualiza sus datos o vinculación.
    /// </summary>
    Task UpsertDailyTrendingBatchAsync(IEnumerable<DailyTrendingGame> items, CancellationToken ct = default);

    /// <summary>
    /// Vincula un juego de catálogo a los registros de tendencias existentes con dicho BGG ID.
    /// </summary>
    Task LinkGameAsync(int bggId, Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene la fecha más reciente de tendencias registrada en la base de datos.
    /// </summary>
    Task<DateOnly?> GetLatestDateAsync(CancellationToken ct = default);
}
