using System;
using System.Collections.Generic;
using Ludeka.Core.Entities;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class GamePriceMetricsTests
{
    [Fact]
    public void Calculate_WithEmptyData_ReturnsEmptyMetrics()
    {
        var gameId = Guid.NewGuid();
        var metrics = GamePriceMetrics.Calculate(gameId, null, null);

        Assert.Equal(gameId, metrics.GameId);
        Assert.Null(metrics.CurrentLowestPrice);
        Assert.Null(metrics.AllTimeLowPrice);
        Assert.False(metrics.IsAllTimeLow);
        Assert.Equal(0, metrics.TotalObservations);
    }

    [Fact]
    public void Constructor_GamePriceSnapshot_ValidatesInvariants()
    {
        var gameId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => new GamePriceSnapshot(Guid.Empty, "Tienda", "https://tienda.es", 10m));
        Assert.Throws<ArgumentException>(() => new GamePriceSnapshot(gameId, "  ", "https://tienda.es", 10m));
        Assert.Throws<ArgumentException>(() => new GamePriceSnapshot(gameId, "Tienda", "  ", 10m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GamePriceSnapshot(gameId, "Tienda", "https://tienda.es", -5m));

        var snapshot = new GamePriceSnapshot(gameId, "  Zacatrus  ", " https://zacatrus.es/juego ", 39.95m);
        Assert.Equal("Zacatrus", snapshot.StoreName);
        Assert.Equal("https://zacatrus.es/juego", snapshot.AffiliateUrl);
        Assert.Equal(39.95m, snapshot.Price);
        Assert.True(snapshot.InStock);
        Assert.Equal("€", snapshot.Currency);
    }

    [Fact]
    public void Calculate_WithHistoricalSnapshots_DeterminesCorrectAllTimeLow()
    {
        var gameId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var snapshots = new List<GamePriceSnapshot>
        {
            new(gameId, "Tienda A", "https://a.com", 45.00m, inStock: true, recordedAtUtc: now.AddDays(-10)),
            new(gameId, "Tienda B", "https://b.com", 38.50m, inStock: true, recordedAtUtc: now.AddDays(-5)),
            new(gameId, "Tienda C", "https://c.com", 42.00m, inStock: true, recordedAtUtc: now.AddDays(-1))
        };

        var offers = new List<GamePurchaseLink>
        {
            new("Tienda A", "https://a.com", 44.00m, inStock: true),
            new("Tienda C", "https://c.com", 41.00m, inStock: true)
        };

        var metrics = GamePriceMetrics.Calculate(gameId, snapshots, offers);

        Assert.Equal(38.50m, metrics.AllTimeLowPrice);
        Assert.Equal("Tienda B", metrics.AllTimeLowStore);
        Assert.Equal(41.00m, metrics.CurrentLowestPrice);
        Assert.Equal("Tienda C", metrics.CurrentLowestStore);
        Assert.False(metrics.IsAllTimeLow); // 41.00 > 38.50
        Assert.Equal(3, metrics.TotalObservations);
    }

    [Fact]
    public void Calculate_WhenCurrentOfferBeatsHistoricalLow_FlagsIsAllTimeLow()
    {
        var gameId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var snapshots = new List<GamePriceSnapshot>
        {
            new(gameId, "Tienda A", "https://a.com", 45.00m, recordedAtUtc: now.AddDays(-10)),
            new(gameId, "Tienda B", "https://b.com", 42.00m, recordedAtUtc: now.AddDays(-5))
        };

        var offers = new List<GamePurchaseLink>
        {
            new("Tienda C", "https://c.com", 34.95m, inStock: true) // Récord absoluto
        };

        var metrics = GamePriceMetrics.Calculate(gameId, snapshots, offers);

        Assert.Equal(34.95m, metrics.CurrentLowestPrice);
        Assert.Equal("Tienda C", metrics.CurrentLowestStore);
        Assert.Equal(34.95m, metrics.AllTimeLowPrice);
        Assert.Equal("Tienda C", metrics.AllTimeLowStore);
        Assert.True(metrics.IsAllTimeLow);
    }

    [Fact]
    public void Calculate_CalculatesExactDiscountPercentage()
    {
        var gameId = Guid.NewGuid();
        var offers = new List<GamePurchaseLink>
        {
            new("Tienda Oferta", "https://oferta.es", 30.00m, inStock: true)
        };

        // PVP base orientativo de 50.00 € -> 30 € representa un 40% de descuento
        var metrics = GamePriceMetrics.Calculate(gameId, null, offers, referenceBasePrice: 50.00m);

        Assert.NotNull(metrics.PriceDropPercentage);
        Assert.Equal(40.0, metrics.PriceDropPercentage.Value);
    }

    [Fact]
    public void Calculate_WithOutOfStockOffers_PrefersInStockOfferForCurrentLowest()
    {
        var gameId = Guid.NewGuid();
        var offers = new List<GamePurchaseLink>
        {
            new("Tienda Barata Agotada", "https://agotada.com", 25.00m, inStock: false),
            new("Tienda Disponible", "https://disponible.com", 35.00m, inStock: true)
        };

        var metrics = GamePriceMetrics.Calculate(gameId, null, offers);

        Assert.Equal(35.00m, metrics.CurrentLowestPrice);
        Assert.Equal("Tienda Disponible", metrics.CurrentLowestStore);
    }
}
