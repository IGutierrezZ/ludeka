using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Data;

public class SqliteUserLikeRepository : DbContextRepositoryBase, IUserLikeRepository
{
    public SqliteUserLikeRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteUserLikeRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<bool> HasUserLikedAsync(Guid userId, LikeTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty || targetId == Guid.Empty)
            return false;

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.UserLikes
            .AsNoTracking()
            .AnyAsync(l => l.UserId == userId && l.TargetType == targetType && l.TargetId == targetId, ct);
    }

    public async Task<bool> ToggleLikeAsync(Guid userId, LikeTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("El identificador del usuario no puede estar vacío.", nameof(userId));
        if (targetId == Guid.Empty)
            throw new ArgumentException("El identificador del destino no puede estar vacío.", nameof(targetId));

        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.UserLikes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.TargetType == targetType && l.TargetId == targetId, ct);

        if (existing != null)
        {
            scope.Context.UserLikes.Remove(existing);
            await scope.Context.SaveChangesAsync(ct);
            return false;
        }
        else
        {
            var newLike = new UserLike(userId, targetType, targetId);
            await scope.Context.UserLikes.AddAsync(newLike, ct);
            await scope.Context.SaveChangesAsync(ct);
            return true;
        }
    }

    public async Task<int> GetLikesCountAsync(LikeTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        if (targetId == Guid.Empty)
            return 0;

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.UserLikes
            .AsNoTracking()
            .CountAsync(l => l.TargetType == targetType && l.TargetId == targetId, ct);
    }

    public async Task<Dictionary<Guid, int>> GetLikesCountsAsync(LikeTargetType targetType, IEnumerable<Guid> targetIds, CancellationToken ct = default)
    {
        var idList = targetIds?.Distinct().Where(id => id != Guid.Empty).ToList() ?? [];
        if (idList.Count == 0)
            return new Dictionary<Guid, int>();

        await using var scope = await CreateScopeAsync(ct);
        var counts = await scope.Context.UserLikes
            .AsNoTracking()
            .Where(l => l.TargetType == targetType && idList.Contains(l.TargetId))
            .GroupBy(l => l.TargetId)
            .Select(g => new { TargetId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var result = idList.ToDictionary(id => id, _ => 0);
        foreach (var item in counts)
        {
            result[item.TargetId] = item.Count;
        }

        return result;
    }

    public async Task<HashSet<Guid>> GetUserLikedTargetIdsAsync(Guid userId, LikeTargetType targetType, IEnumerable<Guid> targetIds, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return [];

        var idList = targetIds?.Distinct().Where(id => id != Guid.Empty).ToList() ?? [];
        if (idList.Count == 0)
            return [];

        await using var scope = await CreateScopeAsync(ct);
        var likedIds = await scope.Context.UserLikes
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.TargetType == targetType && idList.Contains(l.TargetId))
            .Select(l => l.TargetId)
            .ToListAsync(ct);

        return new HashSet<Guid>(likedIds);
    }

    public async Task<int> GetLikesGivenCountByUserAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return 0;

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.UserLikes
            .AsNoTracking()
            .CountAsync(l => l.UserId == userId, ct);
    }
}
