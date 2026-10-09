using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

public interface IWeeklyReleaseService
{
    Task<IReadOnlyList<WeeklyReleaseDto>> GetReleasesAsync(DateOnly? fromDate = null, CancellationToken ct = default);
    Task<IReadOnlyList<WeeklyReleaseDto>> GetPendingModerationReleasesAsync(CancellationToken ct = default);
    Task<WeeklyReleaseDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WeeklyReleaseDto> CreateReleaseAsync(CreateWeeklyReleaseRequest request, CancellationToken ct = default);
    Task<WeeklyReleaseDto> UpdateReleaseAsync(Guid id, UpdateWeeklyReleaseRequest request, CancellationToken ct = default);
    Task<WeeklyReleaseDto> ApproveReleaseAsync(Guid id, Guid? linkedGameId = null, bool useAiSuggestionIfAvailable = true, CancellationToken ct = default);
    Task RejectReleaseAsync(Guid id, CancellationToken ct = default);
    Task DeleteReleaseAsync(Guid id, CancellationToken ct = default);
    Task<WeeklyReleaseDto?> GetUpcomingReprintByGameIdAsync(Guid gameId, CancellationToken ct = default)
        => Task.FromResult<WeeklyReleaseDto?>(null);
}
