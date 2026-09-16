using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Bgg;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>Cliente BGG instrumentado: cuenta llamadas y devuelve respuestas configurables.</summary>
internal sealed class RecordingBggClient : IBggClient
{
    public int FetchCalls { get; private set; }

    public Game? GameToReturn { get; set; }

    public IReadOnlyList<BggTopGameDto> TopGamesToReturn { get; set; } = [];

    public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default)
    {
        FetchCalls++;
        return Task.FromResult(GameToReturn);
    }

    public Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(string username, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<BggCollectionItemDto>>([]);

    public Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(
        string username,
        IProgress<BggImportProgressReport>? progress,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<BggCollectionItemDto>>([]);

    public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<BggSearchResultDto>>([]);

    public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default)
        => Task.FromResult(TopGamesToReturn);
}

internal sealed class NoopGeekDoImagesClient : IGeekDoImagesClient
{
    public Task<GeekDoGalleryImagesDto> GetTopVotedImagesAsync(int bggId, CancellationToken ct = default)
        => Task.FromResult(new GeekDoGalleryImagesDto(null, null, null));
}

internal sealed class StubAiGameSummaryService : IAiGameSummaryService
{
    public Task<AiGameSummaryDto> GenerateSummaryAsync(Game game, CancellationToken ct = default)
        => Task.FromResult(new AiGameSummaryDto(
            game.Id,
            game.SpanishTitle,
            ScalabilitySummary: "Escalabilidad simulada.",
            AgeSummary: "Edad simulada.",
            FootprintSummary: "Huella simulada.",
            GeneralVerdict: "Veredicto simulado.",
            Model: "Heurística Editorial",
            GeneratedAt: DateTime.UtcNow));

    public Task<AiGameSummaryDto> EnsureSummaryForGameAsync(Guid gameId, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<AiBatchProcessingResultDto> ProcessPendingSummariesBatchAsync(int batchSize = 20, CancellationToken ct = default)
        => Task.FromResult(new AiBatchProcessingResultDto(0, 0, 0, []));

    public Task<AiBatchResultDto> GenerateBatchSummariesAsync(IReadOnlyList<AiGameBatchInputDto> games, CancellationToken ct = default)
        => Task.FromResult(new AiBatchResultDto(true, false, new Dictionary<int, AiGameSummaryDto>(), null));
}

internal sealed class NoopNewsGameExtractor : INewsGameExtractor
{
    public string? ExtractGameTitle(string title, string? notes) => null;

    public Task<NewsExtractionResultDto> ProcessReleaseAsync(WeeklyRelease release, CancellationToken ct = default)
        => Task.FromResult(new NewsExtractionResultDto(release.Id, null, false, null, false, null));

    public Task<int> DiscoverAndEnqueueFromReleasesAsync(CancellationToken ct = default) => Task.FromResult(0);
}

/// <summary>Cola de catalogación comunitaria: el panel público y el administrativo ejecutan lotes.</summary>
public class BggCatalogQueueWriteGuardTests : AdministrativeWriteGuardTestBase
{
    private readonly RecordingBggClient _bggClient = new();

    private BggCatalogQueueService CreateService(ISessionPermissionGuard? guard = null)
        => new(
            new SqlitePendingBggImportRepository(Context),
            _bggClient,
            new SqliteGameRepository(Context),
            new SqliteUserCollectionRepository(Context),
            aiSummaryService: null,
            guard);

    private async Task<PendingBggImport> SeedPendingItemAsync()
    {
        var item = new PendingBggImport(
            bggId: 9001,
            title: "Juego pendiente",
            yearPublished: 2024,
            thumbnailUrl: null,
            coverImageUrl: null,
            origin: CatalogQueueOrigin.UserImport,
            extractedTitle: "Juego pendiente");

        Context.PendingBggImports.Add(item);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return item;
    }

    [Fact]
    public async Task ResetFailedItemsAsync_WithoutSession_DeniesAndKeepsTheFailedItem()
    {
        var item = await SeedPendingItemAsync();
        item.MarkAsFailed("sin conexión");
        Context.PendingBggImports.Update(item);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var service = CreateService(CreateGuard(Anonymous()));

        await AssertDenied(() => service.ResetFailedItemsAsync());

        var stored = await Context.PendingBggImports.AsNoTracking().SingleAsync(i => i.Id == item.Id);
        Assert.Equal(CatalogQueueStatus.Failed, stored.Status);
    }

