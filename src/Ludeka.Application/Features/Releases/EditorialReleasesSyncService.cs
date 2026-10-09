using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Logging;

namespace Ludeka.Application.Features.Releases;

/// <summary>
/// Servicio orquestador para la sincronización automática de novedades editoriales y su cruce con el catálogo.
/// </summary>
public class EditorialReleasesSyncService : IEditorialReleasesSyncService
{
    private readonly IDevirReleasesExtractor _devirExtractor;
    private readonly IMalditoReleasesExtractor _malditoExtractor;
    private readonly IArrakisReleasesExtractor? _arrakisExtractor;
    private readonly IWeeklyReleaseRepository _weeklyReleaseRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IBggClient? _bggClient;
    private readonly IReleaseAiMatcherService? _aiMatcherService;
    private readonly ILogger<EditorialReleasesSyncService> _logger;

    public EditorialReleasesSyncService(
        IDevirReleasesExtractor devirExtractor,
        IMalditoReleasesExtractor malditoExtractor,
        IArrakisReleasesExtractor? arrakisExtractor,
        IWeeklyReleaseRepository weeklyReleaseRepository,
        IGameRepository gameRepository,
        IBggClient? bggClient,
        IReleaseAiMatcherService? aiMatcherService,
        ILogger<EditorialReleasesSyncService> logger)
    {
        _devirExtractor = devirExtractor ?? throw new ArgumentNullException(nameof(devirExtractor));
        _malditoExtractor = malditoExtractor ?? throw new ArgumentNullException(nameof(malditoExtractor));
        _arrakisExtractor = arrakisExtractor;
        _weeklyReleaseRepository = weeklyReleaseRepository ?? throw new ArgumentNullException(nameof(weeklyReleaseRepository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _bggClient = bggClient;
        _aiMatcherService = aiMatcherService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public EditorialReleasesSyncService(
        IDevirReleasesExtractor devirExtractor,
        IMalditoReleasesExtractor malditoExtractor,
        IWeeklyReleaseRepository weeklyReleaseRepository,
        IGameRepository gameRepository,
        IBggClient? bggClient,
        IReleaseAiMatcherService? aiMatcherService,
        ILogger<EditorialReleasesSyncService> logger)
        : this(devirExtractor, malditoExtractor, null, weeklyReleaseRepository, gameRepository, bggClient, aiMatcherService, logger)
    {
    }

    public EditorialReleasesSyncService(
        IDevirReleasesExtractor devirExtractor,
        IMalditoReleasesExtractor malditoExtractor,
        IWeeklyReleaseRepository weeklyReleaseRepository,
        IGameRepository gameRepository,
        IBggClient? bggClient,
        ILogger<EditorialReleasesSyncService> logger)
        : this(devirExtractor, malditoExtractor, null, weeklyReleaseRepository, gameRepository, bggClient, null, logger)
    {
    }

    public async Task<EditorialSyncSummaryDto> SyncAllEditorialReleasesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Iniciando sincronización completa de lanzamientos editoriales...");

        var publisherResults = new List<EditorialSyncResultDto>();
        var errors = new List<string>();

        var publishers = new[] { "Devir", "Maldito Games", "Arrakis Games" };

        foreach (var pub in publishers)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var result = await SyncPublisherReleasesAsync(pub, ct).ConfigureAwait(false);
                publisherResults.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no controlado al sincronizar la editorial '{Publisher}'.", pub);
                publisherResults.Add(new EditorialSyncResultDto(pub, false, 0, 0, 0, 0, 0, ex.Message));
                errors.Add($"{pub}: {ex.Message}");
            }
        }

        int totalFound = publisherResults.Sum(r => r.ItemsFound);
        int totalCreated = publisherResults.Sum(r => r.ReleasesCreated);
        int totalUpdated = publisherResults.Sum(r => r.ReleasesUpdated);
        int totalLinked = publisherResults.Sum(r => r.GamesLinked);
        int totalImported = publisherResults.Sum(r => r.GamesImported);

        _logger.LogInformation(
            "Sincronización editorial finalizada. Total encontrados: {Found}, Creados: {Created}, Actualizados: {Updated}, Vinculados: {Linked}, BGG importados: {Imported}.",
            totalFound, totalCreated, totalUpdated, totalLinked, totalImported);

        return new EditorialSyncSummaryDto(
            totalFound,
            totalCreated,
            totalUpdated,
            totalLinked,
            totalImported,
            publisherResults,
            errors);
    }

    public async Task<EditorialSyncResultDto> SyncPublisherReleasesAsync(string publisher, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publisher);
        _logger.LogInformation("Extrayendo lanzamientos para la editorial '{Publisher}'...", publisher);

