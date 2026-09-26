using System;

namespace Ludeka.Core.Entities;

/// <summary>
/// Representa las preferencias personalizadas de un usuario en Ludeka,
/// incluyendo la paleta visual o tema de color preferido.
/// </summary>
public class UserPreference
{
    public string UserId { get; set; } = string.Empty;
    public string PreferredTheme { get; set; } = "charcoal";
    public string? Country { get; set; }
    public bool HidePublicProfile { get; set; } = false;
    public bool LeaderboardOptIn { get; set; } = false;
    public bool LeaderboardAnonymous { get; set; } = false;
    public string? LeaderboardPseudonym { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public UserPreference()
    {
    }

    public UserPreference(
        string userId,
        string preferredTheme,
        string? country = null,
        bool hidePublicProfile = false,
        bool leaderboardOptIn = false,
        bool leaderboardAnonymous = false,
        string? leaderboardPseudonym = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("El identificador de usuario no puede ser nulo o vacío.", nameof(userId));
        }

        UserId = userId.Trim();
        PreferredTheme = NormalizeTheme(preferredTheme);
        Country = string.IsNullOrWhiteSpace(country) ? null : ValueObjects.CountryCatalog.Normalize(country);
        HidePublicProfile = hidePublicProfile;
        LeaderboardOptIn = leaderboardOptIn;
        LeaderboardAnonymous = leaderboardAnonymous;
        LeaderboardPseudonym = Helpers.PseudonymGenerator.NormalizeCustomPseudonym(leaderboardPseudonym);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetTheme(string theme)
    {
        PreferredTheme = NormalizeTheme(theme);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetCountry(string? country)
    {
        Country = string.IsNullOrWhiteSpace(country) ? null : ValueObjects.CountryCatalog.Normalize(country);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetProfileVisibility(bool hidePublicProfile)
    {
        HidePublicProfile = hidePublicProfile;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetLeaderboardPreferences(bool optIn, bool anonymous, string? pseudonym = null)
    {
        LeaderboardOptIn = optIn;
        LeaderboardAnonymous = anonymous;
        LeaderboardPseudonym = Helpers.PseudonymGenerator.NormalizeCustomPseudonym(pseudonym);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Normaliza el tema asegurando que pertenece a los temas soportados
    /// ('editorial', 'wood', 'tabletop', 'midnight', 'charcoal').
    /// </summary>
    public static string NormalizeTheme(string? theme)
    {
        return theme?.Trim().ToLowerInvariant() switch
        {
            "editorial" => "editorial",
            "wood" => "wood",
            "tabletop" => "tabletop",
            "midnight" => "midnight",
            _ => "charcoal"
        };
    }
}
