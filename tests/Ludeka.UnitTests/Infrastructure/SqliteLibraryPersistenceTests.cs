using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class SqliteLibraryPersistenceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private LudekaDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<Game> SeedGameAsync()
    {
        var game = new Game(
            bggId: 5001,
            originalTitle: "Azul",
            spanishTitle: "Azul",
            designer: "Michael Kiesling",
            publisher: "Plan B Games",
            yearPublished: 2017,
            coverImageUrl: "https://example.com/azul.jpg",
            thumbnailUrl: null,
            description: "Juego de azulejos",
            bggRating: 7.8,
            bggRank: 50,
            ludistRating: 0.0,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.FillerAbstract,
            isOfficialSolo: false,
            age: new AgeRating(8, 8),
            language: LanguageDependence.None,
            footprint: TableFootprint.SmallTable,
            duration: new GameDuration(30, 45, 15),
            scalability: [new ScalabilityEntry(2, "2", ScalabilityStatus.MustPlay)]
        );

        _context.Games.Add(game);
        await _context.SaveChangesAsync();
        return game;
    }

    [Fact]
    public async Task UserCollectionRepository_ShouldPersistAndRetrieveItem()
    {
        // Arrange
        var game = await SeedGameAsync();
        var repo = new SqliteUserCollectionRepository(_context);
        var item = new UserCollectionItem("user-abc", game.Id, CollectionStatus.InCollection);

        // Act
        await repo.AddAsync(item);
        var retrieved = await repo.GetByUserAndGameAsync("user-abc", game.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(CollectionStatus.InCollection, retrieved.Status);
        Assert.Equal(game.Id, retrieved.GameId);
        Assert.Equal("Azul", retrieved.Game?.SpanishTitle);
    }

    [Fact]
    public async Task GameLoanRepository_ShouldPersistAndFilterActiveLoans()
    {
        // Arrange
        var game = await SeedGameAsync();
        var repo = new SqliteGameLoanRepository(_context);
        var loan = new GameLoan("user-abc", game.Id, "Laura", DateTimeOffset.UtcNow, "Préstamo fin de semana");

        // Act
        await repo.AddAsync(loan);
        var activeLoans = await repo.GetActiveLoansByUserIdAsync("user-abc");

        // Assert
        Assert.Single(activeLoans);
        Assert.Equal("Laura", activeLoans[0].BorrowerName);
        Assert.False(activeLoans[0].IsReturned);

        // Devolver
        activeLoans[0].MarkAsReturned();
        await repo.UpdateAsync(activeLoans[0]);

        var remainingActive = await repo.GetActiveLoansByUserIdAsync("user-abc");
        Assert.Empty(remainingActive);
    }

    [Fact]
    public async Task UserReviewRepository_ShouldPersistJsonValueObjectsAndCalculateAverage()
    {
        // Arrange
        var game = await SeedGameAsync();
        var repo = new SqliteUserReviewRepository(_context);
        var votes = new List<UserPlayerCountVote>
        {
            new(2, ScalabilityStatus.MustPlay),
            new(3, ScalabilityStatus.Recommended)
        };
        var family = new UserFamilyExperienceVote(true, 7, false);

        var review1 = new UserGameReview("user-1", game.Id, 8.0, "Muy buen juego abstracto", votes, family, PlayContextType.Owned);
        var review2 = new UserGameReview("user-2", game.Id, 9.0, "Precioso en mesa", null, null, PlayContextType.Friends);

        // Act
        await repo.AddAsync(review1);
        await repo.AddAsync(review2);

        var retrieved = await repo.GetByUserAndGameAsync("user-1", game.Id);
        var avg = await repo.GetAverageScoreByGameIdAsync(game.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(8.0, retrieved.Score);
        Assert.Equal(2, retrieved.PlayerCountRatings.Count);
        Assert.NotNull(retrieved.FamilyExperience);
        Assert.Equal(7, retrieved.FamilyExperience.SuggestedMinAge);
        Assert.NotNull(avg);
        Assert.Equal(8.5, avg.Value);
    }

    [Fact]
    public async Task UserCollectionRepository_UpdateAsync_ShouldUpdateStateWithoutDetachedGameConflict()
    {
        // Arrange
        var game = await SeedGameAsync();
        var repo = new SqliteUserCollectionRepository(_context);
        var item = new UserCollectionItem("user-test", game.Id, CollectionStatus.Wishlist, isPlayed: false);
        await repo.AddAsync(item);

        // Act: Obtener el ítem (incluye .Game) y modificar estado de colección y jugado simultáneamente
        var loaded = await repo.GetByUserAndGameAsync("user-test", game.Id);
        Assert.NotNull(loaded);
        Assert.NotNull(loaded.Game); // Game navigation está poblada

        loaded.ChangeStatus(CollectionStatus.InCollection);
        loaded.SetPlayed(true);
        await repo.UpdateAsync(loaded);

        // Assert: Se persiste el nuevo estado sin errores de tracking de Game
        var updated = await repo.GetByUserAndGameAsync("user-test", game.Id);
        Assert.NotNull(updated);
        Assert.Equal(CollectionStatus.InCollection, updated.Status);
        Assert.True(updated.IsPlayed);
        Assert.Equal("Azul", updated.Game?.SpanishTitle);
    }

    [Fact]
    public async Task UserReviewRepository_UpdateAsync_ShouldUpdateReviewWithoutDetachedGameConflict()
    {
        // Arrange
        var game = await SeedGameAsync();
        var repo = new SqliteUserReviewRepository(_context);
        var review = new UserGameReview("user-test", game.Id, 7.0, "Bueno", null, null, PlayContextType.Owned);
        await repo.AddAsync(review);

        // Act: Obtener la reseña (incluye .Game) y actualizar campos
        var loaded = await repo.GetByUserAndGameAsync("user-test", game.Id);
        Assert.NotNull(loaded);
        Assert.NotNull(loaded.Game); // Game navigation poblada

        loaded.Update(9.0, "Excelente tras varias partidas", null, null, PlayContextType.Owned);
        await repo.UpdateAsync(loaded);

        // Assert
        var updated = await repo.GetByUserAndGameAsync("user-test", game.Id);
        Assert.NotNull(updated);
        Assert.Equal(9.0, updated.Score);
        Assert.Equal("Excelente tras varias partidas", updated.MicroReview);
    }
}
