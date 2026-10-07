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
using Moq;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class EditorialReleasesSyncServiceTests
{
    private readonly Mock<IDevirReleasesExtractor> _devirExtractorMock = new();
    private readonly Mock<IMalditoReleasesExtractor> _malditoExtractorMock = new();
    private readonly Mock<IWeeklyReleaseRepository> _weeklyReleaseRepoMock = new();
    private readonly Mock<IGameRepository> _gameRepoMock = new();
    private readonly Mock<IBggClient> _bggClientMock = new();

    private readonly List<WeeklyRelease> _persistedReleases = new();
    private readonly List<Game> _persistedGames = new();

    public EditorialReleasesSyncServiceTests()
    {
        _weeklyReleaseRepoMock
            .Setup(r => r.GetReleasesAsync(It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _persistedReleases.ToList());

        _weeklyReleaseRepoMock
            .Setup(r => r.AddAsync(It.IsAny<WeeklyRelease>(), It.IsAny<CancellationToken>()))
            .Callback<WeeklyRelease, CancellationToken>((rel, _) => _persistedReleases.Add(rel))
            .Returns(Task.CompletedTask);

        _weeklyReleaseRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<WeeklyRelease>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _gameRepoMock
            .Setup(r => r.GetAllGamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _persistedGames.ToList());

        _gameRepoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Game>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Game>, CancellationToken>((games, _) => _persistedGames.AddRange(games))
            .Returns(Task.CompletedTask);

        _gameRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private EditorialReleasesSyncService CreateService()
    {
        return new EditorialReleasesSyncService(
            _devirExtractorMock.Object,
            _malditoExtractorMock.Object,
            _weeklyReleaseRepoMock.Object,
            _gameRepoMock.Object,
            _bggClientMock.Object,
            NullLogger<EditorialReleasesSyncService>.Instance);
    }

    [Fact]
    public async Task SyncAllEditorialReleasesAsync_CreatesNewReleasesAndLinksExistingGames()
    {
        // Arrange: un juego existente en catálogo con EAN
        var existingGame = CreateSampleGame(1001, "vileborn", "Vileborn", "VILEBORN", yearPublished: 2026);
        existingGame.UpdateEan("8436625615992");
        _persistedGames.Add(existingGame);

        // Devir extractor devuelve 1 juego que coincide con el EAN
        _devirExtractorMock
            .Setup(d => d.ExtractReleasesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EditorialReleaseItem>
            {
                new("VILEBORN", "Devir", new DateOnly(2026, 11, 1), "Noviembre 2026", 45m, "8436625615992", "https://devir.es/vileborn.jpg")
            });

        _malditoExtractorMock
            .Setup(m => m.ExtractReleasesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EditorialReleaseItem>());

        var service = CreateService();

        // Act
        var summary = await service.SyncAllEditorialReleasesAsync();

        // Assert
        Assert.Equal(1, summary.TotalFound);
        Assert.Equal(1, summary.CreatedCount);
        Assert.Equal(1, summary.GamesLinkedCount);
        Assert.Single(_persistedReleases);

        var created = _persistedReleases.First();
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
        _devirExtractorMock
            .Setup(d => d.ExtractReleasesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EditorialReleaseItem>
            {
                new("DAGGERHEART", "Devir", new DateOnly(2027, 1, 1), "2027", 59.99m, null, "https://devir.es/dagger.jpg")
            });

        _malditoExtractorMock
            .Setup(m => m.ExtractReleasesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EditorialReleaseItem>());

        // BGG mock search y fetch
        _bggClientMock
            .Setup(b => b.SearchGamesAsync("DAGGERHEART", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BggSearchResultDto>
            {
                new(390001, "Daggerheart", 2025)
            });

        var bggGame = CreateSampleGame(390001, "daggerheart", "Daggerheart", "Daggerheart", yearPublished: 2025);
        _bggClientMock
            .Setup(b => b.FetchGameByBggIdAsync(390001, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bggGame);

        var service = CreateService();

        // Act
        var summary = await service.SyncAllEditorialReleasesAsync();

        // Assert
        Assert.Equal(1, summary.TotalFound);
        Assert.Equal(1, summary.CreatedCount);
        Assert.Equal(1, summary.GamesImportedFromBggCount);
        Assert.Equal(1, summary.GamesLinkedCount);

        var release = _persistedReleases.First();
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
        _persistedReleases.Add(initialRelease);

        _devirExtractorMock
            .Setup(d => d.ExtractReleasesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EditorialReleaseItem>());

        // Maldito ahora devuelve Floe con PVP confirmado
        _malditoExtractorMock
            .Setup(m => m.ExtractReleasesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EditorialReleaseItem>
            {
                new("Floe", "Maldito Games", new DateOnly(2027, 1, 1), "2027", 49.95m, null, "https://maldito.es/floe.jpg", Notes: "Preventa abierta")
            });

        var service = CreateService();

        // Act
        var summary = await service.SyncAllEditorialReleasesAsync();

        // Assert: no se crea un duplicado, se actualiza
        Assert.Equal(1, summary.TotalFound);
        Assert.Equal(0, summary.CreatedCount);
        Assert.Equal(1, summary.UpdatedCount);
        Assert.Single(_persistedReleases);

        _weeklyReleaseRepoMock.Verify(r => r.UpdateAsync(It.Is<WeeklyRelease>(rel => rel.Title == "Floe" && rel.EstimatedPvp == 49.95m), It.IsAny<CancellationToken>()), Times.Once);
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
}
