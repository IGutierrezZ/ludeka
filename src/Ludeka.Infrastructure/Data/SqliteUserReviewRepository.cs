using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ludeka.Infrastructure.Data;

public class SqliteUserReviewRepository : DbContextRepositoryBase, IUserReviewRepository
{
    public SqliteUserReviewRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteUserReviewRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<UserGameReview?> GetByUserAndGameAsync(string userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        return await scope.Context.Reviews
            .Include(r => r.Game)
            .FirstOrDefaultAsync(r => r.UserId == userId && r.GameId == gameId, cancellationToken);
    }

    public async Task<List<UserGameReview>> GetByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        var list = await scope.Context.Reviews
            .Include(r => r.Game)
            .Where(r => r.GameId == gameId)
            .ToListAsync(cancellationToken);

        return list.OrderByDescending(r => r.CreatedAt).ToList();
    }

    public async Task<double?> GetAverageScoreByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        var scores = await scope.Context.Reviews
            .Where(r => r.GameId == gameId)
            .Select(r => r.Score)
            .ToListAsync(cancellationToken);

        if (scores.Count == 0) return null;

        return scores.Average();
    }

    public async Task AddAsync(UserGameReview review, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        await scope.Context.Reviews.AddAsync(review, cancellationToken);
        await scope.Context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(UserGameReview review, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        var existing = await scope.Context.Reviews.FirstOrDefaultAsync(r => r.Id == review.Id, cancellationToken);
        if (existing != null)
        {
            existing.Update(
                review.Score,
                review.MicroReview,
                review.PlayerCountRatings,
                review.FamilyExperience,
                review.PlayContext
            );
            await scope.Context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> GetCountByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return 0;
        await using var scope = await CreateScopeAsync(cancellationToken);
        return await scope.Context.Reviews.CountAsync(r => r.UserId == userId.Trim(), cancellationToken);
    }
}
