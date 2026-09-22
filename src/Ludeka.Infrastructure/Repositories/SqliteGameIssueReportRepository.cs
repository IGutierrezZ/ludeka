using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Repositories;

public class SqliteGameIssueReportRepository : DbContextRepositoryBase, IGameIssueReportRepository
{
    public SqliteGameIssueReportRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteGameIssueReportRepository(LudekaDbContext context) : base(context)
    {
    }

    public async ValueTask<GameIssueReport?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.IssueReports
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async ValueTask<IReadOnlyList<GameIssueReport>> GetAllAsync(GameReportFilter filter, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var query = scope.Context.IssueReports.AsNoTracking().AsQueryable();

        if (filter.Status.HasValue)
        {
            query = query.Where(r => r.Status == filter.Status.Value);
        }

        if (filter.IssueType.HasValue)
        {
            query = query.Where(r => r.IssueType == filter.IssueType.Value);
        }

        if (filter.GameId.HasValue && filter.GameId.Value != Guid.Empty)
        {
            query = query.Where(r => r.GameId == filter.GameId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(r =>
                r.GameTitle.ToLower().Contains(term) ||
                r.GameSlug.ToLower().Contains(term) ||
                (r.Details != null && r.Details.ToLower().Contains(term)) ||
                r.ReporterNameOrAlias.ToLower().Contains(term)
            );
        }

        var list = await query.ToListAsync(ct);
        var ordered = list.OrderByDescending(r => r.CreatedAt).AsEnumerable();

        if (filter.Page > 1)
        {
            var skip = (filter.Page - 1) * filter.PageSize;
            ordered = ordered.Skip(skip);
        }

        if (filter.PageSize > 0)
        {
            ordered = ordered.Take(filter.PageSize);
        }

        return ordered.ToList();
    }

    public async ValueTask<GameIssueReportSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var counts = await scope.Context.IssueReports
            .AsNoTracking()
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var pending = counts.FirstOrDefault(c => c.Status == GameReportStatus.Pending)?.Count ?? 0;
        var inReview = counts.FirstOrDefault(c => c.Status == GameReportStatus.InReview)?.Count ?? 0;
        var resolved = counts.FirstOrDefault(c => c.Status == GameReportStatus.Resolved)?.Count ?? 0;
        var dismissed = counts.FirstOrDefault(c => c.Status == GameReportStatus.Dismissed)?.Count ?? 0;
        var total = pending + inReview + resolved + dismissed;

        return new GameIssueReportSummaryDto(
            TotalCount: total,
            PendingCount: pending,
            InReviewCount: inReview,
            ResolvedCount: resolved,
            DismissedCount: dismissed
        );
    }

    public async ValueTask AddAsync(GameIssueReport report, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.IssueReports.AddAsync(report, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async ValueTask UpdateAsync(GameIssueReport report, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        await using var scope = await CreateScopeAsync(ct);
        scope.Context.IssueReports.Update(report);
        await scope.Context.SaveChangesAsync(ct);
    }
}
