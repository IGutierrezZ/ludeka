using System;
using Ludeka.Core.Enums;

namespace Ludeka.Application.DTOs;

public record MilestoneDto(
    MilestoneType Type,
    MilestoneCategory Category,
    string Title,
    string Description,
    string IconEmoji,
    int SortOrder,
    bool IsUnlocked,
    DateTimeOffset? UnlockedAt
);
