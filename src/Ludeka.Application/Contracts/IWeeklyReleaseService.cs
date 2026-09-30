using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

public interface IWeeklyReleaseService
{
    Task<IReadOnlyList<WeeklyReleaseDto>> GetReleasesAsync(DateOnly? fromDate = null, CancellationToken ct = default);
    Task<WeeklyReleaseDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WeeklyReleaseDto> CreateReleaseAsync(CreateWeeklyReleaseRequest request, CancellationToken ct = default);
    Task<WeeklyReleaseDto> UpdateReleaseAsync(Guid id, UpdateWeeklyReleaseRequest request, CancellationToken ct = default);
    Task DeleteReleaseAsync(Guid id, CancellationToken ct = default);
}
