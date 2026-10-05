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
/// o descriptores genéricos de caja ("Spanish edition"), restaurándolos a su título original o localizado.
/// </summary>
public static class CatalogDataSanitizer
{
    public static async Task SanitizeCorruptedSpanishTitlesAsync(LudekaDbContext db, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            // Detectar juegos cuyo SpanishTitle contiene "korean", "angry lion", o termina en edition/version
            var candidates = await db.Games
                .Where(g => g.SpanishTitle != null && (
                    g.SpanishTitle.ToLower().Contains("korean") ||
                    g.SpanishTitle.ToLower().Contains("angry lion") ||
                    g.SpanishTitle.ToLower().EndsWith(" edition") ||
                    g.SpanishTitle.ToLower().EndsWith(" edicion") ||
                    g.SpanishTitle.ToLower().EndsWith(" edición") ||
                    g.SpanishTitle.ToLower().EndsWith(" version") ||
                    g.SpanishTitle.ToLower().EndsWith(" versión")
                ))
                .ToListAsync(ct);

            if (candidates.Count == 0)
            {
                return;
            }

            var bggIds = candidates.Select(c => c.BggId).ToList();
            var snapshots = await db.BggRawSnapshots
                .Where(s => bggIds.Contains(s.BggId))
                .ToDictionaryAsync(s => s.BggId, ct);

            int repairedCount = 0;

            foreach (var game in candidates)
            {
                if (!BggRawSnapshotParser.IsGenericEditionTitle(game.SpanishTitle) &&
                    game.SpanishTitle.IndexOf("korean", StringComparison.OrdinalIgnoreCase) < 0 &&
                    game.SpanishTitle.IndexOf("angry lion", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                string? validSpanishTitle = null;
                string? validSpanishPublisher = null;
                string? validEan = null;

                if (snapshots.TryGetValue(game.BggId, out var snapshot))
                {
                    var vInfo = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(snapshot.RawJson);
                    if (vInfo != null)
                    {
                        validSpanishTitle = vInfo.Title;
                        validSpanishPublisher = vInfo.Publisher;
                        validEan = vInfo.Ean;
                    }
                }

                // Restaurar título: si no hay título español válido en la versión, usar el OriginalTitle
                string targetTitle = !string.IsNullOrWhiteSpace(validSpanishTitle)
                    ? validSpanishTitle
                    : game.OriginalTitle;

                if (game.SpanishTitle != targetTitle)
                {
                    logger.LogInformation("Saneando SpanishTitle para BggId {BggId}: '{OldTitle}' -> '{NewTitle}'",
                        game.BggId, game.SpanishTitle, targetTitle);
                    game.UpdateSpanishTitle(targetTitle);
                    repairedCount++;
                }

                // Saneamiento de editorial: asignar editorial española válida si está disponible y la actual es nula o coreana
                if (!string.IsNullOrWhiteSpace(validSpanishPublisher) &&
                    (string.IsNullOrWhiteSpace(game.SpanishPublisher) || IsKoreanPublisher(game.SpanishPublisher)))
                {
                    game.UpdateSpanishPublisher(validSpanishPublisher);
                }
                else if (IsKoreanPublisher(game.SpanishPublisher))
                {
                    game.UpdateSpanishPublisher(null);
                }

                // Saneamiento de EAN: asignar EAN español válido o limpiar EAN surcoreano (880...)
                if (!string.IsNullOrWhiteSpace(validEan) &&
                    (string.IsNullOrWhiteSpace(game.Ean) || (game.Ean != null && game.Ean.StartsWith("880"))))
                {
                    game.UpdateEan(validEan);
                }
                else if (game.Ean != null && game.Ean.StartsWith("880") && (validEan == null || validEan != game.Ean))
                {
                    game.UpdateEan(null);
                }
            }

            if (repairedCount > 0)
            {
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Saneamiento de catálogo completado: {Count} títulos reparados.", repairedCount);
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
