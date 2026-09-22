using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class GeminiGameSummaryServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private LudekaDbContext _context = null!;
    private IGameRepository _gameRepository = null!;

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
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static Game CreateTestGame(int bggId = 13, string title = "Catán")
    {
        return new Game(
            bggId: bggId,
            originalTitle: "Catan",
            spanishTitle: title,
            designer: "Klaus Teuber",
            publisher: "Devir",
            yearPublished: 1995,
            coverImageUrl: "https://example.com/catan.jpg",
            thumbnailUrl: null,
            description: "Juego de colonización, comercio y construcción en la isla de Catán.",
            bggRating: 7.1,
            bggRank: 500,
            ludistRating: 7.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(10, 8),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(60, 90, 20),
            scalability: [
                new ScalabilityEntry(3, "3J", ScalabilityStatus.Recommended, 200, 500, 30),
                new ScalabilityEntry(4, "4J", ScalabilityStatus.MustPlay, 1000, 200, 10)
            ]
        );
    }

    [Fact]
    public async Task GenerateSummaryAsync_WhenSimulateIsTrue_ReturnsHeuristicEditorialSummary()
    {
        var game = CreateTestGame();
        var geminiOptions = Options.Create(new GeminiOptions
        {
            Simulate = true,
            ApiKey = "sample-key"
        });

        var fakeHandler = new FakeHttpMessageHandler((req, ct) =>
            throw new InvalidOperationException("No debería llamarse a HTTP en modo simulado"));

        var httpClient = new HttpClient(fakeHandler);
        var service = new GeminiGameSummaryService(
            httpClient,
            geminiOptions,
            _gameRepository,
            NullLogger<GeminiGameSummaryService>.Instance
        );

        var result = await service.GenerateSummaryAsync(game);

        Assert.NotNull(result);
        Assert.Equal(game.Id, result.GameId);
        Assert.Equal(game.SpanishTitle, result.GameTitle);
        Assert.Equal("Heurística Editorial", result.Model);
        Assert.Contains("Catán", result.GeneralVerdict);
        Assert.Contains("consenso", result.ScalabilitySummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("años", result.AgeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mesa", result.FootprintSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WhenGeminiReturnsStructuredJson_ParsesAndReturnsGeminiSummary()
    {
        var game = CreateTestGame();
        var geminiOptions = Options.Create(new GeminiOptions
        {
            Simulate = false,
            ApiKey = "AIzaSyRealSimulatedKey",
            Model = "gemini-3.8-flash"
        });

        string geminiResponseJson = """
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": "{\n  \"generalVerdict\": \"Catán es el clásico indispensable de negociación territorial.\",\n  \"scalabilitySummary\": \"Brilla exactamente a 4 jugadores donde el mapa se estrecha.\",\n  \"ageSummary\": \"Accesible desde los 8 años en partidas guiadas en familia.\",\n  \"footprintSummary\": \"Requiere mesa de comedor estándar y 75 minutos de partida.\"\n}"
                  }
                ]
              }
            }
          ]
        }
        """;

        var fakeHandler = new FakeHttpMessageHandler((req, ct) =>
        {
            Assert.Contains("models/gemini-3.8-flash:generateContent", req.RequestUri?.ToString());
            Assert.Contains("key=AIzaSyRealSimulatedKey", req.RequestUri?.ToString());

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(geminiResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(fakeHandler);
        var service = new GeminiGameSummaryService(
            httpClient,
            geminiOptions,
            _gameRepository,
            NullLogger<GeminiGameSummaryService>.Instance
        );

        var result = await service.GenerateSummaryAsync(game);

        Assert.NotNull(result);
        Assert.Equal("Google Gemini (gemini-3.8-flash)", result.Model);
        Assert.Equal("Catán es el clásico indispensable de negociación territorial.", result.GeneralVerdict);
        Assert.Equal("Brilla exactamente a 4 jugadores donde el mapa se estrecha.", result.ScalabilitySummary);
        Assert.Equal("Accesible desde los 8 años en partidas guiadas en familia.", result.AgeSummary);
        Assert.Equal("Requiere mesa de comedor estándar y 75 minutos de partida.", result.FootprintSummary);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WhenConfiguredModelReturns404_AutoRecoversWithDefaultModel()
    {
        var game = CreateTestGame();
        var geminiOptions = Options.Create(new GeminiOptions
        {
            Simulate = false,
            ApiKey = "AIzaSyKey",
            Model = "gemini-1.5-flash" // Modelo que devolverá 404 para forzar autorrecuperación
        });

        string geminiResponseJson = """
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": "{\n  \"generalVerdict\": \"Respuesta recuperada con 3.8 flash.\",\n  \"scalabilitySummary\": \"Ideal 4.\",\n  \"ageSummary\": \"8+ años.\",\n  \"footprintSummary\": \"Mesa estándar.\"\n}"
                  }
                ]
              }
            }
          ]
        }
        """;

        int callCount = 0;
        var fakeHandler = new FakeHttpMessageHandler((req, ct) =>
        {
            callCount++;
            if (req.RequestUri?.ToString().Contains("models/gemini-1.5-flash") == true)
            {
                // El modelo antiguo devuelve 404
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"error\": {\"code\": 404, \"message\": \"Model deprecated\"}}")
                });
            }

            // El reintento con gemini-3.8-flash funciona
            Assert.Contains("models/gemini-3.8-flash", req.RequestUri?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(geminiResponseJson, System.Text.Encoding.UTF8, "application/json")
            });
        });

        var httpClient = new HttpClient(fakeHandler);
        var service = new GeminiGameSummaryService(
            httpClient,
            geminiOptions,
            _gameRepository,
            NullLogger<GeminiGameSummaryService>.Instance
        );

        var result = await service.GenerateSummaryAsync(game);

        Assert.NotNull(result);
        Assert.Equal(2, callCount);
        Assert.Equal("Google Gemini (gemini-3.8-flash)", result.Model);
        Assert.Equal("Respuesta recuperada con 3.8 flash.", result.GeneralVerdict);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WhenGeminiFailsInProduction_ThrowsExceptionWithoutInventingData()
    {
        var game = CreateTestGame();
        var geminiOptions = Options.Create(new GeminiOptions
        {
            Simulate = false,
            ApiKey = "AIzaSyErrorKey",
            Model = "gemini-3.8-flash"
        });

        var fakeHandler = new FakeHttpMessageHandler((req, ct) =>
        {
            // Simular fallo de cuota HTTP 429
            var errorResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{\"error\": {\"message\": \"Resource has been exhausted\"}}")
            };
            return Task.FromResult(errorResponse);
        });

        var httpClient = new HttpClient(fakeHandler);
        var service = new GeminiGameSummaryService(
            httpClient,
            geminiOptions,
            _gameRepository,
            NullLogger<GeminiGameSummaryService>.Instance
        );

        // En producción no se inventa heurística ante error de API; se lanza excepción para moderación/reintento
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateSummaryAsync(game));
    }

    [Fact]
    public async Task GenerateSummaryAsync_WhenSimulateIsTrue_ReturnsEditorialHeuristic()
    {
        var game = CreateTestGame();
        var geminiOptions = Options.Create(new GeminiOptions
        {
            Simulate = true
        });

        var service = new GeminiGameSummaryService(
            new HttpClient(),
            geminiOptions,
            _gameRepository,
            NullLogger<GeminiGameSummaryService>.Instance
        );

        var result = await service.GenerateSummaryAsync(game);

        Assert.NotNull(result);
        Assert.Equal("Heurística Editorial", result.Model);
        Assert.Contains("Catán", result.GeneralVerdict);
        Assert.False(string.IsNullOrWhiteSpace(result.ScalabilitySummary));
    }

    [Fact]
    public async Task EnsureSummaryForGameAsync_GeneratesAndPersists_WhenGameHasNoSummary()
    {
        var game = CreateTestGame();
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        var geminiOptions = Options.Create(new GeminiOptions { Simulate = true });
        var service = new GeminiGameSummaryService(
            new HttpClient(new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))),
            geminiOptions,
            _gameRepository,
            NullLogger<GeminiGameSummaryService>.Instance
        );

        var result = await service.EnsureSummaryForGameAsync(game.Id);

        Assert.NotNull(result);
        Assert.Equal("Heurística Editorial", result.Model);

        // Verificar persistencia en base de datos SQLite
        var savedGame = await _gameRepository.GetByIdAsync(game.Id);
        Assert.NotNull(savedGame);
        Assert.NotNull(savedGame.AiSummary);
        Assert.Equal("Heurística Editorial", savedGame.AiSummary.Model);
        Assert.Equal(result.GeneralVerdict, savedGame.AiSummary.GeneralVerdict);
    }

    [Fact]
    public async Task EnsureSummaryForGameAsync_ReturnsExistingSummary_WithoutCallingGenerator()
    {
        var game = CreateTestGame();
        var existingVo = new AiGameSummary(
            GeneralVerdict: "Veredicto previo ya persistido.",
            ScalabilitySummary: "Ideal a 4.",
            AgeSummary: "Desde 10 años.",
            FootprintSummary: "Mesa estándar.",
            Model: "Google Gemini (gemini-3.8-flash)",
            GeneratedAt: DateTime.UtcNow.AddDays(-1)
        );
        game.SetAiSummary(existingVo);

        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        var geminiOptions = Options.Create(new GeminiOptions { Simulate = true });
        var service = new GeminiGameSummaryService(
            new HttpClient(new FakeHttpMessageHandler((_, _) => throw new InvalidOperationException("No debe llamarse"))),
            geminiOptions,
            _gameRepository,
            NullLogger<GeminiGameSummaryService>.Instance
        );

        var result = await service.EnsureSummaryForGameAsync(game.Id);

        Assert.Equal("Veredicto previo ya persistido.", result.GeneralVerdict);
        Assert.Equal("Google Gemini (gemini-3.8-flash)", result.Model);
        Assert.Equal(existingVo.GeneratedAt, result.GeneratedAt);
    }

    [Fact]
    public async Task ProcessPendingSummariesBatchAsync_WhenGamesLackSummary_GeneratesAndPersistsInBatch()
    {
        var game1 = CreateTestGame(bggId: 101, title: "Juego Uno");
        var game2 = CreateTestGame(bggId: 102, title: "Juego Dos");
        _context.Games.AddRange(game1, game2);
        await _context.SaveChangesAsync();

        var geminiOptions = Options.Create(new GeminiOptions { Simulate = true });
        var service = new GeminiGameSummaryService(
            new HttpClient(new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))),
            geminiOptions,
            _gameRepository,
            NullLogger<GeminiGameSummaryService>.Instance
        );

        var result = await service.ProcessPendingSummariesBatchAsync(10);

        Assert.Equal(2, result.ProcessedCount);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Contains("Juego Uno", result.SummarizedTitles);
        Assert.Contains("Juego Dos", result.SummarizedTitles);

        var saved1 = await _gameRepository.GetByIdAsync(game1.Id);
        var saved2 = await _gameRepository.GetByIdAsync(game2.Id);
        Assert.NotNull(saved1?.AiSummary);
        Assert.NotNull(saved2?.AiSummary);
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
