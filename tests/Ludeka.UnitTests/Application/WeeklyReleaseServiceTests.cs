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
}
