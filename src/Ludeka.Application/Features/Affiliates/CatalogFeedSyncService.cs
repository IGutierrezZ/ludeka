using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Ludeka.Application.Features.Affiliates;

/// <summary>
/// Servicio orquestador para la descarga, parsing en streaming, matching determinista por EAN
/// y sincronización de ofertas comerciales de tiendas colaboradoras.
/// </summary>
public class CatalogFeedSyncService : ICatalogFeedSyncService
{
    private static readonly Regex TitleCleanerRegex = new(
        @"(?:\s*[-–:]\s*(?:el\s+)?juego(?:\s+de\s+mesa)?|\s*\(juego\s+de\s+mesa\)|\s*\(edici[oó]n.*?\))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IAffiliateFeedSourceRepository _feedSourceRepository;
    private readonly IAffiliateEanDiscrepancyRepository _discrepancyRepository;
    private readonly IGameRepository _gameRepository;
    private readonly GoogleShoppingFeedParser _googleShoppingParser;
    private readonly IShopifyJsonCatalogParser _shopifyParser;
    private readonly IAffiliateUrlResolver _affiliateUrlResolver;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogFeedSyncService> _logger;

    public CatalogFeedSyncService(
        IAffiliateFeedSourceRepository feedSourceRepository,
        IAffiliateEanDiscrepancyRepository discrepancyRepository,
        IGameRepository gameRepository,
        GoogleShoppingFeedParser googleShoppingParser,
        IAffiliateUrlResolver affiliateUrlResolver,
        ILogger<CatalogFeedSyncService> logger,
        HttpClient? httpClient = null,
        IShopifyJsonCatalogParser? shopifyParser = null)
    {
        _feedSourceRepository = feedSourceRepository ?? throw new ArgumentNullException(nameof(feedSourceRepository));
        _discrepancyRepository = discrepancyRepository ?? throw new ArgumentNullException(nameof(discrepancyRepository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _googleShoppingParser = googleShoppingParser ?? throw new ArgumentNullException(nameof(googleShoppingParser));
        _affiliateUrlResolver = affiliateUrlResolver ?? throw new ArgumentNullException(nameof(affiliateUrlResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? new HttpClient();
        _shopifyParser = shopifyParser ?? new ShopifyJsonCatalogParser(affiliateUrlResolver);
    }

    /// <inheritdoc />
    public async Task<FeedSyncResult> SyncFeedSourceByIdAsync(Guid sourceId, CancellationToken ct = default)
    {
        var source = await _feedSourceRepository.GetByIdAsync(sourceId, ct);
        if (source == null)
        {
            return new FeedSyncResult(sourceId, "Desconocida", false, 0, 0, 0, 0, $"No existe ninguna fuente de feed con ID {sourceId}.");
        }

        return await SyncFeedSourceAsync(source, ct);
    }

    /// <inheritdoc />
    public async Task<List<FeedSyncResult>> SyncAllActiveFeedsAsync(CancellationToken ct = default)
    {
        var activeSources = await _feedSourceRepository.GetActiveSourcesAsync(ct);
        var results = new List<FeedSyncResult>();

        _logger.LogInformation("Iniciando sincronización de {Count} fuentes de catálogo comercial activas.", activeSources.Count);

        foreach (var source in activeSources)
        {
            ct.ThrowIfCancellationRequested();
            var result = await SyncFeedSourceAsync(source, ct);
            results.Add(result);
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<FeedSyncResult> SyncFeedSourceAsync(AffiliateFeedSource source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        _logger.LogInformation("Iniciando descarga y procesamiento de feed '{StoreName}' desde '{FeedUrl}'...", source.StoreName, source.FeedUrl);

        try
        {
            if (source.Format == FeedFormat.ShopifyJson)
            {
                var paginatedItems = _shopifyParser.ParsePaginatedAsync(source.FeedUrl, _httpClient, ct: ct);
                return await SyncFeedItemsAsync(source, paginatedItems, ct).ConfigureAwait(false);
            }

            using var response = await _httpClient.GetAsync(source.FeedUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            return await SyncFeedStreamAsync(source, stream, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al sincronizar el feed de '{StoreName}'.", source.StoreName);
            source.RecordSyncResult(false, 0, ex.Message);
            await _feedSourceRepository.UpdateAsync(source, ct).ConfigureAwait(false);

            return new FeedSyncResult(source.Id, source.StoreName, false, 0, 0, 0, 0, ex.Message);
        }
    }

    /// <summary>
    /// Procesa directamente un flujo de datos de feed para cruzar ofertas con el catálogo de juegos.
    /// </summary>
    public async Task<FeedSyncResult> SyncFeedStreamAsync(AffiliateFeedSource source, Stream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(stream);

        IAsyncEnumerable<FeedProductItem> items = source.Format switch
        {
            FeedFormat.ShopifyJson => _shopifyParser.ParseStreamAsync(stream, source.FeedUrl, ct),
            FeedFormat.GoogleShoppingXml => _googleShoppingParser.ParseStreamAsync(stream, ct),
            _ => _googleShoppingParser.ParseStreamAsync(stream, ct)
        };

        return await SyncFeedItemsAsync(source, items, ct).ConfigureAwait(false);
    }

    private async Task<FeedSyncResult> SyncFeedItemsAsync(
        AffiliateFeedSource source,
        IAsyncEnumerable<FeedProductItem> items,
        CancellationToken ct = default)
    {
        int itemsRead = 0;
        int matchedCount = 0;
        int autoAssignedEanCount = 0;
        int discrepanciesCount = 0;

        try
        {
            // 1. Cargar catálogo completo para construir índices de alta velocidad en memoria O(1)
            var allGames = await _gameRepository.GetAllGamesAsync(ct).ConfigureAwait(false);
            var modifiedGames = new Dictionary<Guid, Game>();

            var barcodeIndex = new Dictionary<string, Game>(StringComparer.OrdinalIgnoreCase);
            var slugIndex = new Dictionary<string, List<Game>>(StringComparer.OrdinalIgnoreCase);

            foreach (var g in allGames)
            {
                if (!string.IsNullOrWhiteSpace(g.Ean))
                {
                    barcodeIndex[g.Ean] = g;
                }

                foreach (var b in g.AdditionalBarcodes)
                {
                    if (!string.IsNullOrWhiteSpace(b))
                    {
                        barcodeIndex[b] = g;
                    }
                }

                IndexGameSlugs(slugIndex, g);
            }

            // 2. Cargar discrepancias pendientes para no generar duplicados en la bitácora
            var pendingDiscrepancies = await _discrepancyRepository.GetPendingDiscrepanciesAsync(1000, ct).ConfigureAwait(false);
            var loggedDiscrepancies = new HashSet<string>(
                pendingDiscrepancies.Select(d => $"{d.GameId}:{d.FeedEan}:{d.StoreName}"),
                StringComparer.OrdinalIgnoreCase);

            // 3. Procesar en streaming
            await foreach (var item in items.ConfigureAwait(false))
            {
                itemsRead++;
                Game? matchedGame = null;

                // Paso 1: Cruce determinista por EAN-13
                if (!string.IsNullOrWhiteSpace(item.NormalizedEan) && barcodeIndex.TryGetValue(item.NormalizedEan, out var gameByBarcode))
                {
                    matchedGame = gameByBarcode;
                }

                // Paso 2 y 3: Coincidencia por título si no hubo cruce por código de barras
                if (matchedGame == null)
                {
                    string itemSlug = Game.GenerateSlug(item.Title);
                    string cleanTitle = TitleCleanerRegex.Replace(item.Title, string.Empty).Trim();
                    string cleanSlug = Game.GenerateSlug(cleanTitle);

                    List<Game>? titleMatches = null;
                    if (!slugIndex.TryGetValue(itemSlug, out titleMatches) && cleanSlug != itemSlug)
                    {
                        slugIndex.TryGetValue(cleanSlug, out titleMatches);
                    }

                    if (titleMatches != null && titleMatches.Count == 1)
                    {
                        var candidate = titleMatches[0];

                        if (!string.IsNullOrWhiteSpace(item.NormalizedEan))
                        {
                            if (string.IsNullOrWhiteSpace(candidate.Ean))
                            {
                                // Paso 2: Auto-asignación de EAN a juego sin código
                                candidate.UpdateEan(item.NormalizedEan);
                                barcodeIndex[item.NormalizedEan] = candidate;
                                autoAssignedEanCount++;
                                matchedGame = candidate;
                            }
                            else if (!candidate.MatchesBarcode(item.NormalizedEan))
                            {
                                // Paso 3: Discrepancia detectada (el EAN de la tienda difiere del actual)
                                var barcodes = new List<string>(candidate.AdditionalBarcodes);
                                if (!barcodes.Contains(item.NormalizedEan))
                                {
                                    barcodes.Add(item.NormalizedEan);
                                    candidate.UpdateAdditionalBarcodes(barcodes);
                                    barcodeIndex[item.NormalizedEan] = candidate;
                                }

                                string discKey = $"{candidate.Id}:{item.NormalizedEan}:{source.StoreName}";
                                if (!loggedDiscrepancies.Contains(discKey))
                                {
                                    var log = new AffiliateEanDiscrepancyLog(
                                        gameId: candidate.Id,
                                        gameTitle: candidate.SpanishTitle ?? candidate.OriginalTitle,
                                        gameSlug: candidate.Slug,
                                        currentEan: candidate.Ean,
                                        feedEan: item.NormalizedEan,
                                        storeName: source.StoreName);

                                    await _discrepancyRepository.AddAsync(log, ct).ConfigureAwait(false);
                                    loggedDiscrepancies.Add(discKey);
                                    discrepanciesCount++;
                                }

                                matchedGame = candidate;
                            }
                            else
                            {
                                matchedGame = candidate;
                            }
                        }
                        else
                        {
                            matchedGame = candidate;
                        }
                    }
                }

                // Si se encontró o auto-asignó el juego, actualizamos la oferta de la tienda
                if (matchedGame != null)
                {
                    matchedCount++;
                    string resolvedUrl = _affiliateUrlResolver.ResolveAffiliateUrl(item.ProductUrl, source.StoreName);
                    string currency = item.Currency is "EUR" or "€" ? "€" : item.Currency;

                    var updatedLinks = UpdateOrAddOffer(
                        matchedGame.PurchaseLinks,
                        storeName: source.StoreName,
                        affiliateUrl: resolvedUrl,
                        price: item.Price,
                        currency: currency,
                        inStock: item.InStock,
                        affiliateTag: source.AffiliateTag,
                        country: source.Country);

                    matchedGame.UpdatePurchaseLinks(updatedLinks);
                    modifiedGames[matchedGame.Id] = matchedGame;
                }
            }

            // 4. Guardar todas las entidades de juegos modificadas
            foreach (var g in modifiedGames.Values)
            {
                await _gameRepository.UpdateAsync(g, ct).ConfigureAwait(false);
            }

            source.RecordSyncResult(true, matchedCount);
            await _feedSourceRepository.UpdateAsync(source, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Sincronización de '{StoreName}' finalizada con éxito. Leídos: {Read}, Cruzados: {Matched}, EANs Asignados: {Auto}, Discrepancias: {Disc}.",
                source.StoreName, itemsRead, matchedCount, autoAssignedEanCount, discrepanciesCount);

            return new FeedSyncResult(source.Id, source.StoreName, true, itemsRead, matchedCount, autoAssignedEanCount, discrepanciesCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante el procesamiento del flujo del feed de '{StoreName}'.", source.StoreName);
            source.RecordSyncResult(false, 0, ex.Message);
            await _feedSourceRepository.UpdateAsync(source, ct).ConfigureAwait(false);

            return new FeedSyncResult(source.Id, source.StoreName, false, itemsRead, matchedCount, autoAssignedEanCount, discrepanciesCount, ex.Message);
        }
    }

    private static void IndexGameSlugs(Dictionary<string, List<Game>> slugIndex, Game game)
    {
        AddKey(slugIndex, game.Slug, game);
        if (!string.IsNullOrWhiteSpace(game.SpanishTitle))
        {
            AddKey(slugIndex, Game.GenerateSlug(game.SpanishTitle), game);
        }
        if (!string.IsNullOrWhiteSpace(game.OriginalTitle))
        {
            AddKey(slugIndex, Game.GenerateSlug(game.OriginalTitle), game);
        }

        static void AddKey(Dictionary<string, List<Game>> index, string key, Game g)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (!index.TryGetValue(key, out var list))
            {
                list = [];
                index[key] = list;
            }
            if (!list.Any(existing => existing.Id == g.Id))
            {
                list.Add(g);
            }
        }
    }

    private static List<GamePurchaseLink> UpdateOrAddOffer(
        IEnumerable<GamePurchaseLink> existingLinks,
        string storeName,
        string affiliateUrl,
        decimal price,
        string currency,
        bool inStock,
        string? affiliateTag,
        string country)
    {
        var list = existingLinks.ToList();
        int idx = list.FindIndex(l => string.Equals(l.StoreName, storeName, StringComparison.OrdinalIgnoreCase));

        var newLink = new GamePurchaseLink(
            storeName: storeName,
            affiliateUrl: affiliateUrl,
            price: price > 0 ? price : null,
            currency: currency,
            inStock: inStock,
            badge: inStock ? "Stock Tienda" : "Agotado",
            affiliateTag: affiliateTag,
            country: country);

        if (idx >= 0)
        {
            list[idx] = newLink;
        }
        else
        {
            list.Add(newLink);
        }

        return list;
    }
}
