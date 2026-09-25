using System;

namespace Ludeka.Application.DTOs;

public record UserPreferenceDto(
    string UserId,
    string PreferredTheme,
    DateTime UpdatedAt,
    string? Country = null,
    bool HidePublicProfile = false
);
