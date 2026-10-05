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
using Ludeka.Application.Features.Bgg;
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

        int snapshotsWithVersions = await _snapshotRepo.GetCountWithVersionsAsync(ct);
        int snapshotsPendingVersions = Math.Max(0, totalSnapshots - snapshotsWithVersions);

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
            UnlinkedExpansions: unlinkedExpansions,
            SnapshotsWithVersions: snapshotsWithVersions,
            SnapshotsPendingVersions: snapshotsPendingVersions
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
                var xml = await _bggClient.FetchRawThingsXmlAsync(chunk, includeVersions: true, ct);
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
        var snapshots = await _snapshotRepo.GetAllSnapshotsAsync(2000, ct);
        var candidates = new Dictionary<int, string>();

        foreach (var s in snapshots)
        {
            var outboundLinks = ExtractOutboundExpansionLinksFromJson(s.RawJson);
            foreach (var link in outboundLinks)
            {
                // Pre-filtro léxico para descartar promos, packs promocionales y accesorios
                if (BggRawSnapshotParser.IsProbablePromoOrAccessory(link.Title)) continue;

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
        var unqueuedCandidates = missingCandidates.Where(c => !pendingSet.Contains(c.Key)).ToList();

        // Filtrado por tracción comunitaria o edición en español en bloques de 20
        var toEnqueue = new List<KeyValuePair<int, string>>();
        var candidateChunks = unqueuedCandidates
            .Select((item, index) => new { item, index })
            .GroupBy(x => x.index / 20)
            .Select(g => g.Select(x => x.item).ToList())
            .ToList();

        foreach (var chunk in candidateChunks)
        {
            if (toEnqueue.Count >= maxToEnqueue) break;

            var chunkBggIds = chunk.Select(c => c.Key).ToList();
            var localSnapshots = new Dictionary<int, BggRawSnapshot>();
            var idsNeedingFetch = new List<int>();

            foreach (var bggId in chunkBggIds)
            {
                var localSnap = await _snapshotRepo.GetByBggIdAsync(bggId, ct);
                if (localSnap != null)
                {
                    localSnapshots[bggId] = localSnap;
                }
                else
                {
                    idsNeedingFetch.Add(bggId);
                }
            }

            if (idsNeedingFetch.Count > 0)
            {
                try
                {
                    string? xml = await _bggClient.FetchRawThingsXmlAsync(idsNeedingFetch, includeVersions: true, ct);
                    if (!string.IsNullOrWhiteSpace(xml))
                    {
                        var doc = XDocument.Parse(xml);
                        var items = doc.Root?.Elements("item")?.ToList() ?? new List<XElement>();
                        foreach (var item in items)
                        {
                            if (int.TryParse(item.Attribute("id")?.Value, out int itemBggId) && itemBggId > 0)
                            {
                                string rawJson = BggXmlToJsonConverter.ConvertToJson(item);
                                var newSnapshot = new BggRawSnapshot(itemBggId, rawJson, apiVersion: 2, fetchedAt: DateTimeOffset.UtcNow);
                                await _snapshotRepo.UpsertAsync(newSnapshot, ct);
                                localSnapshots[itemBggId] = newSnapshot;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al consultar BGG para validar tracción de expansiones en lote: {Message}", ex.Message);
                }
            }

            foreach (var candidate in chunk)
            {
                if (toEnqueue.Count >= maxToEnqueue) break;

                if (localSnapshots.TryGetValue(candidate.Key, out var snapshot))
                {
                    if (BggRawSnapshotParser.MeetsExpansionCommunityThresholdFromJson(snapshot.RawJson))
                    {
                        toEnqueue.Add(candidate);
                    }
                }
                else
                {
                    toEnqueue.Add(candidate);
                }
            }
        }

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

    public async Task<BggExpansionReconciliationResultDto> ReconcileAndLinkExpansionsFromSnapshotsAsync(
        int batchSize = 200,
        CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        return await ReconcileAndLinkExpansionsFromSnapshotsCoreAsync(batchSize, ct);
    }

    public Task<BggExpansionReconciliationResultDto> RunScheduledReconcileAndLinkExpansionsFromSnapshotsAsync(
        int batchSize = 200,
        CancellationToken ct = default)
    {
        return ReconcileAndLinkExpansionsFromSnapshotsCoreAsync(batchSize, ct);
    }

    private async Task<BggExpansionReconciliationResultDto> ReconcileAndLinkExpansionsFromSnapshotsCoreAsync(
        int batchSize,
        CancellationToken ct)
    {
        if (batchSize <= 0) batchSize = 200;

        int totalEvaluated = 0;
        int reclassifiedCount = 0;
        int linkedCount = 0;
        var reclassifiedTitles = new List<string>();
        var linkedTitles = new List<string>();

        int lastBggId = 0;

        while (!ct.IsCancellationRequested)
        {
            var snapshots = await _snapshotRepo.GetSnapshotsAfterBggIdAsync(lastBggId, batchSize, ct);
            if (snapshots.Count == 0) break;

            lastBggId = snapshots[^1].BggId;
            totalEvaluated += snapshots.Count;

            var neededBggIds = new HashSet<int>();
            foreach (var s in snapshots)
            {
                neededBggIds.Add(s.BggId);

                int? inboundBase = ExtractInboundBaseGameBggIdFromJson(s.RawJson);
                if (inboundBase.HasValue && inboundBase.Value > 0)
                {
                    neededBggIds.Add(inboundBase.Value);
                }

                var outbound = ExtractOutboundExpansionLinksFromJson(s.RawJson);
                foreach (var link in outbound)
                {
                    if (link.BggId > 0) neededBggIds.Add(link.BggId);
                }
            }

            var games = await _gameRepo.GetByBggIdsAsync(neededBggIds, ct);
            var gamesMap = games.ToDictionary(g => g.BggId);
            var gamesToUpdate = new HashSet<Game>();

            foreach (var s in snapshots)
            {
                bool isExpansion = IsExpansionTypeFromJson(s.RawJson);
                int? inboundBase = ExtractInboundBaseGameBggIdFromJson(s.RawJson);

                if (isExpansion || inboundBase.HasValue)
                {
                    if (gamesMap.TryGetValue(s.BggId, out var expGame))
                    {
                        bool mutated = false;
                        if (expGame.Type != GameType.Expansion)
                        {
                            expGame.SetGameType(GameType.Expansion);
                            reclassifiedCount++;
                            reclassifiedTitles.Add(expGame.SpanishTitle);
                            mutated = true;
                        }

                        if (inboundBase.HasValue && gamesMap.TryGetValue(inboundBase.Value, out var baseGame))
                        {
                            if (expGame.BaseGameId != baseGame.Id)
                            {
                                expGame.SetBaseGameId(baseGame.Id);
                                linkedCount++;
                                linkedTitles.Add($"{expGame.SpanishTitle} → {baseGame.SpanishTitle}");
                                mutated = true;
                            }
                        }

                        if (mutated)
                        {
                            gamesToUpdate.Add(expGame);
                        }
                    }
                }

                var outboundLinks = ExtractOutboundExpansionLinksFromJson(s.RawJson);
                if (outboundLinks.Count > 0 && gamesMap.TryGetValue(s.BggId, out var parentGame))
                {
                    foreach (var link in outboundLinks)
                    {
                        if (gamesMap.TryGetValue(link.BggId, out var childGame))
                        {
                            bool childMutated = false;

                            if (childGame.Type != GameType.Expansion)
                            {
                                childGame.SetGameType(GameType.Expansion);
                                reclassifiedCount++;
                                reclassifiedTitles.Add(childGame.SpanishTitle);
                                childMutated = true;
                            }

                            if (childGame.BaseGameId != parentGame.Id)
                            {
                                childGame.SetBaseGameId(parentGame.Id);
                                linkedCount++;
                                linkedTitles.Add($"{childGame.SpanishTitle} → {parentGame.SpanishTitle}");
                                childMutated = true;
                            }

                            if (childMutated)
                            {
                                gamesToUpdate.Add(childGame);
                            }
                        }
                    }
                }
            }

            foreach (var g in gamesToUpdate)
            {
                try
                {
                    await _gameRepo.UpdateAsync(g, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al actualizar juego ID {GameId} ({SpanishTitle}) durante la reconciliación de expansiones. Omitiendo este juego para continuar el proceso.", g.Id, g.SpanishTitle);
                }
            }
        }

        return new BggExpansionReconciliationResultDto(
            TotalEvaluated: totalEvaluated,
            ReclassifiedExpansionsCount: reclassifiedCount,
            LinkedExpansionsCount: linkedCount,
            ReclassifiedTitles: reclassifiedTitles,
            LinkedExpansions: linkedTitles,
            Message: $"Reconciliación completada: {totalEvaluated} snapshots evaluados, {reclassifiedCount} expansiones reclasificadas, {linkedCount} vinculadas a su juego base."
        );
    }


    public static bool IsExpansionTypeFromJson(string rawJson)
        => BggRawSnapshotParser.IsExpansionTypeFromJson(rawJson);

    public static int? ExtractInboundBaseGameBggIdFromJson(string rawJson)
        => BggRawSnapshotParser.ExtractInboundBaseGameBggIdFromJson(rawJson);

    public static List<(int BggId, string Title)> ExtractOutboundExpansionLinksFromJson(string rawJson)
        => BggRawSnapshotParser.ExtractOutboundExpansionLinksFromJson(rawJson);

    /// <inheritdoc />
    public async Task<bool> EnsureSnapshotAsync(int bggId, CancellationToken ct = default)
    {
        if (bggId <= 0) return false;

        var existing = await _snapshotRepo.GetByBggIdAsync(bggId, ct);
        if (existing != null) return true;

        try
        {
            var xml = await _bggClient.FetchRawThingXmlAsync(bggId, includeVersions: true, ct);
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

    public async Task<BggRawSnapshotSyncResultDto> SyncVersionsBatchAsync(int batchSize = 20, int delayMs = 1200, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        return await SyncVersionsBatchCoreAsync(batchSize, delayMs, ct);
    }

    public Task<BggRawSnapshotSyncResultDto> RunScheduledSyncVersionsBatchAsync(int batchSize = 20, int delayMs = 1200, CancellationToken ct = default)
    {
        return SyncVersionsBatchCoreAsync(batchSize, delayMs, ct);
    }

    private async Task<BggRawSnapshotSyncResultDto> SyncVersionsBatchCoreAsync(int batchSize, int delayMs, CancellationToken ct)
    {
        var targetIds = await _snapshotRepo.GetBggIdsMissingVersionsAsync(batchSize, ct);
        if (targetIds.Count == 0)
        {
            return new BggRawSnapshotSyncResultDto(0, 0, 0, [], [], "Todos los snapshots almacenados ya cuentan con datos de versiones.");
        }

        int successCount = 0;
        int failedCount = 0;
        var syncedTitles = new List<string>();
        var updatedGames = new List<string>();

        var chunks = targetIds
            .Select((id, index) => new { id, index })
            .GroupBy(x => x.index / 20)
            .Select(g => g.Select(x => x.id).ToList())
            .ToList();

        for (int i = 0; i < chunks.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var chunk = chunks[i];

            try
            {
                string? xmlContent = await _bggClient.FetchRawThingsXmlAsync(chunk, includeVersions: true, ct);
                if (string.IsNullOrWhiteSpace(xmlContent))
                {
                    failedCount += chunk.Count;
                }
                else
                {
                    successCount += chunk.Count;

                    var doc = XDocument.Parse(xmlContent);
                    var items = doc.Root?.Elements("item")?.ToList() ?? new List<XElement>();

                    await using var context = await _contextFactory.CreateDbContextAsync(ct);
                    var games = await context.Games.Where(g => chunk.Contains(g.BggId)).ToListAsync(ct);
                    var gamesMap = games.ToDictionary(g => g.BggId);

                    foreach (var item in items)
                    {
                        if (int.TryParse(item.Attribute("id")?.Value, out int bggId) && bggId > 0)
                        {
                            string rawJson = BggXmlToJsonConverter.ConvertToJson(item);
                            var snapshot = new BggRawSnapshot(bggId, rawJson, apiVersion: 2, fetchedAt: DateTimeOffset.UtcNow);
                            await _snapshotRepo.UpsertAsync(snapshot, ct);

                            if (gamesMap.TryGetValue(bggId, out var game))
                            {
                                var vInfo = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(rawJson);
                                if (vInfo != null)
                                {
                                    bool modified = false;
                                    if (!string.IsNullOrWhiteSpace(vInfo.Title) && game.SpanishTitle != vInfo.Title)
                                    {
                                        game.UpdateSpanishTitle(vInfo.Title);
                                        modified = true;
                                    }
                                    if (!string.IsNullOrWhiteSpace(vInfo.Publisher) && string.IsNullOrWhiteSpace(game.SpanishPublisher))
                                    {
                                        game.UpdateSpanishPublisher(vInfo.Publisher);
                                        modified = true;
                                    }
                                    if (!string.IsNullOrWhiteSpace(vInfo.Ean) && game.Ean != vInfo.Ean)
                                    {
                                        game.UpdateEan(vInfo.Ean);
                                        modified = true;
                                    }
                                    if (modified)
                                    {
                                        updatedGames.Add($"{game.OriginalTitle} -> {game.SpanishTitle}");
                                    }
                                }
                            }
                        }
                    }
                    await context.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al sincronizar versiones para lote de BGG: {Message}", ex.Message);
                failedCount += chunk.Count;
            }

            if (i < chunks.Count - 1 && delayMs > 0)
            {
                await Task.Delay(delayMs, ct);
            }
        }

        string msg = $"Sincronización de versiones finalizada: {successCount} snapshots actualizados, {updatedGames.Count} juegos actualizados en catálogo.";
        return new BggRawSnapshotSyncResultDto(targetIds.Count, successCount, failedCount, syncedTitles, updatedGames, msg);
    }

    public async Task<BggVersionCatalogSweepResultDto> SweepCatalogFromVersionsAsync(int batchSize = 200, int lastBggId = 0, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        return await SweepCatalogFromVersionsCoreAsync(batchSize, lastBggId, ct);
    }

    public Task<BggVersionCatalogSweepResultDto> RunScheduledSweepCatalogFromVersionsAsync(int batchSize = 200, int lastBggId = 0, CancellationToken ct = default)
    {
        return SweepCatalogFromVersionsCoreAsync(batchSize, lastBggId, ct);
    }

    private async Task<BggVersionCatalogSweepResultDto> SweepCatalogFromVersionsCoreAsync(int batchSize, int lastBggId, CancellationToken ct)
    {
        var snapshots = await _snapshotRepo.GetSnapshotsAfterBggIdAsync(lastBggId, batchSize, ct);
        if (snapshots.Count == 0)
        {
            return new BggVersionCatalogSweepResultDto(0, 0, 0, 0, 0, lastBggId, false, "No hay más snapshots pendientes de barrido.");
        }

        int evaluated = 0;
        int updatedTitles = 0;
        int updatedEans = 0;
        int skipped = 0;
        int failed = 0;
        int newLastBggId = lastBggId;

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var bggIds = snapshots.Select(s => s.BggId).ToList();
        var gamesMap = await context.Games
            .Where(g => bggIds.Contains(g.BggId))
            .ToDictionaryAsync(g => g.BggId, ct);

        foreach (var s in snapshots)
        {
            newLastBggId = Math.Max(newLastBggId, s.BggId);
            evaluated++;

            try
            {
                if (!BggRawSnapshotParser.HasVersionsFromJson(s.RawJson))
                {
                    skipped++;
                    continue;
                }

                var vInfo = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(s.RawJson);
                if (vInfo == null)
                {
                    skipped++;
                    continue;
                }

                if (!gamesMap.TryGetValue(s.BggId, out var game))
                {
                    skipped++;
                    continue;
                }

                bool modified = false;

                if (!string.IsNullOrWhiteSpace(vInfo.Title) && game.SpanishTitle != vInfo.Title)
                {
                    game.UpdateSpanishTitle(vInfo.Title);
                    updatedTitles++;
                    modified = true;
                }

                if (!string.IsNullOrWhiteSpace(vInfo.Publisher) && string.IsNullOrWhiteSpace(game.SpanishPublisher))
                {
                    game.UpdateSpanishPublisher(vInfo.Publisher);
                    modified = true;
                }

                if (!string.IsNullOrWhiteSpace(vInfo.Ean) && game.Ean != vInfo.Ean)
                {
                    game.UpdateEan(vInfo.Ean);
                    updatedEans++;
                    modified = true;
                }

                if (!modified)
                {
                    skipped++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al evaluar versiones locales para BggId {BggId}: {Message}", s.BggId, ex.Message);
                failed++;
            }
        }

        await context.SaveChangesAsync(ct);

        bool hasMore = snapshots.Count >= batchSize;
        string msg = $"Barrido de versiones completado: {evaluated} evaluados, {updatedTitles} títulos actualizados, {updatedEans} EANs asignados.";
        return new BggVersionCatalogSweepResultDto(evaluated, updatedTitles, updatedEans, skipped, failed, newLastBggId, hasMore, msg);
    }
}
