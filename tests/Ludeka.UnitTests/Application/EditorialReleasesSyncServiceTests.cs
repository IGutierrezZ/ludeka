using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Releases;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class EditorialReleasesSyncServiceTests
{
    private readonly FakeDevirExtractor _devirExtractor = new();
    private readonly FakeMalditoExtractor _malditoExtractor = new();
    private readonly FakeWeeklyReleaseRepository _weeklyReleaseRepo = new();
    private readonly FakeGameRepository _gameRepo = new();
    private readonly FakeBggClient _bggClient = new();

    private EditorialReleasesSyncService CreateService()
    {
        return new EditorialReleasesSyncService(
            _devirExtractor,
            _malditoExtractor,
            _weeklyReleaseRepo,
            _gameRepo,
            _bggClient,
            NullLogger<EditorialReleasesSyncService>.Instance);
    }

    [Fact]
    public async Task SyncAllEditorialReleasesAsync_CreatesNewReleasesAndLinksExistingGames()
    {
        // Arrange: un juego existente en catálogo con EAN
        var existingGame = CreateSampleGame(1001, "vileborn", "Vileborn", "VILEBORN", yearPublished: 2026);
        existingGame.UpdateEan("8436625615992");
        _gameRepo.Add(existingGame);

        // Devir extractor devuelve 1 juego que coincide con el EAN
        _devirExtractor.ItemsToReturn = new List<EditorialReleaseItem>
        {
            new("VILEBORN", "Devir", new DateOnly(2026, 11, 1), "Noviembre 2026", 45m, "8436625615992", "https://devir.es/vileborn.jpg")
        };

        var service = CreateService();

        // Act
        var summary = await service.SyncAllEditorialReleasesAsync();

        // Assert
        Assert.Equal(1, summary.TotalFound);
        Assert.Equal(1, summary.CreatedCount);
        Assert.Equal(1, summary.GamesLinkedCount);
        Assert.Single(_weeklyReleaseRepo.Releases);

        var created = _weeklyReleaseRepo.Releases.First();
        Assert.Equal("VILEBORN", created.Title);
        Assert.Equal("Devir", created.Publisher);
        Assert.Equal(existingGame.Id, created.GameId);
        Assert.Equal(45m, created.EstimatedPvp);
        Assert.False(created.IsReprint);
    }

    [Fact]
    public async Task SyncAllEditorialReleasesAsync_ImportsFromBggWhenGameNotFoundLocally()
    {
        // Devir devuelve Daggerheart (no existe en catálogo local)
        _devirExtractor.ItemsToReturn = new List<EditorialReleaseItem>
        {
            new("DAGGERHEART", "Devir", new DateOnly(2027, 1, 1), "2027", 59.99m, null, "https://devir.es/dagger.jpg")
        };

        // BGG mock search y fetch
        _bggClient.SearchResults["DAGGERHEART"] = new List<BggSearchResultDto>
        {
            new(390001, "Daggerheart", 2025)
        };

        var bggGame = CreateSampleGame(390001, "daggerheart", "Daggerheart", "Daggerheart", yearPublished: 2025);
        _bggClient.GamesById[390001] = bggGame;

        var service = CreateService();

        // Act
        var summary = await service.SyncAllEditorialReleasesAsync();

        // Assert
        Assert.Equal(1, summary.TotalFound);
        Assert.Equal(1, summary.CreatedCount);
        Assert.Equal(1, summary.GamesImportedFromBggCount);
        Assert.Equal(1, summary.GamesLinkedCount);

        var release = _weeklyReleaseRepo.Releases.First();
        Assert.Equal("DAGGERHEART", release.Title);
        Assert.NotNull(release.GameId);
        Assert.Equal(bggGame.Id, release.GameId);
        // Como el año de BGG es 2025 (anterior al actual), se detecta como reimpresión / edición española
        Assert.True(release.IsReprint);
    }

    [Fact]
    public async Task SyncAllEditorialReleasesAsync_IsIdempotent_UpdatesExistingReleaseWithoutDuplicating()
    {
        // Ya existe una novedad registrada previamente sin PVP
        var initialRelease = new WeeklyRelease(
            title: "Floe",
            publisher: "Maldito Games",
            releaseDate: new DateOnly(2027, 1, 1),
            coverImageUrl: "https://maldito.es/floe.jpg",
            estimatedPvp: null);
        _weeklyReleaseRepo.Releases.Add(initialRelease);

        // Maldito ahora devuelve Floe con PVP confirmado
        _malditoExtractor.ItemsToReturn = new List<EditorialReleaseItem>
        {
            new("Floe", "Maldito Games", new DateOnly(2027, 1, 1), "2027", 49.95m, null, "https://maldito.es/floe.jpg", Notes: "Preventa abierta")
        };

        var service = CreateService();

        // Act
        var summary = await service.SyncAllEditorialReleasesAsync();

        // Assert: no se crea un duplicado, se actualiza
        Assert.Equal(1, summary.TotalFound);
        Assert.Equal(0, summary.CreatedCount);
        Assert.Equal(1, summary.UpdatedCount);
        Assert.Single(_weeklyReleaseRepo.Releases);

        var updated = _weeklyReleaseRepo.Releases.First();
        Assert.Equal("Floe", updated.Title);
        Assert.Equal(49.95m, updated.EstimatedPvp);
    }

    private static Game CreateSampleGame(int bggId, string slug, string originalTitle, string spanishTitle, int yearPublished)
    {
        return new Game(
            bggId: bggId,
            slug: slug,
            originalTitle: originalTitle,
            spanishTitle: spanishTitle,
            designer: "Autor Test",
            publisher: "Editorial Test",
            yearPublished: yearPublished,
            age: AgeRating.Create(10),
            duration: GameDuration.Create(45),
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            language: LanguageDependence.Low,
            footprint: TableFootprint.Medium);
    }

    // --- Fakes ---

    private class FakeDevirExtractor : IDevirReleasesExtractor
    {
        public List<EditorialReleaseItem> ItemsToReturn { get; set; } = [];
        public Task<IReadOnlyList<EditorialReleaseItem>> ExtractReleasesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<EditorialReleaseItem>>(ItemsToReturn);
        public IReadOnlyList<EditorialReleaseItem> ParseHtml(string html) => ItemsToReturn;
    }

    private class FakeMalditoExtractor : IMalditoReleasesExtractor
    {
        public List<EditorialReleaseItem> ItemsToReturn { get; set; } = [];
        public Task<IReadOnlyList<EditorialReleaseItem>> ExtractReleasesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<EditorialReleaseItem>>(ItemsToReturn);
        public IReadOnlyList<EditorialReleaseItem> ParseHtml(string homeHtml, string? catalogHtml = null) => ItemsToReturn;
    }

    private class FakeWeeklyReleaseRepository : IWeeklyReleaseRepository
    {
        public List<WeeklyRelease> Releases { get; } = [];

        public Task AddAsync(WeeklyRelease release, CancellationToken ct = default)
        {
            Releases.Add(release);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Releases.RemoveAll(r => r.Id == id);
            return Task.CompletedTask;
        }

        public Task<WeeklyRelease?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Releases.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<WeeklyRelease>> GetReleasesAsync(DateOnly? fromDate = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WeeklyRelease>>(Releases.ToList());

        public Task UpdateAsync(WeeklyRelease release, CancellationToken ct = default)
        {
            int idx = Releases.FindIndex(r => r.Id == release.Id);
            if (idx >= 0) Releases[idx] = release;
            return Task.CompletedTask;
        }
    }

    private class FakeGameRepository : IGameRepository
    {
        private readonly List<Game> _games = [];

        public void Add(Game game) => _games.Add(game);

        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        {
            _games.AddRange(games);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Game>> GetAllGamesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Game>>(_games.ToList());

        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(_games.FirstOrDefault(g => g.BggId == bggId));

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_games.FirstOrDefault(g => g.Id == id));

        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default)
            => Task.FromResult(_games.FirstOrDefault(g => g.Slug == slug));

        public Task<bool> HasAnyAsync(CancellationToken ct = default)
            => Task.FromResult(_games.Count > 0);

        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<Game>, int)>((_games.Take(pageSize).ToList(), _games.Count));

        public Task UpdateAsync(Game game, CancellationToken ct = default)
        {
            int idx = _games.FindIndex(g => g.Id == game.Id);
            if (idx >= 0) _games[idx] = game;
            return Task.CompletedTask;
        }
    }

    private class FakeBggClient : IBggClient
    {
        public Dictionary<string, List<BggSearchResultDto>> SearchResults { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<int, Game> GamesById { get; } = [];

        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(GamesById.TryGetValue(bggId, out var g) ? g : null);

        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggTopGameDto>>([]);

        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggSearchResultDto>>(
                SearchResults.TryGetValue(query, out var list) ? list : []);
    }
}
