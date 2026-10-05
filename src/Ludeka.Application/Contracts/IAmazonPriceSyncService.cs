using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Orquesta la consulta, caché (límite de 24 horas) y sincronización de precios de Amazon para un juego.
/// </summary>
public interface IAmazonPriceSyncService
{
    /// <summary>
    /// Sincroniza el precio y disponibilidad de Amazon para el juego especificado, respetando el TTL de 24h.
    /// </summary>
    Task<GamePriceSnapshot?> SyncGamePriceAsync(Guid gameId, CancellationToken ct = default);
}