        IReadOnlyList<EditorialReleaseItem> extractedItems;

        if (publisher.Equals("Devir", StringComparison.OrdinalIgnoreCase))
        {
            extractedItems = await _devirExtractor.ExtractReleasesAsync(ct).ConfigureAwait(false);
        }
        else if (publisher.Contains("Maldito", StringComparison.OrdinalIgnoreCase))
        {
            extractedItems = await _malditoExtractor.ExtractReleasesAsync(ct).ConfigureAwait(false);
        }
        else if (publisher.Contains("Arrakis", StringComparison.OrdinalIgnoreCase))
        {
            if (_arrakisExtractor == null)
            {
                return new EditorialSyncResultDto(publisher, false, 0, 0, 0, 0, 0, "Extractor de Arrakis Games no configurado.");
            }
            extractedItems = await _arrakisExtractor.ExtractReleasesAsync(ct).ConfigureAwait(false);
        }
        else
        {
            return new EditorialSyncResultDto(publisher, false, 0, 0, 0, 0, 0, $"Editorial '{publisher}' no soportada.");
        }

        // Cargar novedades existentes para idempotencia
        var existingReleases = await _weeklyReleaseRepository.GetReleasesAsync(null, ct).ConfigureAwait(false);

        // Limpiar únicamente registros que estuvieran huérfanos sin estado definido o inválidos (no tocar PendingModeration ni Rejected)
        var orphanReleases = existingReleases
            .Where(r => r.Publisher.Equals(publisher, StringComparison.OrdinalIgnoreCase) && 
                        r.GameId == null && 
                        r.Status == WeeklyReleaseStatus.Published &&
                        r.AiSuggestedBggId == null &&
                        r.AiSuggestedTitle == null)
            .ToList();

        foreach (var orphan in orphanReleases)
        {
            await _weeklyReleaseRepository.DeleteAsync(orphan.Id, ct).ConfigureAwait(false);
        }

        if (extractedItems.Count == 0)
        {
            _logger.LogWarning("No se encontraron lanzamientos para la editorial '{Publisher}'.", publisher);
            return new EditorialSyncResultDto(publisher, true, 0, 0, 0, 0, 0);
        }

