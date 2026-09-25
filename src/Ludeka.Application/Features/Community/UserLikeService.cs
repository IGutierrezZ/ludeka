using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Features.Community;

public class UserLikeService : IUserLikeService
{
    private readonly IUserLikeRepository _userLikeRepository;

    public UserLikeService(IUserLikeRepository userLikeRepository)
    {
        _userLikeRepository = userLikeRepository ?? throw new ArgumentNullException(nameof(userLikeRepository));
    }

    public async Task<LikeStatusDto> GetStatusAsync(Guid? userId, LikeTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        if (targetId == Guid.Empty)
            return new LikeStatusDto(false, 0);

        var count = await _userLikeRepository.GetLikesCountAsync(targetType, targetId, ct);
        var isLiked = userId.HasValue && userId.Value != Guid.Empty &&
                      await _userLikeRepository.HasUserLikedAsync(userId.Value, targetType, targetId, ct);

        return new LikeStatusDto(isLiked, count);
    }

    public async Task<LikeStatusDto> ToggleLikeAsync(Guid userId, LikeTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("El usuario debe estar autenticado.", nameof(userId));

        if (targetId == Guid.Empty)
            throw new ArgumentException("El identificador del destino no puede estar vacío.", nameof(targetId));

        var isLiked = await _userLikeRepository.ToggleLikeAsync(userId, targetType, targetId, ct);
        var count = await _userLikeRepository.GetLikesCountAsync(targetType, targetId, ct);

        return new LikeStatusDto(isLiked, count);
    }

    public async Task<Dictionary<Guid, LikeStatusDto>> GetStatusesAsync(Guid? userId, LikeTargetType targetType, IEnumerable<Guid> targetIds, CancellationToken ct = default)
    {
        var idList = targetIds?.Distinct().Where(id => id != Guid.Empty).ToList() ?? [];
        if (idList.Count == 0)
            return new Dictionary<Guid, LikeStatusDto>();

        var counts = await _userLikeRepository.GetLikesCountsAsync(targetType, idList, ct);
        var likedIds = userId.HasValue && userId.Value != Guid.Empty
            ? await _userLikeRepository.GetUserLikedTargetIdsAsync(userId.Value, targetType, idList, ct)
            : [];

        return idList.ToDictionary(
            id => id,
            id => new LikeStatusDto(likedIds.Contains(id), counts.GetValueOrDefault(id, 0))
        );
    }
}
