using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Features.Bgg;
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
    public static async Task SanitizeCorruptedSpanishTitlesAsync(LudekaDbContext db, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            int totalRepaired = 0;
            const int batchSize = 100;
            int maxBatches = 50; // Salvaguarda defensiva: máximo 5.000 juegos en una pasada

            while (!ct.IsCancellationRequested && maxBatches-- > 0)
            {
                // Detectar juegos que requieren saneamiento:
                // 1) Título español contaminado (diferente de OriginalTitle y con korean/angry lion/edition/version)
                // 2) Editorial coreana atribuida a España
                // 3) Código de barras coreano (880...)
                var candidates = await db.Games
                    .Where(g => (g.SpanishTitle != null && g.SpanishTitle != g.OriginalTitle && (
                                    g.SpanishTitle.ToLower().Contains("korean") ||
                                    g.SpanishTitle.ToLower().Contains("angry lion") ||
                                    g.SpanishTitle.ToLower().EndsWith(" edition") ||
                                    g.SpanishTitle.ToLower().EndsWith(" edicion") ||
                                    g.SpanishTitle.ToLower().EndsWith(" edición") ||
                                    g.SpanishTitle.ToLower().EndsWith(" version") ||
                                    g.SpanishTitle.ToLower().EndsWith(" versión")
                                )) ||
                                (g.SpanishPublisher != null && (
                                    g.SpanishPublisher.ToLower().Contains("angry lion") ||
                                    g.SpanishPublisher.ToLower().Contains("lotus frog") ||
                                    g.SpanishPublisher.ToLower().Contains("board m") ||
                                    g.SpanishPublisher.ToLower().Contains("popcorn games") ||
                                    g.SpanishPublisher.ToLower().Contains("mandoo games")
                                )) ||
                                (g.Ean != null && g.Ean.StartsWith("880"))
                    )
                    .Take(batchSize)
                    .ToListAsync(ct);

                if (candidates.Count == 0)
                {
                    break;
                }

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

                    if (snapshots.TryGetValue(game.BggId, out var rawJson))
                    {
                        var vInfo = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(rawJson);
                        if (vInfo != null)
                        {
                            validSpanishTitle = vInfo.Title;
                            validSpanishPublisher = vInfo.Publisher;
                            validEan = vInfo.Ean;
                        }
                    }

                    // Saneamiento de título si contiene coreano o descriptores genéricos de caja
                    bool hasCorruptedTitle = game.SpanishTitle != null && (
                        BggRawSnapshotParser.IsGenericEditionTitle(game.SpanishTitle) ||
                        game.SpanishTitle.IndexOf("korean", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        game.SpanishTitle.IndexOf("angry lion", StringComparison.OrdinalIgnoreCase) >= 0
                    );

                    if (hasCorruptedTitle)
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
                else
                {
                    // Si en este lote ninguna entidad requirió modificación, salimos para evitar bucle
                    break;
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
