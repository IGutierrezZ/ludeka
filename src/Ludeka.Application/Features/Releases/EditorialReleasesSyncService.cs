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
using Microsoft.Extensions.Logging;

namespace Ludeka.Application.Features.Releases;

/// <summary>
/// Servicio orquestador para la sincronización automática de novedades editoriales y su cruce con el catálogo.
/// </summary>
public class EditorialReleasesSyncService : IEditorialReleasesSyncService
{
    private readonly IDevirReleasesExtractor _devirExtractor;
    private readonly IMalditoReleasesExtractor _malditoExtractor;
    private readonly IWeeklyReleaseRepository _weeklyReleaseRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IBggClient? _bggClient;
    private readonly ILogger<EditorialReleasesSyncService> _logger;

    public EditorialReleasesSyncService(
        IDevirReleasesExtractor devirExtractor,
        IMalditoReleasesExtractor malditoExtractor,
        IWeeklyReleaseRepository weeklyReleaseRepository,
        IGameRepository gameRepository,
        IBggClient? bggClient,
        ILogger<EditorialReleasesSyncService> logger)
    {
        _devirExtractor = devirExtractor ?? throw new ArgumentNullException(nameof(devirExtractor));
        _malditoExtractor = malditoExtractor ?? throw new ArgumentNullException(nameof(malditoExtractor));
        _weeklyReleaseRepository = weeklyReleaseRepository ?? throw new ArgumentNullException(nameof(weeklyReleaseRepository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _bggClient = bggClient;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EditorialSyncSummaryDto> SyncAllEditorialReleasesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Iniciando sincronización completa de lanzamientos editoriales...");

        var publisherResults = new List<EditorialSyncResultDto>();
        var errors = new List<string>();

        var publishers = new[] { "Devir", "Maldito Games" };

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
        else
        {
            return new EditorialSyncResultDto(publisher, false, 0, 0, 0, 0, 0, $"Editorial '{publisher}' no soportada.");
        }

        // Cargar novedades existentes para idempotencia y purga de huérfanos
        var existingReleases = await _weeklyReleaseRepository.GetReleasesAsync(null, ct).ConfigureAwait(false);

        // Limpiar posibles registros huérfanos previos de esta editorial sin juego vinculado (ej. suplementos de rol o cómics)
        var orphanReleases = existingReleases
            .Where(r => r.Publisher.Equals(publisher, StringComparison.OrdinalIgnoreCase) && r.GameId == null)
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
        foreach (var rel in existingReleases.Where(r => r.GameId != null))
        {
            var key = MakeReleaseKey(rel.Title, rel.Publisher);
            releaseIndex[key] = rel;
        }

        // Cargar catálogo de juegos para índices en memoria
        var allGames = await _gameRepository.GetAllGamesAsync(ct).ConfigureAwait(false);
        var barcodeIndex = new Dictionary<string, Game>(StringComparer.OrdinalIgnoreCase);
        var titleIndex = new Dictionary<string, Game>(StringComparer.OrdinalIgnoreCase);

        foreach (var g in allGames)
        {
            IndexGame(barcodeIndex, titleIndex, g);
        }

        int createdCount = 0;
        int updatedCount = 0;
        int linkedCount = 0;
        int importedCount = 0;
        int currentYear = DateTime.UtcNow.Year;

        foreach (var item in extractedItems)
        {
            ct.ThrowIfCancellationRequested();

            Game? matchedGame = null;
            bool isReprint = item.IsReprint;

            // 1. Cruce por EAN (si el item lo incluye)
            if (!string.IsNullOrWhiteSpace(item.Ean) && barcodeIndex.TryGetValue(item.Ean, out var gameByEan))
            {
                matchedGame = gameByEan;
            }

            var cleanedTitle = CleanCommercialTitle(item.Title);
            var baseTitle = ExtractBaseTitle(cleanedTitle);

            // 2. Cruce por Título normalizado si no se encontró por EAN
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
                }
            }

            // 3. Fallback BGG si el juego no está en catálogo local
            if (matchedGame == null && _bggClient != null)
            {
                try
                {
                    var queriesToTry = new List<string> { item.Title };
                    if (!string.IsNullOrWhiteSpace(cleanedTitle) &&
                        !cleanedTitle.Equals(item.Title, StringComparison.OrdinalIgnoreCase))
                    {
                        queriesToTry.Add(cleanedTitle);
                    }
                    if (!string.IsNullOrWhiteSpace(baseTitle) &&
                        !baseTitle.Equals(cleanedTitle, StringComparison.OrdinalIgnoreCase) &&
                        !baseTitle.Equals(item.Title, StringComparison.OrdinalIgnoreCase))
                    {
                        queriesToTry.Add(baseTitle);
                    }

                    BggSearchResultDto? bestMatch = null;
                    string matchedQuery = item.Title;

                    foreach (var query in queriesToTry)
                    {
                        var searchResults = await _bggClient.SearchGamesAsync(query, ct).ConfigureAwait(false);
                        if (searchResults.Count > 0)
                        {
                            bestMatch = FindBestMatch(query, searchResults);
                            if (bestMatch != null)
                            {
                                matchedQuery = query;
                                break;
                            }
                        }
                    }

                    if (bestMatch != null)
                    {
                        var bggGame = await _bggClient.FetchGameByBggIdAsync(bestMatch.BggId, ct).ConfigureAwait(false);
                        if (bggGame != null)
                        {
                            if (!string.IsNullOrWhiteSpace(item.Ean))
                            {
                                bggGame.UpdateEan(item.Ean);
                            }
                            bggGame.UpdateSpanishPublisher(item.Publisher);

                            await _gameRepository.AddRangeAsync(new[] { bggGame }, ct).ConfigureAwait(false);
                            IndexGame(barcodeIndex, titleIndex, bggGame);

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

            // REGLA DE NEGOCIO ESTRICTA: Toda novedad debe corresponder a un juego de mesa de BGG o del catálogo.
            // Si tras buscar en catálogo y BGG no se relaciona con ningún juego, se DESCARTA.
            if (matchedGame == null)
            {
                _logger.LogInformation("Descartando elemento '{Title}' ({Publisher}) por no estar relacionado con ningún juego de mesa de BGG ni catálogo local.", item.Title, item.Publisher);
                continue;
            }

            linkedCount++;

            if (matchedGame.YearPublished > 0 && matchedGame.YearPublished < currentYear)
            {
                isReprint = true;
            }

            // Enriquecer el juego si no tenía EAN o editorial en español
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

            if (gameModified)
            {
                await _gameRepository.UpdateAsync(matchedGame, ct).ConfigureAwait(false);
            }

            // Resolver precio estimado: prioridad al extraído; fallback al mejor precio de ofertas en catálogo
            decimal? finalEstimatedPvp = item.EstimatedPvp;
            if (finalEstimatedPvp == null && matchedGame.PurchaseLinks.Count > 0)
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
            var releaseKey = MakeReleaseKey(item.Title, item.Publisher);

            if (releaseIndex.TryGetValue(releaseKey, out var existingRelease))
            {
                existingRelease.Update(
                    title: existingRelease.Title,
                    publisher: existingRelease.Publisher,
                    releaseDate: item.ReleaseDate ?? existingRelease.ReleaseDate,
                    gameId: matchedGame.Id,
                    coverImageUrl: item.CoverImageUrl ?? existingRelease.CoverImageUrl,
                    estimatedPvp: finalEstimatedPvp ?? existingRelease.EstimatedPvp,
                    isReprint: existingRelease.IsReprint || isReprint,
                    notes: !string.IsNullOrWhiteSpace(item.Notes) ? item.Notes : existingRelease.Notes,
                    sourceUrl: item.SourceUrl ?? existingRelease.SourceUrl,
                    isMonthOnly: item.IsMonthOnly);

                await _weeklyReleaseRepository.UpdateAsync(existingRelease, ct).ConfigureAwait(false);
                updatedCount++;
            }
            else
            {
                var newRelease = new WeeklyRelease(
                    title: item.Title,
                    publisher: item.Publisher,
                    releaseDate: item.ReleaseDate,
                    gameId: matchedGame.Id,
                    coverImageUrl: item.CoverImageUrl,
                    estimatedPvp: finalEstimatedPvp,
                    isReprint: isReprint,
                    notes: item.Notes,
                    sourceUrl: item.SourceUrl,
                    isMonthOnly: item.IsMonthOnly);

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

    private static void IndexGame(Dictionary<string, Game> barcodeIndex, Dictionary<string, Game> titleIndex, Game g)
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
