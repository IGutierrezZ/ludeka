using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

public interface IMediaService
{
    Task<GameMediaHubDto> GetGameMediaAsync(Guid gameId, CancellationToken ct = default);
    Task<IReadOnlyList<MediaItemDto>> GetPendingModerationAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MediaItemDto>> GetOrphansAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MediaItemDto>> GetApprovedAsync(CancellationToken ct = default);
    Task<MediaItemDto?> ApproveMediaAsync(Guid id, MediaCategory? category = null, CancellationToken ct = default);
    Task<MediaItemDto?> RejectMediaAsync(Guid id, CancellationToken ct = default);
    Task<MediaItemDto?> AssignOrphanMediaAsync(Guid id, Guid gameId, CancellationToken ct = default);
    Task<BrokenLinkReportDto> CheckBrokenLinksAsync(CancellationToken ct = default);
    Task<MediaItemDto> CreateMediaItemAsync(MediaItem item, CancellationToken ct = default);
    Task<MediaItemDto?> UpdateMediaCategoryAsync(Guid id, MediaCategory newCategory, CancellationToken ct = default);
    Task<MediaItemDto?> ReassignMediaGameAsync(Guid id, Guid newGameId, CancellationToken ct = default);
    Task<bool> DeleteMediaAsync(Guid id, CancellationToken ct = default);
    Task<MediaItemDto> FlashIngestAsync(Guid gameId, string url, MediaCategory category, string? playerCountBadge = null, CancellationToken ct = default);
}
