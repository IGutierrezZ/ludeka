using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

public interface IUserLikeRepository
{
    Task<bool> HasUserLikedAsync(Guid userId, LikeTargetType targetType, Guid targetId, CancellationToken ct = default);
    Task<bool> ToggleLikeAsync(Guid userId, LikeTargetType targetType, Guid targetId, CancellationToken ct = default);
    Task<int> GetLikesCountAsync(LikeTargetType targetType, Guid targetId, CancellationToken ct = default);
    Task<Dictionary<Guid, int>> GetLikesCountsAsync(LikeTargetType targetType, IEnumerable<Guid> targetIds, CancellationToken ct = default);
    Task<HashSet<Guid>> GetUserLikedTargetIdsAsync(Guid userId, LikeTargetType targetType, IEnumerable<Guid> targetIds, CancellationToken ct = default);
    Task<int> GetLikesGivenCountByUserAsync(Guid userId, CancellationToken ct = default);
}