    [Fact]
    public async Task ResetFailedItemsAsync_WithSuspendedAccount_DeniesEvenWithLiveCookie()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanEditGames));
        await SuspendAsync(ModeratorId);
        var item = await SeedPendingItemAsync();
        var service = CreateService(CreateGuard(LiveCookie()));

        await AssertDenied(() => service.ResetFailedItemsAsync());

        var stored = await Context.PendingBggImports.AsNoTracking().SingleAsync(i => i.Id == item.Id);
        Assert.Equal(CatalogQueueStatus.Pending, stored.Status);
    }

    [Fact]
    public async Task ProcessPendingQueueBatchAsync_WithoutThePermission_DeniesWithoutCallingBgg()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanApproveMedia));
        var item = await SeedPendingItemAsync();
        var service = CreateService(CreateGuard(LiveCookie()));

        await AssertDenied(() => service.ProcessPendingQueueBatchAsync(5));

        Assert.Equal(0, _bggClient.FetchCalls);
        var stored = await Context.PendingBggImports.AsNoTracking().SingleAsync(i => i.Id == item.Id);
        Assert.Equal(CatalogQueueStatus.Pending, stored.Status);
    }

    [Fact]
    public async Task ProcessPendingQueueBatchAsync_WithThePermission_CatalogsThePendingGame()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanEditGames));
        var item = await SeedPendingItemAsync();
        _bggClient.GameToReturn = CreateGame(bggId: 9001, spanishTitle: "Juego catalogado");
        var service = CreateService(CreateGuard(LiveCookie()));

        var result = await service.ProcessPendingQueueBatchAsync(5);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, _bggClient.FetchCalls);
        var stored = await Context.PendingBggImports.AsNoTracking().SingleAsync(i => i.Id == item.Id);
        Assert.Equal(CatalogQueueStatus.Completed, stored.Status);
    }
}

/// <summary>Descubrimiento de tendencias BGG: botón del panel de cola y fase del ciclo nocturno.</summary>
public class BggDiscoveryWriteGuardTests : AdministrativeWriteGuardTestBase
{
    private readonly RecordingBggClient _bggClient = new();

    private BggDiscoveryService CreateService(ISessionPermissionGuard? guard = null)
        => new(
            _bggClient,
            new SqliteGameRepository(Context),
            new SqlitePendingBggImportRepository(Context),
            NullLogger<BggDiscoveryService>.Instance,
            stagingRepo: null,
            guard);

    private void SetTopGames() => _bggClient.TopGamesToReturn =
    [
        new BggTopGameDto(BggId: 7000, Title: "Tendencia de prueba", BggRank: 10, YearPublished: DateTime.UtcNow.Year, ThumbnailUrl: null)
    ];

    [Fact]
    public async Task DiscoverAndEnqueueBggTrendsAsync_WithoutSession_DeniesWithoutCallingBgg()
    {
        SetTopGames();
        var service = CreateService(CreateGuard(Anonymous()));

        await AssertDenied(() => service.DiscoverAndEnqueueBggTrendsAsync(10));

        Assert.Empty(await Context.PendingBggImports.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task DiscoverAndEnqueueBggTrendsAsync_WithSuspendedAccount_DeniesEvenWithLiveCookie()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanEditGames));
        await SuspendAsync(ModeratorId);
        SetTopGames();
        var service = CreateService(CreateGuard(LiveCookie()));

        await AssertDenied(() => service.DiscoverAndEnqueueBggTrendsAsync(10));

        Assert.Empty(await Context.PendingBggImports.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task DiscoverAndEnqueueBggTrendsAsync_WithThePermission_EnqueuesTheDiscoveries()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanEditGames));
        SetTopGames();
        var service = CreateService(CreateGuard(LiveCookie()));

        var result = await service.DiscoverAndEnqueueBggTrendsAsync(10);

        Assert.Equal(1, result.EnqueuedCount);
        Assert.Equal(1, await Context.PendingBggImports.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task RunBggTrendsDiscoveryAsync_WithoutSession_EnqueuesAsTheSystemPath()
    {
        SetTopGames();
        var service = CreateService();

        var result = await service.RunBggTrendsDiscoveryAsync(10);

        Assert.Equal(1, result.EnqueuedCount);
        Assert.Equal(1, await Context.PendingBggImports.AsNoTracking().CountAsync());
    }
}

/// <summary>Ciclo de drenaje del staging masivo: botón del panel y fase del ciclo nocturno.</summary>
public class BggMassIngestionWriteGuardTests : AdministrativeWriteGuardTestBase
{
    private readonly RecordingBggClient _bggClient = new();

    private BggMassIngestionService CreateService(ISessionPermissionGuard? guard = null)
        => new(
            new SqliteBggCatalogStagingRepository(Context),
            _bggClient,
            new NoopGeekDoImagesClient(),
            new NoopImageStorageService(),
            new StubAiGameSummaryService(),
            new SqliteGameRepository(Context),
            new HttpClient(),
            Microsoft.Extensions.Options.Options.Create(new BggMassIngestionOptions()),
            NullLogger<BggMassIngestionService>.Instance,
            guard);

    private async Task<BggCatalogStagingItem> SeedStagingItemAsync()
    {
        var item = new BggCatalogStagingItem(9001, "Juego en staging");
        Context.BggCatalogStaging.Add(item);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return item;
    }

    [Fact]
    public async Task RunDrainCycleAsync_WithoutSession_DeniesWithoutCallingBgg()
    {
        await SeedStagingItemAsync();
        var service = CreateService(CreateGuard(Anonymous()));

        await AssertDenied(() => service.RunDrainCycleAsync());

        Assert.Equal(0, _bggClient.FetchCalls);
    }

