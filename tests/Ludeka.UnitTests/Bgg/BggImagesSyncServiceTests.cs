using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Bgg;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Bgg;

public class BggImagesSyncServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LudekaDbContext _context;
    private readonly TestDbContextFactory _factory;
    private readonly SqliteBggRawSnapshotRepository _snapshotRepo;
    private readonly FakeGeekDoImagesClient _geekDoClient;
    private readonly FakeImageStorageService _storageService;

    public BggImagesSyncServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        _context.Database.EnsureCreated();

        _factory = new TestDbContextFactory(options);
        _snapshotRepo = new SqliteBggRawSnapshotRepository(_factory);
        _geekDoClient = new FakeGeekDoImagesClient();
        _storageService = new FakeImageStorageService();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task SyncTopRankedImagesBatchAsync_ZeroCloud_ShouldPrioritizeSpanishCover_AndSetDirectCdnUrls()
    {
        // Arrange: Juego #1 sin trasera ni mesa
        var game = CreateGame(13, "Catan", bggRank: 1);
        game.UpdateImages("https://cf.geekdo-images.com/catan-intl-old.jpg");
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        // Snapshot con versión en español con portada propia
        const string xml = @"
<items>
  <item type=""boardgame"" id=""13"">
    <name type=""primary"" value=""Catan"" />
    <versions>
      <item type=""boardgameversion"" id=""5001"">
        <name type=""primary"" value=""Catán"" />
        <image>https://cf.geekdo-images.com/catan-devir-es.jpg</image>
        <thumbnail>https://cf.geekdo-images.com/catan-devir-es-thumb.jpg</thumbnail>
        <link type=""language"" id=""2190"" value=""Spanish"" />
        <link type=""boardgamepublisher"" value=""Devir"" />
      </item>
    </versions>
  </item>
</items>";
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(xml);
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(13, json));

        // Fotos comunitarias en GeekDo
        _geekDoClient.SetupGallery(13, new GeekDoGalleryImagesDto(
            FrontCoverUrl: "https://cf.geekdo-images.com/catan-geekdo-front.jpg",
            BackCoverUrl: "https://cf.geekdo-images.com/catan-geekdo-back.jpg",
            TableOrGameplayUrl: "https://cf.geekdo-images.com/catan-geekdo-table.jpg"
        ));

        var r2Options = Options.Create(new CloudflareR2Options { Simulate = true }); // Zero-Cloud sin R2
        var service = new BggImagesSyncService(
            _factory,
            _snapshotRepo,
            _geekDoClient,
            _storageService,
            r2Options,
            new HttpClient(),
            NullLogger<BggImagesSyncService>.Instance
        );

        // Act
        var result = await service.SyncTopRankedImagesBatchAsync(afterRank: 0, batchSize: 10, maxRank: 3000, delayMs: 0);

        // Assert
        Assert.Equal(1, result.EvaluatedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.LastRankProcessed);

        var updated = await _context.Games.AsNoTracking().FirstAsync(g => g.BggId == 13);
        // Prioridad: Portada en español de la versión en snapshot
        Assert.Equal("https://cf.geekdo-images.com/catan-devir-es.jpg", updated.CoverImageUrl);
        Assert.Equal("https://cf.geekdo-images.com/catan-devir-es-thumb.jpg", updated.ThumbnailUrl);
        // Fotos comunitarias de GeekDo asignadas directamente
        Assert.Equal("https://cf.geekdo-images.com/catan-geekdo-back.jpg", updated.BackCoverImageUrl);
        Assert.Equal("https://cf.geekdo-images.com/catan-geekdo-table.jpg", updated.TableImageUrl);
    }

    [Fact]
    public async Task SyncTopRankedImagesBatchAsync_ZeroCloud_ShouldRecoverSimulatedCover_FromGeekDoFront()
    {
        // Arrange: Juego #2 con URL simulada rota
        var game = CreateGame(200, "Lost Ruins", bggRank: 2);
        game.UpdateImages("https://pub-mock123.r2.dev/games/200/cover.webp");
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        _geekDoClient.SetupGallery(200, new GeekDoGalleryImagesDto(
            FrontCoverUrl: "https://cf.geekdo-images.com/lost-ruins-front.jpg",
            BackCoverUrl: "https://cf.geekdo-images.com/lost-ruins-back.jpg",
            TableOrGameplayUrl: "https://cf.geekdo-images.com/lost-ruins-table.jpg"
        ));

        var r2Options = Options.Create(new CloudflareR2Options { Simulate = true });
        var service = new BggImagesSyncService(
            _factory,
            _snapshotRepo,
            _geekDoClient,
            _storageService,
            r2Options,
            new HttpClient(),
            NullLogger<BggImagesSyncService>.Instance
        );

        // Act
        var result = await service.SyncTopRankedImagesBatchAsync(afterRank: 0, batchSize: 10, maxRank: 3000, delayMs: 0);

        // Assert
        Assert.Equal(1, result.UpdatedCount);
        var updated = await _context.Games.AsNoTracking().FirstAsync(g => g.BggId == 200);
        Assert.Equal("https://cf.geekdo-images.com/lost-ruins-front.jpg", updated.CoverImageUrl);
        Assert.Equal("https://cf.geekdo-images.com/lost-ruins-back.jpg", updated.BackCoverImageUrl);
        Assert.Equal("https://cf.geekdo-images.com/lost-ruins-table.jpg", updated.TableImageUrl);
    }

    [Fact]
    public async Task SyncTopRankedImagesBatchAsync_ShouldSkipAlreadyCompleteGames_Idempotently()
    {
        // Arrange: Juego #3 ya completo con todas sus fotos válidas
        var game = CreateGame(300, "Wingspan", bggRank: 3);
        game.UpdateMediaUrls(
            "https://cf.geekdo-images.com/wingspan-cover.jpg",
            "https://cf.geekdo-images.com/wingspan-thumb.jpg",
            "https://cf.geekdo-images.com/wingspan-back.jpg",
            "https://cf.geekdo-images.com/wingspan-table.jpg"
        );
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        var r2Options = Options.Create(new CloudflareR2Options { Simulate = true });
        var service = new BggImagesSyncService(
            _factory,
            _snapshotRepo,
            _geekDoClient,
            _storageService,
            r2Options,
            new HttpClient(),
            NullLogger<BggImagesSyncService>.Instance
        );

        // Act
        var result = await service.SyncTopRankedImagesBatchAsync(afterRank: 0, batchSize: 10, maxRank: 3000, delayMs: 0);

        // Assert
        Assert.Equal(0, result.EvaluatedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(3, result.LastRankProcessed);
    }

    [Fact]
    public async Task SyncTopRankedImagesBatchAsync_ShouldPaginateKeysetByRank()
    {
        // Arrange: 3 juegos con rangos 10, 20, 30
        var g1 = CreateGame(101, "Game 1", bggRank: 10);
        var g2 = CreateGame(102, "Game 2", bggRank: 20);
        var g3 = CreateGame(103, "Game 3", bggRank: 30);
        _context.Games.AddRange(g1, g2, g3);
        await _context.SaveChangesAsync();

        var r2Options = Options.Create(new CloudflareR2Options { Simulate = true });
        var service = new BggImagesSyncService(
            _factory,
            _snapshotRepo,
            _geekDoClient,
            _storageService,
            r2Options,
            new HttpClient(),
            NullLogger<BggImagesSyncService>.Instance
        );

        // Lote 1 con batchSize = 2
        var res1 = await service.SyncTopRankedImagesBatchAsync(afterRank: 0, batchSize: 2, maxRank: 3000, delayMs: 0);
        Assert.Equal(20, res1.LastRankProcessed);
        Assert.True(res1.HasMore);

        // Lote 2 con afterRank = 20
        var res2 = await service.SyncTopRankedImagesBatchAsync(afterRank: 20, batchSize: 2, maxRank: 3000, delayMs: 0);
        Assert.Equal(30, res2.LastRankProcessed);
        Assert.False(res2.HasMore);
    }

    private static Game CreateGame(int bggId, string title, int? bggRank = 100)
    {
        return new Game(
            bggId: bggId,
            originalTitle: title,
            spanishTitle: title,
            designer: "Autor",
            publisher: "Editorial",
            yearPublished: 2020,
            coverImageUrl: null,
            thumbnailUrl: null,
            description: "Desc",
            bggRating: 7.5,
            bggRank: bggRank,
            ludistRating: 7.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(10, 10),
            language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(30, 60, 20)
        );
    }

    private sealed class FakeGeekDoImagesClient : IGeekDoImagesClient
    {
        private readonly Dictionary<int, GeekDoGalleryImagesDto> _galleries = new();

        public void SetupGallery(int bggId, GeekDoGalleryImagesDto gallery)
        {
            _galleries[bggId] = gallery;
        }

        public Task<GeekDoGalleryImagesDto> GetTopVotedImagesAsync(int bggId, CancellationToken ct = default)
        {
            return Task.FromResult(_galleries.GetValueOrDefault(bggId, new GeekDoGalleryImagesDto(null, null, null)));
        }
    }

    private sealed class FakeImageStorageService : IImageStorageService
    {
        public Task<string> UploadOptimizedImageAsync(Stream inputStream, string objectKey, int maxWidth = 1000, int quality = 82, CancellationToken ct = default)
            => Task.FromResult($"https://cdn.ludeka.com/{objectKey}");

        public Task<ImageVariantUrls> UploadGameImageVariantsAsync(Stream rawImageStream, int bggId, string imageType, CancellationToken ct = default)
            => Task.FromResult(new ImageVariantUrls($"https://cdn.ludeka.com/games/{bggId}/{imageType}.webp", $"https://cdn.ludeka.com/games/{bggId}/{imageType}_thumb.webp"));

        public Task<bool> DeleteImageAsync(string objectKey, CancellationToken ct = default)
            => Task.FromResult(true);

        public string GetPublicUrl(string objectKey)
            => $"https://cdn.ludeka.com/{objectKey}";

        public Task<GameImageUploadResult> SaveGameCoverAsync(string slug, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default)
            => Task.FromResult(new GameImageUploadResult(true, "url", null));

        public Task<GameImageUploadResult> SaveEventPosterAsync(string eventSlugOrId, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default)
            => Task.FromResult(new GameImageUploadResult(true, "url", null));

        public Task<GameImageUploadResult> SaveCommunityImageAsync(string subfolder, string identifier, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default)
            => Task.FromResult(new GameImageUploadResult(true, "url", null));

        public Task<GameImageUploadResult> ValidateCoverUrlAsync(string imageUrl, CancellationToken ct = default)
            => Task.FromResult(new GameImageUploadResult(true, imageUrl, null));
    }

    private sealed class TestDbContextFactory : IDbContextFactory<LudekaDbContext>
    {
        private readonly DbContextOptions<LudekaDbContext> _options;

        public TestDbContextFactory(DbContextOptions<LudekaDbContext> options)
        {
            _options = options;
        }

        public LudekaDbContext CreateDbContext() => new(_options);

        public Task<LudekaDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new LudekaDbContext(_options));
    }
}
