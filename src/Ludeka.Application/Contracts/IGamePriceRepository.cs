using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Contrato de persistencia para el histórico de precios e instantáneas de tiendas.
/// </summary>
public interface IGamePriceRepository
{
    /// <summary>
    /// Registra una lectura de precio individual para un juego y tienda, omitiendo duplicados idénticos en la ventana anti-saturación.
    /// </summary>
    Task RecordSnapshotAsync(GamePriceSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra un lote de lecturas de precios concurrentemente.
    /// </summary>
    Task RecordSnapshotsBatchAsync(IEnumerable<GamePriceSnapshot> snapshots, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el histórico cronológico de precios para un juego determinado.
    /// </summary>
    Task<IReadOnlyList<GamePriceSnapshot>> GetHistoryAsync(Guid gameId, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calcula y consolida las métricas de precio (mínimo histórico, media, oferta actual) para un juego.
    /// </summary>
    Task<GamePriceMetrics> GetMetricsAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calcula en lote las métricas de precio para múltiples juegos.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, GamePriceMetrics>> GetMetricsBatchAsync(IEnumerable<Guid> gameIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene las lecturas de precios más recientes en todo el catálogo.
    /// </summary>
    Task<IReadOnlyList<GamePriceSnapshot>> GetRecentSnapshotsAsync(int limit = 100, CancellationToken cancellationToken = default);
}
