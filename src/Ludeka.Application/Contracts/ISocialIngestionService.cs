using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

public interface ISocialIngestionService
{
    Task<SocialInboxItemDto> IngestFromUrlAsync(string url, string? manualCaption = null, CancellationToken ct = default);
    Task<SocialInboxItemDto> IngestManualAdvancedAsync(SocialInboxManualInputDto input, CancellationToken ct = default);
    Task<SocialInboxItemDto> UpdateItemAsync(SocialInboxUpdateDto dto, CancellationToken ct = default);
    Task<Guid> ApproveAndPublishAsync(Guid inboxItemId, string reviewerUserId, CancellationToken ct = default);
    Task RejectItemAsync(Guid inboxItemId, string reason, string reviewerUserId, CancellationToken ct = default);
    Task<IReadOnlyList<SocialInboxItemDto>> GetPendingItemsAsync(SocialSubmissionType? typeFilter = null, CancellationToken ct = default);
    Task<IReadOnlyList<SocialInboxItemDto>> GetAllItemsAsync(SocialInboxStatus? statusFilter = null, SocialSubmissionType? typeFilter = null, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<SocialInboxItemDto?> GetItemByIdAsync(Guid id, CancellationToken ct = default);
    Task<int> GetPendingCountAsync(CancellationToken ct = default);
}
