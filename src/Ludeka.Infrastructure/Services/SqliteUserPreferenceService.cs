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
/// Implementación de persistencia para las preferencias de usuario basada en SQLite y EF Core.
/// </summary>
public class SqliteUserPreferenceService : IUserPreferenceService
{
    private readonly LudekaDbContext _db;

    public SqliteUserPreferenceService(LudekaDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<string> GetUserThemeAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return "charcoal";
        }

        var pref = await _db.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId.Trim(), ct);

        return pref?.PreferredTheme ?? "charcoal";
    }

    public async Task SetUserThemeAsync(string userId, string theme, CancellationToken ct = default)
    {
        // Invariante de anonimia: la preferencia exige sesión; nunca se escribe con identidad vacía.
        string cleanUserId = SessionIdentity.Require(userId);
        var existing = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == cleanUserId, ct);

        if (existing == null)
        {
            var newPref = new UserPreference(cleanUserId, theme);
            _db.UserPreferences.Add(newPref);
        }
        else
        {
            existing.SetTheme(theme);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<UserPreferenceDto> GetUserPreferenceAsync(string userId, CancellationToken ct = default)
    {
        string cleanUserId = string.IsNullOrWhiteSpace(userId) ? "default" : userId.Trim();

        var pref = await _db.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == cleanUserId, ct);

        if (pref != null)
        {
            return new UserPreferenceDto(pref.UserId, pref.PreferredTheme, pref.UpdatedAt, pref.Country);
        }

        return new UserPreferenceDto(cleanUserId, "charcoal", DateTime.UtcNow, null);
    }

    public async Task<string?> GetUserCountryAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        var pref = await _db.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId.Trim(), ct);

        return pref?.Country;
    }

    public async Task SetUserCountryAsync(string userId, string? country, CancellationToken ct = default)
    {
        // Invariante de anonimia: la preferencia exige sesión; nunca se escribe con identidad vacía.
        string cleanUserId = SessionIdentity.Require(userId);
        var existing = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == cleanUserId, ct);

        if (existing == null)
        {
            var newPref = new UserPreference(cleanUserId, "charcoal", country);
            _db.UserPreferences.Add(newPref);
        }
        else
        {
            existing.SetCountry(country);
        }

        await _db.SaveChangesAsync(ct);
    }
}
