using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Contracts;

public interface IWeeklyReleaseRepository
{
    Task<IReadOnlyList<WeeklyRelease>> GetReleasesAsync(DateOnly? fromDate = null, CancellationToken ct = default);
    Task<WeeklyRelease?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(WeeklyRelease release, CancellationToken ct = default);
    Task UpdateAsync(WeeklyRelease release, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<WeeklyRelease?> GetUpcomingReprintByGameIdAsync(Guid gameId, CancellationToken ct = default)
        => Task.FromResult<WeeklyRelease?>(null);
}
