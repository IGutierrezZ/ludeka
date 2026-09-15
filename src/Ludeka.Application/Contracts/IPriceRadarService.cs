using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Orquestador del radar de precios, detección de ofertas destacadas y alertas para títulos seguidos.
/// </summary>
public interface IPriceRadarService
{
    /// <summary>
    /// Obtiene las mejores bajadas de precio y chollos activos en el catálogo, opcionalmente filtrados por país con envíos disponibles.
    /// </summary>
    Task<IReadOnlyList<PriceDropAlertDto>> GetTopDiscountsAsync(int limit = 20, string? country = null, CancellationToken ct = default);

    /// <summary>
    /// Obtiene las alertas de ofertas y mínimos históricos para los juegos en la lista 'Quiero comprar' del usuario.
    /// </summary>
    Task<IReadOnlyList<PriceDropAlertDto>> GetUserWantToBuyAlertsAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene las métricas consolidadas de precio (mínimo histórico, oferta actual, descuento) para un juego.
    /// </summary>
    Task<GamePriceMetrics> GetGamePriceMetricsAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene el histórico cronológico de precios registrados para un juego.
    /// </summary>
    Task<IReadOnlyList<PriceHistoryEntryDto>> GetGamePriceHistoryAsync(Guid gameId, int limit = 30, CancellationToken ct = default);

    /// <summary>
    /// Registra una nueva lectura de precio individual para un juego y tienda.
    /// </summary>
    Task RecordPriceObservationAsync(Guid gameId, string storeName, string affiliateUrl, decimal price, bool inStock, string currency = "€", CancellationToken ct = default);

    /// <summary>
    /// Realiza un barrido desatendido o bajo demanda consultando el stock/precio en vivo de los títulos más seguidos en listas 'Quiero comprar'.
    /// </summary>
    Task<int> ScanWantToBuyPricesAsync(int maxGames = 20, CancellationToken ct = default);
}
