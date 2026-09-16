using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Admin;
using Ludeka.Application.Features.Catalog;
using Ludeka.Application.Features.Directory;
using Ludeka.Application.Features.Media;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// Servicios de escritura con puerta de permiso (INC-46, F4): sin sesión la denegación es controlada
/// y no se persiste ni se audita nada, ni siquiera cuando la identidad declara banderas privilegiadas
/// pero no tiene sesión real.
/// </summary>
public abstract class PrivilegedServiceAnonymityTestBase : AnonymityPolicyTestBase
{
    /// <summary>Verifica que un servicio que exige permiso no escribe sin sesión.</summary>
    protected static Task AssertDenied(Func<Task> action) => Assert.ThrowsAsync<UnauthorizedAccessException>(action);

    /// <summary>Doble sin lógica del comprobador de enlaces rotos.</summary>
    protected sealed class StubBrokenLinkChecker : IBrokenLinkCheckerService
    {
        public Task<BrokenLinkReportDto> CheckLinksAsync(CancellationToken ct = default)
            => Task.FromResult(new BrokenLinkReportDto(0, 0, []));
    }

    /// <summary>Doble sin lógica del servicio de catálogo.</summary>
    protected sealed class StubCatalogService : ICatalogService
    {
        public Task<CatalogResult> GetCatalogAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
            => Task.FromResult(new CatalogResult([], 0, page, pageSize));

        public Task<GameDetailDto?> GetGameBySlugAsync(string slug, CancellationToken ct = default)
            => Task.FromResult<GameDetailDto?>(null);

        public Task<IReadOnlyList<GameSummaryDto>> GetQuickSearchAsync(string term, int limit = 5, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GameSummaryDto>>([]);
    }

    protected AuditService CreateAuditService(ICurrentUserService currentUser)
        => new(new SqliteAuditLogRepository(Context), currentUser);
}

/// <summary>Moderación de contenido multimedia: sin sesión no se aprueba, rechaza ni elimina.</summary>
public class MediaModerationAnonymityTests : PrivilegedServiceAnonymityTestBase
{
    private MediaItem SeedPendingItem()
    {
        var item = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Tutorial de prueba",
            "https://example.test/video",
            "https://example.test/miniatura.jpg",
            "Canal de Prueba",
            gameId: Game.Id);

        Context.MediaItems.Add(item);
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
        return item;
    }

