using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Community;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class GiveawayServiceTests
{
    private class FakeGiveawayRepository : IGiveawayRepository
    {
        public List<Giveaway> Items { get; } = new();

        public Task<IReadOnlyList<Giveaway>> GetGiveawaysAsync(bool includeExpired = false, CancellationToken ct = default)
        {
            var result = includeExpired
                ? (IReadOnlyList<Giveaway>)Items
                : Items.FindAll(g => !g.IsExpired);
            return Task.FromResult(result);
        }

        public Task<Giveaway?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(Items.Find(g => g.Id == id));
        }

        public Task<Giveaway?> FindDuplicateOrCollaborativeAsync(string title, string organizer, DateTimeOffset deadline, CancellationToken ct = default)
        {
            var found = Items.Find(g =>
                g.Title.Equals(title, StringComparison.OrdinalIgnoreCase) &&
                (g.Organizer.Equals(organizer, StringComparison.OrdinalIgnoreCase) ||
                 (g.Collaborator != null && g.Collaborator.Equals(organizer, StringComparison.OrdinalIgnoreCase))));
            return Task.FromResult(found);
        }

        public Task AddAsync(Giveaway giveaway, CancellationToken ct = default)
        {
            Items.Add(giveaway);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Giveaway giveaway, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Items.RemoveAll(g => g.Id == id);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task CreateOrMergeGiveawayAsync_NewGiveaway_AddsToRepository()
    {
        // Arrange
        var repo = new FakeGiveawayRepository();
        var service = new GiveawayService(repo);

        var request = new CreateGiveawayRequest(
            Title: "Sorteo Ark Nova",
            Organizer: "Maldito Games",
            Collaborator: null,
            Url: "https://malditogames.com/sorteo",
            Platform: GiveawayPlatform.Instagram,
            DeadlineAt: DateTimeOffset.UtcNow.AddDays(5));

        // Act
        var result = await service.CreateOrMergeGiveawayAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Sorteo Ark Nova", result.Title);
        Assert.Single(repo.Items);
    }

    [Fact]
    public async Task CreateOrMergeGiveawayAsync_ExistingGiveaway_MergesCollaborator()
    {
        // Arrange
        var repo = new FakeGiveawayRepository();
        var existing = new Giveaway(
            title: "Sorteo Ark Nova",
            organizer: "Maldito Games",
            url: "https://malditogames.com/sorteo",
            platform: GiveawayPlatform.Instagram,
            deadlineAt: DateTimeOffset.UtcNow.AddDays(5));
        repo.Items.Add(existing);

        var service = new GiveawayService(repo);

        var request = new CreateGiveawayRequest(
            Title: "Sorteo Ark Nova",
            Organizer: "Maldito Games",
            Collaborator: "Análisis Parálisis",
            Url: "https://malditogames.com/sorteo",
            Platform: GiveawayPlatform.Instagram,
            DeadlineAt: DateTimeOffset.UtcNow.AddDays(5));

        // Act
        var result = await service.CreateOrMergeGiveawayAsync(request);

        // Assert
        Assert.Single(repo.Items);
        Assert.Equal("Análisis Parálisis", result.Collaborator);
        Assert.Equal("Maldito Games en colaboración con Análisis Parálisis", result.FormattedOrganizer);
    }

    [Fact]
    public async Task GetGiveawaysAsync_FiltersExpiredByDefault()
    {
        // Arrange
        var repo = new FakeGiveawayRepository();
        var active = new Giveaway("Sorteo Activo", "Devir", "https://devir.es", GiveawayPlatform.Instagram, DateTimeOffset.UtcNow.AddDays(2));
        var expired = new Giveaway("Sorteo Pasado", "Devir", "https://devir.es", GiveawayPlatform.Instagram, DateTimeOffset.UtcNow.AddDays(-1));
        repo.Items.Add(active);
        repo.Items.Add(expired);

        var service = new GiveawayService(repo);

        // Act
        var activeOnly = await service.GetGiveawaysAsync(includeExpired: false);
        var all = await service.GetGiveawaysAsync(includeExpired: true);

        // Assert
        Assert.Single(activeOnly);
        Assert.Equal("Sorteo Activo", activeOnly[0].Title);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task UpdateGiveawayAsync_ExistingGiveaway_UpdatesAllFields()
    {
        // Arrange
        var repo = new FakeGiveawayRepository();
        var giveaway = new Giveaway("Original Title", "Original Organizer", "https://original.com", GiveawayPlatform.Instagram, DateTimeOffset.UtcNow.AddDays(2));
        repo.Items.Add(giveaway);
        var service = new GiveawayService(repo);

        var newDeadline = DateTimeOffset.UtcNow.AddDays(10);
        var request = new UpdateGiveawayRequest(
            Id: giveaway.Id,
            Title: "Updated Title",
            Organizer: "Updated Organizer",
            Collaborator: "Updated Collaborator",
            Url: "https://updated.com",
            Platform: GiveawayPlatform.YouTube,
            DeadlineAt: newDeadline,
            Country: "Francia",
            GameId: Guid.NewGuid(),
            GameTitle: "Brass Birmingham",
            ThumbnailUrl: "https://pub-r2/new-thumb.webp",
            IsCommunityExclusive: true,
            IsPromoted: true);

        // Act
        var result = await service.UpdateGiveawayAsync(request);

        // Assert
        Assert.Equal("Updated Title", result.Title);
        Assert.Equal("Updated Organizer", result.Organizer);
        Assert.Equal("Updated Collaborator", result.Collaborator);
        Assert.Equal("https://updated.com", result.Url);
        Assert.Equal(GiveawayPlatform.YouTube, result.Platform);
        Assert.Equal("Francia", result.Country);
        Assert.Equal("Brass Birmingham", result.GameTitle);
        Assert.Equal("https://pub-r2/new-thumb.webp", result.ThumbnailUrl);
        Assert.True(result.IsCommunityExclusive);
        Assert.True(result.IsPromoted);
    }

    [Fact]
    public async Task UpdateGiveawayAsync_NonExistingGiveaway_ThrowsKeyNotFoundException()
    {
        // Arrange
        var repo = new FakeGiveawayRepository();
        var service = new GiveawayService(repo);
        var request = new UpdateGiveawayRequest(
            Id: Guid.NewGuid(),
            Title: "Title",
            Organizer: "Organizer",
            Collaborator: null,
            Url: "https://test.com",
            Platform: GiveawayPlatform.Instagram,
            DeadlineAt: DateTimeOffset.UtcNow.AddDays(1));

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateGiveawayAsync(request));
    }

    [Fact]
    public async Task DeleteGiveawayAsync_ExistingGiveaway_RemovesFromRepository()
    {
        // Arrange
        var repo = new FakeGiveawayRepository();
        var giveaway = new Giveaway("To Delete", "Organizer", "https://test.com", GiveawayPlatform.Instagram, DateTimeOffset.UtcNow.AddDays(2));
        repo.Items.Add(giveaway);
        var service = new GiveawayService(repo);

        // Act
        await service.DeleteGiveawayAsync(giveaway.Id);

        // Assert
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task DeleteGiveawayAsync_NonExistingGiveaway_ThrowsKeyNotFoundException()
    {
        // Arrange
        var repo = new FakeGiveawayRepository();
        var service = new GiveawayService(repo);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteGiveawayAsync(Guid.NewGuid()));
    }
}
