using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Community;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class SocialIngestionServiceTests
{
    private class FakeSocialInboxRepository : ISocialInboxRepository
    {
        public List<SocialInboxItem> Items { get; } = new();

        public Task<IReadOnlyList<SocialInboxItem>> GetPendingAsync(SocialSubmissionType? typeFilter = null, CancellationToken ct = default)
        {
            var pending = Items.Where(i => i.Status == SocialInboxStatus.PendingReview);
            if (typeFilter.HasValue) pending = pending.Where(i => i.DetectedType == typeFilter.Value);
            return Task.FromResult<IReadOnlyList<SocialInboxItem>>(pending.ToList());
        }

        public Task<IReadOnlyList<SocialInboxItem>> GetAllAsync(SocialInboxStatus? statusFilter = null, SocialSubmissionType? typeFilter = null, int page = 1, int pageSize = 50, CancellationToken ct = default)
        {
            var query = Items.AsEnumerable();
            if (statusFilter.HasValue) query = query.Where(i => i.Status == statusFilter.Value);
            if (typeFilter.HasValue) query = query.Where(i => i.DetectedType == typeFilter.Value);
            return Task.FromResult<IReadOnlyList<SocialInboxItem>>(query.Skip((page - 1) * pageSize).Take(pageSize).ToList());
        }

        public Task<SocialInboxItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(Items.Find(i => i.Id == id));
        }

        public Task<int> GetPendingCountAsync(CancellationToken ct = default)
        {
            return Task.FromResult(Items.Count(i => i.Status == SocialInboxStatus.PendingReview));
        }

        public Task<SocialInboxItem> AddAsync(SocialInboxItem item, CancellationToken ct = default)
        {
            Items.Add(item);
            return Task.FromResult(item);
        }

        public Task UpdateAsync(SocialInboxItem item, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task<bool> ExistsBySourceUrlAsync(string sourceUrl, CancellationToken ct = default)
        {
            return Task.FromResult(Items.Any(i => i.SourceUrl.Equals(sourceUrl, StringComparison.OrdinalIgnoreCase)));
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Items.RemoveAll(i => i.Id == id);
            return Task.CompletedTask;
        }

        public Task<int> PurgeSimulatedAsync(CancellationToken ct = default)
        {
            var removed = Items.RemoveAll(i =>
                i.Status == SocialInboxStatus.PendingReview &&
                (i.SourceUrl.Contains("sim_") || i.SourceUrl.Contains("/simulated/")));
            return Task.FromResult(removed);
        }
    }

    private class FakeSocialMetadataExtractor : ISocialMetadataExtractor
    {
        public SocialMetadataResultDto? ResultToReturn { get; set; }

        public Task<SocialMetadataResultDto?> ExtractFromUrlAsync(string url, CancellationToken ct = default)
        {
            return Task.FromResult(ResultToReturn);
        }
    }

    private class FakeSocialAiAnalysisService : ISocialAiAnalysisService
    {
        public SocialAiAnalysisResultDto ResultToReturn { get; set; } = new(
            DetectedType: SocialSubmissionType.Giveaway,
            Title: "Sorteo Ark Nova",
            OrganizerOrAuthor: "Maldito Games",
            Collaborator: null,
            SuggestedGameTitle: "Ark Nova",
            EventOrReleaseDate: DateTimeOffset.UtcNow.AddDays(5),
            EventEndDate: null,
            Location: null,
            EstimatedPvp: null,
            MediaCategory: null,
            PlayerCountBadge: null,
            Notes: null);

        public Task<SocialAiAnalysisResultDto> AnalyzeTextAsync(string text, string? authorOrChannel = null, CancellationToken ct = default)
        {
            return Task.FromResult(ResultToReturn);
        }

        public Task<SocialAiAnalysisResultDto> AnalyzeMultimodalAsync(
            string? rulesText = null,
            byte[]? rulesImageBytes = null,
            string? rulesImageMimeType = null,
            byte[]? coverImageBytes = null,
            string? coverImageMimeType = null,
            string? authorOrChannel = null,
            CancellationToken ct = default)
        {
            return Task.FromResult(ResultToReturn);
        }
    }

    private class FakeImageStorageService : IImageStorageService
    {
        public Task<string> UploadOptimizedImageAsync(Stream inputStream, string objectKey, int maxWidth = 1000, int quality = 82, CancellationToken ct = default)
        {
            return Task.FromResult($"https://cdn.ludeka.com/{objectKey}");
        }

        public Task<ImageVariantUrls> UploadGameImageVariantsAsync(Stream rawImageStream, int bggId, string imageType, CancellationToken ct = default)
        {
            return Task.FromResult(new ImageVariantUrls($"https://cdn.ludeka.com/games/{bggId}/{imageType}.webp", null));
        }

        public Task<bool> DeleteImageAsync(string objectKey, CancellationToken ct = default) => Task.FromResult(true);
        public string GetPublicUrl(string objectKey) => $"https://cdn.ludeka.com/{objectKey}";
        public Task<GameImageUploadResult> SaveGameCoverAsync(string slug, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default)
            => Task.FromResult(new GameImageUploadResult(true, "https://cdn.ludeka.com/cover.webp", null));
        public Task<GameImageUploadResult> SaveEventPosterAsync(string eventSlugOrId, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default)
            => Task.FromResult(new GameImageUploadResult(true, "https://cdn.ludeka.com/poster.webp", null));
        public Task<GameImageUploadResult> SaveCommunityImageAsync(string folder, string entityId, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default)
            => Task.FromResult(new GameImageUploadResult(true, "https://cdn.ludeka.com/comm.webp", null));
        public Task<GameImageUploadResult> ValidateCoverUrlAsync(string coverUrl, CancellationToken ct = default)
            => Task.FromResult(new GameImageUploadResult(true, coverUrl, null));
    }

    private class FakeGameRepository : IGameRepository
    {
        public List<Game> Games { get; } = new();

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Games.Find(g => g.Id == id));
        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Games.Find(g => g.Slug == slug));
        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult(Games.Find(g => g.BggId == bggId));
        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            var matches = Games.Where(g => string.IsNullOrWhiteSpace(criteria.SearchTerm) || g.SpanishTitle.Contains(criteria.SearchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
            return Task.FromResult(((IReadOnlyList<Game>)matches, matches.Count));
        }
        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default) { Games.AddRange(games); return Task.CompletedTask; }
        public Task UpdateAsync(Game game, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(Games.Count > 0);
    }

    private class FakeGiveawayRepository : IGiveawayRepository
    {
        public List<Giveaway> Items { get; } = new();
        public Task<IReadOnlyList<Giveaway>> GetGiveawaysAsync(bool includeExpired = false, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Giveaway>>(Items);
        public Task<Giveaway?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.Find(g => g.Id == id));
        public Task<Giveaway?> FindDuplicateOrCollaborativeAsync(string title, string organizer, DateTimeOffset deadline, CancellationToken ct = default)
        {
            var cleanTitle = title.Trim().ToLowerInvariant();
            var cleanOrganizer = organizer.Trim().ToLowerInvariant();
            var match = Items.FirstOrDefault(g =>
                !g.IsExpired &&
                g.Title.Trim().ToLowerInvariant().Equals(cleanTitle, StringComparison.OrdinalIgnoreCase) &&
                (g.Organizer.Trim().ToLowerInvariant().Contains(cleanOrganizer) ||
                 cleanOrganizer.Contains(g.Organizer.Trim().ToLowerInvariant()) ||
                 (g.Collaborator != null && g.Collaborator.Trim().ToLowerInvariant().Contains(cleanOrganizer))));
            return Task.FromResult(match);
        }
        public Task AddAsync(Giveaway giveaway, CancellationToken ct = default) { Items.Add(giveaway); return Task.CompletedTask; }
        public Task UpdateAsync(Giveaway giveaway, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(g => g.Id == id); return Task.CompletedTask; }
    }

    private class FakeWeeklyReleaseRepository : IWeeklyReleaseRepository
    {
        public List<WeeklyRelease> Items { get; } = new();
        public Task<IReadOnlyList<WeeklyRelease>> GetCurrentWeekReleasesAsync(DateOnly? referenceDate = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WeeklyRelease>>(Items);
        public Task<IReadOnlyList<WeeklyRelease>> GetReleasesAsync(DateOnly? weekOf = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WeeklyRelease>>(Items);
        public Task<WeeklyRelease?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.Find(r => r.Id == id));
        public Task AddAsync(WeeklyRelease release, CancellationToken ct = default) { Items.Add(release); return Task.CompletedTask; }
        public Task UpdateAsync(WeeklyRelease release, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(r => r.Id == id); return Task.CompletedTask; }
        public Task<IReadOnlyList<WeeklyRelease>> GetAllReleasesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WeeklyRelease>>(Items);
    }

    private class FakeBoardGameEventRepository : IBoardGameEventRepository
    {
        public List<BoardGameEvent> Items { get; } = new();
        public Task<IReadOnlyList<BoardGameEvent>> GetUpcomingEventsAsync(string? country = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BoardGameEvent>>(Items);
        public Task<IReadOnlyList<BoardGameEvent>> GetUpcomingEventsAsync(int count, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BoardGameEvent>>(Items.Take(count).ToList());
        public Task<IReadOnlyList<BoardGameEvent>> GetPastEventsAsync(string? country = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BoardGameEvent>>(Items);
        public Task<BoardGameEvent?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.Find(e => e.Id == id));
        public Task AddAsync(BoardGameEvent gameEvent, CancellationToken ct = default) { Items.Add(gameEvent); return Task.CompletedTask; }
        public Task UpdateAsync(BoardGameEvent gameEvent, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(e => e.Id == id); return Task.CompletedTask; }
        public Task<IReadOnlyList<BoardGameEvent>> GetAllEventsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BoardGameEvent>>(Items);
    }

    private class FakeMediaRepository : IMediaRepository
    {
        public List<MediaItem> Items { get; } = new();
        public Task<IReadOnlyList<MediaItem>> GetByGameIdAsync(Guid gameId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaItem>>(Items.Where(m => m.GameId == gameId).ToList());
        public Task<IReadOnlyList<MediaItem>> GetApprovedByGameIdAsync(Guid gameId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaItem>>(Items.Where(m => m.GameId == gameId && m.Status == ModerationStatus.Approved).ToList());
        public Task<IReadOnlyList<MediaItem>> GetPendingModerationAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaItem>>(Items);
        public Task<IReadOnlyList<MediaItem>> GetOrphansAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaItem>>(Items.Where(m => m.IsOrphan).ToList());
        public Task<IReadOnlyList<MediaItem>> GetApprovedAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaItem>>(Items.Where(m => m.Status == ModerationStatus.Approved).ToList());
        public Task<bool> ExistsByUrlAsync(string url, CancellationToken ct = default) => Task.FromResult(Items.Any(m => m.Url == url));
        public Task<MediaItem?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.Find(m => m.Id == id));
        public Task AddAsync(MediaItem item, CancellationToken ct = default) { Items.Add(item); return Task.CompletedTask; }
        public Task UpdateAsync(MediaItem item, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(m => m.Id == id); return Task.CompletedTask; }
        public Task<IReadOnlyList<MediaItem>> GetOrphanMediaAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaItem>>(Items.Where(m => m.IsOrphan).ToList());
        public Task<IReadOnlyList<MediaItem>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaItem>>(Items);
        public Task<IReadOnlyList<MediaItem>> GetFilteredAsync(Guid? gameId, MediaCategory? category, MediaType? type, ModerationStatus? status, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaItem>>(Items);
    }

    private class FakePublisherRepository : IPublisherRepository
    {
        public List<Publisher> Items { get; } = new();
        public Task<IReadOnlyList<Publisher>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Publisher>>(Items);
        public Task<Publisher?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.Find(p => p.Id == id));
        public Task<Publisher?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Items.Find(p => p.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase)));
        public Task<Publisher?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(Items.Find(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        public Task AddAsync(Publisher publisher, CancellationToken ct = default) { Items.Add(publisher); return Task.CompletedTask; }
        public Task UpdateAsync(Publisher publisher, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(p => p.Id == id); return Task.CompletedTask; }
    }

    private class FakeStoreRepository : IStoreRepository
    {
        public List<Store> Items { get; } = new();
        public Task<IReadOnlyList<Store>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Store>>(Items);
        public Task<Store?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.Find(s => s.Id == id));
        public Task<Store?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Items.Find(s => s.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase)));
        public Task<Store?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(Items.Find(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        public Task AddAsync(Store store, CancellationToken ct = default) { Items.Add(store); return Task.CompletedTask; }
        public Task UpdateAsync(Store store, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(s => s.Id == id); return Task.CompletedTask; }
    }

    private class FakeCreatorRepository : ICreatorRepository
    {
        public List<Creator> Items { get; } = new();
        public Task<IReadOnlyList<Creator>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Creator>>(Items);
        public Task<Creator?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.Find(c => c.Id == id));
        public Task<Creator?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Items.Find(c => c.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase)));
        public Task<Creator?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(Items.Find(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        public Task AddAsync(Creator creator, CancellationToken ct = default) { Items.Add(creator); return Task.CompletedTask; }
        public Task UpdateAsync(Creator creator, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(c => c.Id == id); return Task.CompletedTask; }
    }

    private readonly FakeSocialInboxRepository _inboxRepo = new();
    private readonly FakeSocialMetadataExtractor _metadataExtractor = new();
    private readonly FakeSocialAiAnalysisService _aiService = new();
    private readonly FakeImageStorageService _imageStorageService = new();
    private readonly FakeGameRepository _gameRepo = new();
    private readonly FakeGiveawayRepository _giveawayRepo = new();
    private readonly FakeWeeklyReleaseRepository _releaseRepo = new();
    private readonly FakeBoardGameEventRepository _eventRepo = new();
    private readonly FakeMediaRepository _mediaRepo = new();

    private class FakeGiveawayCoverComposer : IGiveawayCoverComposer
    {
        public bool WasCalled { get; set; }
        public byte[] ComposeHorizontalCover(byte[] inputImageBytes, NormalizedBoundingBoxDto? cropBox = null, int targetWidth = 1200, int targetHeight = 675)
        {
            WasCalled = true;
            return new byte[] { 1, 2, 3, 4 };
        }
    }

    private readonly FakeGiveawayCoverComposer _coverComposer = new();

    private SocialIngestionService CreateService(
        IGiveawayCoverComposer? composer = null,
        IPublisherRepository? publisherRepo = null,
        IStoreRepository? storeRepo = null,
        ICreatorRepository? creatorRepo = null)
    {
        return new SocialIngestionService(
            _inboxRepo,
            _metadataExtractor,
            _aiService,
            _imageStorageService,
            _gameRepo,
            _giveawayRepo,
            _releaseRepo,
            _eventRepo,
            _mediaRepo,
            new HttpClient(),
            NullLogger<SocialIngestionService>.Instance,
            permissionGuard: null,
            giveawayCoverComposer: composer ?? _coverComposer,
            publisherRepository: publisherRepo,
            storeRepository: storeRepo,
            creatorRepository: creatorRepo);
    }

    [Fact]
    public async Task IngestFromUrlAsync_ValidGiveawayUrl_CreatesPendingItemInInbox()
    {
        // Arrange
        var service = CreateService();
        _metadataExtractor.ResultToReturn = new SocialMetadataResultDto(
            Url: "https://www.instagram.com/p/test123/",
            Platform: SocialPlatform.Instagram,
            Title: "Post de sorteo",
            AuthorOrChannel: "Maldito Games",
            Description: "Sorteo Ark Nova",
            ImageUrl: null,
            IsVideo: false);

        _aiService.ResultToReturn = new SocialAiAnalysisResultDto(
            DetectedType: SocialSubmissionType.Giveaway,
            Title: "Sorteo Oficial Ark Nova",
            OrganizerOrAuthor: "Maldito Games",
            Collaborator: null,
            SuggestedGameTitle: "Ark Nova",
            EventOrReleaseDate: DateTimeOffset.UtcNow.AddDays(7),
            EventEndDate: null,
            Location: null,
            EstimatedPvp: null,
            MediaCategory: null,
            PlayerCountBadge: null,
            Notes: null);

        // Act
        var result = await service.IngestFromUrlAsync("https://www.instagram.com/p/test123/");

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(SocialInboxStatus.PendingReview, result.Status);
        Assert.Equal(SocialSubmissionType.Giveaway, result.DetectedType);
        Assert.Equal("Sorteo Oficial Ark Nova", result.Title);
        Assert.Equal("Maldito Games", result.OrganizerOrAuthor);
        Assert.Equal(1, _inboxRepo.Items.Count);
    }

    [Fact]
    public async Task IngestManualAdvancedAsync_ValidInput_CreatesPendingItem()
    {
        // Arrange
        var service = CreateService();
        var gameId = Guid.NewGuid();

        var input = new SocialInboxManualInputDto(
            SourceUrl: "https://www.youtube.com/watch?v=12345",
            SubmissionType: SocialSubmissionType.MediaItem,
            Title: "Partida completa Ark Nova a 2",
            OrganizerOrAuthor: "La Mazmorra de Pacheco",
            GameId: gameId,
            GameTitle: "Ark Nova",
            MediaCategory: MediaCategory.Gameplay,
            PlayerCountBadge: "Partida a 2");

        // Act
        var result = await service.IngestManualAdvancedAsync(input);

        // Assert
        Assert.Equal(SocialSubmissionType.MediaItem, result.DetectedType);
        Assert.Equal("Partida completa Ark Nova a 2", result.Title);
        Assert.Equal(gameId, result.GameId);
        Assert.Equal("Partida a 2", result.PlayerCountBadge);
        Assert.Equal(MediaCategory.Gameplay, result.MediaCategory);
        Assert.True(result.IsVideo);
    }

    [Fact]
    public async Task UpdateItemAsync_WhenPending_UpdatesDetails()
    {
        // Arrange
        var service = CreateService();
        var initial = new SocialInboxItem(
            sourceUrl: "https://instagram.com/p/abc",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Título Inicial",
            organizerOrAuthor: "Organizador Inicial");

        _inboxRepo.Items.Add(initial);

        var updateDto = new SocialInboxUpdateDto(
            Id: initial.Id,
            Title: "Título Rectificado",
            OrganizerOrAuthor: "Devir",
            Collaborator: "Zacatrus",
            DetectedType: SocialSubmissionType.Giveaway,
            GameId: null,
            GameTitle: "Cascadia",
            EventOrReleaseDate: DateTimeOffset.UtcNow.AddDays(3),
            EventEndDate: null,
            Location: null,
            EstimatedPvp: null,
            MediaCategory: null,
            PlayerCountBadge: null,
            ThumbnailUrl: "https://cdn.ludeka.com/custom.webp",
            ModeratorNotes: "Corregido");

        // Act
        var updated = await service.UpdateItemAsync(updateDto);

        // Assert
        Assert.Equal("Título Rectificado", updated.Title);
        Assert.Equal("Devir", updated.OrganizerOrAuthor);
        Assert.Equal("Zacatrus", updated.Collaborator);
        Assert.Equal("Cascadia", updated.GameTitle);
        Assert.Equal("Corregido", updated.ModeratorNotes);
    }

    [Fact]
    public async Task ApproveAndPublishAsync_GiveawayItem_CreatesGiveawayAndSetsApproved()
    {
        // Arrange
        var service = CreateService();
        var item = new SocialInboxItem(
            sourceUrl: "https://instagram.com/p/giveaway",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo Brass Birmingham",
            organizerOrAuthor: "Maldito Games",
            eventOrReleaseDate: DateTimeOffset.UtcNow.AddDays(5));

        _inboxRepo.Items.Add(item);

        // Act
        var createdId = await service.ApproveAndPublishAsync(item.Id, "admin_user");

        // Assert
        Assert.NotEqual(Guid.Empty, createdId);
        Assert.Equal(SocialInboxStatus.Approved, item.Status);
        Assert.Equal(createdId, item.CreatedEntityId);
        Assert.Single(_giveawayRepo.Items);
        Assert.Equal("Sorteo Brass Birmingham", _giveawayRepo.Items[0].Title);
    }

    [Fact]
    public async Task ApproveAndPublishAsync_GiveawayWithPastDeadline_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        var item = new SocialInboxItem(
            sourceUrl: "https://instagram.com/p/past-giveaway",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo Con Fecha Pasada",
            organizerOrAuthor: "Asmodee",
            eventOrReleaseDate: DateTimeOffset.UtcNow.AddDays(-3)); // Fecha en el pasado

        _inboxRepo.Items.Add(item);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApproveAndPublishAsync(item.Id, "admin_user"));

        Assert.Contains("pasado", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApproveAndPublishAsync_GiveawayWithoutDeadline_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        var item = new SocialInboxItem(
            sourceUrl: "https://instagram.com/p/no-deadline-giveaway",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo Sin Fecha",
            organizerOrAuthor: "Asmodee",
            eventOrReleaseDate: null,
            eventEndDate: null);

        _inboxRepo.Items.Add(item);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApproveAndPublishAsync(item.Id, "admin_user"));

        Assert.Contains("fecha de fin", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApproveAndPublishAsync_GiveawayWithoutLocation_ResolvesCountryFromPublisher()
    {
        // Arrange
        var publisherRepo = new FakePublisherRepository();
        publisherRepo.Items.Add(new Publisher(
            name: "Devir Argentina",
            slug: "devir-argentina",
            country: "Argentina"));

        var service = CreateService(publisherRepo: publisherRepo);
        var item = new SocialInboxItem(
            sourceUrl: "https://instagram.com/p/argentina-giveaway",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo Catan",
            organizerOrAuthor: "Devir Argentina",
            eventOrReleaseDate: DateTimeOffset.UtcNow.AddDays(5),
            location: null);

        _inboxRepo.Items.Add(item);

        // Act
        var createdId = await service.ApproveAndPublishAsync(item.Id, "admin_user");

        // Assert
        Assert.NotEqual(Guid.Empty, createdId);
        var created = _giveawayRepo.Items.Find(g => g.Id == createdId);
        Assert.NotNull(created);
        Assert.Equal("Argentina", created.Country);
    }

    [Fact]
    public async Task ApproveAndPublishAsync_GiveawayWithoutLocation_ResolvesCountryFromStore()
    {
        // Arrange
        var storeRepo = new FakeStoreRepository();
        storeRepo.Items.Add(new Store(
            name: "Dungeon Dice Chile",
            slug: "dungeon-dice-chile",
            country: "Chile"));

        var service = CreateService(storeRepo: storeRepo);
        var item = new SocialInboxItem(
            sourceUrl: "https://instagram.com/p/chile-giveaway",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo Dixit",
            organizerOrAuthor: "Dungeon Dice Chile",
            eventOrReleaseDate: DateTimeOffset.UtcNow.AddDays(4),
            location: null);

        _inboxRepo.Items.Add(item);

        // Act
        var createdId = await service.ApproveAndPublishAsync(item.Id, "admin_user");

        // Assert
        Assert.NotEqual(Guid.Empty, createdId);
        var created = _giveawayRepo.Items.Find(g => g.Id == createdId);
        Assert.NotNull(created);
        Assert.Equal("Chile", created.Country);
    }

    [Fact]
    public async Task IngestFromCollectorAsync_GiveawayWithoutLocation_ResolvesCountryFromCreator()
    {
        // Arrange
        var creatorRepo = new FakeCreatorRepository();
        creatorRepo.Items.Add(new Creator(
            name: "Meeple Colombia",
            slug: "meeple-colombia",
            nationality: "Colombia"));

        var service = CreateService(creatorRepo: creatorRepo);
        _metadataExtractor.ResultToReturn = new SocialMetadataResultDto(
            Url: "https://www.youtube.com/watch?v=colombia123",
            Platform: SocialPlatform.YouTube,
            Title: "Sorteo Especial",
            AuthorOrChannel: "Meeple Colombia",
            Description: "Sorteamos un juego",
            ImageUrl: null,
            IsVideo: true);

        _aiService.ResultToReturn = new SocialAiAnalysisResultDto(
            DetectedType: SocialSubmissionType.Giveaway,
            Title: "Sorteo Especial Meeple Colombia",
            OrganizerOrAuthor: "Meeple Colombia",
            Collaborator: null,
            SuggestedGameTitle: "Carcassonne",
            EventOrReleaseDate: DateTimeOffset.UtcNow.AddDays(6),
            EventEndDate: null,
            Location: null,
            EstimatedPvp: null,
            MediaCategory: null,
            PlayerCountBadge: null,
            Notes: null);

        // Act
        var result = await service.IngestFromCollectorAsync("https://www.youtube.com/watch?v=colombia123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Colombia", result.Location);
    }

    [Fact]
    public async Task ApproveAndPublishAsync_GiveawayWithEventEndDate_PrefersEndDateOverStartDate()
    {
        // Arrange
        var service = CreateService();
        var futureEnd = DateTimeOffset.UtcNow.AddDays(12);
        var item = new SocialInboxItem(
            sourceUrl: "https://instagram.com/p/with-end-date",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo Con Fin Explícito",
            organizerOrAuthor: "Devir",
            eventOrReleaseDate: DateTimeOffset.UtcNow.AddDays(-2),
            eventEndDate: futureEnd);

        _inboxRepo.Items.Add(item);

        // Act
        var createdId = await service.ApproveAndPublishAsync(item.Id, "admin_user");

        // Assert
        Assert.Single(_giveawayRepo.Items);
        var created = _giveawayRepo.Items[0];
        Assert.Equal(futureEnd, created.DeadlineAt);
        Assert.False(created.IsExpired);
    }

    [Theory]
    [InlineData("Península", "España", false)]
    [InlineData("Baleares", "España", false)]
    [InlineData("Canarias", "España", false)]
    [InlineData("Internacional", "Internacional", true)]
    [InlineData("Mundial", "Internacional", true)]
    [InlineData("México", "México", false)]
    public async Task ApproveAndPublishAsync_GiveawayWithRegionalLocation_NormalizesCountryCorrectly(
        string rawLocation, string expectedCountry, bool expectedInternational)
    {
        // Arrange
        var service = CreateService();
        var item = new SocialInboxItem(
            sourceUrl: $"https://instagram.com/p/location-{Guid.NewGuid():N}",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo con Ámbito Territorial",
            organizerOrAuthor: "TCG Factory",
            eventOrReleaseDate: DateTimeOffset.UtcNow.AddDays(7),
            location: rawLocation);

        _inboxRepo.Items.Add(item);

        // Act
        await service.ApproveAndPublishAsync(item.Id, "admin_user");

        // Assert
        var created = _giveawayRepo.Items.Last();
        Assert.Equal(expectedCountry, created.Country);
        Assert.Equal(expectedInternational, created.IsInternational);
    }

    [Fact]
    public async Task ApproveAndPublishAsync_GiveawayDuplicateOrCollaborative_MergesCollaboratorAndExtendsDeadline()
    {
        // Arrange
        var service = CreateService();
        var existingDeadline = DateTimeOffset.UtcNow.AddDays(3);
        var existingGiveaway = new Giveaway(
            title: "Sorteo Cascadia",
            organizer: "Devir",
            url: "https://instagram.com/p/first",
            platform: GiveawayPlatform.Instagram,
            deadlineAt: existingDeadline,
            collaborator: "Meepletopia");
        _giveawayRepo.Items.Add(existingGiveaway);

        var laterDeadline = DateTimeOffset.UtcNow.AddDays(8);
        var newItem = new SocialInboxItem(
            sourceUrl: "https://instagram.com/p/second",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo Cascadia",
            organizerOrAuthor: "Devir",
            collaborator: "Análisis Parálisis",
            eventEndDate: laterDeadline);

        _inboxRepo.Items.Add(newItem);

        // Act
        var createdId = await service.ApproveAndPublishAsync(newItem.Id, "admin_user");

        // Assert: Reutiliza el ID del sorteo existente sin duplicar la entidad
        Assert.Equal(existingGiveaway.Id, createdId);
        Assert.Single(_giveawayRepo.Items);
        Assert.Contains("Meepletopia", existingGiveaway.Collaborator);
        Assert.Contains("Análisis Parálisis", existingGiveaway.Collaborator);
        Assert.Equal(laterDeadline, existingGiveaway.DeadlineAt);
    }

    [Fact]
    public async Task ApproveAndPublishAsync_BoardGameEventItem_CreatesEventAndSetsApproved()
    {
        // Arrange
        var service = CreateService();
        var item = new SocialInboxItem(
            sourceUrl: "https://interocio.es",
            platform: SocialPlatform.Website,
            detectedType: SocialSubmissionType.BoardGameEvent,
            title: "Feria InterOcio 2027",
            organizerOrAuthor: "InterOcio",
            eventOrReleaseDate: DateTimeOffset.UtcNow.AddMonths(2),
            location: "IFEMA Madrid");

        _inboxRepo.Items.Add(item);

        // Act
        var createdId = await service.ApproveAndPublishAsync(item.Id, "admin_user");

        // Assert
        Assert.Equal(SocialInboxStatus.Approved, item.Status);
        Assert.Single(_eventRepo.Items);
        Assert.Equal("Feria InterOcio 2027", _eventRepo.Items[0].Title);
        Assert.Equal("IFEMA Madrid", _eventRepo.Items[0].Location);
    }

    [Fact]
    public async Task RejectItemAsync_ValidReason_SetsStatusRejected()
    {
        // Arrange
        var service = CreateService();
        var item = new SocialInboxItem(
            sourceUrl: "https://instagram.com/p/spam",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Publicación No Relevante",
            organizerOrAuthor: "Desconocido");

        _inboxRepo.Items.Add(item);

        // Act
        await service.RejectItemAsync(item.Id, "No es juego de mesa", "admin_user");

        // Assert
        Assert.Equal(SocialInboxStatus.Rejected, item.Status);
        Assert.Equal("No es juego de mesa", item.ModeratorNotes);
        Assert.Equal("admin_user", item.ReviewedByUserId);
    }

    [Fact]
    public async Task IngestMultimodalAsync_WithCoverImage_ComposesHorizontalCoverAndStoresInInbox()
    {
        // Arrange
        var service = CreateService();
        var input = new SocialExpressMultimodalInputDto(
            SourceUrl: "https://www.instagram.com/p/multimodal-test/",
            ManualCaption: "Sorteo Ark Nova bases completas",
            CoverImageBytes: new byte[] { 1, 2, 3, 4 },
            CoverImageMimeType: "image/jpeg");

        // Act
        var result = await service.IngestMultimodalAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.True(_coverComposer.WasCalled);
        Assert.Equal("Sorteo Ark Nova", result.Title);
        Assert.Contains("social-inbox", result.ThumbnailUrl);
        Assert.Single(_inboxRepo.Items);
        Assert.Equal(SocialInboxStatus.PendingReview, _inboxRepo.Items[0].Status);
    }

    [Fact]
    public async Task IngestMultimodalAsync_WithoutManualCaption_PopulatesOriginalCaptionFromAiExtractedText()
    {
        // Arrange
        var service = CreateService();
        _aiService.ResultToReturn = _aiService.ResultToReturn with
        {
            Title = "Sorteo Nippon: Zaibatsu",
            OrganizerOrAuthor = "Turol Games",
            ExtractedText = "Bases del sorteo:\n1. Seguir a @turolgames y @malditogames\n2. Mencionar a 2 amigos\nFecha fin: 10 de octubre"
        };

        var input = new SocialExpressMultimodalInputDto(
            SourceUrl: "https://www.instagram.com/p/nippon-test/",
            ManualCaption: null,
            BasesImageBytes: new byte[] { 1, 2, 3 },
            BasesImageMimeType: "image/jpeg");

        // Act
        var result = await service.IngestMultimodalAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Bases del sorteo:\n1. Seguir a @turolgames y @malditogames\n2. Mencionar a 2 amigos\nFecha fin: 10 de octubre", result.OriginalCaption);
    }

    [Fact]
    public async Task IngestMultimodalAsync_MissingAllInputs_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        var input = new SocialExpressMultimodalInputDto(
            SourceUrl: "https://www.instagram.com/p/empty-test/",
            ManualCaption: null,
            CoverImageBytes: null,
            BasesImageBytes: null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.IngestMultimodalAsync(input));
    }

    [Fact]
    public async Task IngestMultimodalAsync_EmptySourceUrl_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();
        var input = new SocialExpressMultimodalInputDto(
            SourceUrl: "   ",
            ManualCaption: "Texto de prueba");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.IngestMultimodalAsync(input));
    }

    [Fact]
    public async Task IngestFromCollectorAsync_BlockedInstagramUrlWithoutContent_ThrowsInvalidOperationException()
    {
        // Arrange: Metadata nulo (página bloqueada por login) y sin texto manual
        var service = CreateService();
        _metadataExtractor.ResultToReturn = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.IngestFromCollectorAsync("https://www.instagram.com/reel/blocked123/", manualCaption: null));

        Assert.Contains("Alta Exprés Multimodal", ex.Message);
    }

    [Theory]
    [InlineData("Análisis Gemini Flash Vision", true)]
    [InlineData("Gemini 1.5 Flash completado con éxito", true)]
    [InlineData("Detección heurística de patrones editoriales en español", false)]
    [InlineData("Detección heuristica sin acento", false)]
    [InlineData("Alta asistida manual avanzada", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void SocialInboxItemDto_IsAiProcessed_IdentifiesAiCorrectly(string? notes, bool expected)
    {
        var item = new SocialInboxItem(
            sourceUrl: "https://example.com/post",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Test",
            organizerOrAuthor: "Test Org",
            aiAnalysisNotes: notes);

        var dto = SocialInboxItemDto.FromEntity(item);

        Assert.Equal(expected, dto.IsAiProcessed);
    }

    [Fact]
    public async Task IngestFromUrlAsync_WhenSourceUrlAlreadyExists_ThrowsInvalidOperationException()
    {
        var service = CreateService();
        var existingUrl = "https://www.instagram.com/p/existing-post-123/";
        _inboxRepo.Items.Add(new SocialInboxItem(
            sourceUrl: existingUrl,
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Ya registrado",
            organizerOrAuthor: "malditogames"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.IngestFromUrlAsync(existingUrl, "Texto manual"));

        Assert.Contains("Ya existe una publicación registrada", ex.Message);
    }

    [Fact]
    public async Task IngestMultimodalAsync_WhenSourceUrlAlreadyExists_ThrowsInvalidOperationException()
    {
        var service = CreateService();
        var existingUrl = "https://www.instagram.com/p/existing-multimodal/";
        _inboxRepo.Items.Add(new SocialInboxItem(
            sourceUrl: existingUrl,
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Ya registrado",
            organizerOrAuthor: "malditogames"));

        var input = new SocialExpressMultimodalInputDto(
            SourceUrl: existingUrl,
            ManualCaption: "Texto descriptivo",
            CoverImageBytes: new byte[] { 1, 2, 3 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.IngestMultimodalAsync(input));

        Assert.Contains("Ya existe una publicación registrada", ex.Message);
    }

    [Fact]
    public async Task IngestManualAdvancedAsync_WhenSourceUrlAlreadyExists_ThrowsInvalidOperationException()
    {
        var service = CreateService();
        var existingUrl = "https://www.instagram.com/p/existing-manual/";
        _inboxRepo.Items.Add(new SocialInboxItem(
            sourceUrl: existingUrl,
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Ya registrado",
            organizerOrAuthor: "malditogames"));

        var input = new SocialInboxManualInputDto(
            SourceUrl: existingUrl,
            SubmissionType: SocialSubmissionType.Giveaway,
            Title: "Manual Nuevo",
            OrganizerOrAuthor: "Editorial");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.IngestManualAdvancedAsync(input));

        Assert.Contains("Ya existe una publicación registrada", ex.Message);
    }

    [Fact]
    public async Task ReanalyzeWithAiAsync_ItemNotFound_ThrowsKeyNotFoundException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ReanalyzeWithAiAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ReanalyzeWithAiAsync_ItemNotPendingReview_ThrowsInvalidOperationException()
    {
        var service = CreateService();
        var item = new SocialInboxItem(
            sourceUrl: "https://instagram.com/p/approved/",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo",
            organizerOrAuthor: "devir");
        item.Approve(Guid.NewGuid(), "admin");
        _inboxRepo.Items.Add(item);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReanalyzeWithAiAsync(item.Id));

        Assert.Contains("Solo se pueden reanalizar publicaciones pendientes", ex.Message);
    }

    [Fact]
    public async Task ReanalyzeWithAiAsync_AiReturnsHeuristicFallback_ThrowsInvalidOperationException()
    {
        var service = CreateService();
        var item = new SocialInboxItem(
            sourceUrl: "https://instagram.com/p/heuristic/",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo inicial",
            organizerOrAuthor: "devir",
            originalCaption: "Participa en el sorteo de Catan",
            aiAnalysisNotes: "Detección heurística de patrones editoriales en español");
        _inboxRepo.Items.Add(item);

        _aiService.ResultToReturn = new SocialAiAnalysisResultDto(
            DetectedType: SocialSubmissionType.Giveaway,
            Title: "Sorteo Catan",
            OrganizerOrAuthor: "devir",
            Collaborator: null,
            SuggestedGameTitle: "Catan",
            EventOrReleaseDate: null,
            EventEndDate: null,
            Location: null,
            EstimatedPvp: null,
            MediaCategory: null,
            PlayerCountBadge: null,
            Notes: "Detección heurística de patrones editoriales en español");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReanalyzeWithAiAsync(item.Id));

        Assert.Contains("El servicio de IA no está disponible", ex.Message);
    }

    [Fact]
    public async Task ReanalyzeWithAiAsync_AiSucceeds_UpdatesItemAndReturnsAiProcessedTrue()
    {
        var service = CreateService();
        var item = new SocialInboxItem(
            sourceUrl: "https://instagram.com/p/real-ai/",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo antiguo",
            organizerOrAuthor: "editorial",
            originalCaption: "¡Sorteamos un ejemplar exclusivo de Ark Nova!",
            aiAnalysisNotes: "Detección heurística de patrones editoriales en español");
        _inboxRepo.Items.Add(item);

        _aiService.ResultToReturn = new SocialAiAnalysisResultDto(
            DetectedType: SocialSubmissionType.Giveaway,
            Title: "Sorteo Oficial de Ark Nova",
            OrganizerOrAuthor: "Maldito Games",
            Collaborator: "@ludocreador",
            SuggestedGameTitle: "Ark Nova",
            EventOrReleaseDate: DateTimeOffset.UtcNow.AddDays(7),
            EventEndDate: null,
            Location: "España Peninsular",
            EstimatedPvp: 65m,
            MediaCategory: null,
            PlayerCountBadge: null,
            Notes: "Extracción Gemini Flash 1.5 con alta certidumbre");

        var result = await service.ReanalyzeWithAiAsync(item.Id);

        Assert.NotNull(result);
        Assert.True(result.IsAiProcessed);
        Assert.Equal("Sorteo Oficial de Ark Nova", result.Title);
        Assert.Equal("Maldito Games", result.OrganizerOrAuthor);
        Assert.Equal("@ludocreador", result.Collaborator);
        Assert.Equal("España", result.Location);
        Assert.Equal("Extracción Gemini Flash 1.5 con alta certidumbre", result.AiAnalysisNotes);
    }

    [Fact]
    public async Task PurgeSimulatedItemsAsync_CallsRepositoryAndReturnsPurgedCount()
    {
        var service = CreateService();
        _inboxRepo.Items.Add(new SocialInboxItem(
            sourceUrl: "https://instagram.com/p/sim_123/",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Simulado 1",
            organizerOrAuthor: "org"));
        _inboxRepo.Items.Add(new SocialInboxItem(
            sourceUrl: "https://instagram.com/p/real_post/",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Real",
            organizerOrAuthor: "org"));

        var count = await service.PurgeSimulatedItemsAsync();

        Assert.Equal(1, count);
        Assert.Single(_inboxRepo.Items);
        Assert.Equal("Real", _inboxRepo.Items[0].Title);
    }
}
