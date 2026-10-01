using System;
using System.Linq;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class SqliteCommunityRepositoriesTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private LudekaDbContext _context = null!;
    private SqliteGiveawayRepository _giveawayRepo = null!;
    private SqliteWeeklyReleaseRepository _releaseRepo = null!;
    private SqliteRuleQARepository _ruleQaRepo = null!;
    private SqliteBoardGameEventRepository _eventRepo = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _giveawayRepo = new SqliteGiveawayRepository(_context);
        _releaseRepo = new SqliteWeeklyReleaseRepository(_context);
        _ruleQaRepo = new SqliteRuleQARepository(_context);
        _eventRepo = new SqliteBoardGameEventRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GiveawayRepository_AddAndQuery_WorksCorrectly()
    {
        // Arrange
        var g1 = new Giveaway("Sorteo Brass", "Maldito Games", "https://instagram.com/p/1", GiveawayPlatform.Instagram, DateTimeOffset.UtcNow.AddDays(2));
        var g2 = new Giveaway("Sorteo Catan", "Devir", "https://instagram.com/p/2", GiveawayPlatform.Instagram, DateTimeOffset.UtcNow.AddDays(-1));

        // Act
        await _giveawayRepo.AddAsync(g1);
        await _giveawayRepo.AddAsync(g2);

        var activeOnly = await _giveawayRepo.GetGiveawaysAsync(includeExpired: false);
        var all = await _giveawayRepo.GetGiveawaysAsync(includeExpired: true);

        // Assert
        Assert.Single(activeOnly);
        Assert.Equal("Sorteo Brass", activeOnly[0].Title);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task WeeklyReleaseRepository_AddAndQuery_WorksCorrectly()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var r1 = new WeeklyRelease("Harmonies", "Asmodee", today.AddDays(1), estimatedPvp: 35.00m, isReprint: false);
        var r2 = new WeeklyRelease("Dune Uprising", "Asmodee", today.AddDays(8), estimatedPvp: 60.00m, isReprint: true);

        // Act
        await _releaseRepo.AddAsync(r1);
        await _releaseRepo.AddAsync(r2);

        var releases = await _releaseRepo.GetReleasesAsync(today);

        // Assert: orden de llegada (CreatedAt desc) -> Dune Uprising se dio de alta después, va primero
        Assert.Equal(2, releases.Count);
        Assert.Equal("Dune Uprising", releases[0].Title);
        Assert.True(releases[0].IsReprint);
        Assert.Equal("Harmonies", releases[1].Title);
        Assert.False(releases[1].IsReprint);
    }

    [Fact]
    public async Task RuleQARepository_QuestionsAnswersAndVotes_PersistCorrectly()
    {
        // Arrange
        var game = new Game(
            bggId: 99999,
            originalTitle: "Test Game",
            spanishTitle: "Juego de Prueba",
            designer: "Autor",
            publisher: "Editorial",
            yearPublished: 2024,
            coverImageUrl: "https://example.com/cover.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            description: "Descripción de prueba",
            bggRating: 7.5,
            bggRank: 100,
            ludistRating: 8.0,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(10, 8),
            language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(30, 60, 20));

        await _context.Games.AddAsync(game);
        await _context.SaveChangesAsync();

        var gameId = game.Id;
        var question = new RuleQuestion(gameId, "u1", "Carlos", "¿Duda?", "Cuerpo");
        await _ruleQaRepo.AddQuestionAsync(question);

        var answer = new RuleAnswer(question.Id, "u2", "Elena", "Solución", "Pág. 3");
        await _ruleQaRepo.AddAnswerAsync(answer);

        var voteQ = new RuleVote("u2", questionId: question.Id);
        await _ruleQaRepo.AddVoteAsync(voteQ);

        // Act 1: Consulta de preguntas
        var questions = await _ruleQaRepo.GetQuestionsByGameIdAsync(gameId);

        // Assert 1
        Assert.Single(questions);
        Assert.Equal(question.Id, questions[0].Id);
        Assert.Single(questions[0].Answers);

        // Act 2: Votos
        var votedIds = await _ruleQaRepo.GetUserVotedQuestionIdsAsync("u2", new[] { question.Id });
        Assert.Contains(question.Id, votedIds);

        // Act 3: Marcar respuesta aceptada y actualizar
        question.MarkAcceptedAnswer(answer.Id, "u1", isModerator: false);
        await _ruleQaRepo.UpdateQuestionAsync(question);
        await _ruleQaRepo.UpdateAnswerAsync(answer);

        var refreshed = await _ruleQaRepo.GetQuestionWithAnswersAsync(question.Id);
        Assert.NotNull(refreshed);
        Assert.Equal(answer.Id, refreshed.AcceptedAnswerId);
        Assert.True(refreshed.Answers.First().IsAccepted);
    }

    [Fact]
    public async Task BoardGameEventRepository_GetUpcomingEventsAsync_ExcludesFinishedEvents()
    {
        // Arrange: evento de varios días ya finalizado, evento en curso y evento futuro
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var finishedMultiDay = new BoardGameEvent(
            "Feria Pasada", "Terminó ayer", "/images/e1.jpg",
            today.AddDays(-5), today.AddDays(-1), "Sevilla");

        var ongoingMultiDay = new BoardGameEvent(
            "Feria En Curso", "Termina mañana", "/images/e2.jpg",
            today.AddDays(-2), today.AddDays(1), "Valencia");

        var futureMultiDay = new BoardGameEvent(
            "Feria Futura", "Empieza en una semana", "/images/e3.jpg",
            today.AddDays(7), today.AddDays(10), "Zaragoza");

        await _eventRepo.AddAsync(finishedMultiDay);
        await _eventRepo.AddAsync(ongoingMultiDay);
        await _eventRepo.AddAsync(futureMultiDay);

        // Act
        var upcoming = await _eventRepo.GetUpcomingEventsAsync(limit: 20);

        // Assert: solo los que no han superado EndDate (en curso o futuros)
        Assert.Equal(2, upcoming.Count);
        Assert.DoesNotContain(upcoming, e => e.Title == "Feria Pasada");
        Assert.Equal("Feria En Curso", upcoming[0].Title);
        Assert.Equal("Feria Futura", upcoming[1].Title);
    }
}
