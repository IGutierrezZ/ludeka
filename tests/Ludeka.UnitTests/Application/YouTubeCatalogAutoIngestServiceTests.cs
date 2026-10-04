using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Media;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class YouTubeCatalogAutoIngestServiceTests
{
    private sealed class FakeGameRepository : IGameRepository
    {
        public List<Game> Games { get; } = new();

        public int RequestedMaxRank { get; private set; }
        public int RequestedLimit { get; private set; }

        public Task<IReadOnlyList<Game>> GetTopRankedGamesWithoutVideosAsync(int maxRank = 4000, int limit = 60, int afterRank = 0, CancellationToken ct = default)
        {
            RequestedMaxRank = maxRank;
            RequestedLimit = limit;

            var filtered = Games
                .Where(g => g.BggRank.HasValue && g.BggRank.Value <= maxRank && g.BggRank.Value > afterRank)
                .OrderBy(g => g.BggRank!.Value)
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<Game>>(filtered);
        }

        public Task<int> GetTopRankedGamesWithoutVideosCountAsync(int maxRank = 4000, CancellationToken ct = default)
        {
            var count = Games.Count(g => g.BggRank.HasValue && g.BggRank.Value <= maxRank);
            return Task.FromResult(count);
        }

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.Id == id));
        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.Slug == slug));
        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.BggId == bggId));
        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<Game> Items, int TotalCount)>((Games, Games.Count));
        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default) { Games.AddRange(games); return Task.CompletedTask; }
        public Task UpdateAsync(Game game, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(Games.Count > 0);
    }

    private sealed class FakeYouTubeSearchService : IYouTubeSearchService
    {
        public List<Guid> IngestedGameIds { get; } = new();
        public Dictionary<Guid, List<MediaItemDto>> IngestionResults { get; } = new();
        public HashSet<Guid> FailingGameIds { get; } = new();

        public Task<IReadOnlyList<MediaItemDto>> AutoSuggestAndIngestConsolidatedForGameAsync(Guid gameId, bool autoApprove = false, CancellationToken ct = default)
        {
            IngestedGameIds.Add(gameId);

            if (FailingGameIds.Contains(gameId))
            {
                throw new InvalidOperationException($"Error simulado de YouTube para {gameId}");
            }

            if (IngestionResults.TryGetValue(gameId, out var list))
            {
                return Task.FromResult<IReadOnlyList<MediaItemDto>>(list);
            }

            return Task.FromResult<IReadOnlyList<MediaItemDto>>([]);
        }

        public Task<IReadOnlyList<YouTubeSearchResultDto>> SearchVideosForGameAsync(Guid gameId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<YouTubeSearchResultDto>>([]);
        public Task<IReadOnlyList<YouTubeSearchResultDto>> SearchQuickOverviewsAsync(string gameTitle, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<YouTubeSearchResultDto>>([]);
        public Task<IReadOnlyList<YouTubeSearchResultDto>> SearchTutorialsAsync(string gameTitle, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<YouTubeSearchResultDto>>([]);
        public Task<IReadOnlyList<YouTubeSearchResultDto>> SearchPlaythroughsAsync(string gameTitle, Guid? gameId = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<YouTubeSearchResultDto>>([]);
        public Task<IReadOnlyList<YouTubeSearchResultDto>> SearchConsolidatedCandidatesAsync(string gameTitle, Guid? gameId = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<YouTubeSearchResultDto>>([]);
        public Task<MediaItemDto> IngestVideoAsync(YouTubeIngestRequestDto request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<MediaItemDto>> AutoSuggestAndIngestForGameAsync(Guid gameId, bool autoApprove = false, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaItemDto>>([]);
    }

    private static MediaItemDto CreateMediaDto(Guid gameId, string videoId, MediaType type)
    {
        var item = new MediaItem(
            type: type,
            platform: MediaPlatform.YouTube,
            title: $"Video {videoId}",
            url: $"https://youtube.com/watch?v={videoId}",
            thumbnailUrl: "https://i.ytimg.com/vi/thumb.jpg",
            authorChannel: "Canal Lúdico",
            gameId: gameId,
            durationSeconds: 600,
            playerCountBadge: null,
            status: ModerationStatus.Approved
        );
        return MediaItemDto.FromDomain(item, "Test Game");
    }

    private static Game CreateGame(int bggId, string title, int rank) =>
        new(
            bggId,
            title,
            title,
            "Autor",
            "Editorial",
            2020,
            "",
            "",
            "",
            8.0,
            rank,
            8.0,
            ConfrontationType.Competitive,
            GameStyle.Eurogame,
            false,
            new AgeRating(10, 10),
            LanguageDependence.None,
            TableFootprint.StandardTable,
            new GameDuration(30, 60, 20),
            []
        );

    [Fact]
    public async Task RunScheduledAutoIngestAsync_WhenDisabled_ReturnsImmediatelyWithoutQuerying()
    {
        var gameRepo = new FakeGameRepository();
        var ytService = new FakeYouTubeSearchService();
        var options = Microsoft.Extensions.Options.Options.Create(new YouTubeAutoIngestOptions { Enabled = false });

        var service = new YouTubeCatalogAutoIngestService(gameRepo, ytService, options, NullLogger<YouTubeCatalogAutoIngestService>.Instance);

        var result = await service.RunScheduledAutoIngestAsync();

        Assert.Equal(0, result.GamesEvaluated);
        Assert.Equal(0, result.VideosIngested);
        Assert.Empty(ytService.IngestedGameIds);
    }

    [Fact]
    public async Task RunScheduledAutoIngestAsync_ProcessesGamesAndAggregatesVideos()
    {
        var gameRepo = new FakeGameRepository();
        var g1 = CreateGame(101, "Gloomhaven", 1);
        var g2 = CreateGame(102, "Brass: Birmingham", 2);
        var g3 = CreateGame(103, "Ark Nova", 3);

        gameRepo.Games.AddRange(new[] { g1, g2, g3 });

        var ytService = new FakeYouTubeSearchService();
        ytService.IngestionResults[g1.Id] = new List<MediaItemDto>
        {
            CreateMediaDto(g1.Id, "v1", MediaType.QuickOverview),
            CreateMediaDto(g1.Id, "v2", MediaType.Tutorial)
        };
        ytService.IngestionResults[g2.Id] = new List<MediaItemDto>
        {
            CreateMediaDto(g2.Id, "v3", MediaType.Tutorial)
        };
        // g3 leaves IngestionResults empty (skipped)

        var options = Microsoft.Extensions.Options.Options.Create(new YouTubeAutoIngestOptions
        {
            Enabled = true,
            DailyGamesLimit = 60,
            MaxBggRank = 4000,
            DelayBetweenGamesMs = 0
        });

        var service = new YouTubeCatalogAutoIngestService(gameRepo, ytService, options, NullLogger<YouTubeCatalogAutoIngestService>.Instance);

        var result = await service.RunScheduledAutoIngestAsync();

        Assert.Equal(3, result.GamesEvaluated);
        Assert.Equal(3, result.VideosIngested);
        Assert.Equal(1, result.SkippedCount); // g3 had no videos
        Assert.Equal(0, result.ErrorsCount);
        Assert.Equal(3, ytService.IngestedGameIds.Count);
        Assert.Equal(4000, gameRepo.RequestedMaxRank);
        Assert.Equal(60, gameRepo.RequestedLimit);
    }

    [Fact]
    public async Task RunScheduledAutoIngestAsync_RespectsCustomLimitAndRank()
    {
        var gameRepo = new FakeGameRepository();
        var ytService = new FakeYouTubeSearchService();
        var options = Microsoft.Extensions.Options.Options.Create(new YouTubeAutoIngestOptions
        {
            Enabled = true,
            DailyGamesLimit = 60,
            MaxBggRank = 4000,
            DelayBetweenGamesMs = 0
        });

        var service = new YouTubeCatalogAutoIngestService(gameRepo, ytService, options, NullLogger<YouTubeCatalogAutoIngestService>.Instance);

        await service.RunScheduledAutoIngestAsync(customLimit: 15, maxRank: 500);

        Assert.Equal(500, gameRepo.RequestedMaxRank);
        Assert.Equal(15, gameRepo.RequestedLimit);
    }

    [Fact]
    public async Task RunScheduledAutoIngestAsync_WhenGameThrows_RecordsErrorAndContinuesWithNext()
    {
        var gameRepo = new FakeGameRepository();
        var g1 = CreateGame(201, "Game With Error", 10);
        var g2 = CreateGame(202, "Game Success", 11);
        gameRepo.Games.AddRange(new[] { g1, g2 });

        var ytService = new FakeYouTubeSearchService();
        ytService.FailingGameIds.Add(g1.Id);
        ytService.IngestionResults[g2.Id] = new List<MediaItemDto>
        {
            CreateMediaDto(g2.Id, "v4", MediaType.Tutorial)
        };

        var options = Microsoft.Extensions.Options.Options.Create(new YouTubeAutoIngestOptions
        {
            Enabled = true,
            DailyGamesLimit = 60,
            DelayBetweenGamesMs = 0
        });

        var service = new YouTubeCatalogAutoIngestService(gameRepo, ytService, options, NullLogger<YouTubeCatalogAutoIngestService>.Instance);

        var result = await service.RunScheduledAutoIngestAsync();

        Assert.Equal(2, result.GamesEvaluated);
        Assert.Equal(1, result.VideosIngested);
        Assert.Equal(1, result.ErrorsCount);
        Assert.Equal(2, ytService.IngestedGameIds.Count);
    }
}
