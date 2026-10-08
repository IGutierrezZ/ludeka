using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Community;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class WeeklyReleaseServiceTests
{
    private class FakeWeeklyReleaseRepository : IWeeklyReleaseRepository
    {
        public List<WeeklyRelease> Items { get; } = new();

        public Task<IReadOnlyList<WeeklyRelease>> GetReleasesAsync(DateOnly? fromDate = null, CancellationToken ct = default)
        {
            var query = Items.AsEnumerable();
            if (fromDate.HasValue)
            {
                query = query.Where(r => r.ReleaseDate >= fromDate.Value);
            }
            return Task.FromResult<IReadOnlyList<WeeklyRelease>>(query.OrderBy(r => r.ReleaseDate).ToList());
        }

        public Task<WeeklyRelease?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(Items.FirstOrDefault(r => r.Id == id));
        }

        public Task AddAsync(WeeklyRelease release, CancellationToken ct = default)
        {
            Items.Add(release);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(WeeklyRelease release, CancellationToken ct = default)
        {
            var idx = Items.FindIndex(r => r.Id == release.Id);
            if (idx >= 0) Items[idx] = release;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Items.RemoveAll(r => r.Id == id);
            return Task.CompletedTask;
        }
    }

    private class FakePermissionGuard : ISessionPermissionGuard
    {
        private readonly bool _allow;

        public FakePermissionGuard(bool allow = true)
        {
            _allow = allow;
        }

        public Task RequireAsync(ModeratorPermission permission, string? failureMessage = null, CancellationToken ct = default)
        {
            if (!_allow)
                throw new UnauthorizedAccessException(failureMessage ?? "Acceso denegado.");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsMappedDto()
    {
        var repo = new FakeWeeklyReleaseRepository();
        var release = new WeeklyRelease(
            title: "Cascadia Rolling",
            publisher: "Delirium Games",
            releaseDate: new DateOnly(2026, 10, 15),
            coverImageUrl: "https://example.com/cascadia.webp",
            estimatedPvp: 24.95m,
            isReprint: false,
            notes: "Edición especial",
            sourceUrl: "https://deliriumgames.com/cascadia");
        repo.Items.Add(release);

        var service = new WeeklyReleaseService(repo);
        var result = await service.GetByIdAsync(release.Id);

        Assert.NotNull(result);
        Assert.Equal("Cascadia Rolling", result.Title);
        Assert.Equal("Delirium Games", result.Publisher);
        Assert.Equal("https://deliriumgames.com/cascadia", result.SourceUrl);
        Assert.Equal(24.95m, result.EstimatedPvp);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        var repo = new FakeWeeklyReleaseRepository();
        var service = new WeeklyReleaseService(repo);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateReleaseAsync_WithSourceUrl_PersistsAndReturnsSourceUrl()
    {
        var repo = new FakeWeeklyReleaseRepository();
        var guard = new FakePermissionGuard(allow: true);
        var service = new WeeklyReleaseService(repo, guard);

        var request = new CreateWeeklyReleaseRequest(
            Title: "Apiary",
            Publisher: "Devir",
            ReleaseDate: new DateOnly(2026, 11, 1),
            EstimatedPvp: 65.00m,
            SourceUrl: "https://devir.es/apiary");

        var created = await service.CreateReleaseAsync(request);

        Assert.NotNull(created);
        Assert.Equal("https://devir.es/apiary", created.SourceUrl);
        Assert.Single(repo.Items);
        Assert.Equal("https://devir.es/apiary", repo.Items[0].SourceUrl);
    }

    [Fact]
    public async Task UpdateReleaseAsync_WithPermission_UpdatesEntityAndReturnsDto()
    {
        var repo = new FakeWeeklyReleaseRepository();
        var existing = new WeeklyRelease(
            title: "Original Title",
            publisher: "Original Publisher",
            releaseDate: new DateOnly(2026, 10, 1));
        repo.Items.Add(existing);

        var guard = new FakePermissionGuard(allow: true);
        var service = new WeeklyReleaseService(repo, guard);

        var updateReq = new UpdateWeeklyReleaseRequest(
            Id: existing.Id,
            Title: "Updated Title",
            Publisher: "Updated Publisher",
            ReleaseDate: new DateOnly(2026, 10, 20),
            CoverImageUrl: "https://example.com/updated.webp",
            EstimatedPvp: 49.99m,
            IsReprint: true,
            Notes: "Reimpresión revisada",
            SourceUrl: "https://publisher.com/updated");

        var updated = await service.UpdateReleaseAsync(existing.Id, updateReq);

        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal("Updated Publisher", updated.Publisher);
        Assert.True(updated.IsReprint);
        Assert.Equal("https://publisher.com/updated", updated.SourceUrl);
        Assert.Equal(49.99m, updated.EstimatedPvp);
        Assert.Equal(existing.Id, updated.Id);
    }

    [Fact]
    public async Task UpdateReleaseAsync_WhenNotFound_ThrowsKeyNotFoundException()
    {
        var repo = new FakeWeeklyReleaseRepository();
        var guard = new FakePermissionGuard(allow: true);
        var service = new WeeklyReleaseService(repo, guard);

        var nonExistentId = Guid.NewGuid();
        var updateReq = new UpdateWeeklyReleaseRequest(
            Id: nonExistentId,
            Title: "Title",
            Publisher: "Publisher",
            ReleaseDate: new DateOnly(2026, 10, 1));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateReleaseAsync(nonExistentId, updateReq));
    }

    [Fact]
    public async Task UpdateReleaseAsync_WithoutPermission_ThrowsUnauthorizedAccessException()
    {
        var repo = new FakeWeeklyReleaseRepository();
        var existing = new WeeklyRelease("Test", "Publisher", new DateOnly(2026, 10, 1));
        repo.Items.Add(existing);

        var guard = new FakePermissionGuard(allow: false);
        var service = new WeeklyReleaseService(repo, guard);

        var updateReq = new UpdateWeeklyReleaseRequest(
            Id: existing.Id,
            Title: "Hacked",
            Publisher: "Hacker",
            ReleaseDate: new DateOnly(2026, 10, 1));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateReleaseAsync(existing.Id, updateReq));
    }

    [Fact]
    public async Task DeleteReleaseAsync_WithPermission_RemovesFromRepository()
    {
        var repo = new FakeWeeklyReleaseRepository();
        var release = new WeeklyRelease("To Delete", "Publisher", new DateOnly(2026, 10, 1));
        repo.Items.Add(release);

        var guard = new FakePermissionGuard(allow: true);
        var service = new WeeklyReleaseService(repo, guard);

        await service.DeleteReleaseAsync(release.Id);

        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task DeleteReleaseAsync_WithoutPermission_ThrowsUnauthorizedAccessException()
    {
        var repo = new FakeWeeklyReleaseRepository();
        var release = new WeeklyRelease("To Delete", "Publisher", new DateOnly(2026, 10, 1));
        repo.Items.Add(release);

        var guard = new FakePermissionGuard(allow: false);
        var service = new WeeklyReleaseService(repo, guard);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteReleaseAsync(release.Id));
        Assert.Single(repo.Items);
    }

    [Fact]
    public async Task GetReleasesAsync_FiltersOnlyPublishedReleases()
    {
        var repo = new FakeWeeklyReleaseRepository();
        var published = new WeeklyRelease("Publicado", "Devir", new DateOnly(2026, 10, 1));
        var pending = new WeeklyRelease("En Espera", "Maldito", new DateOnly(2026, 10, 2));
        pending.SetPendingModeration(367209, "Galactic Cruise", "Inferencia IA");

        repo.Items.Add(published);
        repo.Items.Add(pending);

        var service = new WeeklyReleaseService(repo);
        var results = await service.GetReleasesAsync();

        Assert.Single(results);
        Assert.Equal("Publicado", results[0].Title);
        Assert.Equal(WeeklyReleaseStatus.Published, results[0].Status);
    }

    [Fact]
    public async Task GetPendingModerationReleasesAsync_WithPermission_ReturnsOnlyPending()
    {
        var repo = new FakeWeeklyReleaseRepository();
        var published = new WeeklyRelease("Publicado", "Devir", new DateOnly(2026, 10, 1));
        var pending = new WeeklyRelease("En Espera", "Maldito", new DateOnly(2026, 10, 2));
        pending.SetPendingModeration(367209, "Galactic Cruise", "Inferencia IA");

        repo.Items.Add(published);
        repo.Items.Add(pending);

        var guard = new FakePermissionGuard(allow: true);
        var service = new WeeklyReleaseService(repo, guard);

        var results = await service.GetPendingModerationReleasesAsync();

        Assert.Single(results);
        Assert.Equal("En Espera", results[0].Title);
        Assert.Equal(WeeklyReleaseStatus.PendingModeration, results[0].Status);
        Assert.Equal(367209, results[0].AiSuggestedBggId);
        Assert.Equal("Galactic Cruise", results[0].AiSuggestedTitle);
    }

    [Fact]
    public async Task ApproveReleaseAsync_WithLinkedGameId_ApprovesAndLinksGame()
    {
        var repo = new FakeWeeklyReleaseRepository();
        var pending = new WeeklyRelease("Crucero Galáctico", "Maldito", new DateOnly(2026, 10, 2));
        pending.SetPendingModeration(367209, "Galactic Cruise", "Inferencia IA");
        repo.Items.Add(pending);

        var gameId = Guid.NewGuid();
        var guard = new FakePermissionGuard(allow: true);
        var service = new WeeklyReleaseService(repo, guard);

        var approved = await service.ApproveReleaseAsync(pending.Id, linkedGameId: gameId);

        Assert.Equal(WeeklyReleaseStatus.Published, approved.Status);
        Assert.Equal(gameId, approved.GameId);
        Assert.Equal(WeeklyReleaseStatus.Published, repo.Items[0].Status);
        Assert.Equal(gameId, repo.Items[0].GameId);
    }

    [Fact]
    public async Task ApproveReleaseAsync_WithAiSuggestion_WhenLocalGameExists_LinksLocalGame()
    {
        var repo = new FakeWeeklyReleaseRepository();
        var pending = new WeeklyRelease("Crucero Galáctico", "Maldito", new DateOnly(2026, 10, 2));
        pending.SetPendingModeration(367209, "Galactic Cruise", "Inferencia IA");
        repo.Items.Add(pending);

        var gameRepo = new FakeGameRepository();
        var localGame = new Game(
            bggId: 367209,
            originalTitle: "Galactic Cruise",
            spanishTitle: "Crucero Galáctico",
            designer: "T.K. King",
            publisher: "Maldito Games",
            yearPublished: 2024,
            coverImageUrl: "https://example.com/gc.webp",
            thumbnailUrl: null,
            description: null,
            bggRating: 8.0,
            bggRank: 50,
            ludistRating: 8.2,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(14, 14),
            language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(120, 120, 120));
        gameRepo.Games.Add(localGame);

        var guard = new FakePermissionGuard(allow: true);
        var service = new WeeklyReleaseService(repo, guard, gameRepository: gameRepo);

        var approved = await service.ApproveReleaseAsync(pending.Id, linkedGameId: null, useAiSuggestionIfAvailable: true);

        Assert.Equal(WeeklyReleaseStatus.Published, approved.Status);
        Assert.Equal(localGame.Id, approved.GameId);
    }

    [Fact]
    public async Task ApproveReleaseAsync_WithoutAiSuggestionFlag_ApprovesWithoutGame()
    {
        var repo = new FakeWeeklyReleaseRepository();
        var pending = new WeeklyRelease("Maldito Juego", "Maldito", new DateOnly(2026, 10, 2));
        pending.SetPendingModeration(367209, "Galactic Cruise", "Inferencia IA");
        repo.Items.Add(pending);

        var guard = new FakePermissionGuard(allow: true);
        var service = new WeeklyReleaseService(repo, guard);

        var approved = await service.ApproveReleaseAsync(pending.Id, linkedGameId: null, useAiSuggestionIfAvailable: false);

        Assert.Equal(WeeklyReleaseStatus.Published, approved.Status);
        Assert.Null(approved.GameId);
    }

    [Fact]
    public async Task RejectReleaseAsync_WithPermission_SetsStatusToRejected()
    {
        var repo = new FakeWeeklyReleaseRepository();
        var pending = new WeeklyRelease("Juego Erróneo", "Maldito", new DateOnly(2026, 10, 2));
        pending.SetPendingModeration(null, null, null);
        repo.Items.Add(pending);

        var guard = new FakePermissionGuard(allow: true);
        var service = new WeeklyReleaseService(repo, guard);

        await service.RejectReleaseAsync(pending.Id);

        Assert.Equal(WeeklyReleaseStatus.Rejected, repo.Items[0].Status);
    }

    private class FakeGameRepository : IGameRepository
    {
        public List<Game> Games { get; } = new();

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Games.FirstOrDefault(g => g.Id == id));

        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default)
            => Task.FromResult(Games.FirstOrDefault(g => g.Slug == slug));

        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(Games.FirstOrDefault(g => g.BggId == bggId));

        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<Game>, int)>((Games, Games.Count));

        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        {
            Games.AddRange(games);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Game game, CancellationToken ct = default)
        {
            var idx = Games.FindIndex(g => g.Id == game.Id);
            if (idx >= 0) Games[idx] = game;
            return Task.CompletedTask;
        }

        public Task<bool> HasAnyAsync(CancellationToken ct = default)
            => Task.FromResult(Games.Count > 0);
    }
}
