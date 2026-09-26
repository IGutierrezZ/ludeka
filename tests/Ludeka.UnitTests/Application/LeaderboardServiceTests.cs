using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Gamification;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class LeaderboardServiceTests
{
    private sealed class FakeLeaderboardRepository : ILeaderboardRepository
    {
        public List<UserMonthlyPlaysAggregate> Aggregates { get; set; } = [];
        public DateTimeOffset? CapturedStart { get; private set; }
        public DateTimeOffset? CapturedEnd { get; private set; }

        public Task<List<UserMonthlyPlaysAggregate>> GetMonthlyPlaysAsync(
            DateTimeOffset periodStart,
            DateTimeOffset periodEnd,
            CancellationToken ct = default)
        {
            CapturedStart = periodStart;
            CapturedEnd = periodEnd;
            return Task.FromResult(Aggregates);
        }
    }

    private sealed class FakeUserPreferenceService : IUserPreferenceService
    {
        public Dictionary<string, UserPreferenceDto> Preferences { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<UserPreferenceDto> GetUserPreferenceAsync(string userId, CancellationToken ct = default)
        {
            if (Preferences.TryGetValue(userId, out var pref))
            {
                return Task.FromResult(pref);
            }

            return Task.FromResult(new UserPreferenceDto(userId, "charcoal", DateTime.UtcNow, null, false, false, false, null));
        }

        public Task SetLeaderboardPreferencesAsync(string userId, bool optIn, bool anonymous, string? pseudonym = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("Sesión requerida.");
            }

            var current = Preferences.TryGetValue(userId, out var p) ? p : new UserPreferenceDto(userId, "charcoal", DateTime.UtcNow);
            Preferences[userId] = current with
            {
                LeaderboardOptIn = optIn,
                LeaderboardAnonymous = anonymous,
                LeaderboardPseudonym = pseudonym
            };
            return Task.CompletedTask;
        }

        public Task<string?> GetUserCountryAsync(string userId, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task SetUserCountryAsync(string userId, string? country, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string> GetUserThemeAsync(string userId, CancellationToken ct = default) => Task.FromResult("charcoal");
        public Task SetUserThemeAsync(string userId, string theme, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsPublicProfileHiddenAsync(string userId, CancellationToken ct = default) => Task.FromResult(false);
        public Task SetPublicProfileHiddenAsync(string userId, bool hide, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeMilestoneRepository : IUserMilestoneRepository
    {
        public Dictionary<string, List<UserMilestone>> Milestones { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<List<UserMilestone>> GetByUserIdAsync(string userId, CancellationToken ct = default)
        {
            return Task.FromResult(Milestones.TryGetValue(userId, out var list) ? list : []);
        }

        public Task<bool> HasMilestoneAsync(string userId, MilestoneType type, CancellationToken ct = default)
        {
            return Task.FromResult(Milestones.TryGetValue(userId, out var list) && list.Any(m => m.Type == type));
        }

        public Task<bool> UnlockMilestoneAsync(UserMilestone milestone, CancellationToken ct = default) => Task.FromResult(true);
        public Task<int> UnlockMilestonesAsync(IEnumerable<UserMilestone> milestones, CancellationToken ct = default) => Task.FromResult(milestones.Count());
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public Dictionary<string, AppUser> Users { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<AppUser?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            return Task.FromResult(Users.TryGetValue(id, out var u) ? u : null);
        }

        public Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult<AppUser?>(null);
        public Task<IReadOnlyList<AppUser>> GetAllAsync(string? search = null, UserRole? role = null, UserStatus? status = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<AppUser>>([]);
        public Task AddAsync(AppUser user, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(AppUser user, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public IReadOnlyList<string> Roles => [];
        public bool IsFoundingTeam => false;
        public bool IsInRole(string role) => false;
        public bool HasPermission(ModeratorPermission permission) => false;
    }

    [Fact]
    public async Task GetMonthlyLeaderboardAsync_WhenUserHasNoOptIn_ExcludesUserCompletely()
    {
        // Arrange (Criterio 1: Test de exclusión sin consentimiento)
        var repo = new FakeLeaderboardRepository
        {
            Aggregates =
            [
                new UserMonthlyPlaysAggregate("user-sin-optin", 15, DateTimeOffset.UtcNow.AddDays(-2)),
                new UserMonthlyPlaysAggregate("user-con-optin", 5, DateTimeOffset.UtcNow.AddDays(-1))
            ]
        };

        var prefService = new FakeUserPreferenceService();
        prefService.Preferences["user-sin-optin"] = new UserPreferenceDto("user-sin-optin", "charcoal", DateTime.UtcNow, LeaderboardOptIn: false);
        prefService.Preferences["user-con-optin"] = new UserPreferenceDto("user-con-optin", "charcoal", DateTime.UtcNow, LeaderboardOptIn: true);

        var service = new LeaderboardService(
            repo,
            prefService,
            new FakeMilestoneRepository(),
            new FakeUserRepository(),
            new FakeCurrentUserService());

        // Act
        var leaderboard = await service.GetMonthlyLeaderboardAsync(2026, 9);

        // Assert
        Assert.Single(leaderboard.Entries);
        Assert.DoesNotContain(leaderboard.Entries, e => e.DisplayName.Contains("user-sin-optin"));
        Assert.Equal(5, leaderboard.Entries[0].TotalPlaysThisMonth);
    }

    [Fact]
    public async Task GetMonthlyLeaderboardAsync_WhenAnonymous_NeverDerivesOrExposesPii()
    {
        // Arrange (Criterio 2: Test de no-derivación y anonimato)
        string email = "patricia.mendez@empresa.com";
        var repo = new FakeLeaderboardRepository
        {
            Aggregates = [new UserMonthlyPlaysAggregate(email, 12, DateTimeOffset.UtcNow)]
        };

        var prefService = new FakeUserPreferenceService();
        prefService.Preferences[email] = new UserPreferenceDto(
            email,
            "charcoal",
            DateTime.UtcNow,
            Country: "España",
            HidePublicProfile: false,
            LeaderboardOptIn: true,
            LeaderboardAnonymous: true,
            LeaderboardPseudonym: null);

        var userRepo = new FakeUserRepository();
        userRepo.Users[email] = new AppUser(email, "Patricia Méndez", email);

        var service = new LeaderboardService(
            repo,
            prefService,
            new FakeMilestoneRepository(),
            userRepo,
            new FakeCurrentUserService());

        // Act
        var leaderboard = await service.GetMonthlyLeaderboardAsync(2026, 9);

        // Assert
        var entry = Assert.Single(leaderboard.Entries);
        Assert.True(entry.IsAnonymous);
        Assert.Null(entry.PublicProfileUrl); // No expone enlace al perfil
        Assert.Null(entry.Country); // No expone geolocalización
        Assert.StartsWith("Mesa #", entry.DisplayName);
        Assert.DoesNotContain("@", entry.DisplayName);
        Assert.DoesNotContain("patricia", entry.DisplayName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mendez", entry.DisplayName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("empresa", entry.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetMonthlyLeaderboardAsync_CalculatesCorrectDateWindow()
    {
        // Arrange (Criterio 3: Ventana temporal mensual declarada)
        var repo = new FakeLeaderboardRepository();
        var service = new LeaderboardService(
            repo,
            new FakeUserPreferenceService(),
            new FakeMilestoneRepository(),
            new FakeUserRepository(),
            new FakeCurrentUserService());

        // Act
        var result = await service.GetMonthlyLeaderboardAsync(2026, 9);

        // Assert
        Assert.Equal(2026, result.Year);
        Assert.Equal(9, result.Month);
        Assert.Equal("Septiembre 2026", result.MonthLabel);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), repo.CapturedStart);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), repo.CapturedEnd);
    }

    [Fact]
    public async Task GetMonthlyLeaderboardAsync_OrdersByPlaysDescendingWithDeterministicTieBreak()
    {
        // Arrange (Criterio 4: Idempotencia y ordenación determinista)
        var date1 = new DateTimeOffset(2026, 9, 2, 10, 0, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

        var repo = new FakeLeaderboardRepository
        {
            Aggregates =
            [
                new UserMonthlyPlaysAggregate("user-a", 10, date2),
                new UserMonthlyPlaysAggregate("user-b", 20, date1),
                new UserMonthlyPlaysAggregate("user-c", 10, date1) // Mismo número que A, pero antes
            ]
        };

        var prefService = new FakeUserPreferenceService();
        prefService.Preferences["user-a"] = new UserPreferenceDto("user-a", "charcoal", DateTime.UtcNow, LeaderboardOptIn: true);
        prefService.Preferences["user-b"] = new UserPreferenceDto("user-b", "charcoal", DateTime.UtcNow, LeaderboardOptIn: true);
        prefService.Preferences["user-c"] = new UserPreferenceDto("user-c", "charcoal", DateTime.UtcNow, LeaderboardOptIn: true);

        var service = new LeaderboardService(
            repo,
            prefService,
            new FakeMilestoneRepository(),
            new FakeUserRepository(),
            new FakeCurrentUserService());

        // Act
        var result = await service.GetMonthlyLeaderboardAsync(2026, 9);

        // Assert
        Assert.Equal(3, result.Entries.Count);
        Assert.Equal("user-b", result.Entries[0].DisplayName);
        Assert.Equal(1, result.Entries[0].Rank);
        Assert.Equal(20, result.Entries[0].TotalPlaysThisMonth);

        // Empate a 10 partidas: user-c jugó antes que user-a
        Assert.Equal("user-c", result.Entries[1].DisplayName);
        Assert.Equal(2, result.Entries[1].Rank);

        Assert.Equal("user-a", result.Entries[2].DisplayName);
        Assert.Equal(3, result.Entries[2].Rank);
    }

    [Fact]
    public async Task GetMonthlyLeaderboardAsync_EnrichesWithMilestonesCountAndBadge()
    {
        // Arrange (Integración de condecoración INC-67)
        var repo = new FakeLeaderboardRepository
        {
            Aggregates = [new UserMonthlyPlaysAggregate("user-pro", 8, DateTimeOffset.UtcNow)]
        };

        var prefService = new FakeUserPreferenceService();
        prefService.Preferences["user-pro"] = new UserPreferenceDto("user-pro", "charcoal", DateTime.UtcNow, LeaderboardOptIn: true);

        var milestoneRepo = new FakeMilestoneRepository();
        milestoneRepo.Milestones["user-pro"] =
        [
            new UserMilestone("user-pro", MilestoneType.FirstGameInCollection),
            new UserMilestone("user-pro", MilestoneType.FirstGamePlayed),
            new UserMilestone("user-pro", MilestoneType.FirstPlayLogged)
        ];

        var service = new LeaderboardService(
            repo,
            prefService,
            milestoneRepo,
            new FakeUserRepository(),
            new FakeCurrentUserService());

        // Act
        var result = await service.GetMonthlyLeaderboardAsync(2026, 9);

        // Assert
        var entry = Assert.Single(result.Entries);
        Assert.Equal(3, entry.UnlockedMilestonesCount);
        Assert.NotNull(entry.BadgeName);
    }

    [Fact]
    public async Task SetUserParticipationAsync_WithoutSession_ThrowsUnauthorized()
    {
        var service = new LeaderboardService(
            new FakeLeaderboardRepository(),
            new FakeUserPreferenceService(),
            new FakeMilestoneRepository(),
            new FakeUserRepository(),
            new FakeCurrentUserService());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.SetUserParticipationAsync(string.Empty, true, false));
    }

    [Fact]
    public async Task SetUserParticipationAsync_WithSession_PersistsAndReturnsUpdatedParticipation()
    {
        var prefService = new FakeUserPreferenceService();
        var service = new LeaderboardService(
            new FakeLeaderboardRepository(),
            prefService,
            new FakeMilestoneRepository(),
            new FakeUserRepository(),
            new FakeCurrentUserService());

        await service.SetUserParticipationAsync("jugador-real", optIn: true, anonymous: true, customPseudonym: "Estratega");

        var status = await service.GetUserParticipationAsync("jugador-real");
        Assert.True(status.OptIn);
        Assert.True(status.Anonymous);
        Assert.Equal("Estratega", status.CustomPseudonym);
        Assert.Equal("Estratega", status.EffectivePseudonym);
    }
}
