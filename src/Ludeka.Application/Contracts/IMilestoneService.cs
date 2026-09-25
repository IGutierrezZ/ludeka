using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

public interface IMilestoneService
{
    Task<UserMilestoneProgressDto> GetUserMilestonesAsync(string userId, CancellationToken ct = default);
    Task<UserMilestoneProgressDto> EvaluateAndSyncMilestonesAsync(string userId, CancellationToken ct = default);
}
