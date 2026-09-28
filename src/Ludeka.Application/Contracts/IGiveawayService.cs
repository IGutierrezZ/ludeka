using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

public interface IGiveawayService
{
    Task<IReadOnlyList<GiveawayDto>> GetGiveawaysAsync(bool includeExpired = false, string? country = null, CancellationToken ct = default);
    Task<GiveawayDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<GiveawayDto> CreateOrMergeGiveawayAsync(CreateGiveawayRequest request, CancellationToken ct = default);
    Task<GiveawayDto> UpdateGiveawayAsync(UpdateGiveawayRequest request, CancellationToken ct = default);
    Task SetPromotedAsync(Guid id, bool isPromoted, CancellationToken ct = default);
    Task DeleteGiveawayAsync(Guid id, CancellationToken ct = default);
}
