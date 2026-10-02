using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ludeka.Infrastructure.Bgg;

/// <summary>
/// Orquestador para sincronización de payloads brutos en BggRawSnapshots,
/// auto-vinculación de expansiones y descubrimiento de expansiones no catalogadas.
/// </summary>
public class BggRawSnapshotSyncService : IBggRawSnapshotSyncService
{
    private const string DenialMessage =
        "Se requiere el permiso de moderación 'CanEditGames' para operar sobre los snapshots de BGG.";

    private readonly IBggRawSnapshotRepository _snapshotRepo;
    private readonly IBggClient _bggClient;
    private readonly IGameRepository _gameRepo;
    private readonly IPendingBggImportRepository _pendingRepo;
    private readonly IDbContextFactory<LudekaDbContext> _contextFactory;
    private readonly ILogger<BggRawSnapshotSyncService> _logger;
    private readonly ISessionPermissionGuard? _permissionGuard;

    public BggRawSnapshotSyncService(
        IBggRawSnapshotRepository snapshotRepo,
        IBggClient bggClient,
        IGameRepository gameRepo,
        IPendingBggImportRepository pendingRepo,
        IDbContextFactory<LudekaDbContext> contextFactory,
        ILogger<BggRawSnapshotSyncService> logger,
        ISessionPermissionGuard? permissionGuard = null)
    {
        _snapshotRepo = snapshotRepo ?? throw new ArgumentNullException(nameof(snapshotRepo));
        _bggClient = bggClient ?? throw new ArgumentNullException(nameof(bggClient));
        _gameRepo = gameRepo ?? throw new ArgumentNullException(nameof(gameRepo));
        _pendingRepo = pendingRepo ?? throw new ArgumentNullException(nameof(pendingRepo));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _permissionGuard = permissionGuard;
    }

    private Task RequirePermissionAsync(CancellationToken ct)
        => _permissionGuard is null
            ? Task.CompletedTask
            : _permissionGuard.RequireAsync(ModeratorPermission.CanEditGames, DenialMessage, ct);

    public async Task<BggRawSnapshotStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        int totalGamesWithBgg = await _snapshotRepo.GetTotalGamesWithBggIdCountAsync(ct);
        int totalSnapshots = await _snapshotRepo.GetCountAsync(ct);
        int pendingSnapshots = Math.Max(0, totalGamesWithBgg - totalSnapshots);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        int totalExpansions = await context.Games.CountAsync(g => g.Type == GameType.Expansion, ct);
        int linkedExpansions = await context.Games.CountAsync(g => g.Type == GameType.Expansion && g.BaseGameId != null, ct);
        int unlinkedExpansions = Math.Max(0, totalExpansions - linkedExpansions);

