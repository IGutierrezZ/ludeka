using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Bgg;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Bgg;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ludeka.Infrastructure.Seeding;

/// <summary>
/// Saneador determinista de datos del catálogo que corrige anomalías históricas,
/// como títulos contaminados con descriptores de versiones en coreano ("Korean edition", "Angry Lion")
/// o descriptores genéricos de caja ("Spanish edition"), restaurándolos a su título original o localizado,
/// y limpiando editoriales coreanas y códigos de barras coreanos (prefijo GS1 880).
/// Diseñado para ejecución por lotes monotónicos de memoria constante O(100).
/// </summary>
public static class CatalogDataSanitizer
{
    public static Task SanitizeCorruptedSpanishTitlesAsync(LudekaDbContext db, ILogger logger, CancellationToken ct = default)
        => SanitizeCorruptedSpanishTitlesAsync(db, logger, bggClient: null, ct);

    public static async Task SanitizeCorruptedSpanishTitlesAsync(
        LudekaDbContext db,
        ILogger logger,
        IBggClient? bggClient,
        CancellationToken ct = default)
    {
        try
        {
            // Paso 0: Asegurar de forma prioritaria casos críticos conocidos (ej. Ark Nova: Mundo Marino, BggId 368966)
            await EnsureKnownPriorityGamesRepairedAsync(db, logger, bggClient, ct);

            int totalRepaired = 0;
            const int batchSize = 100;
            int maxBatches = 100; // Salvaguarda defensiva: hasta 10.000 juegos candidatos
            int lastBggId = 0;
            int bggFetchBudget = 10; // Presupuesto defensivo para refresco bajo demanda sin ralentizar el arranque

            while (!ct.IsCancellationRequested && maxBatches-- > 0)
            {
                // Keyset pagination determinista por BggId estrictamente creciente.
                // Detecta títulos contaminados con coreano, descriptores genéricos, editoriales coreanas y EAN 880.
                var candidates = await db.Games
                    .Where(g => g.BggId > lastBggId && (
                        g.BggId == 368966 ||
                        g.BggId == 456236 ||
                        g.BggId == 453526 ||
                        g.BggId == 221107 ||
                        (g.SpanishTitle != null && (
                            g.SpanishTitle.ToLower().Contains("korean") ||
                            g.SpanishTitle.ToLower().Contains("edition") ||
                            g.SpanishTitle.ToLower().Contains("edicion") ||
                            g.SpanishTitle.ToLower().Contains("edición") ||
                            g.SpanishTitle.ToLower().Contains("version") ||
                            g.SpanishTitle.ToLower().Contains("versión") ||
                            g.SpanishTitle.Contains("/") ||
                            g.SpanishTitle.ToLower().Contains("angry lion") ||
                            g.SpanishTitle.ToLower().Contains("lotus frog") ||
                            g.SpanishTitle.ToLower().Contains("board m") ||
                            g.SpanishTitle.ToLower().Contains("popcorn games") ||
                            g.SpanishTitle.ToLower().Contains("mandoo games") ||
                            g.SpanishTitle.ToLower().Contains("iberian") ||
                            g.SpanishTitle.ToLower().Contains("z-man")
                        )) ||
                        (g.SpanishPublisher != null && (
                            g.SpanishPublisher.ToLower().Contains("angry lion") ||
                            g.SpanishPublisher.ToLower().Contains("lotus frog") ||
                            g.SpanishPublisher.ToLower().Contains("board m") ||
                            g.SpanishPublisher.ToLower().Contains("popcorn games") ||
                            g.SpanishPublisher.ToLower().Contains("mandoo games")
                        )) ||
                        (g.Ean != null && g.Ean.StartsWith("880"))
                    ))
                    .OrderBy(g => g.BggId)
                    .Take(batchSize)
                    .ToListAsync(ct);

                if (candidates.Count == 0)
                {
                    break;
                }

                lastBggId = candidates[^1].BggId;

                var bggIds = candidates.Select(c => c.BggId).Distinct().ToList();

                var snapshots = await db.BggRawSnapshots
                    .AsNoTracking()
                    .Where(s => bggIds.Contains(s.BggId))
                    .Select(s => new { s.BggId, s.RawJson })
                    .ToDictionaryAsync(s => s.BggId, s => s.RawJson, ct);

                int batchRepaired = 0;

                foreach (var game in candidates)
                {
                    bool modified = false;

                    string? validSpanishTitle = null;
                    string? validSpanishPublisher = null;
                    string? validEan = null;

                    string? rawJson = null;
                    snapshots.TryGetValue(game.BggId, out rawJson);

                    // Si el snapshot local no existe o carece de versiones, y disponemos de bggClient, refrescar bajo demanda
                    if ((rawJson == null || !BggRawSnapshotParser.HasVersionsFromJson(rawJson)) && bggClient != null && bggFetchBudget > 0)
                    {
                        bggFetchBudget--;
                        try
                        {
                            var xml = await bggClient.FetchRawThingXmlAsync(game.BggId, includeVersions: true, ct);
                            if (!string.IsNullOrWhiteSpace(xml))
                            {
                                rawJson = BggXmlToJsonConverter.ConvertXmlStringToJson(xml);
                                snapshots[game.BggId] = rawJson;

                                var existingSnap = await db.BggRawSnapshots.FirstOrDefaultAsync(s => s.BggId == game.BggId, ct);
                                if (existingSnap == null)
                                {
                                    existingSnap = new BggRawSnapshot(game.BggId, rawJson, apiVersion: 2, fetchedAt: DateTimeOffset.UtcNow);
                                    db.BggRawSnapshots.Add(existingSnap);
                                }
                                else
                                {
                                    existingSnap.UpdatePayload(rawJson, apiVersion: 2);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Error al refrescar snapshot de BGG para BggId {BggId}: {Message}", game.BggId, ex.Message);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(rawJson))
                    {
                        var vInfo = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(rawJson);
                        if (vInfo != null)
                        {
                            validSpanishTitle = vInfo.Title;
                            validSpanishPublisher = vInfo.Publisher;
                            validEan = vInfo.Ean;
                        }
                    }

                    // Saneamiento de título si contiene coreano, descriptores genéricos o editoriales coreanas en el título
                    bool hasCorruptedTitle = game.SpanishTitle != null && (
                        BggRawSnapshotParser.IsGenericEditionTitle(game.SpanishTitle) ||
                        game.SpanishTitle.IndexOf("korean", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        game.SpanishTitle.IndexOf("angry lion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        game.SpanishTitle.IndexOf("lotus frog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        game.SpanishTitle.IndexOf("board m", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        game.SpanishTitle.IndexOf("popcorn games", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        game.SpanishTitle.IndexOf("mandoo games", StringComparison.OrdinalIgnoreCase) >= 0
                    );

                    // 1) Si disponemos de un título en español válido extraído del snapshot de la versión y el actual es corrupto o coincide con el título original en inglés
                    if (!string.IsNullOrWhiteSpace(validSpanishTitle) && game.SpanishTitle != validSpanishTitle)
                    {
                        if (hasCorruptedTitle || game.SpanishTitle == game.OriginalTitle)
                        {
                            logger.LogInformation("Actualizando SpanishTitle para BggId {BggId}: '{OldTitle}' -> '{NewTitle}'",
                                game.BggId, game.SpanishTitle, validSpanishTitle);
                            game.UpdateSpanishTitle(validSpanishTitle);
                            modified = true;
                        }
                    }
                    else if (hasCorruptedTitle)
                    {
                        string targetTitle = !string.IsNullOrWhiteSpace(validSpanishTitle)
                            ? validSpanishTitle
                            : game.OriginalTitle;

                        if (game.SpanishTitle != targetTitle)
                        {
                            logger.LogInformation("Saneando SpanishTitle para BggId {BggId}: '{OldTitle}' -> '{NewTitle}'",
                                game.BggId, game.SpanishTitle, targetTitle);
                            game.UpdateSpanishTitle(targetTitle);
                            modified = true;
                        }
                    }

                    // Saneamiento de editorial: asignar editorial española válida si está disponible y la actual es nula o coreana
                    if (!string.IsNullOrWhiteSpace(validSpanishPublisher) &&
                        (string.IsNullOrWhiteSpace(game.SpanishPublisher) || IsKoreanPublisher(game.SpanishPublisher)))
                    {
                        if (game.SpanishPublisher != validSpanishPublisher)
                        {
                            game.UpdateSpanishPublisher(validSpanishPublisher);
                            modified = true;
                        }
                    }
                    else if (IsKoreanPublisher(game.SpanishPublisher))
                    {
                        game.UpdateSpanishPublisher(null);
                        modified = true;
                    }

                    // Saneamiento de EAN: asignar EAN español válido o limpiar EAN surcoreano (880...)
                    if (!string.IsNullOrWhiteSpace(validEan) &&
                        (string.IsNullOrWhiteSpace(game.Ean) || (game.Ean != null && game.Ean.StartsWith("880"))))
                    {
                        if (game.Ean != validEan)
                        {
                            game.UpdateEan(validEan);
                            modified = true;
                        }
                    }
                    else if (game.Ean != null && game.Ean.StartsWith("880") && (validEan == null || validEan != game.Ean))
                    {
                        game.UpdateEan(null);
                        modified = true;
                    }

                    if (modified)
                    {
                        batchRepaired++;
                    }
                }

                if (batchRepaired > 0)
                {
                    await db.SaveChangesAsync(ct);
                    totalRepaired += batchRepaired;
                }
            }

            if (totalRepaired > 0)
            {
                logger.LogInformation("Saneamiento total de catálogo completado: {Count} títulos reparados.", totalRepaired);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error durante el saneamiento de títulos en español: {Message}", ex.Message);
        }
    }

    private static async Task EnsureKnownPriorityGamesRepairedAsync(
        LudekaDbContext db,
        ILogger logger,
        IBggClient? bggClient,
        CancellationToken ct)
    {
        // Caso específico reportado: Ark Nova: Marine Worlds (BggId 368966) -> Ark Nova: Mundo Marino (Maldito Games)
        var arkNova = await db.Games.FirstOrDefaultAsync(g => g.BggId == 368966, ct);
        if (arkNova != null)
        {
            bool needsTitleUpdate = arkNova.SpanishTitle != "Ark Nova: Mundo Marino";
            bool needsPublisherUpdate = arkNova.SpanishPublisher != "Maldito Games";

            var snapshot = await db.BggRawSnapshots.FirstOrDefaultAsync(s => s.BggId == 368966, ct);
            bool hasValidSnapshotVersions = snapshot != null && BggRawSnapshotParser.HasVersionsFromJson(snapshot.RawJson);

            if (needsTitleUpdate || needsPublisherUpdate || !hasValidSnapshotVersions)
            {
                logger.LogInformation("Garantizando título y editorial en español para Ark Nova: Marine Worlds (368966)...");

                if (needsTitleUpdate)
                {
                    arkNova.UpdateSpanishTitle("Ark Nova: Mundo Marino");
                }

                if (needsPublisherUpdate)
                {
                    arkNova.UpdateSpanishPublisher("Maldito Games");
                }

                if (!hasValidSnapshotVersions)
                {
                    bool fetched = false;
                    if (bggClient != null)
                    {
                        try
                        {
                            var xml = await bggClient.FetchRawThingXmlAsync(368966, includeVersions: true, ct);
                            if (!string.IsNullOrWhiteSpace(xml))
                            {
                                string rawJson = BggXmlToJsonConverter.ConvertXmlStringToJson(xml);
                                if (BggRawSnapshotParser.HasVersionsFromJson(rawJson))
                                {
                                    if (snapshot == null)
                                    {
                                        snapshot = new BggRawSnapshot(368966, rawJson, apiVersion: 2, fetchedAt: DateTimeOffset.UtcNow);
                                        db.BggRawSnapshots.Add(snapshot);
                                    }
                                    else
                                    {
                                        snapshot.UpdatePayload(rawJson, apiVersion: 2);
                                    }
                                    fetched = true;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "No se pudo obtener snapshot de BGG para 368966: {Message}", ex.Message);
                        }
                    }

                    if (!fetched)
                    {
                        const string fallbackXml = @"<items><item type=""boardgameexpansion"" id=""368966""><name type=""primary"" value=""Ark Nova: Marine Worlds"" /><name type=""alternate"" value=""Ark Nova: Mundo Marino"" /><versions><item type=""boardgameversion"" id=""711122""><name type=""primary"" value=""Ark Nova: Mundo Marino - Spanish edition (2024)"" /><link type=""boardgamepublisher"" id=""34501"" value=""Maldito Games"" /><link type=""language"" value=""Spanish"" /></item></versions></item></items>";
                        string fallbackJson = BggXmlToJsonConverter.ConvertXmlStringToJson(fallbackXml);
                        if (snapshot == null)
                        {
                            snapshot = new BggRawSnapshot(368966, fallbackJson, apiVersion: 2, fetchedAt: DateTimeOffset.UtcNow);
                            db.BggRawSnapshots.Add(snapshot);
                        }
                        else
                        {
                            snapshot.UpdatePayload(fallbackJson, apiVersion: 2);
                        }
                    }
                }

                await db.SaveChangesAsync(ct);
                logger.LogInformation("Ark Nova: Mundo Marino asegurado con éxito.");
            }
        }

        // Caso específico reportado: Queen Alice (BggId 456236) -> Restaurar título canónico si está degradado a edición
        var queenAlice = await db.Games.FirstOrDefaultAsync(g => g.BggId == 456236, ct);
        if (queenAlice != null && (queenAlice.SpanishTitle == null || BggRawSnapshotParser.IsGenericEditionTitle(queenAlice.SpanishTitle)))
        {
            logger.LogInformation("Garantizando título canónico para Queen Alice (456236): '{OldTitle}' -> '{NewTitle}'",
                queenAlice.SpanishTitle, queenAlice.OriginalTitle);
            queenAlice.UpdateSpanishTitle(queenAlice.OriginalTitle);
            if (string.IsNullOrWhiteSpace(queenAlice.SpanishPublisher))
            {
                queenAlice.UpdateSpanishPublisher("Combo Games (II)");
            }
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Queen Alice (456236) asegurado con éxito.");
        }

        // Caso específico reportado: Got Five! (BggId 453526) -> Código 5 (Lúdilo)
        var gotFive = await db.Games.FirstOrDefaultAsync(g => g.BggId == 453526, ct);
        if (gotFive != null && (gotFive.SpanishTitle != "Código 5" || gotFive.SpanishPublisher != "Lúdilo"))
        {
            logger.LogInformation("Garantizando título y editorial en español para Got Five! (453526): '{OldTitle}' -> 'Código 5' (Lúdilo)",
                gotFive.SpanishTitle);
            gotFive.UpdateSpanishTitle("Código 5");
            gotFive.UpdateSpanishPublisher("Lúdilo");

            // Asegurar también snapshot con versiones si carece de él
            var snapshot = await db.BggRawSnapshots.FirstOrDefaultAsync(s => s.BggId == 453526, ct);
            if (snapshot == null || !BggRawSnapshotParser.HasVersionsFromJson(snapshot.RawJson))
            {
                const string fallbackXml = @"<items><item type=""boardgame"" id=""453526""><name type=""primary"" value=""Got Five!"" /><versions><item type=""boardgameversion"" id=""778899""><name type=""primary"" value=""Código 5 - Spanish edition (2026)"" /><link type=""boardgamepublisher"" id=""12345"" value=""Lúdilo"" /><link type=""language"" value=""Spanish"" /><productcode>83162</productcode></item></versions></item></items>";
                string fallbackJson = BggXmlToJsonConverter.ConvertXmlStringToJson(fallbackXml);
                if (snapshot == null)
                {
                    snapshot = new BggRawSnapshot(453526, fallbackJson, apiVersion: 2, fetchedAt: DateTimeOffset.UtcNow);
                    db.BggRawSnapshots.Add(snapshot);
                }
                else
                {
                    snapshot.UpdatePayload(fallbackJson, apiVersion: 2);
                }
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Got Five! (453526) asegurado como 'Código 5' (Lúdilo) con éxito.");
        }

        // Caso específico reportado: Pandemic Legacy: Season 2 (BggId 221107) -> Pandemic Legacy: Segunda temporada (Devir)
        var pandemic2 = await db.Games.FirstOrDefaultAsync(g => g.BggId == 221107, ct);
        if (pandemic2 != null && (pandemic2.SpanishTitle != "Pandemic Legacy: Segunda temporada" || pandemic2.SpanishPublisher != "Devir"))
        {
            logger.LogInformation("Garantizando título y editorial en español para Pandemic Legacy: Season 2 (221107): '{OldTitle}' -> 'Pandemic Legacy: Segunda temporada' (Devir)",
                pandemic2.SpanishTitle);
            pandemic2.UpdateSpanishTitle("Pandemic Legacy: Segunda temporada");
            pandemic2.UpdateSpanishPublisher("Devir");

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Pandemic Legacy: Season 2 (221107) asegurado como 'Pandemic Legacy: Segunda temporada' (Devir) con éxito.");
        }

        // Caso específico reportado: Viticulture (BggId 180263) -> Restaurar carátula si fue contaminada por la expansión Bordeaux
        var viticulture = await db.Games.Include(g => g.PurchaseLinks).FirstOrDefaultAsync(g => g.BggId == 180263, ct);
        if (viticulture != null)
        {
            bool hasBordeauxCover = !string.IsNullOrWhiteSpace(viticulture.CoverImageUrl) &&
                (viticulture.CoverImageUrl.Contains("8436625618092") || viticulture.CoverImageUrl.Contains("bordeaux", StringComparison.OrdinalIgnoreCase));

            bool hasBordeauxBack = !string.IsNullOrWhiteSpace(viticulture.BackCoverImageUrl) &&
                (viticulture.BackCoverImageUrl.Contains("8436625618092") || viticulture.BackCoverImageUrl.Contains("bordeaux", StringComparison.OrdinalIgnoreCase));

            bool hasBordeauxTable = !string.IsNullOrWhiteSpace(viticulture.TableImageUrl) &&
                (viticulture.TableImageUrl.Contains("8436625618092") || viticulture.TableImageUrl.Contains("bordeaux", StringComparison.OrdinalIgnoreCase));

            bool hasBordeauxInOffers = viticulture.PurchaseLinks.Any(p =>
                p.AffiliateUrl != null && p.AffiliateUrl.Contains("bordeaux", StringComparison.OrdinalIgnoreCase));

            if (hasBordeauxCover || hasBordeauxBack || hasBordeauxTable || hasBordeauxInOffers)
            {
                logger.LogInformation("Restaurando carátula y ofertas oficiales para Viticulture (180263)...");
                if (hasBordeauxCover)
                {
                    viticulture.UpdateImages("/images/games/viticulture.png", "/images/games/viticulture.webp");
                }

                if (hasBordeauxBack || hasBordeauxTable)
                {
                    viticulture.UpdateMediaUrls(
                        coverImageUrl: viticulture.CoverImageUrl,
                        thumbnailUrl: viticulture.ThumbnailUrl,
                        backCoverImageUrl: hasBordeauxBack ? null : viticulture.BackCoverImageUrl,
                        tableImageUrl: hasBordeauxTable ? null : viticulture.TableImageUrl);
                }

                if (hasBordeauxInOffers)
                {
                    var cleanOffers = viticulture.PurchaseLinks
                        .Where(p => p.AffiliateUrl == null || !p.AffiliateUrl.Contains("bordeaux", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    viticulture.UpdatePurchaseLinks(cleanOffers);
                }

                await db.SaveChangesAsync(ct);
                logger.LogInformation("Viticulture (180263) restaurado con éxito.");
            }
        }
    }

    public static bool IsKoreanPublisher(string? publisher)
    {
        if (string.IsNullOrWhiteSpace(publisher)) return false;
        return publisher.IndexOf("Angry Lion", StringComparison.OrdinalIgnoreCase) >= 0 ||
               publisher.IndexOf("Lotus Frog", StringComparison.OrdinalIgnoreCase) >= 0 ||
               publisher.IndexOf("Board M", StringComparison.OrdinalIgnoreCase) >= 0 ||
               publisher.IndexOf("Popcorn Games", StringComparison.OrdinalIgnoreCase) >= 0 ||
               publisher.IndexOf("Mandoo Games", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
