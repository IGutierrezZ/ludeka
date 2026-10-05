using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Discovery;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class NewsGameExtractorTests
{
    [Theory]
    [InlineData("Devir anuncia la edición en castellano de Apiary para este otoño.", "Apiary")]
    [InlineData("Maldito Games publicará Ark Nova en castellano este año", "Ark Nova")]
    [InlineData("Devir anuncia Wingspan", "Wingspan")]
    [InlineData("Lanzamiento de Cascadia en tiendas especializadas", "Cascadia")]
    [InlineData("Asmodee: Catan", "Catan")]
    [InlineData("Nueva edición de «Harmonies» confirmada", "Harmonies")]
    [InlineData("Reimpresión de Dune: Imperium confirmada", "Dune: Imperium")]
    [InlineData("Preventa de Heat: Pedal to the Metal abierta", "Heat: Pedal to the Metal")]
    public void ExtractGameTitle_WithValidNewsPatterns_ExtractsCleanGameTitle(string input, string expectedTitle)
    {
        // Arrange
        var extractor = CreateExtractor(out _, out _, out _, out _);

        // Act
        var result = extractor.ExtractGameTitle(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedTitle, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Novedad")]
    [InlineData("Lanzamiento")]
    public void ExtractGameTitle_WithInvalidOrStopWords_ReturnsNull(string? invalidInput)
    {
        // Arrange
        var extractor = CreateExtractor(out _, out _, out _, out _);

        // Act
        var result = extractor.ExtractGameTitle(invalidInput!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessReleaseAsync_WhenGameAlreadyExistsInCatalog_LinksDirectlyWithoutEnqueuing()
    {
        // Arrange
        var extractor = CreateExtractor(out var gameRepo, out var bggClient, out var pendingRepo, out var releaseRepo);

        var existingGame = new Game(
            bggId: 266192,
            originalTitle: "Wingspan",
            spanishTitle: "Wingspan",
            designer: "Elizabeth Hargrave",
            publisher: "Stonemaier Games",
            yearPublished: 2019,
            coverImageUrl: "https://example.com/wingspan.jpg",
            thumbnailUrl: null,
            description: "Juego de aves",
            bggRating: 8.1,
            bggRank: 30,
            ludistRating: 8.1,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new AgeRating(10, 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(40, 70, 20)
        );
        gameRepo.Games.Add(existingGame);

        var release = new WeeklyRelease("Devir anuncia la reimpresión de Wingspan", "Devir", new DateOnly(2026, 9, 20));
        releaseRepo.Releases.Add(release);

        // Act
        var result = await extractor.ProcessReleaseAsync(release);

        // Assert
        Assert.True(result.LinkedToExistingGame);
        Assert.Equal(existingGame.Id, result.ExistingGameId);
        Assert.Equal(existingGame.Id, release.GameId);
        Assert.False(result.EnqueuedToBgg);
        Assert.Empty(pendingRepo.Items);
    }

    [Fact]
    public async Task ProcessReleaseAsync_WhenGameNotInCatalogButFoundInBgg_EnqueuesWithNewsDiscoveryOrigin()
    {
        // Arrange
        var extractor = CreateExtractor(out _, out var bggClient, out var pendingRepo, out var releaseRepo);

        bggClient.SearchResults.Add(new BggSearchResultDto(
            BggId: 370132,
            Title: "Apiary",
            YearPublished: 2023
        ));

        var release = new WeeklyRelease("Devir anuncia la edición en castellano de Apiary", "Devir", new DateOnly(2026, 9, 25));
        releaseRepo.Releases.Add(release);

        // Act
        var result = await extractor.ProcessReleaseAsync(release);

        // Assert
        Assert.False(result.LinkedToExistingGame);
        Assert.True(result.EnqueuedToBgg);
        Assert.Equal(370132, result.EnqueuedBggId);
        Assert.Null(release.GameId); // Aún no está en catálogo

        var enqueued = Assert.Single(pendingRepo.Items);
        Assert.Equal(370132, enqueued.BggId);
        Assert.Equal("Apiary", enqueued.Title);
        Assert.Equal(CatalogQueueOrigin.NewsDiscovery, enqueued.Origin);
        Assert.Equal("Apiary", enqueued.ExtractedTitle);
    }

    [Fact]
    public async Task ProcessReleaseAsync_WhenGameAlreadyEnqueued_IncrementsRequestCount()
    {
        // Arrange
        var extractor = CreateExtractor(out _, out var bggClient, out var pendingRepo, out var releaseRepo);

        var existingItem = new PendingBggImport(
            bggId: 370132,
            title: "Apiary",
            yearPublished: 2023,
            origin: CatalogQueueOrigin.NewsDiscovery,
            extractedTitle: "Apiary"
        );
        pendingRepo.Items.Add(existingItem);

        bggClient.SearchResults.Add(new BggSearchResultDto(
            BggId: 370132,
            Title: "Apiary",
            YearPublished: 2023
        ));

        var release = new WeeklyRelease("Maldito Games anuncia Apiary", "Maldito Games", new DateOnly(2026, 10, 1));
        releaseRepo.Releases.Add(release);

        // Act
        var result = await extractor.ProcessReleaseAsync(release);

        // Assert
        Assert.True(result.EnqueuedToBgg);
        Assert.Equal(2, existingItem.RequestedCount);
    }

    [Fact]
    public async Task DiscoverAndEnqueueFromReleasesAsync_ProcessesOnlyUnlinkedReleases()
    {
        // Arrange
        var extractor = CreateExtractor(out _, out var bggClient, out var pendingRepo, out var releaseRepo);

        bggClient.SearchResults.Add(new BggSearchResultDto(BggId: 370132, Title: "Apiary", YearPublished: 2023));

        var linkedRelease = new WeeklyRelease("Lanzamiento 1", "Editorial", new DateOnly(2026, 9, 10), gameId: Guid.NewGuid());
        var unlinkedRelease = new WeeklyRelease("Devir anuncia Apiary", "Devir", new DateOnly(2026, 9, 15));

        releaseRepo.Releases.Add(linkedRelease);
        releaseRepo.Releases.Add(unlinkedRelease);

        // Act
        int processedCount = await extractor.DiscoverAndEnqueueFromReleasesAsync();

        // Assert
        Assert.Equal(1, processedCount);
        Assert.Single(pendingRepo.Items);
    }

    private static NewsGameExtractor CreateExtractor(
        out FakeGameRepo gameRepo,
        out FakeBggClient bggClient,
        out FakePendingRepo pendingRepo,
        out FakeReleaseRepo releaseRepo)
    {
        gameRepo = new FakeGameRepo();
        bggClient = new FakeBggClient();
        pendingRepo = new FakePendingRepo();
        releaseRepo = new FakeReleaseRepo();

        return new NewsGameExtractor(
            gameRepo,
            bggClient,
            pendingRepo,
            releaseRepo,
            NullLogger<NewsGameExtractor>.Instance
        );
    }

    private class FakeGameRepo : IGameRepository
    {
        public List<Game> Games = [];

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Games.FirstOrDefault(g => g.Id == id));

        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(Games.FirstOrDefault(g => g.Slug == slug));

        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Games.FirstOrDefault(g => g.BggId == bggId));

        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            var query = Games.AsQueryable();
            if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
            {
                var term = criteria.SearchTerm.Trim();
                query = query.Where(g => g.SpanishTitle.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                         g.OriginalTitle.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            var list = query.ToList();
            return Task.FromResult(((IReadOnlyList<Game>)list, list.Count));
        }

        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        {
            Games.AddRange(games);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Game game, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(Games.Count > 0);
    }

    private class FakeBggClient : IBggClient
    {
        public List<BggSearchResultDto> SearchResults = [];

        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(string username, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BggCollectionItemDto>>([]);
        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BggSearchResultDto>>(SearchResults);
        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BggTopGameDto>>([]);
    }

    private class FakePendingRepo : IPendingBggImportRepository
    {
        public List<PendingBggImport> Items = [];

        public Task<PendingBggImport?> GetByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.BggId == bggId));

        public Task<IReadOnlyList<PendingBggImport>> GetTopPendingAsync(int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PendingBggImport>>(Items.Take(limit).ToList());

        public Task<IReadOnlyList<PendingBggImport>> GetAllAsync(CatalogQueueStatus? status = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PendingBggImport>>(status.HasValue ? Items.Where(i => i.Status == status.Value).ToList() : Items);

        public Task AddAsync(PendingBggImport item, CancellationToken ct = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PendingBggImport item, CancellationToken ct = default) => Task.CompletedTask;
        public Task ResetFailedToPendingAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetTotalPendingCountAsync(CancellationToken ct = default) => Task.FromResult(Items.Count(i => i.Status == CatalogQueueStatus.Pending));

        public Task<int> RecoverStaleProcessingToPendingAsync(CancellationToken ct = default)
        {
            var stale = Items.Where(i => i.Status == CatalogQueueStatus.Processing).ToList();
            foreach (var item in stale)
            {
                item.ResetToPending();
            }
            return Task.FromResult(stale.Count);
        }
    }

    private class FakeReleaseRepo : IWeeklyReleaseRepository
    {
        public List<WeeklyRelease> Releases = [];

        public Task<IReadOnlyList<WeeklyRelease>> GetReleasesAsync(DateOnly? fromDate = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WeeklyRelease>>(Releases);

        public Task<WeeklyRelease?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Releases.FirstOrDefault(r => r.Id == id));

        public Task AddAsync(WeeklyRelease release, CancellationToken ct = default)
        {
            Releases.Add(release);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(WeeklyRelease release, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Releases.RemoveAll(r => r.Id == id);
            return Task.CompletedTask;
        }
    }
}