        var releaseIndex = new Dictionary<string, WeeklyRelease>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in existingReleases)
        {
            var key = MakeReleaseKey(rel.Title, rel.Publisher);
            releaseIndex[key] = rel;
        }

        // Cargar catálogo de juegos para índices en memoria
        var allGames = await _gameRepository.GetAllGamesAsync(ct).ConfigureAwait(false);
        var barcodeIndex = new Dictionary<string, Game>(StringComparer.OrdinalIgnoreCase);
        var titleIndex = new Dictionary<string, Game>(StringComparer.OrdinalIgnoreCase);
        var bggIndex = new Dictionary<int, Game>();

        foreach (var g in allGames)
        {
            IndexGame(barcodeIndex, titleIndex, bggIndex, g);
        }

        int createdCount = 0;
        int updatedCount = 0;
        int linkedCount = 0;
        int importedCount = 0;
        int currentYear = DateTime.UtcNow.Year;

        foreach (var item in extractedItems)
        {
            ct.ThrowIfCancellationRequested();

            var releaseKey = MakeReleaseKey(item.Title, item.Publisher);
            releaseIndex.TryGetValue(releaseKey, out var existingRelease);

            // Si el moderador ya rechazó este elemento previamente, respetamos su decisión
            if (existingRelease != null && existingRelease.Status == WeeklyReleaseStatus.Rejected)
            {
                continue;
            }

            Game? matchedGame = null;
            bool isReprint = item.IsReprint;

            // 1. Cruce por EAN (si el item lo incluye)
            if (!string.IsNullOrWhiteSpace(item.Ean) && barcodeIndex.TryGetValue(item.Ean, out var gameByEan))
            {
                matchedGame = gameByEan;
            }

            // 1.5 Cruce canónico por BggId en memoria si el ítem lo incluye (fichas directas de Arrakis Games)
            if (matchedGame == null && item.BggId.HasValue && bggIndex.TryGetValue(item.BggId.Value, out var gameByBgg))
            {
                matchedGame = gameByBgg;
            }

            var cleanedTitle = CleanCommercialTitle(item.Title);
            var baseTitle = ExtractBaseTitle(cleanedTitle);
            bool matchedByBaseTitleOnly = false;

            // 2. Cruce por Título normalizado si no se encontró por EAN ni por BggId
            if (matchedGame == null)
            {
                var normTitle = Normalize(item.Title);
                if (titleIndex.TryGetValue(normTitle, out var gameByTitle))
                {
                    matchedGame = gameByTitle;
                }
                else if (!string.IsNullOrWhiteSpace(cleanedTitle) &&
                         titleIndex.TryGetValue(Normalize(cleanedTitle), out var gameByClean))
                {
                    matchedGame = gameByClean;
                }
                else if (!string.IsNullOrWhiteSpace(baseTitle) &&
                         titleIndex.TryGetValue(Normalize(baseTitle), out var gameByBase))
                {
                    matchedGame = gameByBase;
                    matchedByBaseTitleOnly = true;
                }
            }

            // 2.5 Si el item incluye BggId directo y no se localizó en catálogo local en memoria:
            if (matchedGame == null && item.BggId.HasValue)
            {
                try
                {
                    var existingByBgg = await _gameRepository.GetByBggIdAsync(item.BggId.Value, ct).ConfigureAwait(false);
                    if (existingByBgg != null)
                    {
                        IndexGame(barcodeIndex, titleIndex, bggIndex, existingByBgg);
                        matchedGame = existingByBgg;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error al consultar juego por BggId {BggId}.", item.BggId.Value);
                }

                if (matchedGame == null && _bggClient != null)
                {
                    try
                    {
                        var bggGame = await _bggClient.FetchGameByBggIdAsync(item.BggId.Value, ct).ConfigureAwait(false);
                        if (bggGame != null)
                        {
                            if (!string.IsNullOrWhiteSpace(item.Ean)) bggGame.UpdateEan(item.Ean);
                            bggGame.UpdateSpanishPublisher(item.Publisher);
                            await _gameRepository.AddRangeAsync(new[] { bggGame }, ct).ConfigureAwait(false);
                            IndexGame(barcodeIndex, titleIndex, bggIndex, bggGame);
                            matchedGame = bggGame;
                            importedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo importar directamente de BGG con BggId {BggId} para '{Title}'.", item.BggId.Value, item.Title);
                        try
                        {
                            var concurrentGame = await _gameRepository.GetByBggIdAsync(item.BggId.Value, ct).ConfigureAwait(false);
                            if (concurrentGame != null)
                            {
                                IndexGame(barcodeIndex, titleIndex, bggIndex, concurrentGame);
                                matchedGame = concurrentGame;
                            }
                        }
                        catch
                        {
                            // Ignorar fallback secundario
                        }
                    }
                }
            }

            // 3. Si no hay cruce en catálogo local:
            AiReleaseMatchResultDto? aiMatch = null;
            if (matchedGame == null)
            {
                if (_aiMatcherService != null)
                {
                    try
                    {
                        using var aiCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        aiCts.CancelAfter(TimeSpan.FromSeconds(4));
                        aiMatch = await _aiMatcherService.SuggestMatchAsync(item.Title, item.Publisher, item.EstimatedPvp, item.Notes, aiCts.Token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Timeout o error al invocar el asistente de IA para el lanzamiento '{Title}'. Aplicando fallback.", item.Title);
                    }
                }
                else if (_bggClient != null)
                {
                    // Fallback para suites de tests unitarios que configuran BggClient simulado sin servicio de IA
                    try
                    {
                        var queriesToTry = new List<string> { item.Title };
                        if (!string.IsNullOrWhiteSpace(cleanedTitle) && !cleanedTitle.Equals(item.Title, StringComparison.OrdinalIgnoreCase))
                            queriesToTry.Add(cleanedTitle);
                        if (!string.IsNullOrWhiteSpace(baseTitle) && !baseTitle.Equals(cleanedTitle, StringComparison.OrdinalIgnoreCase))
                            queriesToTry.Add(baseTitle);

                        BggSearchResultDto? bestMatch = null;
                        foreach (var query in queriesToTry)
                        {
                            var searchResults = await _bggClient.SearchGamesAsync(query, ct).ConfigureAwait(false);
                            if (searchResults.Count > 0)
                            {
                                bestMatch = FindBestMatch(query, searchResults);
                                if (bestMatch != null) break;
                            }
                        }

                        if (bestMatch != null)
                        {
                            var bggGame = await _bggClient.FetchGameByBggIdAsync(bestMatch.BggId, ct).ConfigureAwait(false);
                            if (bggGame != null)
                            {
                                if (!string.IsNullOrWhiteSpace(item.Ean)) bggGame.UpdateEan(item.Ean);
                                bggGame.UpdateSpanishPublisher(item.Publisher);
                                await _gameRepository.AddRangeAsync(new[] { bggGame }, ct).ConfigureAwait(false);
                                IndexGame(barcodeIndex, titleIndex, bggIndex, bggGame);
                                matchedGame = bggGame;
                                importedCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo consultar BGG para el título '{Title}'.", item.Title);
                    }
                }
            }

            // Enriquecer el juego y calcular reprint si está vinculado
            if (matchedGame != null)
            {
                linkedCount++;

                if (matchedGame.YearPublished > 0 && matchedGame.YearPublished < currentYear)
                {
                    isReprint = true;
                }

                bool isSubtitleProduct = !string.IsNullOrWhiteSpace(baseTitle) &&
                                         !string.Equals(cleanedTitle, baseTitle, StringComparison.OrdinalIgnoreCase);

                // PROTECCIÓN ESTRICTA: Solo mutar metadatos del juego si la coincidencia fue directa (por EAN o por título completo),
                // NUNCA si fue una vinculación laxa por título base (ej. "Viticulture: Bordeaux" cortado a "Viticulture")
                // ni cuando el producto editorial tiene subtítulo y el juego vinculado es un juego base.
                if (!matchedByBaseTitleOnly && !(isSubtitleProduct && !matchedGame.IsExpansion))
                {
                    bool gameModified = false;
                    if (!string.IsNullOrWhiteSpace(item.Ean) && string.IsNullOrWhiteSpace(matchedGame.Ean))
                    {
                        matchedGame.UpdateEan(item.Ean);
                        gameModified = true;
                    }

                    if (string.IsNullOrWhiteSpace(matchedGame.SpanishPublisher))
                    {
                        matchedGame.UpdateSpanishPublisher(item.Publisher);
                        gameModified = true;
                    }

                    if (!string.IsNullOrWhiteSpace(item.CoverImageUrl) &&
                        (item.CoverImageUrl.Contains("face3d", StringComparison.OrdinalIgnoreCase) ||
                         item.CoverImageUrl.Contains("3d", StringComparison.OrdinalIgnoreCase) ||
                         string.IsNullOrWhiteSpace(matchedGame.CoverImageUrl)))
                    {
                        matchedGame.UpdateImages(item.CoverImageUrl, matchedGame.ThumbnailUrl ?? item.CoverImageUrl);
                        gameModified = true;
                    }

                    if (!string.IsNullOrWhiteSpace(item.TableImageUrl) || !string.IsNullOrWhiteSpace(item.BackCoverImageUrl))
                    {
                        var currentTable = matchedGame.TableImageUrl;
                        var currentBack = matchedGame.BackCoverImageUrl;

                        var newTable = !string.IsNullOrWhiteSpace(item.TableImageUrl) ? item.TableImageUrl : currentTable;
                        var newBack = !string.IsNullOrWhiteSpace(item.BackCoverImageUrl) ? item.BackCoverImageUrl : currentBack;

                        if (newTable != currentTable || newBack != currentBack)
                        {
                            matchedGame.UpdateMediaUrls(
                                coverImageUrl: matchedGame.CoverImageUrl,
                                thumbnailUrl: matchedGame.ThumbnailUrl,
                                backCoverImageUrl: newBack,
                                tableImageUrl: newTable);
                            gameModified = true;
                        }
                    }

                    if (gameModified)
                    {
                        await _gameRepository.UpdateAsync(matchedGame, ct).ConfigureAwait(false);
                    }
                }
            }

            // Resolver precio estimado
            decimal? finalEstimatedPvp = item.EstimatedPvp;
            if (finalEstimatedPvp == null && matchedGame?.PurchaseLinks.Count > 0)
            {
                var minOfferPrice = matchedGame.PurchaseLinks
                    .Where(p => p.Price.HasValue && p.Price.Value > 0)
                    .Select(p => p.Price!.Value)
                    .DefaultIfEmpty(0)
                    .Min();

                if (minOfferPrice > 0)
                {
                    finalEstimatedPvp = minOfferPrice;
                }
            }

            // 4. Crear o actualizar WeeklyRelease
            if (existingRelease != null)
            {
                existingRelease.Update(
                    title: existingRelease.Title,
                    publisher: existingRelease.Publisher,
                    releaseDate: item.ReleaseDate ?? existingRelease.ReleaseDate,
                    gameId: matchedGame != null ? matchedGame.Id : existingRelease.GameId,
                    coverImageUrl: item.CoverImageUrl ?? existingRelease.CoverImageUrl,
                    estimatedPvp: finalEstimatedPvp ?? existingRelease.EstimatedPvp,
                    isReprint: existingRelease.IsReprint || isReprint,
                    notes: !string.IsNullOrWhiteSpace(item.Notes) ? item.Notes : existingRelease.Notes,
                    sourceUrl: item.SourceUrl ?? existingRelease.SourceUrl,
                    isMonthOnly: item.IsMonthOnly);

                if (matchedGame != null && existingRelease.Status == WeeklyReleaseStatus.PendingModeration)
                {
                    existingRelease.Approve(matchedGame.Id);
                }
                else if (matchedGame == null && aiMatch != null && existingRelease.Status == WeeklyReleaseStatus.PendingModeration)
                {
                    existingRelease.SetAiSuggestion(aiMatch.SuggestedBggId, aiMatch.SuggestedTitle, aiMatch.Reasoning);
                }

                await _weeklyReleaseRepository.UpdateAsync(existingRelease, ct).ConfigureAwait(false);
                updatedCount++;
            }
            else
            {
                var newRelease = new WeeklyRelease(
                    title: item.Title,
                    publisher: item.Publisher,
                    releaseDate: item.ReleaseDate,
                    gameId: matchedGame?.Id,
                    coverImageUrl: item.CoverImageUrl,
                    estimatedPvp: finalEstimatedPvp,
                    isReprint: isReprint,
                    notes: item.Notes,
                    sourceUrl: item.SourceUrl,
                    isMonthOnly: item.IsMonthOnly);

                if (matchedGame == null)
                {
                    newRelease.SetPendingModeration(
                        aiMatch?.SuggestedBggId,
                        aiMatch?.SuggestedTitle,
                        aiMatch?.Reasoning ?? "Pendiente de revisión y vinculación por moderador.");
                }

                await _weeklyReleaseRepository.AddAsync(newRelease, ct).ConfigureAwait(false);
                releaseIndex[releaseKey] = newRelease;
                createdCount++;
            }
        }

        return new EditorialSyncResultDto(
            publisher,
            true,
            extractedItems.Count,
            createdCount,
            updatedCount,
            linkedCount,
            importedCount);
    }

    private static void IndexGame(
        Dictionary<string, Game> barcodeIndex,
        Dictionary<string, Game> titleIndex,
        Dictionary<int, Game> bggIndex,
        Game g)
    {
        if (g.BggId > 0)
        {
            bggIndex[g.BggId] = g;
        }

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

        if (!string.IsNullOrWhiteSpace(g.SpanishTitle))
        {
            titleIndex[Normalize(g.SpanishTitle)] = g;
        }

        if (!string.IsNullOrWhiteSpace(g.OriginalTitle))
        {
            titleIndex[Normalize(g.OriginalTitle)] = g;
        }
    }

    private static BggSearchResultDto? FindBestMatch(string queryTitle, IReadOnlyList<BggSearchResultDto> results)
    {
        var norm = Normalize(queryTitle);

        // 1. Coincidencia exacta normalizada
        var exact = results.FirstOrDefault(r => Normalize(r.Title) == norm);
        if (exact != null)
            return exact;

        // 2. Coincidencia con título comercial limpio
        var normClean = Normalize(CleanCommercialTitle(queryTitle));
        if (!string.IsNullOrWhiteSpace(normClean))
        {
            var cleanMatch = results.FirstOrDefault(r => Normalize(r.Title) == normClean);
            if (cleanMatch != null)
                return cleanMatch;
        }

        // 3. Coincidencia con título base
        var normBase = Normalize(ExtractBaseTitle(queryTitle));
        if (!string.IsNullOrWhiteSpace(normBase))
        {
            var baseMatch = results.FirstOrDefault(r => Normalize(r.Title) == normBase);
            if (baseMatch != null)
                return baseMatch;
        }

        // 4. Primer resultado devuelto por BGG
        return results.FirstOrDefault();
    }

    public static string CleanCommercialTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var decoded = WebUtility.HtmlDecode(title).Trim();

        // Eliminar coletillas de edición: "Edición Kickstarter", "Edición Esencial", "Edición Almirante..."
        decoded = Regex.Replace(
            decoded,
            @"\s*[-–:]?\s*(?:Edici[oó]n|Version|Versi[oó]n)\s+(?:Kickstarter|Esencial|Deluxe|Coleccionista|Especial|Mecenas|Almirante|Retail|Definitiva)(?:.*)?$",
            "",
            RegexOptions.IgnoreCase).Trim();

        return decoded;
    }

    public static string ExtractBaseTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var parts = title.Split(new[] { " - ", " – ", " — ", ": " }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
        {
            return parts[0].Trim();
        }

        return title.Trim();
    }

    private static string MakeReleaseKey(string title, string publisher)
        => $"{Normalize(title)}:{Normalize(publisher)}";

    private static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var normalized = input.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^\w\d]", "");
        return normalized;
    }
}
