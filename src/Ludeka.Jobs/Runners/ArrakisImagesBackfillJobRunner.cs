using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Jobs;
using Ludeka.Core.Entities;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Ludeka.Jobs.Runners;

/// <summary>
/// Trabajo autónomo para el barrido puntual y actualización de imágenes de alta calidad, EAN, PVP y ofertas del catálogo de Arrakis Games.
/// Recorre el catálogo completo (sitemap XML oficial y catálogo HTML), extrae los metadatos de cada ficha de producto
/// (EAN, PVPr, enlace BGG y fotos en wp-content/uploads), y los aplica en la réplica de base de datos.
/// </summary>
public sealed class ArrakisImagesBackfillJobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly IArrakisReleasesExtractor _arrakisExtractor;
    private readonly IGameRepository _gameRepository;
    private readonly IBggClient? _bggClient;
    private readonly ILogger<ArrakisImagesBackfillJobRunner> _logger;

    public ArrakisImagesBackfillJobRunner(
        IJobExecutionCoordinator coordinator,
        IArrakisReleasesExtractor arrakisExtractor,
        IGameRepository gameRepository,
        IBggClient? bggClient,
        ILogger<ArrakisImagesBackfillJobRunner> logger)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _arrakisExtractor = arrakisExtractor ?? throw new ArgumentNullException(nameof(arrakisExtractor));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _bggClient = bggClient;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ArrakisImagesBackfillJobRunner(
        IJobExecutionCoordinator coordinator,
        IArrakisReleasesExtractor arrakisExtractor,
        IGameRepository gameRepository,
        ILogger<ArrakisImagesBackfillJobRunner> logger)
        : this(coordinator, arrakisExtractor, gameRepository, null, logger)
    {
    }

    public string Name => JobNames.ArrakisImagesBackfill;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var windowKey = JobWindowKeyCalculator.PerSecond(nowUtc);

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (heartbeat, workCt) =>
            {
                _logger.LogInformation("Iniciando trabajo '{JobName}' para barrido de imágenes, EAN y ofertas de Arrakis Games...", Name);
                await heartbeat.BeatAsync(workCt);

                var allGames = await _gameRepository.GetAllGamesAsync(workCt).ConfigureAwait(false);
                var eanIndex = new Dictionary<string, Game>(StringComparer.OrdinalIgnoreCase);
                var bggIndex = new Dictionary<int, Game>();
                var titleIndex = new Dictionary<string, Game>(StringComparer.OrdinalIgnoreCase);

                foreach (var game in allGames)
                {
                    if (!string.IsNullOrWhiteSpace(game.Ean))
                    {
                        eanIndex.TryAdd(game.Ean.Trim(), game);
                    }

                    foreach (var barcode in game.AdditionalBarcodes)
                    {
                        if (!string.IsNullOrWhiteSpace(barcode))
                        {
                            eanIndex.TryAdd(barcode.Trim(), game);
                        }
                    }

                    if (game.BggId > 0)
                    {
                        bggIndex.TryAdd(game.BggId, game);
                    }

                    if (!string.IsNullOrWhiteSpace(game.SpanishTitle))
                    {
                        titleIndex.TryAdd(Normalize(game.SpanishTitle), game);
                    }

                    if (!string.IsNullOrWhiteSpace(game.OriginalTitle))
                    {
                        titleIndex.TryAdd(Normalize(game.OriginalTitle), game);
                    }
                }

                int totalCatalogItems = 0;
                int totalMatched = 0;
                int totalUpdated = 0;
                int totalImported = 0;
                int totalSkipped = 0;
                int totalFailed = 0;

                var catalogItems = await _arrakisExtractor.ExtractFullCatalogAsync(workCt).ConfigureAwait(false);
                totalCatalogItems = catalogItems.Count;
                _logger.LogInformation("Se obtuvieron {Count} productos para procesar en el catálogo de Arrakis Games.", totalCatalogItems);

                int itemIndex = 0;
                foreach (var item in catalogItems)
                {
                    if (workCt.IsCancellationRequested) break;

                    itemIndex++;
                    if (itemIndex % 10 == 0)
                    {
                        await heartbeat.BeatAsync(workCt);
                        _logger.LogInformation("Progreso de barrido Arrakis Games: {Current}/{Total}...", itemIndex, totalCatalogItems);
                    }

                    try
                    {
                        var gallery = await _arrakisExtractor.ExtractProductGalleryAsync(item.ProductUrl, workCt).ConfigureAwait(false);
                        if (gallery == null)
                        {
                            totalFailed++;
                            continue;
                        }

                        Game? matchedGame = null;

                        // 1. Cruce por EAN si está en la ficha
                        if (!string.IsNullOrWhiteSpace(gallery.Ean) && eanIndex.TryGetValue(gallery.Ean.Trim(), out var byEan))
                        {
                            matchedGame = byEan;
                        }

                        // 2. Cruce por BggId exacto (Arrakis suele enlazar su ficha canónica de BGG)
                        if (matchedGame == null && gallery.BggId.HasValue && bggIndex.TryGetValue(gallery.BggId.Value, out var byBgg))
                        {
                            matchedGame = byBgg;
                        }

                        // 3. Cruce por Título normalizado
                        var candidateTitle = !string.IsNullOrWhiteSpace(gallery.Title) ? gallery.Title : item.Title;
                        if (matchedGame == null && !string.IsNullOrWhiteSpace(candidateTitle))
                        {
                            var norm = Normalize(candidateTitle);
                            if (titleIndex.TryGetValue(norm, out var byTitle))
                            {
                                matchedGame = byTitle;
                            }
                        }

                        // 4. Si no se encontró localmente pero tenemos BggId y BggClient disponible, importar de BGG
                        if (matchedGame == null && gallery.BggId.HasValue && _bggClient != null)
                        {
                            try
                            {
                                var bggGame = await _bggClient.FetchGameByBggIdAsync(gallery.BggId.Value, workCt).ConfigureAwait(false);
                                if (bggGame != null)
                                {
                                    if (!string.IsNullOrWhiteSpace(gallery.Ean))
                                    {
                                        bggGame.UpdateEan(gallery.Ean);
                                    }
                                    bggGame.UpdateSpanishPublisher("Arrakis Games");

                                    if (!string.IsNullOrWhiteSpace(gallery.CoverImageUrl))
                                    {
                                        bggGame.UpdateImages(gallery.CoverImageUrl, bggGame.ThumbnailUrl ?? gallery.CoverImageUrl);
                                    }

                                    if (!string.IsNullOrWhiteSpace(gallery.TableImageUrl))
                                    {
                                        bggGame.UpdateMediaUrls(
                                            coverImageUrl: bggGame.CoverImageUrl,
                                            thumbnailUrl: bggGame.ThumbnailUrl,
                                            backCoverImageUrl: bggGame.BackCoverImageUrl,
                                            tableImageUrl: gallery.TableImageUrl);
                                    }

                                    if (gallery.Pvp.HasValue && gallery.Pvp.Value > 0)
                                    {
                                        var newLink = new GamePurchaseLink(
                                            storeName: "Arrakis Games",
                                            affiliateUrl: item.ProductUrl,
                                            price: gallery.Pvp.Value,
                                            currency: "€",
                                            inStock: !IsOutOfStock(gallery.StatusText),
                                            country: "España");

                                        bggGame.UpdatePurchaseLinks([newLink]);
                                    }

                                    await _gameRepository.AddRangeAsync(new[] { bggGame }, workCt).ConfigureAwait(false);

                                    // Indexar para evitar duplicidades
                                    if (!string.IsNullOrWhiteSpace(bggGame.Ean)) eanIndex.TryAdd(bggGame.Ean.Trim(), bggGame);
                                    if (bggGame.BggId > 0) bggIndex.TryAdd(bggGame.BggId, bggGame);
                                    if (!string.IsNullOrWhiteSpace(bggGame.SpanishTitle)) titleIndex.TryAdd(Normalize(bggGame.SpanishTitle), bggGame);
                                    if (!string.IsNullOrWhiteSpace(bggGame.OriginalTitle)) titleIndex.TryAdd(Normalize(bggGame.OriginalTitle), bggGame);

                                    matchedGame = bggGame;
                                    totalImported++;
                                    totalMatched++;
                                    continue;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "No se pudo importar de BGG con ID {BggId} para '{Title}'.", gallery.BggId.Value, candidateTitle);
                            }
                        }

                        if (matchedGame == null)
                        {
                            totalSkipped++;
                            continue;
                        }

                        totalMatched++;
                        bool modified = false;

                        // Portada: si Arrakis tiene portada y el juego carece de ella o es de baja calidad
                        if (!string.IsNullOrWhiteSpace(gallery.CoverImageUrl) &&
                            (string.IsNullOrWhiteSpace(matchedGame.CoverImageUrl) ||
                             !matchedGame.CoverImageUrl.Contains("arrakisgames.com", StringComparison.OrdinalIgnoreCase)))
                        {
                            matchedGame.UpdateImages(gallery.CoverImageUrl, matchedGame.ThumbnailUrl ?? gallery.CoverImageUrl);
                            modified = true;
                        }

                        // Imagen de mesa / componentes
                        if (!string.IsNullOrWhiteSpace(gallery.TableImageUrl) &&
                            string.IsNullOrWhiteSpace(matchedGame.TableImageUrl))
                        {
                            matchedGame.UpdateMediaUrls(
                                coverImageUrl: matchedGame.CoverImageUrl,
                                thumbnailUrl: matchedGame.ThumbnailUrl,
                                backCoverImageUrl: matchedGame.BackCoverImageUrl,
                                tableImageUrl: gallery.TableImageUrl);
                            modified = true;
                        }

                        // EAN si el juego no lo tiene
                        if (!string.IsNullOrWhiteSpace(gallery.Ean) && string.IsNullOrWhiteSpace(matchedGame.Ean))
                        {
                            if (BarcodeValidator.TryNormalizeEan13(gallery.Ean, out var validEan))
                            {
                                matchedGame.UpdateEan(validEan);
                                eanIndex.TryAdd(validEan, matchedGame);
                                modified = true;
                            }
                        }

                        // Editorial española
                        if (string.IsNullOrWhiteSpace(matchedGame.SpanishPublisher))
                        {
                            matchedGame.UpdateSpanishPublisher("Arrakis Games");
                            modified = true;
                        }

                        // Oferta / PVP oficial de Arrakis Games en PurchaseLinks
                        if (gallery.Pvp.HasValue && gallery.Pvp.Value > 0)
                        {
                            var existingOffer = matchedGame.PurchaseLinks.FirstOrDefault(p =>
                                string.Equals(p.StoreName, "Arrakis Games", StringComparison.OrdinalIgnoreCase));

                            bool inStock = !IsOutOfStock(gallery.StatusText);

                            if (existingOffer == null)
                            {
                                var newLink = new GamePurchaseLink(
                                    storeName: "Arrakis Games",
                                    affiliateUrl: item.ProductUrl,
                                    price: gallery.Pvp.Value,
                                    currency: "€",
                                    inStock: inStock,
                                    country: "España");

                                var updatedLinks = new List<GamePurchaseLink>(matchedGame.PurchaseLinks) { newLink };
                                matchedGame.UpdatePurchaseLinks(updatedLinks);
                                modified = true;
                            }
                            else if (existingOffer.Price != gallery.Pvp.Value || existingOffer.InStock != inStock || existingOffer.AffiliateUrl != item.ProductUrl)
                            {
                                var updatedLink = existingOffer with
                                {
                                    Price = gallery.Pvp.Value,
                                    AffiliateUrl = item.ProductUrl,
                                    InStock = inStock
                                };
                                var updatedLinks = matchedGame.PurchaseLinks
                                    .Select(p => string.Equals(p.StoreName, "Arrakis Games", StringComparison.OrdinalIgnoreCase) ? updatedLink : p)
                                    .ToList();
                                matchedGame.UpdatePurchaseLinks(updatedLinks);
                                modified = true;
                            }
                        }

                        if (modified)
                        {
                            await _gameRepository.UpdateAsync(matchedGame, workCt).ConfigureAwait(false);
                            totalUpdated++;
                        }
                        else
                        {
                            totalSkipped++;
                        }
                    }
                    catch (Exception ex)
                    {
                        totalFailed++;
                        _logger.LogWarning(ex, "Error al procesar el producto '{Url}' de Arrakis Games.", item.ProductUrl);
                    }
                }

                string summary = $"Barrido de Arrakis Games finalizado. Evaluados: {totalCatalogItems}, Coincidentes: {totalMatched}, Actualizados: {totalUpdated}, Importados: {totalImported}, Omitidos: {totalSkipped}, Errores: {totalFailed}.";
                _logger.LogInformation(summary);

                return new JobWorkResult(totalUpdated, totalFailed, summary);
            },
            ct);
    }

    private static bool IsOutOfStock(string? statusText)
    {
        if (string.IsNullOrWhiteSpace(statusText)) return false;
        var s = statusText.ToLowerInvariant();
        return s.Contains("agotado") || s.Contains("sin stock") || s.Contains("próximamente") || s.Contains("proximamente");
    }

    private static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var norm = input.Trim().ToLowerInvariant();
        norm = Regex.Replace(norm, @"[^\w\d]", "");
        return norm;
    }
}
