using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Services;

/// <summary>
/// Implementación de persistencia para las preferencias de usuario basada en EF Core y ámbitos efímeros.
/// </summary>
public class SqliteUserPreferenceService : DbContextRepositoryBase, IUserPreferenceService
{
    public SqliteUserPreferenceService(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteUserPreferenceService(LudekaDbContext db) : base(db)
    {
    }

    public async Task<string> GetUserThemeAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return "charcoal";
        }

        await using var scope = await CreateScopeAsync(ct);
        var pref = await scope.Context.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId.Trim(), ct);

        return pref?.PreferredTheme ?? "charcoal";
    }

    public async Task SetUserThemeAsync(string userId, string theme, CancellationToken ct = default)
    {
        // Invariante de anonimia: la preferencia exige sesión; nunca se escribe con identidad vacía.
        string cleanUserId = SessionIdentity.Require(userId);
        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.UserPreferences.FirstOrDefaultAsync(p => p.UserId == cleanUserId, ct);

        if (existing == null)
        {
            var newPref = new UserPreference(cleanUserId, theme);
            scope.Context.UserPreferences.Add(newPref);
        }
        else
        {
            existing.SetTheme(theme);
        }

        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task<UserPreferenceDto> GetUserPreferenceAsync(string userId, CancellationToken ct = default)
    {
        string cleanUserId = string.IsNullOrWhiteSpace(userId) ? "default" : userId.Trim();

        await using var scope = await CreateScopeAsync(ct);
        var pref = await scope.Context.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == cleanUserId, ct);

        if (pref != null)
        {
            return new UserPreferenceDto(
                pref.UserId,
                pref.PreferredTheme,
                pref.UpdatedAt,
                pref.Country,
                pref.HidePublicProfile,
                pref.LeaderboardOptIn,
                pref.LeaderboardAnonymous,
                pref.LeaderboardPseudonym);
        }

        return new UserPreferenceDto(cleanUserId, "charcoal", DateTime.UtcNow, null, false, false, false, null);
    }

    public async Task<string?> GetUserCountryAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        await using var scope = await CreateScopeAsync(ct);
        var pref = await scope.Context.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId.Trim(), ct);

        return pref?.Country;
    }

    public async Task SetUserCountryAsync(string userId, string? country, CancellationToken ct = default)
    {
        // Invariante de anonimia: la preferencia exige sesión; nunca se escribe con identidad vacía.
        string cleanUserId = SessionIdentity.Require(userId);
        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.UserPreferences.FirstOrDefaultAsync(p => p.UserId == cleanUserId, ct);

        if (existing == null)
        {
            var newPref = new UserPreference(cleanUserId, "charcoal", country);
            scope.Context.UserPreferences.Add(newPref);
        }
        else
        {
            existing.SetCountry(country);
        }

        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task<bool> IsPublicProfileHiddenAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        await using var scope = await CreateScopeAsync(ct);
        var pref = await scope.Context.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId.Trim(), ct);

        return pref?.HidePublicProfile ?? false;
    }

    public async Task SetPublicProfileHiddenAsync(string userId, bool hide, CancellationToken ct = default)
    {
        string cleanUserId = SessionIdentity.Require(userId);
        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.UserPreferences.FirstOrDefaultAsync(p => p.UserId == cleanUserId, ct);

        if (existing == null)
        {
            var newPref = new UserPreference(cleanUserId, "charcoal", null, hide);
            scope.Context.UserPreferences.Add(newPref);
        }
        else
        {
            existing.SetProfileVisibility(hide);
        }

        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task SetLeaderboardPreferencesAsync(string userId, bool optIn, bool anonymous, string? pseudonym = null, CancellationToken ct = default)
    {
        string cleanUserId = SessionIdentity.Require(userId);
        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.UserPreferences.FirstOrDefaultAsync(p => p.UserId == cleanUserId, ct);

        if (existing == null)
        {
            var newPref = new UserPreference(cleanUserId, "charcoal", null, false, optIn, anonymous, pseudonym);
            scope.Context.UserPreferences.Add(newPref);
        }
        else
        {
            existing.SetLeaderboardPreferences(optIn, anonymous, pseudonym);
        }

        await scope.Context.SaveChangesAsync(ct);
    }
}
