using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

public interface IInstagramReelSyncRepository
{
    Task<InstagramReelSyncItem?> GetByInstagramMediaIdAsync(string instagramMediaId, CancellationToken ct = default);
    Task<InstagramReelSyncItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<InstagramReelSyncItem>> GetPendingForUploadAsync(int maxItems, int maxAttempts = 3, CancellationToken ct = default);
    Task<IReadOnlyList<InstagramReelSyncItem>> GetAllAsync(InstagramReelSyncStatus? statusFilter = null, int limit = 100, CancellationToken ct = default);
    Task AddAsync(InstagramReelSyncItem item, CancellationToken ct = default);
    Task UpdateAsync(InstagramReelSyncItem item, CancellationToken ct = default);
}

public interface IInstagramToYouTubeCaptionAdapter
{
    Task<YouTubeAdaptedMetadataDto> AdaptCaptionForYouTubeShortAsync(string? instagramCaption, CancellationToken ct = default);
}

public interface IYouTubeShortsUploadClient
{
    bool IsConfigured { get; }
    Task<YouTubeShortUploadResultDto> UploadShortFromUrlAsync(
        string mediaUrl,
        string title,
        string description,
        CancellationToken ct = default);
}

public interface IInstagramYouTubeSyncService
{
    Task<InstagramYouTubeSyncResultDto> SyncReelsToYouTubeAsync(int? maxUploads = null, CancellationToken ct = default);
    Task<IReadOnlyList<InstagramReelSyncItemDto>> GetSyncItemsAsync(InstagramReelSyncStatus? statusFilter = null, int limit = 50, CancellationToken ct = default);
}
