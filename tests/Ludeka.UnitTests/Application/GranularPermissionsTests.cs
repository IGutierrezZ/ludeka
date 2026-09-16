using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Catalog;
using Ludeka.Application.Features.Directory;
using Ludeka.Application.Features.Media;
using Ludeka.Application.Features.Reports;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class GranularPermissionsTests
{
    private class TestCurrentUserService : ICurrentUserService
    {
        public string UserId { get; set; } = "moderador_test";
        public string UserName { get; set; } = "Moderador de Pruebas";
        public List<string> RolesList { get; set; } = ["Moderator"];
        public ModeratorPermission Permissions { get; set; } = ModeratorPermission.None;

        public IReadOnlyList<string> Roles => RolesList;
        public bool IsFoundingTeam => RolesList.Contains("FoundingTeam", StringComparer.OrdinalIgnoreCase);

        public bool IsInRole(string role) => RolesList.Any(r => r.Equals(role, StringComparison.OrdinalIgnoreCase));

        public bool HasPermission(ModeratorPermission permission)
        {
            if (IsFoundingTeam) return true;
            return (Permissions & permission) == permission;
        }
    }

    private class FakePublisherRepo : IPublisherRepository
    {
        public List<Publisher> Items = [];
        public Task<IReadOnlyList<Publisher>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Publisher>>(Items);
        public Task<Publisher?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(p => p.Id == id));
        public Task<Publisher?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(p => p.Slug == slug));
        public Task<Publisher?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(p => p.Name == name));
        public Task AddAsync(Publisher publisher, CancellationToken ct = default) { Items.Add(publisher); return Task.CompletedTask; }
        public Task UpdateAsync(Publisher publisher, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(p => p.Id == id); return Task.CompletedTask; }
    }

    private class FakeCreatorRepo : ICreatorRepository
    {
        public List<Creator> Items = [];
        public Task<IReadOnlyList<Creator>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Creator>>(Items);
        public Task<Creator?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(c => c.Id == id));
        public Task<Creator?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(c => c.Slug == slug));
        public Task<Creator?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(c => c.Name == name));
        public Task AddAsync(Creator creator, CancellationToken ct = default) { Items.Add(creator); return Task.CompletedTask; }
        public Task UpdateAsync(Creator creator, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(c => c.Id == id); return Task.CompletedTask; }
    }

    private class FakeStoreRepo : IStoreRepository
    {
        public List<Store> Items = [];
        public Task<IReadOnlyList<Store>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Store>>(Items);
        public Task<Store?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(s => s.Id == id));
        public Task<Store?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(s => s.Slug == slug));
        public Task<Store?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(s => s.Name == name));
        public Task AddAsync(Store store, CancellationToken ct = default) { Items.Add(store); return Task.CompletedTask; }
        public Task UpdateAsync(Store store, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(s => s.Id == id); return Task.CompletedTask; }
    }

    private class FakeGameRepo : IGameRepository
    {
        public List<Game> Games = [];
        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.Id == id));
        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.Slug == slug));
        public Task<IReadOnlyList<Game>> GetAllGamesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Game>>(Games);
        public Task<IReadOnlyList<Game>> GetByPublisherAsync(string publisher, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Game>>([]);
        public Task<IReadOnlyList<Game>> GetByDesignerAsync(string designer, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Game>>([]);
        public Task AddAsync(Game game, CancellationToken ct = default) { Games.Add(game); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default) { Games.AddRange(games); return Task.CompletedTask; }
        public Task UpdateAsync(Game game, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.BggId == bggId));
        public Task<IReadOnlyList<Game>> SearchAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Game>>([]);
        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 24, CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<Game> Items, int TotalCount)>((Games, Games.Count));
        public Task<IReadOnlyList<Game>> GetExpansionsForGameAsync(Guid baseGameId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Game>>([]);
        public Task<IReadOnlyList<Game>> GetBaseGamesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Game>>([]);
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(Games.Count > 0);
    }

    private class FakeReportRepo : IGameIssueReportRepository
    {
        public List<GameIssueReport> Reports = [];
        public ValueTask AddAsync(GameIssueReport report, CancellationToken ct = default) { Reports.Add(report); return ValueTask.CompletedTask; }
        public ValueTask<GameIssueReport?> GetByIdAsync(Guid id, CancellationToken ct = default) => ValueTask.FromResult(Reports.FirstOrDefault(r => r.Id == id));
        public ValueTask<IReadOnlyList<GameIssueReport>> GetAllAsync(GameReportFilter filter, CancellationToken ct = default) => ValueTask.FromResult<IReadOnlyList<GameIssueReport>>(Reports);
        public ValueTask<GameIssueReportSummaryDto> GetSummaryAsync(CancellationToken ct = default) => ValueTask.FromResult(new GameIssueReportSummaryDto(0, 0, 0, 0, 0));
        public ValueTask UpdateAsync(GameIssueReport report, CancellationToken ct = default) => ValueTask.CompletedTask;
    }

    private class FakeMediaRepo : IMediaRepository
    {
        public List<MediaItem> Items = [];
        public Task AddAsync(MediaItem item, CancellationToken ct = default) { Items.Add(item); return Task.CompletedTask; }
        public Task<MediaItem?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(m => m.Id == id));
        public Task<IReadOnlyList<MediaItem>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaItem>>(Items);
        public Task<IReadOnlyList<MediaItem>> GetApprovedAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaItem>>(Items.Where(m => m.Status == ModerationStatus.Approved).ToList());
        public Task<IReadOnlyList<MediaItem>> GetApprovedByGameIdAsync(Guid gameId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaItem>>([]);
        public Task<IReadOnlyList<MediaItem>> GetOrphansAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaItem>>([]);
        public Task<IReadOnlyList<MediaItem>> GetPendingModerationAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaItem>>(Items.Where(m => m.Status == ModerationStatus.PendingApproval).ToList());
        public Task UpdateAsync(MediaItem item, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(m => m.Id == id); return Task.CompletedTask; }
        public Task<bool> ExistsByUrlAsync(string url, CancellationToken ct = default) => Task.FromResult(Items.Any(m => m.Url.Equals(url, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task PublisherService_WithoutCanManagePublishers_ThrowsUnauthorized()
    {
        var pubRepo = new FakePublisherRepo();
        var gameRepo = new FakeGameRepo();
        var currentUser = new TestCurrentUserService { Permissions = ModeratorPermission.None };

        var service = new PublisherService(pubRepo, gameRepo, currentUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(new CreatePublisherDto("Devir", "devir", "EspaÃ±a", null, null, null, null, null)));
    }

    [Fact]
    public async Task PublisherService_WithCanManagePublishers_Succeeds()
    {
        var pubRepo = new FakePublisherRepo();
        var gameRepo = new FakeGameRepo();
        var currentUser = new TestCurrentUserService { Permissions = ModeratorPermission.CanManagePublishers };

        var service = new PublisherService(pubRepo, gameRepo, currentUser);

        var result = await service.CreateAsync(new CreatePublisherDto("Devir", "devir", "EspaÃ±a", null, null, null, null, null));
        Assert.NotNull(result);
        Assert.Equal("Devir", result.Name);
    }

    [Fact]
    public async Task CreatorService_WithoutCanManageCreators_ThrowsUnauthorized()
    {
        var creatorRepo = new FakeCreatorRepo();

        var currentUser = new TestCurrentUserService { Permissions = ModeratorPermission.None };

        var service = new CreatorService(creatorRepo, currentUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(new CreateCreatorDto("Klaus Teuber", "klaus-teuber", null, null, null, null, null, null)));
    }

    [Fact]
    public async Task CreatorService_WithCanManageCreators_Succeeds()
    {
        var creatorRepo = new FakeCreatorRepo();

        var currentUser = new TestCurrentUserService { Permissions = ModeratorPermission.CanManageCreators };

        var service = new CreatorService(creatorRepo, currentUser);

        var result = await service.CreateAsync(new CreateCreatorDto("Klaus Teuber", "klaus-teuber", null, null, null, null, null, null));
        Assert.NotNull(result);
        Assert.Equal("Klaus Teuber", result.Name);
    }

    [Fact]
    public async Task StoreService_WithoutCanManageStoreLinks_ThrowsUnauthorized()
    {
        var storeRepo = new FakeStoreRepo();
        var gameRepo = new FakeGameRepo();
        var currentUser = new TestCurrentUserService { Permissions = ModeratorPermission.None };

        var service = new StoreService(storeRepo, gameRepo, currentUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(new CreateStoreDto("Zacatrus", "zacatrus", StoreType.Hybrid, null, null, null, null, null, null, false, null)));
    }

    [Fact]
    public async Task ReportService_WithoutCanResolveReports_ThrowsUnauthorized()
    {
        var reportRepo = new FakeReportRepo();
        var report = new GameIssueReport(Guid.NewGuid(), "catan", "Catan", GameIssueType.Other, "Detalles");
        reportRepo.Reports.Add(report);

        var currentUser = new TestCurrentUserService { Permissions = ModeratorPermission.None };
        var service = new GameIssueReportService(reportRepo, currentUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ChangeStatusAsync(report.Id, new UpdateGameReportStatusCommand(GameReportStatus.Resolved, "mod", "Resuelto")).AsTask());
    }

    private class FakeBrokenLinkChecker : IBrokenLinkCheckerService
    {
        public Task<BrokenLinkReportDto> CheckLinksAsync(CancellationToken ct = default) =>
            Task.FromResult(new BrokenLinkReportDto(0, 0, []));
    }

    [Fact]
    public async Task MediaService_WithoutCanApproveMedia_ThrowsUnauthorized()
    {
        var mediaRepo = new FakeMediaRepo();
        var item = new MediaItem(MediaType.Tutorial, MediaPlatform.YouTube, "Tutorial", "https://youtube.com/watch?v=123", "https://img.youtube.com/123.jpg", "Canal");
        mediaRepo.Items.Add(item);

        var currentUser = new TestCurrentUserService { Permissions = ModeratorPermission.None };
        var service = new MediaService(mediaRepo, new FakeGameRepo(), new FakeBrokenLinkChecker(), currentUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ApproveMediaAsync(item.Id));
    }

    [Fact]
    public void All_WithTwelveGranularFlags_EqualsExpectedMask()
    {
        Assert.Equal(4095, (int)ModeratorPermission.All);
    }

    [Theory]
    [InlineData(ModeratorPermission.CanEditGames)]
    [InlineData(ModeratorPermission.CanUploadImages)]
    [InlineData(ModeratorPermission.CanManagePublishers)]
    [InlineData(ModeratorPermission.CanManageCreators)]
    [InlineData(ModeratorPermission.CanApproveMedia)]
    [InlineData(ModeratorPermission.CanResolveReports)]
    [InlineData(ModeratorPermission.CanManageStoreLinks)]
    [InlineData(ModeratorPermission.CanPublishInstagram)]
    [InlineData(ModeratorPermission.CanManageUsers)]
    [InlineData(ModeratorPermission.CanViewAuditLog)]
    [InlineData(ModeratorPermission.CanManageEvents)]
    [InlineData(ModeratorPermission.CanManageNotifications)]
    public void All_IncludesEveryGranularFlag(ModeratorPermission flag)
    {
        Assert.True(ModeratorPermission.All.HasFlag(flag));
    }

    [Theory]
    [InlineData(ModeratorPermission.CanManageEvents, 1 << 10)]
    [InlineData(ModeratorPermission.CanManageNotifications, 1 << 11)]
    public void NewFlags_ShouldKeepTheNextTwoBitPositionsWithoutShiftingStoredMasks(
        ModeratorPermission flag,
        int expectedBitValue)
    {
        // Contrato de persistencia: AppUsers.Permissions guarda el entero de la máscara, así que
        // las banderas nuevas deben ocupar bits vírgenes y no desplazar los valores ya almacenados.
        Assert.Equal(expectedBitValue, (int)flag);
    }

    [Fact]
    public void All_ShouldEqualTheOrOfEveryDeclaredFlag()
    {
        var everyDeclaredFlag = Enum.GetValues<ModeratorPermission>()
            .Where(flag => flag != ModeratorPermission.All)
            .Aggregate(ModeratorPermission.None, (mask, flag) => mask | flag);

        Assert.Equal(ModeratorPermission.All, everyDeclaredFlag);
    }

    [Fact]
    public void CanManageUsers_DoesNotGrantCatalogMediaOrAuditPermissions()
    {
        var mask = ModeratorPermission.CanManageUsers;

        Assert.False(mask.HasFlag(ModeratorPermission.CanEditGames));
        Assert.False(mask.HasFlag(ModeratorPermission.CanApproveMedia));
        Assert.False(mask.HasFlag(ModeratorPermission.CanViewAuditLog));
    }

    [Fact]
    public void CanViewAuditLog_DoesNotGrantManagementOrPublishingPermissions()
    {
        var mask = ModeratorPermission.CanViewAuditLog;

        Assert.False(mask.HasFlag(ModeratorPermission.CanManageUsers));
        Assert.False(mask.HasFlag(ModeratorPermission.CanResolveReports));
        Assert.False(mask.HasFlag(ModeratorPermission.CanPublishInstagram));
    }

    [Fact]
    public async Task PublisherService_WithOnlyNewFlags_ThrowsUnauthorized()
    {
        var pubRepo = new FakePublisherRepo();
        var gameRepo = new FakeGameRepo();
        var currentUser = new TestCurrentUserService
        {
            Permissions = ModeratorPermission.CanManageUsers | ModeratorPermission.CanViewAuditLog
        };

        var service = new PublisherService(pubRepo, gameRepo, currentUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(new CreatePublisherDto("Devir", "devir", null, null, null, null, null, null)));
    }
}
