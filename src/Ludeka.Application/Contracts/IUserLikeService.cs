using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

public record LikeStatusDto(bool IsLiked, int LikesCount);

public interface IUserLikeService
{
    Task<LikeStatusDto> GetStatusAsync(Guid? userId, LikeTargetType targetType, Guid targetId, CancellationToken ct = default);
    Task<LikeStatusDto> ToggleLikeAsync(Guid userId, LikeTargetType targetType, Guid targetId, CancellationToken ct = default);
    Task<Dictionary<Guid, LikeStatusDto>> GetStatusesAsync(Guid? userId, LikeTargetType targetType, IEnumerable<Guid> targetIds, CancellationToken ct = default);
}
