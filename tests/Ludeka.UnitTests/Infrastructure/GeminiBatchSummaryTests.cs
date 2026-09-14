using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class GeminiBatchSummaryTests
{
    [Fact]
    public async Task GenerateBatchSummariesAsync_InSimulateMode_GeneratesSummariesForAllGamesInBatch()
    {
        // Arrange
        var geminiOptions = Options.Create(new GeminiOptions { Simulate = true, ApiKey = "" });
        using var httpClient = new HttpClient();

        var service = new GeminiGameSummaryService(
            httpClient,
            geminiOptions,
            new FakeGameRepo(),
            NullLogger<GeminiGameSummaryService>.Instance
        );

        var batchInputs = new List<AiGameBatchInputDto>
        {
            new(174430, "Gloomhaven", "Gloomhaven", "Isaac Childres", "Cephalofair", 2017, "Mazmorras", 8.7, 1, 4, 14),
            new(224517, "Brass: Birmingham", "Brass: Birmingham", "Martin Wallace", "Roxley", 2018, "Revolución industrial", 8.6, 2, 4, 14),
            new(342942, "Ark Nova", "Ark Nova", "Mathias Wigge", "Feuerland", 2021, "Zoológico moderno", 8.5, 1, 4, 14)
        };

        // Act
        var result = await service.GenerateBatchSummariesAsync(batchInputs);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.QuotaExhausted);
        Assert.Equal(3, result.Summaries.Count);

        foreach (var input in batchInputs)
        {
            Assert.True(result.Summaries.ContainsKey(input.BggId));
            var summary = result.Summaries[input.BggId];
            Assert.False(string.IsNullOrWhiteSpace(summary.GeneralVerdict));
            Assert.False(string.IsNullOrWhiteSpace(summary.ScalabilitySummary));
            Assert.False(string.IsNullOrWhiteSpace(summary.AgeSummary));
            Assert.False(string.IsNullOrWhiteSpace(summary.FootprintSummary));
            Assert.Equal("Heurística Editorial", summary.Model);
        }
    }

    [Fact]
    public async Task GenerateBatchSummariesAsync_WithEmptyList_ReturnsSuccessEmpty()
    {
        // Arrange
        var geminiOptions = Options.Create(new GeminiOptions { Simulate = true });
        using var httpClient = new HttpClient();

        var service = new GeminiGameSummaryService(
            httpClient,
            geminiOptions,
            new FakeGameRepo(),
            NullLogger<GeminiGameSummaryService>.Instance
        );

        // Act
        var result = await service.GenerateBatchSummariesAsync([]);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Summaries);
    }

    private class FakeGameRepo : IGameRepository
    {
        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default) => Task.FromResult(((IReadOnlyList<Game>)Array.Empty<Game>(), 0));
        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Game game, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(false);
    }
}
