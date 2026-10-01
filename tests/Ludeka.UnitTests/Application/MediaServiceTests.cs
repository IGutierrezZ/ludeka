using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Media;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class MediaServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private LudekaDbContext _context = null!;
    private IMediaRepository _mediaRepository = null!;
    private IGameRepository _gameRepository = null!;
    private IBrokenLinkCheckerService _brokenLinkChecker = null!;
    private MediaService _service = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _mediaRepository = new SqliteMediaRepository(_context);
        _gameRepository = new SqliteGameRepository(_context);
        _brokenLinkChecker = new BrokenLinkCheckerService(_mediaRepository, null);

        _service = new MediaService(_mediaRepository, _gameRepository, _brokenLinkChecker);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static int _nextBggId = 167791;

    private async Task<Game> SeedGameAsync(string title = "Terraforming Mars", int? bggId = null)
    {
        var assignedBggId = bggId ?? System.Threading.Interlocked.Increment(ref _nextBggId);
        var game = new Game(
            bggId: assignedBggId,
            originalTitle: title,
            spanishTitle: title,
            designer: "Jacob Fryxelius",
            publisher: "Maldito Games",
            yearPublished: 2016,
            coverImageUrl: "https://example.com/tfm.jpg",
            thumbnailUrl: "https://example.com/tfm-thumb.jpg",
            description: "Terraformación de Marte en el año 2400.",
            bggRating: 8.4,
            bggRank: 5,
            ludistRating: 8.6,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new AgeRating(12, 11),
            language: LanguageDependence.High,
            footprint: TableFootprint.TableMonster,
            duration: new GameDuration(90, 150, 45)
        );

        await _context.Games.AddAsync(game);
        await _context.SaveChangesAsync();
        return game;
    }

    [Fact]
    public async Task GetGameMediaAsync_ShouldSegregateIntoFourCategoriesAndFilterBroken()
    {
        var game = await SeedGameAsync();

        // 1. Tutorial aprobado
        var tutorial = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Tutorial TFM",
            "https://youtube.com/watch?v=tuto",
            "https://thumb.jpg",
            "@analisisparalisis",
            gameId: game.Id,
            durationSeconds: 1200,
            status: ModerationStatus.Approved
        );

        // 2. Partida aprobada con badge
        var playthrough = new MediaItem(
            MediaType.Playthrough,
            MediaPlatform.YouTube,
            "Partida a 2 TFM",
            "https://youtube.com/watch?v=play",
            "https://thumb.jpg",
            "@rinconlegacy",
            gameId: game.Id,
            durationSeconds: 4500,
            playerCountBadge: "Partida a 2",
            status: ModerationStatus.Approved
        );

        // 3. Instagram post
        var post = new MediaItem(
            MediaType.InstagramPost,
            MediaPlatform.Instagram,
            "Mesa repleta",
            "https://instagram.com/p/foto",
            "https://thumb.jpg",
            "@eldardoludico",
            gameId: game.Id,
            likesCount: 150,
            status: ModerationStatus.Approved
        );

        // 4. Short Reel
        var reel = new MediaItem(
            MediaType.ShortReel,
            MediaPlatform.YouTube,
            "Reel TFM",
            "https://youtube.com/shorts/reel",
            "https://thumb.jpg",
            "@zacatrustv",
            gameId: game.Id,
            durationSeconds: 50,
            status: ModerationStatus.Approved
        );

        // 5. Tutorial Roto (IsBroken = true)
        var brokenItem = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Tutorial Antiguo Borrado",
            "https://youtube.com/watch?v=broken",
            "https://thumb.jpg",
            "@borrado",
            gameId: game.Id,
            status: ModerationStatus.Approved
        );
        brokenItem.MarkAsBroken(true);

        // 6. Tutorial no aprobado (PendingApproval)
        var pendingItem = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Tutorial pendiente",
            "https://youtube.com/watch?v=pending",
            "https://thumb.jpg",
            "@novato",
            gameId: game.Id,
            status: ModerationStatus.PendingApproval
        );

        await _mediaRepository.AddAsync(tutorial);
        await _mediaRepository.AddAsync(playthrough);
        await _mediaRepository.AddAsync(post);
        await _mediaRepository.AddAsync(reel);
        await _mediaRepository.AddAsync(brokenItem);
        await _mediaRepository.AddAsync(pendingItem);

        var hub = await _service.GetGameMediaAsync(game.Id);

        Assert.NotNull(hub);
        Assert.Single(hub.Tutorials);
        Assert.Equal("Tutorial TFM", hub.Tutorials[0].Title);

        Assert.Single(hub.Playthroughs);
        Assert.Equal("Partida a 2 TFM", hub.Playthroughs[0].Title);
        Assert.Equal("Partida a 2", hub.Playthroughs[0].PlayerCountBadge);

        Assert.Single(hub.InstagramPosts);
        Assert.Equal("Mesa repleta", hub.InstagramPosts[0].Title);

        Assert.Single(hub.ShortReels);
        Assert.Equal("Reel TFM", hub.ShortReels[0].Title);

        Assert.Equal(4, hub.TotalCount);
        Assert.True(hub.HasMedia);
        Assert.True(hub.HasTutorials);
        Assert.True(hub.HasPlaythroughs);
        Assert.True(hub.HasSocial);
    }

    [Fact]
    public async Task ApproveMediaAsync_WhenFound_ShouldSetApproved()
    {
        var game = await SeedGameAsync();
        var item = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Tutorial Pendiente",
            "https://youtube.com/watch?v=test",
            "https://thumb.jpg",
            "@autor",
            gameId: game.Id,
            status: ModerationStatus.PendingApproval
        );

        await _mediaRepository.AddAsync(item);

        var result = await _service.ApproveMediaAsync(item.Id);

        Assert.NotNull(result);
        Assert.Equal(ModerationStatus.Approved, result.Status);
        Assert.Equal(game.SpanishTitle, result.GameTitle);

        var inDb = await _mediaRepository.GetByIdAsync(item.Id);
        Assert.NotNull(inDb);
        Assert.Equal(ModerationStatus.Approved, inDb.Status);
    }

    [Fact]
    public async Task RejectMediaAsync_WhenFound_ShouldSetRejected()
    {
        var item = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Spam de vídeo",
            "https://youtube.com/watch?v=spam",
            "https://thumb.jpg",
            "@spammer",
            status: ModerationStatus.PendingApproval
        );

        await _mediaRepository.AddAsync(item);

        var result = await _service.RejectMediaAsync(item.Id);

        Assert.NotNull(result);
        Assert.Equal(ModerationStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task AssignOrphanMediaAsync_WithValidGame_ShouldAssignGame()
    {
        var game = await SeedGameAsync("Wingspan");
        var orphan = new MediaItem(
            MediaType.ShortReel,
            MediaPlatform.YouTube,
            "Reel de pájaros",
            "https://youtube.com/shorts/aves",
            "https://thumb.jpg",
            "@autor",
            gameId: null,
            status: ModerationStatus.PendingApproval
        );

        await _mediaRepository.AddAsync(orphan);

        var result = await _service.AssignOrphanMediaAsync(orphan.Id, game.Id);

        Assert.NotNull(result);
        Assert.False(result.IsOrphan);
        Assert.Equal(game.Id, result.GameId);
        Assert.Equal("Wingspan", result.GameTitle);
    }

    [Fact]
    public async Task AssignOrphanMediaAsync_WithNonExistentGame_ShouldThrowArgumentException()
    {
        var orphan = new MediaItem(
            MediaType.ShortReel,
            MediaPlatform.YouTube,
            "Reel huérfano",
            "https://youtube.com/shorts/aves2",
            "https://thumb.jpg",
            "@autor",
            gameId: null
        );

        await _mediaRepository.AddAsync(orphan);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AssignOrphanMediaAsync(orphan.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task CheckBrokenLinksAsync_ShouldDetectBrokenLinks()
    {
        var brokenItem = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Enlace roto conocido",
            "https://youtube.com/watch?v=broken_video_404",
            "https://thumb.jpg",
            "@canal"
        );

        var validItem = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Enlace válido",
            "https://youtube.com/watch?v=valid_video",
            "https://thumb.jpg",
            "@canal"
        );

        await _mediaRepository.AddAsync(brokenItem);
        await _mediaRepository.AddAsync(validItem);

        var report = await _service.CheckBrokenLinksAsync();

        Assert.Equal(2, report.TotalChecked);
        Assert.Equal(1, report.BrokenCount);
        Assert.Single(report.BrokenItems);
        Assert.Equal("Enlace roto conocido", report.BrokenItems[0].Title);
    }

    [Fact]
    public async Task UpdateMediaCategoryAsync_WhenItemExists_ShouldUpdateCategoryAndReturnDto()
    {
        var game = await SeedGameAsync("Root");
        var item = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Tutorial de Root",
            "https://youtube.com/watch?v=root1",
            "https://thumb.jpg",
            "@canal",
            gameId: game.Id,
            category: MediaCategory.Tutorial
        );
        await _mediaRepository.AddAsync(item);

        var updated = await _service.UpdateMediaCategoryAsync(item.Id, MediaCategory.QuickOverview);

        Assert.NotNull(updated);
        Assert.Equal(MediaCategory.QuickOverview, updated.Category);
        Assert.Equal(game.SpanishTitle, updated.GameTitle);

        var inDb = await _mediaRepository.GetByIdAsync(item.Id);
        Assert.NotNull(inDb);
        Assert.Equal(MediaCategory.QuickOverview, inDb.Category);
    }

    [Fact]
    public async Task UpdateMediaCategoryAsync_WhenNotFound_ShouldReturnNull()
    {
        var result = await _service.UpdateMediaCategoryAsync(Guid.NewGuid(), MediaCategory.Gameplay);
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateMediaCategoryAsync_WithAuditAndCurrentUser_ShouldRecordAuditLog()
    {
        var game = await SeedGameAsync("Scythe");
        var item = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Tutorial Scythe",
            "https://youtube.com/watch?v=scythe1",
            "https://thumb.jpg",
            "@canal",
            gameId: game.Id,
            category: MediaCategory.Tutorial
        );
        await _mediaRepository.AddAsync(item);

        var currentUser = new TestCurrentUserService { Permissions = ModeratorPermission.CanApproveMedia };
        var auditService = new TestAuditService();
        var serviceWithAudit = new MediaService(_mediaRepository, _gameRepository, _brokenLinkChecker, currentUser, auditService);

        var updated = await serviceWithAudit.UpdateMediaCategoryAsync(item.Id, MediaCategory.ReviewOpinion);

        Assert.NotNull(updated);
        Assert.Equal(MediaCategory.ReviewOpinion, updated.Category);
        Assert.Single(auditService.Commands);
        var audit = auditService.Commands[0];
        Assert.Equal(AuditEntityType.Media, audit.EntityType);
        Assert.Equal(AuditAction.Updated, audit.Action);
        Assert.Contains("ReviewOpinion", audit.Summary);
    }

    [Fact]
    public async Task UpdateMediaCategoryAsync_WithoutPermission_ShouldThrowUnauthorized()
    {
        var item = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Tutorial",
            "https://youtube.com/watch?v=vid1",
            "https://thumb.jpg",
            "@canal"
        );
        await _mediaRepository.AddAsync(item);

        var currentUser = new TestCurrentUserService { Permissions = ModeratorPermission.None };
        var service = new MediaService(_mediaRepository, _gameRepository, _brokenLinkChecker, currentUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateMediaCategoryAsync(item.Id, MediaCategory.Gameplay));
    }

    [Fact]
    public async Task ReassignMediaGameAsync_WithValidGames_ShouldReassignAndReturnDto()
    {
        var game1 = await SeedGameAsync("Juego Original");
        var game2 = await SeedGameAsync("Juego Destino");

        var item = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Vídeo mal asignado",
            "https://youtube.com/watch?v=reassign1",
            "https://thumb.jpg",
            "@canal",
            gameId: game1.Id
        );
        await _mediaRepository.AddAsync(item);

        var result = await _service.ReassignMediaGameAsync(item.Id, game2.Id);

        Assert.NotNull(result);
        Assert.Equal(game2.Id, result.GameId);
        Assert.Equal(game2.SpanishTitle, result.GameTitle);

        var inDb = await _mediaRepository.GetByIdAsync(item.Id);
        Assert.NotNull(inDb);
        Assert.Equal(game2.Id, inDb.GameId);
    }

    [Fact]
    public async Task ReassignMediaGameAsync_WithNonExistentGame_ShouldThrowArgumentException()
    {
        var game = await SeedGameAsync("Juego Inicial");
        var item = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Vídeo",
            "https://youtube.com/watch?v=reassign2",
            "https://thumb.jpg",
            "@canal",
            gameId: game.Id
        );
        await _mediaRepository.AddAsync(item);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ReassignMediaGameAsync(item.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task ReassignMediaGameAsync_WithAudit_ShouldRecordAuditLog()
    {
        var game1 = await SeedGameAsync("Juego Origen Audit");
        var game2 = await SeedGameAsync("Juego Destino Audit");

        var item = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Vídeo Audit",
            "https://youtube.com/watch?v=audit_reassign",
            "https://thumb.jpg",
            "@canal",
            gameId: game1.Id
        );
        await _mediaRepository.AddAsync(item);

        var currentUser = new TestCurrentUserService { Permissions = ModeratorPermission.CanApproveMedia };
        var auditService = new TestAuditService();
        var serviceWithAudit = new MediaService(_mediaRepository, _gameRepository, _brokenLinkChecker, currentUser, auditService);

        var result = await serviceWithAudit.ReassignMediaGameAsync(item.Id, game2.Id);

        Assert.NotNull(result);
        Assert.Single(auditService.Commands);
        var audit = auditService.Commands[0];
        Assert.Equal(AuditEntityType.Media, audit.EntityType);
        Assert.Equal(AuditAction.Updated, audit.Action);
        Assert.Contains(game2.SpanishTitle, audit.Summary);
    }

    [Fact]
    public async Task DeleteMediaAsync_WhenFound_ShouldDeleteAndReturnTrue()
    {
        var game = await SeedGameAsync("Juego Para Borrar");
        var item = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Vídeo a eliminar",
            "https://youtube.com/watch?v=delete1",
            "https://thumb.jpg",
            "@canal",
            gameId: game.Id
        );
        await _mediaRepository.AddAsync(item);

        var currentUser = new TestCurrentUserService { Permissions = ModeratorPermission.CanApproveMedia };
        var auditService = new TestAuditService();
        var service = new MediaService(_mediaRepository, _gameRepository, _brokenLinkChecker, currentUser, auditService);

        var success = await service.DeleteMediaAsync(item.Id);

        Assert.True(success);
        var inDb = await _mediaRepository.GetByIdAsync(item.Id);
        Assert.Null(inDb);

        Assert.Single(auditService.Commands);
        var audit = auditService.Commands[0];
        Assert.Equal(AuditEntityType.Media, audit.EntityType);
        Assert.Equal(AuditAction.Deleted, audit.Action);
    }

    [Fact]
    public async Task DeleteMediaAsync_WhenNotFound_ShouldReturnFalse()
    {
        var success = await _service.DeleteMediaAsync(Guid.NewGuid());
        Assert.False(success);
    }

    [Fact]
    public async Task DeleteMediaAsync_WithoutPermission_ShouldThrowUnauthorized()
    {
        var item = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Vídeo sin permiso",
            "https://youtube.com/watch?v=delete2",
            "https://thumb.jpg",
            "@canal"
        );
        await _mediaRepository.AddAsync(item);

        var currentUser = new TestCurrentUserService { Permissions = ModeratorPermission.None };
        var service = new MediaService(_mediaRepository, _gameRepository, _brokenLinkChecker, currentUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeleteMediaAsync(item.Id));
    }

    [Fact]
    public async Task ApproveMediaAsync_WithCategory_ShouldApproveAndChangeCategory()
    {
        var game = await SeedGameAsync("Juego Aprobación");
        var item = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Vídeo con categoría errónea",
            "https://youtube.com/watch?v=approve_cat",
            "https://thumb.jpg",
            "@canal",
            gameId: game.Id,
            category: MediaCategory.Tutorial,
            status: ModerationStatus.PendingApproval
        );
        await _mediaRepository.AddAsync(item);

        var currentUser = new TestCurrentUserService { Permissions = ModeratorPermission.CanApproveMedia };
        var auditService = new TestAuditService();
        var service = new MediaService(_mediaRepository, _gameRepository, _brokenLinkChecker, currentUser, auditService);

        var result = await service.ApproveMediaAsync(item.Id, MediaCategory.ReviewOpinion);

        Assert.NotNull(result);
        Assert.Equal(ModerationStatus.Approved, result.Status);
        Assert.Equal(MediaCategory.ReviewOpinion, result.Category);

        Assert.Single(auditService.Commands);
        var audit = auditService.Commands[0];
        Assert.Equal(AuditEntityType.Media, audit.EntityType);
        Assert.Equal(AuditAction.StatusChanged, audit.Action);
        Assert.Contains("ReviewOpinion", audit.Summary);
    }

    [Fact]
    public async Task GetGameMediaAsync_ShouldPopulateQuickOverviewsAndReviewsAndOpinions()
    {
        var game = await SeedGameAsync("Dune Imperium");

        var overview = new MediaItem(
            MediaType.QuickOverview,
            MediaPlatform.YouTube,
            "Dune Imperium en 2 minutos",
            "https://youtube.com/watch?v=dune_overview",
            "https://thumb.jpg",
            "@canal",
            gameId: game.Id,
            category: MediaCategory.QuickOverview,
            status: ModerationStatus.Approved
        );

        var review = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Reseña y veredicto de Dune Imperium",
            "https://youtube.com/watch?v=dune_review",
            "https://thumb.jpg",
            "@canal",
            gameId: game.Id,
            category: MediaCategory.ReviewOpinion,
            status: ModerationStatus.Approved
        );

        await _mediaRepository.AddAsync(overview);
        await _mediaRepository.AddAsync(review);

        var hub = await _service.GetGameMediaAsync(game.Id);

        Assert.NotNull(hub);
        Assert.Single(hub.QuickOverviews);
        Assert.Equal("Dune Imperium en 2 minutos", hub.QuickOverviews[0].Title);
        Assert.Single(hub.ReviewsAndOpinions);
        Assert.Equal("Reseña y veredicto de Dune Imperium", hub.ReviewsAndOpinions[0].Title);
        Assert.True(hub.HasQuickOverviews);
        Assert.True(hub.HasSocial);
        Assert.Equal(2, hub.TotalCount);
    }

    [Fact]
    public async Task FlashIngestAsync_WithValidYouTubeUrl_ExtractsMetadataAndCreatesApprovedItem()
    {
        var game = await SeedGameAsync("Ark Nova");
        var fakeExtractor = new FakeSocialMetadataExtractor("Cómo jugar a Ark Nova", "Zacatrus!", "https://img.youtube.com/vi/abc12345678/hqdefault.jpg");
        var currentUser = new TestCurrentUserService();
        var auditService = new TestAuditService();

        var service = new MediaService(_mediaRepository, _gameRepository, _brokenLinkChecker, currentUser, auditService, metadataExtractor: fakeExtractor);

        var result = await service.FlashIngestAsync(
            game.Id,
            "https://www.youtube.com/watch?v=abc12345678",
            MediaCategory.Tutorial
        );

        Assert.NotNull(result);
        Assert.Equal("Cómo jugar a Ark Nova", result.Title);
        Assert.Equal("Zacatrus!", result.AuthorChannel);
        Assert.Equal(MediaCategory.Tutorial, result.Category);
        Assert.Equal(MediaType.Tutorial, result.Type);
        Assert.Equal(ModerationStatus.Approved, result.Status);
        Assert.Equal(game.Id, result.GameId);
        Assert.Single(auditService.Commands);
        Assert.Equal(AuditAction.Created, auditService.Commands[0].Action);

        var hub = await service.GetGameMediaAsync(game.Id);
        Assert.Single(hub.Tutorials);
        Assert.Equal("Cómo jugar a Ark Nova", hub.Tutorials[0].Title);
    }

    [Fact]
    public async Task FlashIngestAsync_ForPlaythroughWithoutBadge_ExtractsBadgeFromGame()
    {
        var game = await SeedGameAsync("Ark Nova");
        var fakeExtractor = new FakeSocialMetadataExtractor("Partida a 2 comensales Ark Nova", "Meeple", "https://thumb.jpg");
        var currentUser = new TestCurrentUserService();

        var service = new MediaService(_mediaRepository, _gameRepository, _brokenLinkChecker, currentUser, metadataExtractor: fakeExtractor);

        var result = await service.FlashIngestAsync(
            game.Id,
            "https://youtu.be/xyz98765432",
            MediaCategory.Gameplay
        );

        Assert.NotNull(result);
        Assert.Equal(MediaType.Playthrough, result.Type);
        Assert.Equal(MediaCategory.Gameplay, result.Category);
        Assert.NotNull(result.PlayerCountBadge);
        Assert.Equal("Partida a 2", result.PlayerCountBadge);
    }

    [Fact]
    public async Task FlashIngestAsync_WithDuplicateUrl_ThrowsInvalidOperationException()
    {
        var game = await SeedGameAsync("Catan");
        var currentUser = new TestCurrentUserService();
        var service = new MediaService(_mediaRepository, _gameRepository, _brokenLinkChecker, currentUser);

        await service.FlashIngestAsync(game.Id, "https://www.youtube.com/watch?v=dup12345678", MediaCategory.QuickOverview);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FlashIngestAsync(game.Id, "https://www.youtube.com/watch?v=dup12345678", MediaCategory.Tutorial));
    }

    [Fact]
    public async Task FlashIngestAsync_WithInvalidUrl_ThrowsArgumentException()
    {
        var game = await SeedGameAsync("Catan");
        var currentUser = new TestCurrentUserService();
        var service = new MediaService(_mediaRepository, _gameRepository, _brokenLinkChecker, currentUser);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.FlashIngestAsync(game.Id, "https://vimeo.com/123456", MediaCategory.Tutorial));
    }

    [Fact]
    public async Task FlashIngestAsync_WithoutPermission_ThrowsUnauthorized()
    {
        var game = await SeedGameAsync("Catan");
        var unprivilegedUser = new TestCurrentUserService
        {
            RolesList = ["Player"],
            Permissions = ModeratorPermission.None
        };
        var service = new MediaService(_mediaRepository, _gameRepository, _brokenLinkChecker, unprivilegedUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.FlashIngestAsync(game.Id, "https://www.youtube.com/watch?v=perm1234567", MediaCategory.Tutorial));
    }

    private class FakeSocialMetadataExtractor(string title, string channel, string image) : ISocialMetadataExtractor
    {
        public Task<SocialMetadataResultDto?> ExtractFromUrlAsync(string url, System.Threading.CancellationToken ct = default)
        {
            return Task.FromResult<SocialMetadataResultDto?>(new SocialMetadataResultDto(
                Url: url,
                Platform: SocialPlatform.YouTube,
                Title: title,
                AuthorOrChannel: channel,
                Description: title,
                ImageUrl: image,
                IsVideo: true,
                VideoId: "test12345"
            ));
        }
    }

    private class TestCurrentUserService : ICurrentUserService
    {
        public string UserId { get; set; } = "user_mod_1";
        public string UserName { get; set; } = "Moderador Editorial";
        public List<string> RolesList { get; set; } = ["Moderator"];
        public ModeratorPermission Permissions { get; set; } = ModeratorPermission.CanApproveMedia;

        public IReadOnlyList<string> Roles => RolesList;
        public bool IsFoundingTeam => RolesList.Contains("FoundingTeam", StringComparer.OrdinalIgnoreCase);
        public bool IsInRole(string role) => RolesList.Any(r => r.Equals(role, StringComparison.OrdinalIgnoreCase));
        public bool HasPermission(ModeratorPermission permission)
        {
            if (IsFoundingTeam) return true;
            return (Permissions & permission) == permission;
        }
    }

    private class TestAuditService : IAuditService
    {
        public List<RecordAuditCommand> Commands { get; } = [];

        public Task RecordChangeAsync(RecordAuditCommand command, CancellationToken ct = default)
        {
            Commands.Add(command);
            return Task.CompletedTask;
        }

        public Task<AuditLogPageDto> GetAuditLogsAsync(AuditLogFilterDto filter, CancellationToken ct = default)
        {
            return Task.FromResult(new AuditLogPageDto([], 0, 1, 50, 0));
        }
    }
}
