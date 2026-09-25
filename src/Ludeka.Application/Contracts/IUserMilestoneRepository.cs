using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

public interface IUserMilestoneRepository
{
    Task<List<UserMilestone>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<bool> HasMilestoneAsync(string userId, MilestoneType type, CancellationToken ct = default);
    Task<bool> UnlockMilestoneAsync(UserMilestone milestone, CancellationToken ct = default);
    Task<int> UnlockMilestonesAsync(IEnumerable<UserMilestone> milestones, CancellationToken ct = default);
}