    [Fact]
    public async Task ApproveMediaAsync_WithoutSession_DeniesAndPersistsNothing()
    {
        var item = SeedPendingItem();
        var currentUser = Anonymous();
        var service = new MediaService(
            new SqliteMediaRepository(Context),
            new SqliteGameRepository(Context),
            new StubBrokenLinkChecker(),
            currentUser,
            CreateAuditService(currentUser));

        await AssertDenied(() => service.ApproveMediaAsync(item.Id));

        var stored = await Context.MediaItems.AsNoTracking().SingleAsync(m => m.Id == item.Id);
        Assert.Equal(ModerationStatus.PendingApproval, stored.Status);
        Assert.Empty(await Context.AuditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ApproveMediaAsync_WithPrivilegesButWithoutSession_DeniesAndPersistsNothing()
    {
        var item = SeedPendingItem();
        var currentUser = PrivilegedWithoutSession();
        var service = new MediaService(
            new SqliteMediaRepository(Context),
            new SqliteGameRepository(Context),
            new StubBrokenLinkChecker(),
            currentUser,
            CreateAuditService(currentUser));

        await AssertDenied(() => service.ApproveMediaAsync(item.Id));

        var stored = await Context.MediaItems.AsNoTracking().SingleAsync(m => m.Id == item.Id);
        Assert.Equal(ModerationStatus.PendingApproval, stored.Status);
        Assert.Empty(await Context.AuditLogs.AsNoTracking().ToListAsync());
    }
}

/// <summary>Gestión de usuarios: solo la Mesa Fundadora con sesión real puede escribir y auditar.</summary>
public class UserManagementAnonymityTests : PrivilegedServiceAnonymityTestBase
{
    [Fact]
    public async Task CreateUserAsync_WithPrivilegesButWithoutSession_DeniesAndPersistsNoUserNorAudit()
    {
        var currentUser = PrivilegedWithoutSession();
        var service = new UserManagementService(
            new SqliteUserRepository(Context),
            CreateAuditService(currentUser),
            currentUser);

        await AssertDenied(() => service.CreateUserAsync(new CreateUserCommand(
            Id: "ana_mod",
            UserName: "Ana Moderadora",
            Email: "ana@ludeka.es",
            Role: UserRole.Moderator,
            Permissions: ModeratorPermission.CanEditGames)));

        Assert.Empty(await Context.AppUsers.AsNoTracking().ToListAsync());
        Assert.Empty(await Context.AuditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateUserAsync_WithSession_PersistsUserAndAuditsTheRealActor()
    {
        var currentUser = WithSession("carlos_fundador", "Carlos Fundador");
        currentUser.Roles = ["FoundingTeam"];
        currentUser.IsFoundingTeam = true;
        var service = new UserManagementService(
            new SqliteUserRepository(Context),
            CreateAuditService(currentUser),
            currentUser);

        var created = await service.CreateUserAsync(new CreateUserCommand(
            Id: "ana_mod",
            UserName: "Ana Moderadora",
            Email: "ana@ludeka.es",
            Role: UserRole.Moderator,
            Permissions: ModeratorPermission.CanEditGames));

        Assert.Equal("ana_mod", created.Id);
        var storedUser = await Context.AppUsers.AsNoTracking().SingleAsync(u => u.Id == "ana_mod");
        Assert.Equal("Ana Moderadora", storedUser.UserName);

        var storedAudit = await Context.AuditLogs.AsNoTracking().SingleAsync();
        Assert.Equal("carlos_fundador", storedAudit.UserId);
    }
}

/// <summary>Edición editorial de fichas: sin sesión no se modifica el juego ni se firma la bitácora.</summary>
public class GameEditingAnonymityTests : PrivilegedServiceAnonymityTestBase
{
    private static UpdateGameDetailsCommand CreateCommand(Guid gameId, string spanishTitle) => new(
        GameId: gameId,
        SpanishTitle: spanishTitle,
        OriginalTitle: "Juego de Prueba",
        Designer: "Diseñadora de Prueba",
        Publisher: "Editorial Local",
        YearPublished: 2024,
        Description: "Descripción editada.",
        MinPlayers: 2,
        MaxPlayers: 4,
        MinDurationMinutes: 30,
        MaxDurationMinutes: 60,
        EstimatedPerPlayerMinutes: 15,
        BoxAge: 10,
        CommunityAge: 10,
        Confrontation: ConfrontationType.Competitive,
        Style: GameStyle.Eurogame,
        IsOfficialSolo: false,
        Language: LanguageDependence.None,
        Footprint: TableFootprint.StandardTable,
        CoverImageUrl: null);

    [Fact]
    public async Task UpdateGameAsync_WithPrivilegesButWithoutSession_DeniesAndLeavesNoEditLog()
    {
        var currentUser = PrivilegedWithoutSession();
        var service = new GameEditorService(
            new SqliteGameRepository(Context),
            currentUser,
            new SqliteGameEditLogRepository(Context),
            new StubCatalogService());

        await AssertDenied(() => service.UpdateGameAsync(CreateCommand(Game.Id, "Título Editado Sin Sesión")));

        var stored = await Context.Games.AsNoTracking().SingleAsync(g => g.Id == Game.Id);
        Assert.Equal("Juego de Prueba", stored.SpanishTitle);
        Assert.Empty(await Context.GameEditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task UpdateGameAsync_WithSessionAndPermission_EditsAndSignsTheLogWithTheSessionIdentity()
    {
        var currentUser = WithSession("moderadora-real", "Moderadora Real");
        currentUser.Roles = ["Moderator"];
        currentUser.Permissions = ModeratorPermission.CanEditGames;
        var service = new GameEditorService(
            new SqliteGameRepository(Context),
            currentUser,
            new SqliteGameEditLogRepository(Context),
            new StubCatalogService());

        await service.UpdateGameAsync(CreateCommand(Game.Id, "Título Editado Con Sesión"));

        var stored = await Context.Games.AsNoTracking().SingleAsync(g => g.Id == Game.Id);
        Assert.Equal("Título Editado Con Sesión", stored.SpanishTitle);

        var log = await Context.GameEditLogs.AsNoTracking().SingleAsync();
        Assert.Equal("moderadora-real", log.EditorUserId);
    }
}

/// <summary>Directorios (creadores, editoriales, tiendas): sin sesión no se dan de alta fichas.</summary>
public class DirectoryManagementAnonymityTests : PrivilegedServiceAnonymityTestBase
{
    private static readonly CreateCreatorDto CreatorRequest = new(
        Name: "Creadora de Prueba",
        Slug: "creadora-de-prueba",
        Nationality: "España",
        Bio: "Bio de prueba.",
        AvatarUrl: null,
        BggPersonId: null,
        WebsiteUrl: null,
        SocialLinks: null);

    [Fact]
    public async Task CreateAsync_WithoutSession_DeniesAndPersistsNothing()
    {
        var currentUser = Anonymous();
        var service = new CreatorService(
            new SqliteCreatorRepository(Context),
            currentUser,
            CreateAuditService(currentUser));

        await AssertDenied(() => service.CreateAsync(CreatorRequest));

        Assert.Empty(await Context.Creators.AsNoTracking().ToListAsync());
        Assert.Empty(await Context.AuditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_WithPrivilegesButWithoutSession_DeniesAndPersistsNothing()
    {
        var currentUser = PrivilegedWithoutSession();
        var service = new CreatorService(
            new SqliteCreatorRepository(Context),
            currentUser,
            CreateAuditService(currentUser));

        await AssertDenied(() => service.CreateAsync(CreatorRequest));

        Assert.Empty(await Context.Creators.AsNoTracking().ToListAsync());
        Assert.Empty(await Context.AuditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_WithSessionAndPermission_PersistsTheCreator()
    {
        var currentUser = WithSession("moderadora-real", "Moderadora Real");
        currentUser.Roles = ["Moderator"];
        currentUser.Permissions = ModeratorPermission.CanManageCreators;
        var service = new CreatorService(
            new SqliteCreatorRepository(Context),
            currentUser,
            CreateAuditService(currentUser));

        var created = await service.CreateAsync(CreatorRequest);

        Assert.Equal("creadora-de-prueba", created.Slug);
        var stored = await Context.Creators.AsNoTracking().SingleAsync();
        Assert.Equal("Creadora de Prueba", stored.Name);
    }
}
