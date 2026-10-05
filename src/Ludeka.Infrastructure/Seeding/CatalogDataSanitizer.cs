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
/// restaurándolos a su título original o localizado, y limpiando editoriales y códigos de barras coreanos.
/// Diseñado para ejecución ultraligera y segura en el arranque (O(1) memoria, batching defensivo).
/// </summary>
public static class CatalogDataSanitizer
{
    public static async Task SanitizeCorruptedSpanishTitlesAsync(LudekaDbContext db, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            // Detectar exclusivamente títulos contaminados por la anomalía de coreano ("korean", "angry lion")
            // Usamos Take(200) para garantizar que nunca sature la memoria heap en Cloud Run
            var candidates = await db.Games
                .Where(g => g.SpanishTitle != null && (
                    g.SpanishTitle.ToLower().Contains("korean") ||
                    g.SpanishTitle.ToLower().Contains("angry lion")
                ))
                .Take(200)
                .ToListAsync(ct);

            if (candidates.Count == 0)
            {
                return;
            }

            var bggIds = candidates.Select(c => c.BggId).Distinct().ToList();

            // Consultar snapshots sin tracking y en lotes acotados proyectando solo BggId y RawJson
            var snapshots = await db.BggRawSnapshots
                .AsNoTracking()
                .Where(s => bggIds.Contains(s.BggId))
                .Select(s => new { s.BggId, s.RawJson })
                .ToDictionaryAsync(s => s.BggId, s => s.RawJson, ct);

            int repairedCount = 0;

            foreach (var game in candidates)
            {
                if (game.SpanishTitle == null ||
                    (game.SpanishTitle.IndexOf("korean", StringComparison.OrdinalIgnoreCase) < 0 &&
                     game.SpanishTitle.IndexOf("angry lion", StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }

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
