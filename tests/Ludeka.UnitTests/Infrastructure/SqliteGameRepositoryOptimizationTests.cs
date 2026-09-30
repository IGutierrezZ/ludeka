using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class SqliteGameRepositoryOptimizationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LudekaDbContext _context;
    private readonly SqliteGameRepository _repository;
    private readonly SqliteGiveawayRepository _giveawayRepository;

    public SqliteGameRepositoryOptimizationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new SqliteGameRepository(_context);
        _giveawayRepository = new SqliteGiveawayRepository(_context);
    }

    [Fact]
    public async Task GetOfferCountsByStoreAsync_ShouldReturnAggregatedStoreCounts()
    {
        // Arrange
        var game1 = CreateTestGame(101, "Juego 1", "Devir");
        game1.PurchaseLinks.Add(new GamePurchaseLink("Zacatrus", "https://zacatrus.es/1", 25.0m, "€", true, "Oferta"));
        game1.PurchaseLinks.Add(new GamePurchaseLink("Dungeon Marvels", "https://dm.es/1", 24.5m, "€", true));

        var game2 = CreateTestGame(102, "Juego 2", "Asmodee");
        game2.PurchaseLinks.Add(new GamePurchaseLink("Zacatrus", "https://zacatrus.es/2", 40.0m, "€", true));

        var game3 = CreateTestGame(103, "Juego 3", "Maldito Games"); // Sin enlaces

        await _repository.AddRangeAsync([game1, game2, game3]);

        // Act
        var counts = await _repository.GetOfferCountsByStoreAsync();

        // Assert
        Assert.NotNull(counts);
        Assert.Equal(2, counts["Zacatrus"]);
        Assert.Equal(1, counts["Dungeon Marvels"]);
        Assert.False(counts.ContainsKey("Crash Comics"));
    }

    [Fact]
    public async Task GetGameCountsByPublisherAsync_ShouldAggregatePublishersCorrectly()
    {
        // Arrange
        var game1 = CreateTestGame(201, "Catan", "Kosmos", spanishPublisher: "Devir");
        var game2 = CreateTestGame(202, "Carcassonne", "Hans im Glück", spanishPublisher: "Devir");
        var game3 = CreateTestGame(203, "Terraforming Mars", "FryxGames", spanishPublisher: "Maldito Games");
        game3.UpdateRegionalPublishers([new RegionalPublisherEntry("ES", "España", "Giga Games")]);

        await _repository.AddRangeAsync([game1, game2, game3]);

        // Act
        var counts = await _repository.GetGameCountsByPublisherAsync();

        // Assert
        Assert.NotNull(counts);
        Assert.Equal(2, counts["Devir"]);
        Assert.Equal(1, counts["Kosmos"]);
        Assert.Equal(1, counts["Hans im Glück"]);
        Assert.Equal(1, counts["Maldito Games"]);
        Assert.Equal(1, counts["FryxGames"]);
        Assert.Equal(1, counts["Giga Games"]);
    }

    [Fact]
    public async Task GetGamesWithStoreOffersAsync_ShouldReturnOnlyGamesWithOffersFromStore()
    {
        // Arrange
        var game1 = CreateTestGame(301, "Dixit", "Libellud");
        game1.PurchaseLinks.Add(new GamePurchaseLink("Zacatrus", "https://zacatrus.es/dixit", 29.99m, "€", true));

        var game2 = CreateTestGame(302, "7 Wonders", "Repos");
        game2.PurchaseLinks.Add(new GamePurchaseLink("Jugamos Otra", "https://jugamosotra.com/7w", 42.0m, "€", true));

        await _repository.AddRangeAsync([game1, game2]);

        // Act
        var zacatrusGames = await _repository.GetGamesWithStoreOffersAsync("Zacatrus");
        var jugamosGames = await _repository.GetGamesWithStoreOffersAsync("Jugamos Otra");
        var emptyGames = await _repository.GetGamesWithStoreOffersAsync("Tienda Inexistente");

        // Assert
        Assert.Single(zacatrusGames);
        Assert.Equal("Dixit", zacatrusGames[0].SpanishTitle);

        Assert.Single(jugamosGames);
        Assert.Equal("7 Wonders", jugamosGames[0].SpanishTitle);

        Assert.Empty(emptyGames);
    }

    [Fact]
    public async Task GetGamesWithPurchaseLinksAsync_ShouldReturnOnlyGamesWithLinksAndRespectLimit()
    {
        // Arrange
        var g1 = CreateTestGame(401, "Juego Con Link 1", "Editorial A");
        g1.PurchaseLinks.Add(new GamePurchaseLink("Tienda", "https://tienda.com/1", 10m, "€", true));

        var g2 = CreateTestGame(402, "Juego Con Link 2", "Editorial B");
        g2.PurchaseLinks.Add(new GamePurchaseLink("Tienda", "https://tienda.com/2", 20m, "€", true));

        var g3 = CreateTestGame(403, "Juego Sin Enlaces", "Editorial C");

        await _repository.AddRangeAsync([g1, g2, g3]);

        // Act
        var allWithLinks = await _repository.GetGamesWithPurchaseLinksAsync();
        var limitedWithLinks = await _repository.GetGamesWithPurchaseLinksAsync(limit: 1);

        // Assert
        Assert.Equal(2, allWithLinks.Count);
        Assert.DoesNotContain(allWithLinks, g => g.Id == g3.Id);

        Assert.Single(limitedWithLinks);
    }

    [Fact]
    public async Task SearchAsync_TwoPhasePagination_ShouldCorrectlyFilterAndHydrateEntities()
    {
        // Arrange
        var g1 = CreateTestGame(501, "Juego Ligero", "Editorial");
        g1.UpdateScalability([
            new ScalabilityEntry(2, "2 jugadores", ScalabilityStatus.MustPlay, 50, 10, 0),
            new ScalabilityEntry(3, "3 jugadores", ScalabilityStatus.Recommended, 20, 30, 2)
        ]);

        var g2 = CreateTestGame(502, "Juego Familiar", "Editorial");
        g2.UpdateScalability([
            new ScalabilityEntry(2, "2 jugadores", ScalabilityStatus.NotRecommended, 0, 5, 40),
            new ScalabilityEntry(4, "4 jugadores", ScalabilityStatus.MustPlay, 60, 5, 0)
        ]);

        await _repository.AddRangeAsync([g1, g2]);

        // Act: Filtro Especial Parejas (debe retornar solo g1)
        var criteria = new GameFilterCriteria(EspecialParejas: true);
        var (items, total) = await _repository.SearchAsync(criteria, page: 1, pageSize: 10);

        // Assert
        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal(g1.Id, items[0].Id);
        Assert.Equal("Juego Ligero", items[0].SpanishTitle);
        // Verificar que la entidad final está completamente hidratada
        Assert.NotEmpty(items[0].Scalability);
    }

    [Fact]
    public async Task SqliteGiveawayRepository_ShouldFilterExpiredInSqlWhenNotIncluded()
    {
        // Arrange
        var activeGiveaway = new Giveaway(
            title: "Sorteo Activo",
            organizer: "Devir",
            url: "https://instagram.com/p/1",
            platform: GiveawayPlatform.Instagram,
            deadlineAt: DateTimeOffset.UtcNow.AddDays(3),
            country: "ES"
        );

        var expiredGiveaway = new Giveaway(
            title: "Sorteo Finalizado",
            organizer: "Asmodee",
            url: "https://instagram.com/p/2",
            platform: GiveawayPlatform.Instagram,
            deadlineAt: DateTimeOffset.UtcNow.AddDays(-2),
            country: "ES"
        );

        await _giveawayRepository.AddAsync(activeGiveaway);
        await _giveawayRepository.AddAsync(expiredGiveaway);

        // Act
        var activeOnly = await _giveawayRepository.GetGiveawaysAsync(includeExpired: false);
        var all = await _giveawayRepository.GetGiveawaysAsync(includeExpired: true);

        // Assert
        Assert.Single(activeOnly);
        Assert.Equal("Sorteo Activo", activeOnly[0].Title);

        Assert.Equal(2, all.Count);
    }

    private static Game CreateTestGame(int bggId, string title, string publisher, string? spanishPublisher = null)
    {
        var game = new Game(
            bggId: bggId,
            originalTitle: title,
            spanishTitle: title,
            designer: "Autor Test",
            publisher: publisher,
            yearPublished: 2024,
            coverImageUrl: "https://cdn.ludeka.es/cover.jpg",
            thumbnailUrl: "https://cdn.ludeka.es/thumb.jpg",
            description: "Descripción larga para simular consumo de datos en consultas sin optimizar.",
            bggRating: 7.8,
            bggRank: bggId,
            ludistRating: 8.0,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(10, 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(45, 60, 50)
        );

        if (!string.IsNullOrWhiteSpace(spanishPublisher))
        {
            game.UpdateSpanishPublisher(spanishPublisher);
        }

        return game;
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
