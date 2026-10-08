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
/// Trabajo autónomo para el barrido puntual y actualización de imágenes, EAN y precios del catálogo oficial de Maldito Games.
/// Recorre la paginación del catálogo de Maldito (https://tienda.malditogames.com/juegos?p={page}),
/// localiza juegos coincidentes en el repositorio local y enriquece sus recursos multimedia (caja 3D, mesa y contraportada),
/// asigna el EAN-13 si faltaba y registra la oferta oficial con su PVP en PurchaseLinks.
/// </summary>
public sealed class MalditoImagesBackfillJobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly IMalditoReleasesExtractor _malditoExtractor;
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<MalditoImagesBackfillJobRunner> _logger;

    public MalditoImagesBackfillJobRunner(
        IJobExecutionCoordinator coordinator,
        IMalditoReleasesExtractor malditoExtractor,
        IGameRepository gameRepository,
        ILogger<MalditoImagesBackfillJobRunner> logger)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _malditoExtractor = malditoExtractor ?? throw new ArgumentNullException(nameof(malditoExtractor));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => JobNames.MalditoImagesBackfill;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var windowKey = JobWindowKeyCalculator.PerSecond(nowUtc);

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (heartbeat, workCt) =>
            {
                _logger.LogInformation("Iniciando trabajo '{JobName}' para barrido de imágenes, EAN y ofertas de Maldito Games...", Name);
                await heartbeat.BeatAsync(workCt);

                var allGames = await _gameRepository.GetAllGamesAsync(workCt).ConfigureAwait(false);
                var eanIndex = new Dictionary<string, Game>(StringComparer.OrdinalIgnoreCase);
                var titleIndex = new Dictionary<string, Game>(StringComparer.OrdinalIgnoreCase);

                foreach (var game in allGames)
                {
                    if (!string.IsNullOrWhiteSpace(game.Ean))
                    {
                        eanIndex.TryAdd(game.Ean.Trim(), game);
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
                int totalSkipped = 0;
                int totalFailed = 0;
                int page = 1;
                const int maxPages = 60;
                int consecutiveFailures = 0;
                bool hasMore = true;

                while (hasMore && page <= maxPages && !workCt.IsCancellationRequested)
                {
                    await heartbeat.BeatAsync(workCt);
                    _logger.LogInformation("Consultando página {Page} del catálogo general de Maldito Games...", page);

                    var pageResult = await _malditoExtractor.ExtractCatalogPageAsync(page, workCt).ConfigureAwait(false);
                    if (!pageResult.Success)
                    {
                        consecutiveFailures++;
                        totalFailed++;
                        _logger.LogWarning("Fallo al consultar la página {Page} del catálogo de Maldito Games (fallos consecutivos: {Count}).", page, consecutiveFailures);
                        if (consecutiveFailures >= 2)
                        {
                            _logger.LogWarning("Se alcanzaron 2 fallos consecutivos al consultar el catálogo de Maldito Games. Interrumpiendo recorrido.");
                            break;
                        }

                        page++;
                        await Task.Delay(1000, workCt).ConfigureAwait(false);
                        continue;
                    }

                    consecutiveFailures = 0;

                    if (pageResult.Items.Count == 0)
                    {
                        _logger.LogInformation("Página {Page} de Maldito Games no devolvió productos. Finalizando recorrido.", page);
                        break;
                    }

                    totalCatalogItems += pageResult.Items.Count;

                    foreach (var item in pageResult.Items)
                    {
                        if (workCt.IsCancellationRequested) break;

                        Game? matchedGame = null;

                        // 1. Cruce por EAN
                        if (!string.IsNullOrWhiteSpace(item.Ean) && eanIndex.TryGetValue(item.Ean.Trim(), out var byEan))
                        {
                            matchedGame = byEan;
                        }

                        // 2. Cruce por Título normalizado
                        if (matchedGame == null && !string.IsNullOrWhiteSpace(item.Title))
                        {
                            var norm = Normalize(item.Title);
                            if (titleIndex.TryGetValue(norm, out var byTitle))
                            {
                                matchedGame = byTitle;
                            }
                        }

                        if (matchedGame == null)
                        {
                            totalSkipped++;
                            continue;
                        }

                        totalMatched++;

                        // Evaluar si ya tiene portada 3D, mesa, contraportada, EAN y oferta de Maldito Games
                        bool has3dCover = !string.IsNullOrWhiteSpace(matchedGame.CoverImageUrl) &&
                                          (matchedGame.CoverImageUrl.Contains("face3d", StringComparison.OrdinalIgnoreCase) ||
                                           matchedGame.CoverImageUrl.Contains("3d", StringComparison.OrdinalIgnoreCase));
                        bool hasTable = !string.IsNullOrWhiteSpace(matchedGame.TableImageUrl);
                        bool hasBack = !string.IsNullOrWhiteSpace(matchedGame.BackCoverImageUrl);
                        bool hasEan = !string.IsNullOrWhiteSpace(matchedGame.Ean);
                        bool hasMalditoOffer = matchedGame.PurchaseLinks.Any(p =>
                            string.Equals(p.StoreName, "Maldito Games", StringComparison.OrdinalIgnoreCase) &&
                            p.Price.HasValue && p.Price.Value > 0);

                        if (has3dCover && hasTable && hasBack && hasEan && hasMalditoOffer)
                        {
                            totalSkipped++;
                            continue;
                        }

                        try
                        {
                            var gallery = await _malditoExtractor.ExtractProductGalleryAsync(item.ProductUrl, workCt).ConfigureAwait(false);
                            if (gallery == null)
                            {
                                totalSkipped++;
                                continue;
                            }

                            bool modified = false;

                            // Actualizar portada si la nueva es de alta resolución o falta la actual
                            if (!string.IsNullOrWhiteSpace(gallery.CoverImageUrl) &&
                                (!has3dCover || string.IsNullOrWhiteSpace(matchedGame.CoverImageUrl)))
                            {
                                matchedGame.UpdateImages(gallery.CoverImageUrl, matchedGame.ThumbnailUrl ?? gallery.CoverImageUrl);
                                modified = true;
                            }

                            // Actualizar mesa y contraportada
                            var newTable = !string.IsNullOrWhiteSpace(gallery.TableImageUrl) ? gallery.TableImageUrl : matchedGame.TableImageUrl;
                            var newBack = !string.IsNullOrWhiteSpace(gallery.BackCoverImageUrl) ? gallery.BackCoverImageUrl : matchedGame.BackCoverImageUrl;

                            if (newTable != matchedGame.TableImageUrl || newBack != matchedGame.BackCoverImageUrl)
                            {
                                matchedGame.UpdateMediaUrls(
                                    coverImageUrl: matchedGame.CoverImageUrl,
                                    thumbnailUrl: matchedGame.ThumbnailUrl,
                                    backCoverImageUrl: newBack,
                                    tableImageUrl: newTable);
                                modified = true;
                            }

                            // Asignar EAN si el juego no lo tenía
                            if (!string.IsNullOrWhiteSpace(gallery.Ean) && string.IsNullOrWhiteSpace(matchedGame.Ean))
                            {
                                if (BarcodeValidator.TryNormalizeEan13(gallery.Ean, out var validEan))
                                {
                                    matchedGame.UpdateEan(validEan);
                                    modified = true;
                                }
                            }

                            // Actualizar o registrar oferta de Maldito Games en PurchaseLinks
                            if (gallery.Pvp.HasValue && gallery.Pvp.Value > 0)
                            {
                                var existingOffer = matchedGame.PurchaseLinks.FirstOrDefault(p =>
                                    string.Equals(p.StoreName, "Maldito Games", StringComparison.OrdinalIgnoreCase));

                                if (existingOffer == null)
                                {
                                    var newLink = new GamePurchaseLink(
                                        storeName: "Maldito Games",
                                        affiliateUrl: item.ProductUrl,
                                        price: gallery.Pvp.Value,
                                        currency: "€",
                                        inStock: true,
                                        country: "España");

                                    var updatedLinks = new List<GamePurchaseLink>(matchedGame.PurchaseLinks) { newLink };
                                    matchedGame.UpdatePurchaseLinks(updatedLinks);
                                    modified = true;
                                }
                                else if (existingOffer.Price != gallery.Pvp.Value || existingOffer.AffiliateUrl != item.ProductUrl)
                                {
                                    var updatedLink = existingOffer with
                                    {
                                        Price = gallery.Pvp.Value,
                                        AffiliateUrl = item.ProductUrl,
                                        InStock = true
                                    };
                                    var updatedLinks = matchedGame.PurchaseLinks
                                        .Select(p => string.Equals(p.StoreName, "Maldito Games", StringComparison.OrdinalIgnoreCase) ? updatedLink : p)
                                        .ToList();
                                    matchedGame.UpdatePurchaseLinks(updatedLinks);
                                    modified = true;
                                }
                            }

                            if (modified)
                            {
                                await _gameRepository.UpdateAsync(matchedGame, workCt).ConfigureAwait(false);
                                totalUpdated++;
                                _logger.LogInformation("Actualizados datos/imágenes/oferta para '{Title}' ({Slug}) desde Maldito Games.", matchedGame.SpanishTitle, matchedGame.Slug);
                            }
                            else
                            {
                                totalSkipped++;
                            }

                            // Pausa defensiva de cortesía para no saturar Maldito Games
                            await Task.Delay(250, workCt).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException || !workCt.IsCancellationRequested)
                        {
                            totalFailed++;
                            _logger.LogWarning(ex, "Error al extraer galería para '{ProductUrl}'.", item.ProductUrl);
                        }
                    }

                    hasMore = pageResult.HasNextPage;
                    page++;

                    // Breve pausa cortés entre páginas del catálogo
                    await Task.Delay(750, workCt).ConfigureAwait(false);
                }

                string summary = $"Barrido de catálogo de Maldito Games finalizado. Evaluados: {totalCatalogItems}, Coincidentes: {totalMatched}, Actualizados: {totalUpdated}, Omitidos: {totalSkipped}, Fallos: {totalFailed}.";
                _logger.LogInformation(summary);

                return new JobWorkResult(totalUpdated, totalFailed, summary);
            },
            ct);
    }

    private static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var normalized = input.Trim().ToLowerInvariant();
        return Regex.Replace(normalized, @"[^\w\d]", "");
    }
}