    [Fact]
    public async Task RunDrainCycleAsync_WithSuspendedAccount_DeniesEvenWithLiveCookie()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanEditGames));
        await SuspendAsync(ModeratorId);
        await SeedStagingItemAsync();
        var service = CreateService(CreateGuard(LiveCookie()));

        await AssertDenied(() => service.RunDrainCycleAsync());

        Assert.Equal(0, _bggClient.FetchCalls);
    }

    [Fact]
    public async Task RunScheduledDrainCycleAsync_WithoutSession_DrainsAsTheSystemPath()
    {
        await SeedStagingItemAsync();
        var service = CreateService();

        var result = await service.RunScheduledDrainCycleAsync();

        Assert.NotNull(result);
        Assert.Equal(1, _bggClient.FetchCalls);
    }
}

/// <summary>Ciclo nocturno de catalogación: botón del panel y servicio hospedado programado.</summary>
public class NightlyCatalogingWriteGuardTests : AdministrativeWriteGuardTestBase
{
    private NightlyCatalogingService CreateService(ISessionPermissionGuard? guard = null)
        => new(
            new SqlitePendingBggImportRepository(Context),
            new RecordingBggClient(),
            new SqliteGameRepository(Context),
            new SqliteUserCollectionRepository(Context),
            new SqliteWeeklyReleaseRepository(Context),
            new NoopNewsGameExtractor(),
            new SqliteNightlyCatalogingLogRepository(Context),
            Microsoft.Extensions.Options.Options.Create(new NightlyCatalogingOptions()),
            NullLogger<NightlyCatalogingService>.Instance,
            aiSummaryService: null,
            massIngestionService: null,
            discoveryService: null,
            guard);

    [Fact]
    public async Task ExecuteNightlyCatalogingAsync_WithoutSession_DeniesWithoutWritingTheLog()
    {
        var service = CreateService(CreateGuard(Anonymous()));

        await AssertDenied(() => service.ExecuteNightlyCatalogingAsync());

        Assert.Empty(await Context.NightlyCatalogingExecutionLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ExecuteNightlyCatalogingAsync_WithSuspendedAccount_DeniesEvenWithLiveCookie()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanEditGames));
        await SuspendAsync(ModeratorId);
        var service = CreateService(CreateGuard(LiveCookie()));

        await AssertDenied(() => service.ExecuteNightlyCatalogingAsync());

        Assert.Empty(await Context.NightlyCatalogingExecutionLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task RunScheduledCatalogingAsync_WithoutSession_WritesTheExecutionLogAsTheSystemPath()
    {
        var service = CreateService();

        var result = await service.RunScheduledCatalogingAsync();

        Assert.NotNull(result);
        Assert.Single(await Context.NightlyCatalogingExecutionLogs.AsNoTracking().ToListAsync());
    }
}

/// <summary>Carga nocturna de síntesis con IA: botón del panel de cola comunitaria.</summary>
public class AiBatchSummaryWriteGuardTests : AdministrativeWriteGuardTestBase
{
    private GeminiGameSummaryService CreateService(ISessionPermissionGuard? guard = null)
        => new(
            new HttpClient(),
            Microsoft.Extensions.Options.Options.Create(new GeminiOptions()),
            new SqliteGameRepository(Context),
            NullLogger<GeminiGameSummaryService>.Instance,
            guard);

    [Fact]
    public async Task ProcessPendingSummariesBatchAsync_WithoutSession_DeniesAndLeavesTheGameWithoutSummary()
    {
        var game = CreateGame(bggId: 6001, spanishTitle: "Juego sin síntesis");
        Context.Games.Add(game);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var service = CreateService(CreateGuard(Anonymous()));

        await AssertDenied(() => service.ProcessPendingSummariesBatchAsync(5));

        var stored = await Context.Games.AsNoTracking().SingleAsync(g => g.Id == game.Id);
        Assert.Null(stored.AiSummary);
    }

    [Fact]
    public async Task ProcessPendingSummariesBatchAsync_WithSuspendedAccount_DeniesEvenWithLiveCookie()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanEditGames));
        await SuspendAsync(ModeratorId);
        var game = CreateGame(bggId: 6002, spanishTitle: "Juego sin síntesis");
        Context.Games.Add(game);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var service = CreateService(CreateGuard(LiveCookie()));

        await AssertDenied(() => service.ProcessPendingSummariesBatchAsync(5));

        var stored = await Context.Games.AsNoTracking().SingleAsync(g => g.Id == game.Id);
        Assert.Null(stored.AiSummary);
    }

    [Fact]
    public async Task ProcessPendingSummariesBatchAsync_WithThePermission_SetsTheHeuristicSummary()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanEditGames));
        var game = CreateGame(bggId: 6003, spanishTitle: "Juego con síntesis");
        Context.Games.Add(game);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var service = CreateService(CreateGuard(LiveCookie()));

        var result = await service.ProcessPendingSummariesBatchAsync(5);

        Assert.Contains("Juego con síntesis", result.SummarizedTitles);
        var stored = await Context.Games.AsNoTracking().SingleAsync(g => g.Id == game.Id);
        Assert.NotNull(stored.AiSummary);
    }
}
