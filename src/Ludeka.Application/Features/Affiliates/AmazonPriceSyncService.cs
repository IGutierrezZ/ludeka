using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Application.Features.Affiliates;

/// <summary>
/// Orquestador central para la consulta, caché (límite estricto de 24 horas) y sincronización de precios de Amazon.
/// </summary>
public class AmazonPriceSyncService : IAmazonPriceSyncService
{
    private readonly IGameRepository _gameRepository;
    private readonly IGamePriceRepository _priceRepository;
    private readonly IAmazonProductProvider _amazonProductProvider;
    private readonly IAffiliateUrlResolver _affiliateUrlResolver;
    private readonly AmazonOptions _options;
    private readonly ILogger<AmazonPriceSyncService> _logger;

    public AmazonPriceSyncService(
        IGameRepository gameRepository,
        IGamePriceRepository priceRepository,
        IAmazonProductProvider amazonProductProvider,
        IAffiliateUrlResolver affiliateUrlResolver,
        IOptions<AmazonOptions> options,
        ILogger<AmazonPriceSyncService> logger)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _priceRepository = priceRepository ?? throw new ArgumentNullException(nameof(priceRepository));
        _amazonProductProvider = amazonProductProvider ?? throw new ArgumentNullException(nameof(amazonProductProvider));
        _affiliateUrlResolver = affiliateUrlResolver ?? throw new ArgumentNullException(nameof(affiliateUrlResolver));
        _options = options?.Value ?? new AmazonOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GamePriceSnapshot?> SyncGamePriceAsync(Guid gameId, CancellationToken ct = default)
    {
        var game = await _gameRepository.GetByIdAsync(gameId, ct);
        if (game == null)
        {
            _logger.LogWarning("Juego {GameId} no encontrado para sincronización de precio en Amazon.", gameId);
            return null;
        }

        // 1. Comprobar si existe un snapshot de Amazon de menos de 24 horas (cumplimiento legal estricto de Amazon)
        var history = await _priceRepository.GetHistoryAsync(gameId, limit: 20, ct);
        var latestSnapshot = history.FirstOrDefault(s => string.Equals(s.StoreName, "Amazon", StringComparison.OrdinalIgnoreCase));

        if (latestSnapshot != null && (DateTimeOffset.UtcNow - latestSnapshot.RecordedAtUtc) < TimeSpan.FromHours(24))
        {
            _logger.LogDebug("Precio de Amazon en caché válido para el juego {GameId} ({Price}{Currency}, registrado hace {Hours:F1}h).",
                gameId, latestSnapshot.Price, latestSnapshot.Currency, (DateTimeOffset.UtcNow - latestSnapshot.RecordedAtUtc).TotalHours);
            return latestSnapshot;
        }

        // 2. Si el juego no tiene ASIN pero tiene EAN, resolver ASIN primero
        if (string.IsNullOrWhiteSpace(game.Asin) && !string.IsNullOrWhiteSpace(game.Ean))
        {
            _logger.LogInformation("Juego {GameId} carece de ASIN. Intentando resolver por EAN {Ean}.", gameId, game.Ean);
            var resolvedAsin = await _amazonProductProvider.LookupAsinByEanAsync(game.Ean, ct);
            if (!string.IsNullOrWhiteSpace(resolvedAsin))
            {
                game.SetAsin(resolvedAsin);
                await _gameRepository.UpdateAsync(game, ct);
                _logger.LogInformation("ASIN {Asin} resuelto y persistido para el juego {GameId}.", resolvedAsin, gameId);
            }
        }

        // 3. Si no hay ASIN (ni se pudo resolver), no se puede consultar precio
        if (string.IsNullOrWhiteSpace(game.Asin))
        {
            _logger.LogDebug("No se dispone de ASIN para el juego {GameId}. Omitiendo consulta de precio.", gameId);
            return null;
        }

        // 4. Consultar precio y disponibilidad al proveedor configurado
        var priceResult = await _amazonProductProvider.GetPriceAndStockAsync(game.Asin, ct);
        if (priceResult == null)
        {
            _logger.LogWarning("No se pudo obtener precio para el juego {GameId} con ASIN {Asin}.", gameId, game.Asin);
            return null;
        }

        // 5. Construir y enriquecer la URL de afiliado
        var rawUrl = !string.IsNullOrWhiteSpace(priceResult.ProductUrl)
            ? priceResult.ProductUrl
            : $"https://www.amazon.es/dp/{game.Asin}";

        var affiliateUrl = _affiliateUrlResolver.ResolveAffiliateUrl(rawUrl, "Amazon");

        // 6. Registrar nuevo snapshot en el histórico
        var newSnapshot = new GamePriceSnapshot(
            gameId: game.Id,
            storeName: "Amazon",
            affiliateUrl: affiliateUrl,
            price: priceResult.Price,
            inStock: priceResult.InStock,
            currency: priceResult.Currency,
            recordedAtUtc: DateTimeOffset.UtcNow
        );

        await _priceRepository.RecordSnapshotAsync(newSnapshot, ct);

        // 7. Actualizar el enlace de compra de Amazon en el juego
        var remainingLinks = game.PurchaseLinks
            .Where(l => !string.Equals(l.StoreName, "Amazon", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var amazonLink = new GamePurchaseLink(
            storeName: "Amazon",
            affiliateUrl: affiliateUrl,
            price: priceResult.Price,
            currency: priceResult.Currency,
            inStock: priceResult.InStock,
            country: "España",
            badge: "Amazon",
            shippingCountries: ["España", "Portugal", "Internacional"]
        );
        remainingLinks.Add(amazonLink);

        game.UpdatePurchaseLinks(remainingLinks);
        await _gameRepository.UpdateAsync(game, ct);

        _logger.LogInformation("Precio de Amazon actualizado para {GameId}: {Price}{Currency} (En stock: {InStock}).",
            gameId, priceResult.Price, priceResult.Currency, priceResult.InStock);

        return newSnapshot;
    }
}
