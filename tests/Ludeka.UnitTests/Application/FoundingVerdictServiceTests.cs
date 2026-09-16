using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Founding;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class FoundingVerdictServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private LudekaDbContext _context = null!;
    private IGameRepository _gameRepository = null!;
    private IUserReviewRepository _reviewRepository = null!;
    private IFoundingVerdictRepository _verdictRepository = null!;
    private FakeCurrentUserService _currentUserService = null!;
    private FoundingVerdictService _service = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _gameRepository = new SqliteGameRepository(_context);
        _reviewRepository = new SqliteUserReviewRepository(_context);
        _verdictRepository = new SqliteFoundingVerdictRepository(_context);
        _currentUserService = new FakeCurrentUserService();

        _service = new FoundingVerdictService(
            _verdictRepository,
            _gameRepository,
            _reviewRepository,
            _currentUserService
        );
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<Game> SeedGameAsync()
    {
        var game = new Game(
            bggId: 9001,
            originalTitle: "Ark Nova",
            spanishTitle: "Ark Nova",
            designer: "Mathias Wigge",
            publisher: "Maldito Games",
            yearPublished: 2021,
            coverImageUrl: "https://example.com/ark.jpg",
            thumbnailUrl: null,
            description: "Planifica y diseña un zoológico moderno y científicamente gestionado.",
            bggRating: 8.5,
            bggRank: 4,
            ludistRating: 8.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new AgeRating(14, 12),
            language: LanguageDependence.Low,
            footprint: TableFootprint.TableMonster,
            duration: new GameDuration(90, 150, 45),
            scalability: [
                new ScalabilityEntry(1, "1J", ScalabilityStatus.Recommended, 150, 400, 50),
                new ScalabilityEntry(2, "2J", ScalabilityStatus.MustPlay, 1200, 300, 20),
                new ScalabilityEntry(3, "3J", ScalabilityStatus.Recommended, 500, 600, 80),
                new ScalabilityEntry(4, "4J", ScalabilityStatus.NotRecommended, 80, 250, 800)
            ]
        );
        _context.Games.Add(game);
        await _context.SaveChangesAsync();
        return game;
    }

    [Fact]
    public async Task GetAiSummaryForGameAsync_WhenGameHasNoSummary_ShouldReturnNullWithoutCallingAi()
    {
        var game = await SeedGameAsync();

        var aiSummary = await _service.GetAiSummaryForGameAsync(game.Id);

        Assert.Null(aiSummary);
    }

    [Fact]
    public async Task GetAiSummaryForGameAsync_WhenGameHasPersistedSummary_ShouldReturnSummaryDto()
    {
        var game = await SeedGameAsync();
        var persistedVo = new AiGameSummary(
            "Veredicto persistido de prueba",
            "Ideal: 2 jugadores",
            "14+ años",
            "Monstruo de mesa",
            "Google Gemini (gemini-3.6-flash)",
            DateTime.UtcNow
        );
        game.SetAiSummary(persistedVo);
        await _gameRepository.UpdateAsync(game);

        var aiSummary = await _service.GetAiSummaryForGameAsync(game.Id);

        Assert.NotNull(aiSummary);
        Assert.Equal(game.Id, aiSummary.GameId);
        Assert.Equal("Ark Nova", aiSummary.GameTitle);
        Assert.Equal("Veredicto persistido de prueba", aiSummary.GeneralVerdict);
        Assert.Equal("Google Gemini (gemini-3.6-flash)", aiSummary.Model);
    }

    [Fact]
    public async Task RequestAiSummaryGenerationAsync_WhenUserLacksModerationRole_ShouldThrowUnauthorized()
    {
        var game = await SeedGameAsync();
        _currentUserService.RolesList = ["User"];

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.RequestAiSummaryGenerationAsync(game.Id));
    }

    [Fact]
    public async Task RequestAiSummaryGenerationAsync_WhenUserIsFoundingOrModerator_ShouldGenerateAndPersistSummary()
    {
        var game = await SeedGameAsync();
        _currentUserService.RolesList = ["Moderator"];

        var result = await _service.RequestAiSummaryGenerationAsync(game.Id);

        Assert.NotNull(result);
        Assert.Equal(game.Id, result.GameId);
        Assert.Equal("Ark Nova", result.GameTitle);
        Assert.False(string.IsNullOrWhiteSpace(result.GeneralVerdict));

        // Verificar que quedó guardado en BD
        var updatedGame = await _gameRepository.GetByIdAsync(game.Id);
        Assert.NotNull(updatedGame?.AiSummary);
        Assert.Equal(result.GeneralVerdict, updatedGame.AiSummary.GeneralVerdict);
    }

    [Fact]
    public async Task SaveVerdictAsync_WhenUserLacksFoundingRole_ShouldThrowUnauthorized()
    {
        var game = await SeedGameAsync();
        _currentUserService.RolesList = ["User"];

        var request = new SaveFoundingVerdictRequest(
            game.Id,
            FoundingRecommendation.MustPlay,
            "Análisis general de la mesa fundadora con longitud suficiente.",
            "Análisis enfocado a dos jugadores con longitud suficiente.",
            "Análisis enfocado a familias con niños con longitud suficiente."
        );

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.SaveVerdictAsync(request));
    }

    [Fact]
    public async Task SaveVerdictAsync_WhenUserIsFoundingTeam_ShouldSaveAndRecalculateLudistRating()
    {
        var game = await SeedGameAsync();
        Assert.True(_currentUserService.IsFoundingTeam);

        var request = new SaveFoundingVerdictRequest(
            game.Id,
            FoundingRecommendation.MustPlay,
            "Análisis general de Ark Nova por la mesa fundadora: un juego colosal.",
            "A 2 jugadores es su número perfecto. Cero entreturno y máxima tensión.",
            "Para familias, solo con adolescentes motivados mayores de 14 años.",
            [new FoundingPhotoDto("https://example.com/mesa.jpg", "Mesa de juego real")]
        );

        var result = await _service.SaveVerdictAsync(request);

        Assert.NotNull(result);
        Assert.Equal(game.Id, result.GameId);
        Assert.Equal(FoundingRecommendation.MustPlay, result.Recommendation);
        Assert.Equal("🏆 Imprescindible de la Mesa", result.RecommendationLabel);
        Assert.Single(result.Photos);

        // Comprobar que el juego tiene actualizado su LudistRating
        var updatedGame = await _gameRepository.GetByIdAsync(game.Id);
        Assert.NotNull(updatedGame);
        // Al otorgar MustPlay (10.0 con 3 votos de peso y sin votos comunitarios), el rating pasa a 10.0
        Assert.Equal(10.0, updatedGame.LudistRating);
    }

    /// <summary>
    /// Identidad de sesión controlada por el test que sustituye al antiguo servicio simulado
    /// (<c>DefaultCurrentUserService</c>, retirado en INC-46 F3): los roles ya no se conmutan,
    /// se fijan en el constructor del doble.
    /// </summary>
    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public string UserId { get; set; } = "carlos_fundador";
        public string UserName { get; set; } = "Carlos Fundador";
        public List<string> RolesList { get; set; } = ["FoundingTeam", "Moderator"];
        public ModeratorPermission Permissions { get; set; } = ModeratorPermission.All;

        public IReadOnlyList<string> Roles => RolesList;
        public bool IsFoundingTeam => RolesList.Contains("FoundingTeam", StringComparer.OrdinalIgnoreCase);
        public bool IsInRole(string role) => RolesList.Any(r => r.Equals(role, StringComparison.OrdinalIgnoreCase));

        public bool HasPermission(ModeratorPermission permission)
        {
            if (IsFoundingTeam) return true;
            return IsInRole("Moderator") && (Permissions & permission) == permission;
        }
    }
}
