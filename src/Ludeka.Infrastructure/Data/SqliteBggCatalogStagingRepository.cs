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
using Microsoft.Extensions.DependencyInjection;

namespace Ludeka.Infrastructure.Data;

/// <summary>
/// Implementación basada en EF Core (compatible con SQLite y PostgreSQL) para el repositorio de staging.
/// Hereda de <see cref="DbContextRepositoryBase"/> para aislar cada operación en un ámbito efímero
/// mediante <see cref="IDbContextFactory{LudekaDbContext}"/>, garantizando rendimiento, consumo menor a 30 MB RAM
/// y ausencia de colisiones en el ChangeTracker de Blazor Server.
/// </summary>
public class SqliteBggCatalogStagingRepository : DbContextRepositoryBase, IBggCatalogStagingRepository
{
    [ActivatorUtilitiesConstructor]
    public SqliteBggCatalogStagingRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteBggCatalogStagingRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task UpsertBatchAsync(IEnumerable<BggCatalogStagingItem> items, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        // Desduplicar el lote recibido defensivamente por BggId tomando el último estado
        var itemList = items
            .GroupBy(i => i.BggId)
            .Select(g => g.Last())
            .ToList();

        if (itemList.Count == 0) return;

        await using var scope = await CreateScopeAsync(ct);
        var db = scope.Context;

        var bggIds = itemList.Select(i => i.BggId).ToList();

        // Cargar los que ya existen para actualizar o evitar duplicados
        var existingItems = await db.BggCatalogStaging
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
            await db.BggCatalogStaging.AddRangeAsync(toAdd, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<BggCatalogStagingItem?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.BggCatalogStaging
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.BggId == bggId, ct);
    }

    public async Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingFetchBatchAsync(int batchSize = 20, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.BggCatalogStaging
            .AsNoTracking()
            .Where(s => s.FetchStatus == StagingFetchStatus.Pending)
            .OrderByDescending(s => s.UsersRated)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingImagesBatchAsync(int batchSize = 10, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.BggCatalogStaging
            .AsNoTracking()
            .Where(s => s.FetchStatus == StagingFetchStatus.Fetched && s.ImagesStatus == StagingImagesStatus.Pending)
            .OrderByDescending(s => s.UsersRated)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingAiBatchAsync(int batchSize = 10, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.BggCatalogStaging
            .AsNoTracking()
            .Where(s => s.FetchStatus == StagingFetchStatus.Fetched && s.AiStatus == StagingAiStatus.Pending)
            .OrderByDescending(s => s.UsersRated)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingPromotionBatchAsync(int batchSize = 50, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.BggCatalogStaging
            .AsNoTracking()
            .Where(s => s.FetchStatus == StagingFetchStatus.Fetched &&
                        s.PromotionStatus == StagingPromotionStatus.Pending &&
                        s.ImagesStatus != StagingImagesStatus.Pending &&
                        s.ImagesStatus != StagingImagesStatus.InProgress &&
                        s.AiStatus != StagingAiStatus.Pending &&
                        s.AiStatus != StagingAiStatus.InProgress &&
                        s.AiStatus != StagingAiStatus.QuotaExceeded)
            .OrderByDescending(s => s.UsersRated)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(BggCatalogStagingItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await using var scope = await CreateScopeAsync(ct);
        var db = scope.Context;
        db.BggCatalogStaging.Update(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateBatchAsync(IEnumerable<BggCatalogStagingItem> items, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        await using var scope = await CreateScopeAsync(ct);
        var db = scope.Context;
        db.BggCatalogStaging.UpdateRange(items);
        await db.SaveChangesAsync(ct);
    }

    public async Task<BggStagingMetricsDto> GetMetricsAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var db = scope.Context;

        int total = await db.BggCatalogStaging.CountAsync(ct);
        int pendingFetch = await db.BggCatalogStaging.CountAsync(s => s.FetchStatus == StagingFetchStatus.Pending, ct);
        int fetched = await db.BggCatalogStaging.CountAsync(s => s.FetchStatus == StagingFetchStatus.Fetched, ct);
        int pendingImages = await db.BggCatalogStaging.CountAsync(s => s.ImagesStatus == StagingImagesStatus.Pending, ct);
        int imagesCompleted = await db.BggCatalogStaging.CountAsync(s => s.ImagesStatus == StagingImagesStatus.Completed, ct);
        int pendingAi = await db.BggCatalogStaging.CountAsync(s => s.AiStatus == StagingAiStatus.Pending, ct);
        int aiCompleted = await db.BggCatalogStaging.CountAsync(s => s.AiStatus == StagingAiStatus.Completed, ct);
        int aiQuotaExceeded = await db.BggCatalogStaging.CountAsync(s => s.AiStatus == StagingAiStatus.QuotaExceeded, ct);
        int pendingPromotion = await db.BggCatalogStaging.CountAsync(s => s.PromotionStatus == StagingPromotionStatus.Pending, ct);
        int promoted = await db.BggCatalogStaging.CountAsync(s => s.PromotionStatus == StagingPromotionStatus.Promoted, ct);
        int failed = await db.BggCatalogStaging.CountAsync(s =>
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
        await using var scope = await CreateScopeAsync(ct);
        var db = scope.Context;

        var quotaExceededItems = await db.BggCatalogStaging
            .Where(s => s.AiStatus == StagingAiStatus.QuotaExceeded)
            .ToListAsync(ct);

        foreach (var item in quotaExceededItems)
        {
            item.ResetAiQuotaToPending();
        }

        if (quotaExceededItems.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.BggCatalogStaging.CountAsync(ct);
    }

    public async Task ClearStagingAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.BggCatalogStaging.ExecuteDeleteAsync(ct);
    }
}
