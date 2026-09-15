using System;
using System.Collections.Generic;
using System.Linq;
using Ludeka.Core.Entities;

namespace Ludeka.Core.ValueObjects;

/// <summary>
/// Encapsula métricas consolidadas sobre el comportamiento y evolución del precio de un juego.
/// </summary>
public record GamePriceMetrics
{
    public Guid GameId { get; init; }
    public decimal? CurrentLowestPrice { get; init; }
    public string? CurrentLowestStore { get; init; }
    public decimal? AllTimeLowPrice { get; init; }
    public string? AllTimeLowStore { get; init; }
    public DateTimeOffset? AllTimeLowDateUtc { get; init; }
    public decimal? AveragePrice { get; init; }
    public double? PriceDropPercentage { get; init; }
    public bool IsAllTimeLow { get; init; }
    public int TotalObservations { get; init; }

    public static GamePriceMetrics Empty(Guid gameId) => new() { GameId = gameId };

    /// <summary>
    /// Calcula las métricas a partir del histórico de instantáneas y/o los enlaces de compra actuales.
    /// </summary>
    public static GamePriceMetrics Calculate(
        Guid gameId,
        IEnumerable<GamePriceSnapshot>? snapshots,
        IEnumerable<GamePurchaseLink>? currentOffers = null,
        decimal? referenceBasePrice = null)
    {
        var snapshotList = snapshots?.Where(s => s.Price > 0).ToList() ?? [];
        var offerList = currentOffers?.Where(o => o.Price.HasValue && o.Price.Value > 0).ToList() ?? [];

        if (snapshotList.Count == 0 && offerList.Count == 0)
        {
            return Empty(gameId);
        }

        // 1. Mínimo histórico absoluto
        decimal? allTimeLowPrice = null;
        string? allTimeLowStore = null;
        DateTimeOffset? allTimeLowDate = null;

        if (snapshotList.Count > 0)
        {
            var lowestSnapshot = snapshotList
                .OrderBy(s => s.Price)
                .ThenBy(s => s.RecordedAtUtc)
                .First();

            allTimeLowPrice = lowestSnapshot.Price;
            allTimeLowStore = lowestSnapshot.StoreName;
            allTimeLowDate = lowestSnapshot.RecordedAtUtc;
        }

        // 2. Oferta actual más baja en stock
        decimal? currentLowestPrice = null;
        string? currentLowestStore = null;

        var availableOffers = offerList.Where(o => o.InStock).ToList();
        if (availableOffers.Count > 0)
        {
            var bestOffer = availableOffers.OrderBy(o => o.Price!.Value).First();
            currentLowestPrice = bestOffer.Price;
            currentLowestStore = bestOffer.StoreName;
        }
        else if (offerList.Count > 0)
        {
            var bestOffer = offerList.OrderBy(o => o.Price!.Value).First();
            currentLowestPrice = bestOffer.Price;
            currentLowestStore = bestOffer.StoreName;
        }
        else if (snapshotList.Count > 0)
        {
            // Tomar la lectura más reciente en stock si no hay lista de ofertas explícita
            var latestInStock = snapshotList
                .Where(s => s.InStock)
                .OrderByDescending(s => s.RecordedAtUtc)
                .FirstOrDefault();

            if (latestInStock != null)
            {
                currentLowestPrice = latestInStock.Price;
                currentLowestStore = latestInStock.StoreName;
            }
        }

        // Si la oferta actual es menor que el histórico anterior, actualiza el récord
        if (currentLowestPrice.HasValue && (!allTimeLowPrice.HasValue || currentLowestPrice.Value < allTimeLowPrice.Value))
        {
            allTimeLowPrice = currentLowestPrice.Value;
            allTimeLowStore = currentLowestStore;
            allTimeLowDate = DateTimeOffset.UtcNow;
        }
        else if (!allTimeLowPrice.HasValue && currentLowestPrice.HasValue)
        {
            allTimeLowPrice = currentLowestPrice;
            allTimeLowStore = currentLowestStore;
            allTimeLowDate = DateTimeOffset.UtcNow;
        }

        // 3. Precio medio histórico
        decimal? averagePrice = null;
        if (snapshotList.Count > 0)
        {
            averagePrice = Math.Round(snapshotList.Average(s => s.Price), 2);
        }
        else if (offerList.Count > 0)
        {
            averagePrice = Math.Round(offerList.Average(o => o.Price!.Value), 2);
        }

        // 4. Bandera de Mínimo Histórico
        bool isAllTimeLow = currentLowestPrice.HasValue &&
                            allTimeLowPrice.HasValue &&
                            currentLowestPrice.Value <= allTimeLowPrice.Value;

        // 5. Cálculo del porcentaje de descuento
        // Se contrasta contra el precio de referencia explícito (ej. PVP orientativo), o contra el precio medio
        decimal? baselinePrice = referenceBasePrice ?? averagePrice;
        double? priceDropPercentage = null;

        if (currentLowestPrice.HasValue && baselinePrice.HasValue && baselinePrice.Value > 0)
        {
            if (currentLowestPrice.Value < baselinePrice.Value)
            {
                var drop = (1.0 - (double)(currentLowestPrice.Value / baselinePrice.Value)) * 100.0;
                priceDropPercentage = Math.Round(Math.Max(0.0, drop), 1);
            }
            else
            {
                priceDropPercentage = 0.0;
            }
        }

        return new GamePriceMetrics
        {
            GameId = gameId,
            CurrentLowestPrice = currentLowestPrice,
            CurrentLowestStore = currentLowestStore,
            AllTimeLowPrice = allTimeLowPrice,
            AllTimeLowStore = allTimeLowStore,
            AllTimeLowDateUtc = allTimeLowDate,
            AveragePrice = averagePrice,
            PriceDropPercentage = priceDropPercentage,
            IsAllTimeLow = isAllTimeLow,
            TotalObservations = snapshotList.Count
        };
    }
}