        return new BggRawSnapshotStatusDto(
            TotalGamesWithBggId: totalGamesWithBgg,
            TotalSnapshots: totalSnapshots,
            PendingSnapshots: pendingSnapshots,
            TotalExpansions: totalExpansions,
            LinkedExpansions: linkedExpansions,
            UnlinkedExpansions: unlinkedExpansions
        );
    }

    public async Task<BggRawSnapshotSyncResultDto> SyncBatchAsync(int batchSize = 20, int delayMs = 1200, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        return await SyncBatchCoreAsync(batchSize, delayMs, ct);
    }

    public Task<BggRawSnapshotSyncResultDto> RunScheduledSyncBatchAsync(int batchSize = 20, int delayMs = 1200, CancellationToken ct = default)
    {
        return SyncBatchCoreAsync(batchSize, delayMs, ct);
    }

    private async Task<BggRawSnapshotSyncResultDto> SyncBatchCoreAsync(int batchSize, int delayMs, CancellationToken ct)
    {
        var missingIds = await _snapshotRepo.GetMissingBggIdsAsync(batchSize, ct);
        if (missingIds.Count == 0)
        {
            return new BggRawSnapshotSyncResultDto(0, 0, 0, [], [], "Todos los juegos del catálogo ya disponen de snapshot satélite.");
        }

        int successCount = 0;
        int failedCount = 0;
        var syncedTitles = new List<string>();
        var linkedExpansions = new List<string>();

        // Particionar en bloques de hasta 20 identificadores para consultar a BGG en lote
        var chunks = missingIds.Chunk(20).ToList();

        for (int c = 0; c < chunks.Count; c++)
        {
            var chunk = chunks[c];

            try
            {
                var xml = await _bggClient.FetchRawThingsXmlAsync(chunk, ct);
                if (string.IsNullOrWhiteSpace(xml))
                {
                    _logger.LogWarning("BGG no devolvió XML válido para el bloque de {Count} títulos", chunk.Length);
                    failedCount += chunk.Length;
                    continue;
                }

                var doc = XDocument.Parse(xml);
                var items = doc.Root?.Elements("item")?.ToList() ?? new List<XElement>();
                var returnedIds = new HashSet<int>();

                foreach (var item in items)
                {
                    if (!int.TryParse(item.Attribute("id")?.Value, out int bggId) || bggId <= 0)
                    {
                        continue;
                    }

                    returnedIds.Add(bggId);

                    try
                    {
                        string rawJson = BggXmlToJsonConverter.ConvertToJson(item);
                        var snapshot = new BggRawSnapshot(bggId, rawJson, apiVersion: 2, fetchedAt: DateTimeOffset.UtcNow);
                        await _snapshotRepo.UpsertAsync(snapshot, ct);

                        string title = item.Elements("name").FirstOrDefault(n => n.Attribute("type")?.Value == "primary")?.Attribute("value")?.Value
                            ?? $"Juego #{bggId}";
                        syncedTitles.Add(title);
                        successCount++;

                        // Auto-vinculación defensiva de expansiones
                        bool isExpansion = string.Equals(item.Attribute("type")?.Value, "boardgameexpansion", StringComparison.OrdinalIgnoreCase);
                        if (isExpansion)
                        {
                            int? baseBggId = BggXmlParser.ExtractInboundBaseGameBggId(item);
                            if (baseBggId.HasValue)
                            {
                                var expGame = await _gameRepo.GetByBggIdAsync(bggId, ct);
                                var baseGame = await _gameRepo.GetByBggIdAsync(baseBggId.Value, ct);

                                if (expGame != null && baseGame != null && expGame.BaseGameId != baseGame.Id)
                                {
                                    expGame.SetBaseGameId(baseGame.Id);
                                    await _gameRepo.UpdateAsync(expGame, ct);
                                    linkedExpansions.Add($"{expGame.SpanishTitle} → {baseGame.SpanishTitle}");
                                }
                            }
                        }
                        else
                        {
                            var outboundExpIds = BggXmlParser.ExtractOutboundExpansionBggIds(item);
                            if (outboundExpIds.Count > 0)
                            {
                                var baseGame = await _gameRepo.GetByBggIdAsync(bggId, ct);
                                if (baseGame != null)
                                {
                                    var expGames = await _gameRepo.GetByBggIdsAsync(outboundExpIds, ct);
                                    foreach (var eg in expGames.Where(g => g.BaseGameId == null))
                                    {
                                        eg.SetBaseGameId(baseGame.Id);
                                        await _gameRepo.UpdateAsync(eg, ct);
                                        linkedExpansions.Add($"{eg.SpanishTitle} → {baseGame.SpanishTitle}");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al procesar snapshot para BggId {BggId}", bggId);
                        failedCount++;
                    }
                }

                // Identificar IDs solicitados que no fueron devueltos en el documento de BGG
                foreach (int requestedId in chunk)
                {
                    if (!returnedIds.Contains(requestedId))
                    {
                        _logger.LogWarning("BGG no devolvió elemento <item> para BggId {BggId} (posiblemente eliminado o retirado en BGG). Registrando snapshot de control.", requestedId);
                        var placeholderSnapshot = new BggRawSnapshot(requestedId, "{\"notFound\":true}", apiVersion: 2, fetchedAt: DateTimeOffset.UtcNow);
                        await _snapshotRepo.UpsertAsync(placeholderSnapshot, ct);
                        failedCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al sincronizar bloque de snapshots");
                failedCount += chunk.Length;
            }

            // Pausa de cortesía hacia los servidores de BGG entre bloques
            if (delayMs > 0 && c < chunks.Count - 1)
            {
                await Task.Delay(delayMs, ct);
            }
        }

        return new BggRawSnapshotSyncResultDto(
            ProcessedCount: missingIds.Count,
            SuccessCount: successCount,
            FailedCount: failedCount,
            SyncedTitles: syncedTitles,
            LinkedExpansions: linkedExpansions,
            Message: $"Lote completado: {successCount} snapshots guardados, {failedCount} incidencias."
        );
    }

    public async Task<BggExpansionDiscoveryResultDto> DiscoverAndEnqueueMissingExpansionsAsync(int maxToEnqueue = 50, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        return await DiscoverAndEnqueueMissingExpansionsCoreAsync(maxToEnqueue, ct);
    }

    public Task<BggExpansionDiscoveryResultDto> RunScheduledDiscoverAndEnqueueMissingExpansionsAsync(int maxToEnqueue = 50, CancellationToken ct = default)
    {
        return DiscoverAndEnqueueMissingExpansionsCoreAsync(maxToEnqueue, ct);
    }

    private async Task<BggExpansionDiscoveryResultDto> DiscoverAndEnqueueMissingExpansionsCoreAsync(int maxToEnqueue, CancellationToken ct)
    {
        var snapshots = await _snapshotRepo.GetAllSnapshotsAsync(500, ct);
        var candidates = new Dictionary<int, string>();

        foreach (var s in snapshots)
        {
            var outboundLinks = ExtractOutboundExpansionLinksFromJson(s.RawJson);
            foreach (var link in outboundLinks)
            {
                candidates.TryAdd(link.BggId, link.Title);
            }
        }

        if (candidates.Count == 0)
        {
            return new BggExpansionDiscoveryResultDto(0, 0, []);
        }

        // Descartar expansiones que ya figuren en el catálogo
        var existingGames = await _gameRepo.GetByBggIdsAsync(candidates.Keys, ct);
        var existingBggIds = existingGames.Select(g => g.BggId).ToHashSet();
        var missingCandidates = candidates.Where(kvp => !existingBggIds.Contains(kvp.Key)).ToList();

        // Descartar expansiones ya encoladas en PendingBggImports
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var alreadyPendingBggIds = await context.PendingBggImports
            .Where(p => p.Status == CatalogQueueStatus.Pending || p.Status == CatalogQueueStatus.Processing)
            .Select(p => p.BggId)
            .ToListAsync(ct);

        var pendingSet = alreadyPendingBggIds.ToHashSet();
        var toEnqueue = missingCandidates.Where(c => !pendingSet.Contains(c.Key)).Take(maxToEnqueue).ToList();

        var enqueuedTitles = new List<string>();
        foreach (var exp in toEnqueue)
        {
            var pendingItem = new PendingBggImport(
                bggId: exp.Key,
                title: exp.Value,
                yearPublished: null,
                thumbnailUrl: null,
                coverImageUrl: null,
                origin: CatalogQueueOrigin.BggExpansionDiscovery,
                extractedTitle: exp.Value
            );

            await _pendingRepo.AddAsync(pendingItem, ct);
            enqueuedTitles.Add(exp.Value);
        }

        return new BggExpansionDiscoveryResultDto(
            DiscoveredCount: missingCandidates.Count,
            EnqueuedCount: enqueuedTitles.Count,
            EnqueuedTitles: enqueuedTitles
        );
    }

    public async Task<int> AutoLinkExistingExpansionsAsync(CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        return await AutoLinkExistingExpansionsCoreAsync(ct);
    }

    public Task<int> RunScheduledAutoLinkExistingExpansionsAsync(CancellationToken ct = default)
    {
        return AutoLinkExistingExpansionsCoreAsync(ct);
    }

    private async Task<int> AutoLinkExistingExpansionsCoreAsync(CancellationToken ct)
    {
        var unlinkedExpansions = await _gameRepo.GetUnlinkedExpansionsAsync(ct);
        if (unlinkedExpansions.Count == 0) return 0;

        int linkedCount = 0;

        foreach (var exp in unlinkedExpansions)
        {
            var snapshot = await _snapshotRepo.GetByBggIdAsync(exp.BggId, ct);
            if (snapshot == null) continue;

            int? baseBggId = ExtractInboundBaseGameBggIdFromJson(snapshot.RawJson);
            if (!baseBggId.HasValue) continue;

            var baseGame = await _gameRepo.GetByBggIdAsync(baseBggId.Value, ct);
            if (baseGame != null)
            {
                exp.SetBaseGameId(baseGame.Id);
                await _gameRepo.UpdateAsync(exp, ct);
                linkedCount++;
            }
        }

        return linkedCount;
    }

    public static int? ExtractInboundBaseGameBggIdFromJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (!doc.RootElement.TryGetProperty("link", out var linkProp)) return null;

            if (linkProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in linkProp.EnumerateArray())
                {
                    if (CheckInboundExpansionLink(item, out int baseId))
                        return baseId;
                }
            }
            else if (linkProp.ValueKind == JsonValueKind.Object)
            {
                if (CheckInboundExpansionLink(linkProp, out int baseId))
                    return baseId;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static bool CheckInboundExpansionLink(JsonElement elem, out int baseId)
    {
        baseId = 0;
        if (elem.TryGetProperty("@type", out var typeProp) &&
            typeProp.GetString() == "boardgameexpansion" &&
            elem.TryGetProperty("@inbound", out var inProp) &&
            inProp.GetString() == "true" &&
            elem.TryGetProperty("@id", out var idProp) &&
            int.TryParse(idProp.GetString(), out int parsedId) &&
            parsedId > 0)
        {
            baseId = parsedId;
            return true;
        }
        return false;
    }

    public static List<(int BggId, string Title)> ExtractOutboundExpansionLinksFromJson(string rawJson)
    {
        var results = new List<(int, string)>();
        if (string.IsNullOrWhiteSpace(rawJson)) return results;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (!doc.RootElement.TryGetProperty("link", out var linkProp)) return results;

            if (linkProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in linkProp.EnumerateArray())
                {
                    if (CheckOutboundExpansionLink(item, out int expId, out string title))
                        results.Add((expId, title));
                }
            }
            else if (linkProp.ValueKind == JsonValueKind.Object)
            {
                if (CheckOutboundExpansionLink(linkProp, out int expId, out string title))
                    results.Add((expId, title));
            }
        }
        catch
        {
            // Salida silenciosa si el JSON es atípico
        }

        return results;
    }

    private static bool CheckOutboundExpansionLink(JsonElement elem, out int expId, out string title)
    {
        expId = 0;
        title = string.Empty;
        if (elem.TryGetProperty("@type", out var typeProp) &&
            typeProp.GetString() == "boardgameexpansion")
        {
            bool isInbound = elem.TryGetProperty("@inbound", out var inProp) && inProp.GetString() == "true";
            if (!isInbound &&
                elem.TryGetProperty("@id", out var idProp) &&
                int.TryParse(idProp.GetString(), out int parsedId) &&
                parsedId > 0)
            {
                expId = parsedId;
                title = elem.TryGetProperty("@value", out var valProp) ? valProp.GetString() ?? $"Expansión #{expId}" : $"Expansión #{expId}";
                return true;
            }
        }
        return false;
    }

    /// <inheritdoc />
    public async Task<bool> EnsureSnapshotAsync(int bggId, CancellationToken ct = default)
    {
        if (bggId <= 0) return false;

        var existing = await _snapshotRepo.GetByBggIdAsync(bggId, ct);
        if (existing != null) return true;

        try
        {
            var xml = await _bggClient.FetchRawThingXmlAsync(bggId, ct);
            if (string.IsNullOrWhiteSpace(xml)) return false;

            var doc = XDocument.Parse(xml);
            var item = doc.Root?.Element("item");
            if (item == null) return false;

            string rawJson = BggXmlToJsonConverter.ConvertToJson(item);
            var snapshot = new BggRawSnapshot(bggId, rawJson, apiVersion: 2, fetchedAt: DateTimeOffset.UtcNow);
            await _snapshotRepo.UpsertAsync(snapshot, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al asegurar snapshot para BggId {BggId}: {Message}", bggId, ex.Message);
            return false;
        }
    }
}
