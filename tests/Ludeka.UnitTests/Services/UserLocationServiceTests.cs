using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Services;
using Xunit;

namespace Ludeka.UnitTests.Services;

public class UserLocationServiceTests
{
    private class FakeCurrentUserService : ICurrentUserService
    {
        public string UserId { get; set; } = "user-123";
        public string UserName { get; set; } = "testuser";
        public IReadOnlyList<string> Roles => ["User"];
        public bool IsFoundingTeam => false;
        public bool IsInRole(string role) => false;
        public bool HasPermission(ModeratorPermission permission) => false;
    }

    private class FakeUserPreferenceService : IUserPreferenceService
    {
        public string? SavedCountry { get; set; }

        public Task<string> GetUserThemeAsync(string userId, CancellationToken ct = default) => Task.FromResult("charcoal");
        public Task SetUserThemeAsync(string userId, string theme, CancellationToken ct = default) => Task.CompletedTask;
        public Task<UserPreferenceDto> GetUserPreferenceAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(new UserPreferenceDto(userId, "charcoal", DateTime.UtcNow, SavedCountry));

        public Task<string?> GetUserCountryAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(SavedCountry);

        public Task SetUserCountryAsync(string userId, string? country, CancellationToken ct = default)
        {
            SavedCountry = country;
            return Task.CompletedTask;
        }

        public bool SavedHidePublicProfile { get; set; }
        public Task<bool> IsPublicProfileHiddenAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(SavedHidePublicProfile);
        public Task SetPublicProfileHiddenAsync(string userId, bool hide, CancellationToken ct = default)
        {
            SavedHidePublicProfile = hide;
            return Task.CompletedTask;
        }

        public Task SetLeaderboardPreferencesAsync(string userId, bool optIn, bool anonymous, string? pseudonym = null, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GetEffectiveCountryAsync_UsesSavedPreferenceWhenAuthenticated()
    {
        var currentUserService = new FakeCurrentUserService();
        var preferenceService = new FakeUserPreferenceService { SavedCountry = "Argentina" };
        var service = new UserLocationService(preferenceService, currentUserService);

        var country = await service.GetEffectiveCountryAsync();

        Assert.Equal("Argentina", country);
        Assert.Equal("Argentina", service.CurrentCountry);
    }

    [Fact]
    public async Task GetEffectiveCountryAsync_FallsBackToSessionOrDetectedWhenNoSavedPreference()
    {
        var service = new UserLocationService();
        service.SetDetectedCountry("Chile");

        var country = await service.GetEffectiveCountryAsync();
        Assert.Equal("Chile", country);

        service.SetUserCountry("Perú");
        var countryAfterUserChoice = await service.GetEffectiveCountryAsync();
        Assert.Equal("Perú", countryAfterUserChoice);
    }

    private record TestItem(string Title, string Country);

    [Fact]
    public void PrioritizeByCountry_OrdersLocalFirstThenInternationalThenOthers()
    {
        var service = new UserLocationService();
        service.SetUserCountry("España");

        var items = new List<TestItem>
        {
            new("Tienda Argentina", "Argentina"),
            new("Tienda España Local", "España"),
            new("Tienda Internacional", "Internacional"),
            new("Tienda México", "México"),
            new("Tienda España 2", "España")
        };

        var prioritized = service.PrioritizeByCountry(items, i => i.Country).ToList();

        // Primeros deben ser España
        Assert.Equal("España", prioritized[0].Country);
        Assert.Equal("España", prioritized[1].Country);

        // Segundo grupo debe ser Internacional
        Assert.Equal("Internacional", prioritized[2].Country);

        // Resto de países al final
        Assert.Contains(prioritized.Skip(3), i => i.Country == "Argentina");
        Assert.Contains(prioritized.Skip(3), i => i.Country == "México");
    }

    [Fact]
    public void PrioritizeByCountry_ReturnsOriginalCollectionWhenNoTargetCountry()
    {
        var service = new UserLocationService(); // Sin país establecido

        var items = new List<TestItem>
        {
            new("Item 1", "Chile"),
            new("Item 2", "México"),
            new("Item 3", "España")
        };

        var result = service.PrioritizeByCountry(items, i => i.Country).ToList();

        Assert.Equal(items.Count, result.Count);
        Assert.Equal("Item 1", result[0].Title);
        Assert.Equal("Item 2", result[1].Title);
        Assert.Equal("Item 3", result[2].Title);
    }
}
