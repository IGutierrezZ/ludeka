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

public class SqliteSocialInboxRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private LudekaDbContext _context = null!;
    private SqliteSocialInboxRepository _inboxRepository = null!;
    private SqliteGiveawayRepository _giveawayRepository = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _inboxRepository = new SqliteSocialInboxRepository(_context);
        _giveawayRepository = new SqliteGiveawayRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static int _nextBggId = 2000;

    private async Task<Game> SeedGameWithScalabilityAsync(string title = "1%: A Game of Strategic Chance")
    {
        var scalability = new List<ScalabilityEntry>
        {
            new(2, "2J", ScalabilityStatus.MustPlay, 100, 20, 2),
            new(3, "3J", ScalabilityStatus.Recommended, 80, 40, 5),
            new(4, "4J", ScalabilityStatus.Recommended, 70, 50, 10)
        };

        var game = new Game(
            bggId: System.Threading.Interlocked.Increment(ref _nextBggId),
            originalTitle: title,
            spanishTitle: title,
            designer: "Magic Box Games",
            publisher: "Magic Box",
            yearPublished: 2024,
            coverImageUrl: "https://example.com/game.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            description: "Juego de cartas estratégico.",
            bggRating: 7.5,
            bggRank: 1200,
            ludistRating: 8.0,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.FillerAbstract,
            isOfficialSolo: false,
            age: new AgeRating(8, 8),
            language: LanguageDependence.None,
            footprint: TableFootprint.SmallTable,
            duration: new GameDuration(15, 30, 20),
            scalability: scalability
        );

        await _context.Games.AddAsync(game);
        await _context.SaveChangesAsync();
        return game;
    }

    [Fact]
    public async Task UpdateAsync_WhenItemLinkedToGameWithScalability_ShouldNotThrowShadowOrdinalException()
    {
        // 1. Arrange: Juego existente con colecciones JSON propias (Scalability)
        var game = await SeedGameWithScalabilityAsync();

        var item = new SocialInboxItem(
            sourceUrl: "https://www.instagram.com/p/DAcde123/",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo de 5 juegos 1%",
            organizerOrAuthor: "magicboxgames_es",
            gameId: game.Id,
            gameTitle: game.SpanishTitle
        );

        await _inboxRepository.AddAsync(item);

        // 2. Act: Obtener ítem por id, modificarlo y actualizarlo
        var retrieved = await _inboxRepository.GetByIdAsync(item.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(game.Id, retrieved.GameId);

        retrieved.UpdateDetails(
            title: "Sorteo Actualizado de 5 juegos 1%",
            organizerOrAuthor: "magicboxgames_es",
            collaborator: "Ludeka",
            detectedType: SocialSubmissionType.Giveaway,
            gameId: game.Id,
            gameTitle: game.SpanishTitle,
            eventOrReleaseDate: DateTimeOffset.UtcNow.AddDays(5),
            eventEndDate: null,
            location: "España",
            estimatedPvp: null,
            mediaCategory: null,
            playerCountBadge: null,
            thumbnailUrl: "https://example.com/updated-thumb.webp",
            moderatorNotes: "Bases revisadas"
        );

        // Al hacer UpdateAsync en entidad desconectada vinculada a juego con Scalability
        // no debe lanzar: The value of shadow key property 'ScalabilityEntry.__synthesizedOrdinal' is unknown
        await _inboxRepository.UpdateAsync(retrieved);

        // 3. Assert: Verificar persistencia correcta
        var updated = await _inboxRepository.GetByIdAsync(item.Id);
        Assert.NotNull(updated);
        Assert.Equal("Sorteo Actualizado de 5 juegos 1%", updated.Title);
        Assert.Equal("Ludeka", updated.Collaborator);
        Assert.Equal("Bases revisadas", updated.ModeratorNotes);
    }

    [Fact]
    public async Task Approve_WhenItemLinkedToGameWithScalability_ShouldUpdateStatusAndCreatedEntityId()
    {
        // 1. Arrange
        var game = await SeedGameWithScalabilityAsync();

        var item = new SocialInboxItem(
            sourceUrl: "https://www.instagram.com/p/DAcde456/",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo Express 1%",
            organizerOrAuthor: "magicboxgames_es",
            gameId: game.Id,
            gameTitle: game.SpanishTitle
        );

        await _inboxRepository.AddAsync(item);

        // 2. Act: Simular flujo de aprobación (crear giveaway y aprobar inbox item)
        var retrieved = await _inboxRepository.GetByIdAsync(item.Id);
        Assert.NotNull(retrieved);

        var createdGiveawayId = Guid.NewGuid();
        retrieved.Approve(createdGiveawayId, "moderator-user-id");

        await _inboxRepository.UpdateAsync(retrieved);

        // 3. Assert
        var updated = await _inboxRepository.GetByIdAsync(item.Id);
        Assert.NotNull(updated);
        Assert.Equal(SocialInboxStatus.Approved, updated.Status);
        Assert.Equal(createdGiveawayId, updated.CreatedEntityId);
        Assert.Equal("moderator-user-id", updated.ReviewedByUserId);
    }

    [Fact]
    public async Task GiveawayRepository_UpdateAsync_WhenLinkedToGameWithScalability_ShouldNotThrow()
    {
        // 1. Arrange
        var game = await SeedGameWithScalabilityAsync("Catan Especial");

        var giveaway = new Giveaway(
            title: "Sorteo Catan",
            organizer: "Devir",
            url: "https://instagram.com/p/123",
            platform: GiveawayPlatform.Instagram,
            deadlineAt: DateTimeOffset.UtcNow.AddDays(7),
            country: "España",
            gameId: game.Id,
            gameTitle: game.SpanishTitle
        );

        await _giveawayRepository.AddAsync(giveaway);

        // 2. Act: Recuperar, modificar y actualizar
        var retrieved = await _giveawayRepository.GetByIdAsync(giveaway.Id);
        Assert.NotNull(retrieved);

        retrieved.SetPromoted(true);
        await _giveawayRepository.UpdateAsync(retrieved);

        // 3. Assert
        var updated = await _giveawayRepository.GetByIdAsync(giveaway.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsPromoted);
    }

    [Fact]
    public async Task PurgeSimulatedAsync_RemovesOnlySimulatedPendingItems()
    {
        // 1. Arrange: 1 ítem real pendiente, 2 ítems simulados pendientes, 1 ítem simulado ya aprobado
        var realPending = new SocialInboxItem(
            sourceUrl: "https://www.instagram.com/p/REAL_POST_123/",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo Real",
            organizerOrAuthor: "malditogames");

        var simulatedPending1 = new SocialInboxItem(
            sourceUrl: "https://www.instagram.com/p/sim_cuartodejuegos_0/",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Gran sorteo exclusivo",
            organizerOrAuthor: "cuartodejuegos");

        var simulatedPending2 = new SocialInboxItem(
            sourceUrl: "https://youtube.com/simulated/video123",
            platform: SocialPlatform.YouTube,
            detectedType: SocialSubmissionType.MediaItem,
            title: "Vídeo simulado",
            organizerOrAuthor: "ludocreador");

        var simulatedApproved = new SocialInboxItem(
            sourceUrl: "https://www.instagram.com/p/sim_aprobado/",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo Simulado Aprobado",
            organizerOrAuthor: "cuartodejuegos");
        simulatedApproved.Approve(Guid.NewGuid(), "admin");

        await _inboxRepository.AddAsync(realPending);
        await _inboxRepository.AddAsync(simulatedPending1);
        await _inboxRepository.AddAsync(simulatedPending2);
        await _inboxRepository.AddAsync(simulatedApproved);

        // 2. Act
        var purgedCount = await _inboxRepository.PurgeSimulatedAsync();

        // 3. Assert: 2 eliminados, el real y el aprobado se mantienen
        Assert.Equal(2, purgedCount);

        var pendingRemaining = await _inboxRepository.GetPendingAsync();
        Assert.Single(pendingRemaining);
        Assert.Equal(realPending.Id, pendingRemaining[0].Id);

        var retrievedApproved = await _inboxRepository.GetByIdAsync(simulatedApproved.Id);
        Assert.NotNull(retrievedApproved);
    }

    [Fact]
    public async Task ExistsBySourceUrlAsync_ReturnsTrueForExistingUrl_IgnoringCase()
    {
        var item = new SocialInboxItem(
            sourceUrl: "https://www.instagram.com/p/CASE_SENSITIVE_123/",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo URL Check",
            organizerOrAuthor: "test");

        await _inboxRepository.AddAsync(item);

        Assert.True(await _inboxRepository.ExistsBySourceUrlAsync("https://www.instagram.com/p/CASE_SENSITIVE_123/"));
        Assert.True(await _inboxRepository.ExistsBySourceUrlAsync("https://www.instagram.com/p/case_sensitive_123/"));
        Assert.False(await _inboxRepository.ExistsBySourceUrlAsync("https://www.instagram.com/p/otra_cosa/"));
    }
}
