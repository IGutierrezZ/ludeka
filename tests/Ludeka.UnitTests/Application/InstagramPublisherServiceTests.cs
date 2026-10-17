using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Community;
using Ludeka.Application.Features.Instagram;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class InstagramPublisherServiceTests
{
    private class FakeDraftRepository : IInstagramPostDraftRepository
    {
        public List<InstagramPostDraft> Items { get; } = new();

        public Task<IReadOnlyList<InstagramPostDraft>> GetDraftsAsync(InstagramPostDraftStatus? status = null, CancellationToken ct = default)
        {
            var query = Items.AsEnumerable();
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            return Task.FromResult<IReadOnlyList<InstagramPostDraft>>(query.OrderByDescending(x => x.CreatedAt).ToList());
        }

        public Task<InstagramPostDraft?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

        public Task<InstagramPostDraft?> GetBySourceAsync(InstagramPostSourceType sourceType, string sourceId, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.SourceType == sourceType && x.SourceId == sourceId));

        public Task AddAsync(InstagramPostDraft draft, CancellationToken ct = default)
        {
            Items.Add(draft);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(InstagramPostDraft draft, CancellationToken ct = default)
        {
            var idx = Items.FindIndex(x => x.Id == draft.Id);
            if (idx >= 0) Items[idx] = draft;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Items.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
        }
    }

    private class FakeGiveawayRepository : IGiveawayRepository
    {
        public List<Giveaway> Items { get; } = new();

        public Task<IReadOnlyList<Giveaway>> GetGiveawaysAsync(bool includeExpired = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Giveaway>>(Items.ToList());

        public Task<Giveaway?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(g => g.Id == id));

        public Task<Giveaway?> FindDuplicateOrCollaborativeAsync(string title, string organizer, DateTimeOffset deadline, CancellationToken ct = default)
            => Task.FromResult<Giveaway?>(null);

        public Task AddAsync(Giveaway giveaway, CancellationToken ct = default)
        {
            Items.Add(giveaway);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Giveaway giveaway, CancellationToken ct = default)
        {
            var idx = Items.FindIndex(g => g.Id == giveaway.Id);
            if (idx >= 0) Items[idx] = giveaway;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Items.RemoveAll(g => g.Id == id);
            return Task.CompletedTask;
        }
    }

    private class FakeWeeklyReleaseRepository : IWeeklyReleaseRepository
    {
        public List<WeeklyRelease> Items { get; } = new();

        public Task<IReadOnlyList<WeeklyRelease>> GetReleasesAsync(DateOnly? fromDate = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WeeklyRelease>>(Items.ToList());

        public Task<WeeklyRelease?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(r => r.Id == id));

        public Task AddAsync(WeeklyRelease release, CancellationToken ct = default)
        {
            Items.Add(release);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(WeeklyRelease release, CancellationToken ct = default)
        {
            var idx = Items.FindIndex(r => r.Id == release.Id);
            if (idx >= 0) Items[idx] = release;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Items.RemoveAll(r => r.Id == id);
            return Task.CompletedTask;
        }
    }

    private class FakeGameRepository : IGameRepository
    {
        public List<Game> Items { get; } = new();

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(g => g.Id == id));

        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(g => g.Slug == slug));

        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(g => g.BggId == bggId));

        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<Game>, int)>((Items, Items.Count));

        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        {
            Items.AddRange(games);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Game game, CancellationToken ct = default)
        {
            var idx = Items.FindIndex(g => g.Id == game.Id);
            if (idx >= 0) Items[idx] = game;
            return Task.CompletedTask;
        }

        public Task<bool> HasAnyAsync(CancellationToken ct = default)
            => Task.FromResult(Items.Count > 0);
    }

    private class FakeAuditService : IAuditService
    {
        public List<RecordAuditCommand> LoggedCommands { get; } = new();

        public Task RecordChangeAsync(RecordAuditCommand command, CancellationToken ct = default)
        {
            LoggedCommands.Add(command);
            return Task.CompletedTask;
        }

        public Task<AuditLogPageDto> GetAuditLogsAsync(AuditLogFilterDto filter, CancellationToken ct = default)
            => Task.FromResult(new AuditLogPageDto([], 0, 1, 20, 0));
    }

    private class FakeInstagramApiClient : IInstagramApiClient
    {
        public bool ShouldSucceed { get; set; } = true;
        public string LastImageUrl { get; private set; } = string.Empty;
        public string LastCaption { get; private set; } = string.Empty;

        public Task<string> CreateMediaContainerAsync(string imageUrl, string caption, CancellationToken ct = default)
        {
            LastImageUrl = imageUrl;
            LastCaption = caption;

            if (!ShouldSucceed)
                throw new InvalidOperationException("Error simulado al crear contenedor en Meta API");

            return Task.FromResult("container_fake_123");
        }

        public Task<string> PublishMediaAsync(string creationId, CancellationToken ct = default)
        {
            if (!ShouldSucceed)
                throw new InvalidOperationException("Error simulado al publicar contenedor en Meta API");

            return Task.FromResult("media_fake_456");
        }

        public Task<string?> GetPermalinkAsync(string mediaId, CancellationToken ct = default)
        {
            return Task.FromResult<string?>($"https://www.instagram.com/p/{mediaId}/");
        }

        public Task<IReadOnlyList<InstagramAccountMediaDto>> GetAccountReelsAsync(int limit = 25, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<InstagramAccountMediaDto>>([]);
        }
    }

    private readonly FakeDraftRepository _draftRepo = new();
    private readonly InstagramComposerService _composer = new(new SocialCardService());
    private readonly FakeInstagramApiClient _apiClient = new();
    private readonly FakeGiveawayRepository _giveawayRepo = new();
    private readonly FakeWeeklyReleaseRepository _releaseRepo = new();
    private readonly FakeGameRepository _gameRepo = new();
    private readonly FakeAuditService _auditService = new();

    private InstagramPublisherService CreateService()
    {
        return new InstagramPublisherService(
            _draftRepo,
            _giveawayRepo,
            _releaseRepo,
            _gameRepo,
            _composer,
            _apiClient,
            _auditService);
    }

    [Fact]
    public async Task CreateDraftFromGiveawayAsync_CreatesAndPersistsDraft()
    {
        // Arrange
        var giveaway = new Giveaway(
            title: "Sorteo Dune Imperium",
            organizer: "Asmodee",
            url: "https://instagram.com/p/dune",
            platform: GiveawayPlatform.Instagram,
            deadlineAt: DateTimeOffset.UtcNow.AddDays(4));
        _giveawayRepo.Items.Add(giveaway);

        var service = CreateService();

        // Act
        var draft = await service.CreateDraftFromGiveawayAsync(giveaway.Id, "user_mod", "Moderador");

        // Assert
        Assert.NotNull(draft);
        Assert.Equal(giveaway.Id.ToString(), draft.SourceId);
        Assert.Equal(InstagramPostSourceType.Giveaway, draft.SourceType);
        Assert.Equal(InstagramPostDraftStatus.Draft, draft.Status);
        Assert.Contains("Sorteo Dune Imperium", draft.Title);
        Assert.Single(_draftRepo.Items);
    }

    [Fact]
    public async Task CreateDraftFromWeeklyReleaseAsync_CreatesAndPersistsDraft()
    {
        // Arrange
        var release = new WeeklyRelease(
            title: "Ark Nova Marine Worlds",
            publisher: "Maldito Games",
            releaseDate: new DateOnly(2026, 9, 25),
            coverImageUrl: "https://cf.geekdo-images.com/arknova.jpg");
        _releaseRepo.Items.Add(release);

        var service = CreateService();

        // Act
        var draft = await service.CreateDraftFromWeeklyReleaseAsync(release.Id, "user_mod", "Moderador");

        // Assert
        Assert.NotNull(draft);
        Assert.Equal(release.Id.ToString(), draft.SourceId);
        Assert.Equal(InstagramPostSourceType.WeeklyRelease, draft.SourceType);
        Assert.Contains("Ark Nova Marine Worlds", draft.Title);
        Assert.Single(_draftRepo.Items);
    }

    [Fact]
    public async Task PublishDraftAsync_Success_PublishesUpdatesSourceAndAudits()
    {
        // Arrange
        var giveaway = new Giveaway(
            title: "Sorteo Cascadia",
            organizer: "Delirium Games",
            url: "https://instagram.com/p/cascadia",
            platform: GiveawayPlatform.Instagram,
            deadlineAt: DateTimeOffset.UtcNow.AddDays(5));
        _giveawayRepo.Items.Add(giveaway);

        var service = CreateService();
        var draft = await service.CreateDraftFromGiveawayAsync(giveaway.Id, "user_mod", "Moderador");

        // Act
        var result = await service.PublishDraftAsync(draft.Id, "user_mod", "Moderador");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("media_fake_456", result.MediaId);
        Assert.Equal("https://www.instagram.com/p/media_fake_456/", result.Permalink);

        // Verificar estado del borrador en el repositorio
        var updatedDraft = await _draftRepo.GetByIdAsync(draft.Id);
        Assert.NotNull(updatedDraft);
        Assert.Equal(InstagramPostDraftStatus.Published, updatedDraft.Status);
        Assert.Equal("media_fake_456", updatedDraft.InstagramMediaId);
        Assert.Equal("https://www.instagram.com/p/media_fake_456/", updatedDraft.InstagramPermalink);

        // Verificar sincronización con la entidad Giveaway
        var updatedGiveaway = await _giveawayRepo.GetByIdAsync(giveaway.Id);
        Assert.NotNull(updatedGiveaway);
        Assert.True(updatedGiveaway.IsPublishedOnInstagram);
        Assert.Equal("https://www.instagram.com/p/media_fake_456/", updatedGiveaway.InstagramPermalink);

        // Verificar auditoría
        Assert.Single(_auditService.LoggedCommands);
        var audit = _auditService.LoggedCommands[0];
        Assert.Equal(AuditEntityType.InstagramPost, audit.EntityType);
        Assert.Equal(draft.Id.ToString(), audit.EntityId);
        Assert.Equal(AuditAction.Published, audit.Action);
        Assert.Contains("media_fake_456", audit.Summary);
    }

    [Fact]
    public async Task PublishDraftAsync_ApiFails_MarksDraftAsFailedAndDoesNotAudit()
    {
        // Arrange
        _apiClient.ShouldSucceed = false;
        var service = CreateService();

        var draft = new InstagramPostDraft(
            "Terraforming Mars",
            "Texto de Mars",
            InstagramPostSourceType.Game,
            Guid.NewGuid().ToString(),
            "user_mod",
            "Moderador");
        _draftRepo.Items.Add(draft);

        // Act
        var result = await service.PublishDraftAsync(draft.Id, "user_mod", "Moderador");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Error simulado al crear contenedor", result.ErrorMessage);

        var updatedDraft = await _draftRepo.GetByIdAsync(draft.Id);
        Assert.NotNull(updatedDraft);
        Assert.Equal(InstagramPostDraftStatus.Failed, updatedDraft.Status);
        Assert.Empty(_auditService.LoggedCommands); // No se audita publicación exitosa
    }
}
