using System.Collections.Generic;

namespace Ludeka.Application.DTOs;

public record UserMilestoneProgressDto(
    string UserId,
    int TotalUnlocked,
    int TotalMilestones,
    double CompletionPercentage,
    IReadOnlyList<MilestoneDto> Milestones
);
