using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Data;

/// <summary>
/// Implementación basada en EF Core (compatible con SQLite y PostgreSQL) para el repositorio de staging.
/// </summary>
public class SqliteBggCatalogStagingRepository : IBggCatalogStagingRepository
{
    private readonly LudekaDbContext _context;

    public SqliteBggCatalogStagingRepository(LudekaDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task UpsertBatchAsync(IEnumerable<BggCatalogStagingItem> items, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var itemList = items.ToList();
        if (itemList.Count == 0) return;

        var bggIds = itemList.Select(i => i.BggId).ToList();

        // Cargar los que ya existen para actualizar o evitar duplicados
        var existingItems = await _context.BggCatalogStaging
            .Where(s => bggIds.Contains(s.BggId))
            .ToDictionaryAsync(s => s.BggId, ct);

        var toAdd = new List<BggCatalogStagingItem>();

        foreach (var item in itemList)
        {
            if (existingItems.TryGetValue(item.BggId, out var existing))
            {
                existing.UpdateRankMetrics(item.BggRank, item.UsersRated, item.BayesAverage, item.AverageRating);
            }
            else
            {
                toAdd.Add(item);
            }
        }

        if (toAdd.Count > 0)
        {
            await _context.BggCatalogStaging.AddRangeAsync(toAdd, ct);
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<BggCatalogStagingItem?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
    {
        return await _context.BggCatalogStaging
            .FirstOrDefaultAsync(s => s.BggId == bggId, ct);
    }

    public async Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingFetchBatchAsync(int batchSize = 20, CancellationToken ct = default)
    {
        return await _context.BggCatalogStaging
            .Where(s => s.FetchStatus == StagingFetchStatus.Pending)
            .OrderByDescending(s => s.UsersRated)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingImagesBatchAsync(int batchSize = 10, CancellationToken ct = default)
    {
        return await _context.BggCatalogStaging
            .Where(s => s.FetchStatus == StagingFetchStatus.Fetched && s.ImagesStatus == StagingImagesStatus.Pending)
            .OrderByDescending(s => s.UsersRated)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingAiBatchAsync(int batchSize = 10, CancellationToken ct = default)
    {
        return await _context.BggCatalogStaging
            .Where(s => s.FetchStatus == StagingFetchStatus.Fetched && s.AiStatus == StagingAiStatus.Pending)
            .OrderByDescending(s => s.UsersRated)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingPromotionBatchAsync(int batchSize = 50, CancellationToken ct = default)
    {
        return await _context.BggCatalogStaging
            .Where(s => s.FetchStatus == StagingFetchStatus.Fetched &&
                        s.PromotionStatus == StagingPromotionStatus.Pending &&
                        s.ImagesStatus != StagingImagesStatus.Pending &&
                        s.ImagesStatus != StagingImagesStatus.InProgress &&
                        s.AiStatus != StagingAiStatus.Pending &&
                        s.AiStatus != StagingAiStatus.InProgress)
            .OrderByDescending(s => s.UsersRated)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(BggCatalogStagingItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        _context.BggCatalogStaging.Update(item);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateBatchAsync(IEnumerable<BggCatalogStagingItem> items, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        _context.BggCatalogStaging.UpdateRange(items);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<BggStagingMetricsDto> GetMetricsAsync(CancellationToken ct = default)
    {
        int total = await _context.BggCatalogStaging.CountAsync(ct);
        int pendingFetch = await _context.BggCatalogStaging.CountAsync(s => s.FetchStatus == StagingFetchStatus.Pending, ct);
        int fetched = await _context.BggCatalogStaging.CountAsync(s => s.FetchStatus == StagingFetchStatus.Fetched, ct);
        int pendingImages = await _context.BggCatalogStaging.CountAsync(s => s.ImagesStatus == StagingImagesStatus.Pending, ct);
        int imagesCompleted = await _context.BggCatalogStaging.CountAsync(s => s.ImagesStatus == StagingImagesStatus.Completed, ct);
        int pendingAi = await _context.BggCatalogStaging.CountAsync(s => s.AiStatus == StagingAiStatus.Pending, ct);
        int aiCompleted = await _context.BggCatalogStaging.CountAsync(s => s.AiStatus == StagingAiStatus.Completed, ct);
        int aiQuotaExceeded = await _context.BggCatalogStaging.CountAsync(s => s.AiStatus == StagingAiStatus.QuotaExceeded, ct);
        int pendingPromotion = await _context.BggCatalogStaging.CountAsync(s => s.PromotionStatus == StagingPromotionStatus.Pending, ct);
        int promoted = await _context.BggCatalogStaging.CountAsync(s => s.PromotionStatus == StagingPromotionStatus.Promoted, ct);
        int failed = await _context.BggCatalogStaging.CountAsync(s =>
            s.FetchStatus == StagingFetchStatus.Failed ||
            s.ImagesStatus == StagingImagesStatus.Failed ||
            s.AiStatus == StagingAiStatus.Failed ||
            s.PromotionStatus == StagingPromotionStatus.Failed, ct);

        return new BggStagingMetricsDto(
            TotalInStaging: total,
            PendingFetchCount: pendingFetch,
            FetchedCount: fetched,
            PendingImagesCount: pendingImages,
            ImagesCompletedCount: imagesCompleted,
            PendingAiCount: pendingAi,
            AiCompletedCount: aiCompleted,
            AiQuotaExceededCount: aiQuotaExceeded,
            PendingPromotionCount: pendingPromotion,
            PromotedCount: promoted,
            FailedCount: failed
        );
    }

    public async Task ResetQuotaExceededStatusAsync(CancellationToken ct = default)
    {
        var quotaExceededItems = await _context.BggCatalogStaging
            .Where(s => s.AiStatus == StagingAiStatus.QuotaExceeded)
            .ToListAsync(ct);

        foreach (var item in quotaExceededItems)
        {
            item.ResetAiQuotaToPending();
        }

        if (quotaExceededItems.Count > 0)
        {
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
    {
        return await _context.BggCatalogStaging.CountAsync(ct);
    }

    public async Task ClearStagingAsync(CancellationToken ct = default)
    {
        await _context.BggCatalogStaging.ExecuteDeleteAsync(ct);
    }
}
