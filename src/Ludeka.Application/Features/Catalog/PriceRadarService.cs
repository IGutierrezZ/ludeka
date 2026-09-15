using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Application.Features.Catalog;

/// <summary>
/// Orquestador del radar de precios, detección de ofertas destacadas y alertas para la lista 'Quiero comprar'.
/// </summary>
public class PriceRadarService : IPriceRadarService
{
    private readonly IGamePriceRepository _priceRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IUserCollectionRepository _collectionRepository;
    private readonly IStoreStockService _stockService;
    private readonly PriceRadarOptions _options;
    private readonly ILogger<PriceRadarService> _logger;

    public PriceRadarService(
        IGamePriceRepository priceRepository,
        IGameRepository gameRepository,
        IUserCollectionRepository collectionRepository,
        IStoreStockService stockService,
        IOptions<PriceRadarOptions> options,
        ILogger<PriceRadarService> logger)
    {
        _priceRepository = priceRepository ?? throw new ArgumentNullException(nameof(priceRepository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _collectionRepository = collectionRepository ?? throw new ArgumentNullException(nameof(collectionRepository));
        _stockService = stockService ?? throw new ArgumentNullException(nameof(stockService));
        _options = options?.Value ?? new PriceRadarOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<PriceDropAlertDto>> GetTopDiscountsAsync(int limit = 20, string? country = null, CancellationToken ct = default)
    {
        if (limit <= 0) limit = 20;

        var allGames = await _gameRepository.GetAllGamesAsync(ct);
        var gamesWithOffers = allGames
            .Where(g => g.PurchaseLinks != null && g.PurchaseLinks.Count > 0)
            .ToList();

        if (gamesWithOffers.Count == 0)
        {
            return [];
        }

        var gameIds = gamesWithOffers.Select(g => g.Id).ToList();
        var metricsByGame = await _priceRepository.GetMetricsBatchAsync(gameIds, ct);

        var alerts = new List<PriceDropAlertDto>();

        foreach (var game in gamesWithOffers)
        {
            var offers = game.PurchaseLinks.AsEnumerable();

            // Filtrar por país si se ha especificado
            if (!string.IsNullOrWhiteSpace(country) && country != "Internacional")
            {
                offers = offers.Where(o => o.ShipsTo(country));
            }

            var inStockOffers = offers
                .Where(o => o.InStock && o.Price.HasValue && o.Price.Value > 0)
                .OrderBy(o => o.Price!.Value)
                .ToList();

            if (inStockOffers.Count == 0) continue;

            var bestOffer = inStockOffers[0];
            metricsByGame.TryGetValue(game.Id, out var metrics);

            decimal currentPrice = bestOffer.Price!.Value;
            decimal? referencePrice = metrics?.AveragePrice;

            // Si no hay media histórica, el PVP orientativo más alto de otra tienda o el propio juego
            if (!referencePrice.HasValue || referencePrice.Value <= currentPrice)
            {
                var maxOfferPrice = game.PurchaseLinks.Max(o => o.Price);
                if (maxOfferPrice.HasValue && maxOfferPrice.Value > currentPrice)
                {
                    referencePrice = maxOfferPrice.Value;
                }
            }

            double discountPercentage = 0.0;
            if (referencePrice.HasValue && referencePrice.Value > currentPrice)
            {
                discountPercentage = Math.Round((1.0 - (double)(currentPrice / referencePrice.Value)) * 100.0, 1);
            }

            bool isAllTimeLow = metrics?.IsAllTimeLow == true ||
                                (metrics?.AllTimeLowPrice.HasValue == true && currentPrice <= metrics.AllTimeLowPrice.Value);

            // Califica como chollo si el descuento alcanza el umbral mínimo (ej. >=10%)
            // o si representa un nuevo mínimo histórico con bajada real respecto a la referencia anterior
            bool isDeal = (discountPercentage >= _options.MinDiscountPercentage) ||
                          (isAllTimeLow && discountPercentage > 0);

            if (isDeal)
            {
                alerts.Add(new PriceDropAlertDto(
                    GameId: game.Id,
                    GameTitle: game.SpanishTitle,
                    GameSlug: game.Slug,
                    GameCoverUrl: game.CoverImageUrl,
                    StoreName: bestOffer.StoreName,
                    Country: bestOffer.Country,
                    AffiliateUrl: bestOffer.AffiliateUrl,
                    CurrentPrice: currentPrice,
                    Currency: bestOffer.Currency,
                    ReferencePrice: referencePrice,
                    DiscountPercentage: discountPercentage,
                    IsAllTimeLow: isAllTimeLow,
                    DetectedAtUtc: DateTimeOffset.UtcNow
                ));
            }
        }

        return alerts
            .OrderByDescending(a => a.IsAllTimeLow)
            .ThenByDescending(a => a.DiscountPercentage)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<PriceDropAlertDto>> GetUserWantToBuyAlertsAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return [];

        var wantToBuyItems = await _collectionRepository.GetByUserIdAsync(userId, CollectionStatus.WantToBuy, ct);
        var gamesFollowed = new List<Game>();
        foreach (var item in wantToBuyItems)
        {
            if (item.Game != null && item.Game.PurchaseLinks != null && item.Game.PurchaseLinks.Count > 0)
            {
                gamesFollowed.Add(item.Game);
            }
            else if (item.GameId.HasValue)
            {
                var g = await _gameRepository.GetByIdAsync(item.GameId.Value, ct);
                if (g != null && g.PurchaseLinks != null && g.PurchaseLinks.Count > 0)
                {
                    gamesFollowed.Add(g);
                }
            }
        }

        if (gamesFollowed.Count == 0) return [];

        var gameIds = gamesFollowed.Select(g => g.Id).ToList();
        var metricsByGame = await _priceRepository.GetMetricsBatchAsync(gameIds, ct);

        var alerts = new List<PriceDropAlertDto>();

        foreach (var game in gamesFollowed)
        {
            var bestOffer = game.PurchaseLinks
                .Where(o => o.InStock && o.Price.HasValue && o.Price.Value > 0)
                .OrderBy(o => o.Price!.Value)
                .FirstOrDefault();

            if (bestOffer == null) continue;

            metricsByGame.TryGetValue(game.Id, out var metrics);

            decimal currentPrice = bestOffer.Price!.Value;
            decimal? referencePrice = metrics?.AveragePrice;

            if (!referencePrice.HasValue || referencePrice.Value <= currentPrice)
            {
                var maxPrice = game.PurchaseLinks.Max(o => o.Price);
                if (maxPrice.HasValue && maxPrice.Value > currentPrice)
                {
                    referencePrice = maxPrice.Value;
                }
            }

            double discountPercentage = 0.0;
            if (referencePrice.HasValue && referencePrice.Value > currentPrice)
            {
                discountPercentage = Math.Round((1.0 - (double)(currentPrice / referencePrice.Value)) * 100.0, 1);
            }

            bool isAllTimeLow = metrics?.IsAllTimeLow == true ||
                                (metrics?.AllTimeLowPrice.HasValue == true && currentPrice <= metrics.AllTimeLowPrice.Value);

            bool isDeal = (discountPercentage >= _options.MinDiscountPercentage) ||
                          (isAllTimeLow && discountPercentage > 0);

            if (isDeal)
            {
                alerts.Add(new PriceDropAlertDto(
                    GameId: game.Id,
                    GameTitle: game.SpanishTitle,
                    GameSlug: game.Slug,
                    GameCoverUrl: game.CoverImageUrl,
                    StoreName: bestOffer.StoreName,
                    Country: bestOffer.Country,
                    AffiliateUrl: bestOffer.AffiliateUrl,
                    CurrentPrice: currentPrice,
                    Currency: bestOffer.Currency,
                    ReferencePrice: referencePrice,
                    DiscountPercentage: discountPercentage,
                    IsAllTimeLow: isAllTimeLow,
                    DetectedAtUtc: DateTimeOffset.UtcNow
                ));
            }
        }

        return alerts
            .OrderByDescending(a => a.IsAllTimeLow)
            .ThenByDescending(a => a.DiscountPercentage)
            .ToList();
    }

    public async Task<GamePriceMetrics> GetGamePriceMetricsAsync(Guid gameId, CancellationToken ct = default)
    {
        return await _priceRepository.GetMetricsAsync(gameId, ct);
    }

    public async Task<IReadOnlyList<PriceHistoryEntryDto>> GetGamePriceHistoryAsync(Guid gameId, int limit = 30, CancellationToken ct = default)
    {
        var history = await _priceRepository.GetHistoryAsync(gameId, limit, ct);
        return history.Select(h => new PriceHistoryEntryDto(
            RecordedAtUtc: h.RecordedAtUtc,
            StoreName: h.StoreName,
            Price: h.Price,
            Currency: h.Currency,
            InStock: h.InStock,
            AffiliateUrl: h.AffiliateUrl
        )).ToList();
    }

    public async Task RecordPriceObservationAsync(
        Guid gameId,
        string storeName,
        string affiliateUrl,
        decimal price,
        bool inStock,
        string currency = "€",
        CancellationToken ct = default)
    {
        var snapshot = new GamePriceSnapshot(gameId, storeName, affiliateUrl, price, inStock, currency);
        await _priceRepository.RecordSnapshotAsync(snapshot, ct);
    }

    public async Task<int> ScanWantToBuyPricesAsync(int maxGames = 20, CancellationToken ct = default)
    {
        if (maxGames <= 0) maxGames = _options.MaxGamesPerScan;

        var allGames = await _gameRepository.GetAllGamesAsync(ct);
        var targetGames = allGames
            .Where(g => g.PurchaseLinks != null && g.PurchaseLinks.Count > 0)
            .Take(maxGames)
            .ToList();

        int scanned = 0;
        var snapshotsToRecord = new List<GamePriceSnapshot>();

        foreach (var game in targetGames)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var batchResults = await _stockService.GetStockBatchAsync(game.PurchaseLinks, ct);
                foreach (var offer in game.PurchaseLinks)
                {
                    if (batchResults.TryGetValue(offer.AffiliateUrl, out var stockInfo) &&
                        stockInfo.CurrentPrice.HasValue &&
                        stockInfo.CurrentPrice.Value > 0)
                    {
                        snapshotsToRecord.Add(new GamePriceSnapshot(
                            gameId: game.Id,
                            storeName: offer.StoreName,
                            affiliateUrl: offer.AffiliateUrl,
                            price: stockInfo.CurrentPrice.Value,
                            inStock: stockInfo.IsInStock,
                            currency: offer.Currency
                        ));
                    }
                }
                scanned++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al sondear ofertas para el juego {GameTitle} ({GameId})", game.SpanishTitle, game.Id);
            }
        }

        if (snapshotsToRecord.Count > 0)
        {
            await _priceRepository.RecordSnapshotsBatchAsync(snapshotsToRecord, ct);
        }

        return scanned;
    }
}
