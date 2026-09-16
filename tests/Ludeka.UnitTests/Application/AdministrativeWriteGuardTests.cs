using System;
using System.Linq;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Community;
using Ludeka.Application.Features.Events;
using Ludeka.Application.Features.Media;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// Escrituras administrativas revalidadas por sesión (INC-46, hallazgo W1): cada servicio relee el
/// permiso sobre el <c>AppUser</c> actual antes de mutar. Esta base siembra un <c>AppUser</c> con el
/// permiso concedido y ofrece la cookie (doble de identidad) para los casos de suspensión y revocación.
/// </summary>
public abstract class AdministrativeWriteGuardTestBase : AnonymityPolicyTestBase
{
    protected const string ModeratorId = "moderadora-real";
    protected const string ModeratorName = "Moderadora Real";

    protected AppUser ModeratorWith(params ModeratorPermission[] permissions) => new(
        id: ModeratorId,
        userName: ModeratorName,
        email: "moderadora@ludeka.es",
        role: UserRole.Moderator,
        permissions: permissions.Aggregate(ModeratorPermission.None, (mask, next) => mask | next));

    /// <summary>Cookie vigente de una moderadora que declara rol y todos los permisos.</summary>
    protected static StubCurrentUserService LiveCookie() => new()
    {
        UserId = ModeratorId,
        UserName = ModeratorName,
        Roles = ["Moderator"],
        Permissions = ModeratorPermission.All
    };

    /// <summary>Sesión real con el identificador indicado y el permiso concedido en el doble.</summary>
    protected static StubCurrentUserService SessionWithPermission(ModeratorPermission permission)
    {
        var session = StubCurrentUserService.WithSession(ModeratorId, ModeratorName);
        session.Roles = ["Moderator"];
        session.Permissions = permission;
        return session;
    }

    protected void SeedUser(AppUser user)
    {
        Context.AppUsers.Add(user);
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    protected async Task SuspendAsync(string userId)
    {
        var user = await Context.AppUsers.SingleAsync(u => u.Id == userId);
        user.UpdateStatus(UserStatus.Suspended);
        Context.AppUsers.Update(user);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    protected async Task RevokeAsync(string userId, ModeratorPermission permission)
    {
        var user = await Context.AppUsers.SingleAsync(u => u.Id == userId);
        user.RevokePermission(permission);
        Context.AppUsers.Update(user);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    protected ISessionPermissionGuard CreateGuard(ICurrentUserService currentUser)
        => new SessionPermissionGuard(currentUser, new SqliteUserRepository(Context));

    protected static Task AssertDenied(Func<Task> action)
        => Assert.ThrowsAsync<UnauthorizedAccessException>(action);
}

/// <summary>Moderación multimedia: la verificación de enlaces también es una escritura administrativa.</summary>
public class MediaBrokenLinksWriteGuardTests : AdministrativeWriteGuardTestBase
{
    private MediaItem SeedMediaItem(string url)
    {
        var item = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Tutorial de prueba",
            url,
            "https://example.test/miniatura.jpg",
            "Canal de Prueba",
            gameId: Game.Id);

        Context.MediaItems.Add(item);
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
        return item;
    }

    private MediaService CreateService(ICurrentUserService currentUser)
        => new(
            new SqliteMediaRepository(Context),
            new SqliteGameRepository(Context),
            new BrokenLinkCheckerService(new SqliteMediaRepository(Context)),
            currentUser);

    [Fact]
    public async Task CheckBrokenLinksAsync_WithoutSession_DeniesAndLeavesLinksUntouched()
    {
        var item = SeedMediaItem("https://example.test/recurso-broken");
        var service = CreateService(Anonymous());

        await AssertDenied(() => service.CheckBrokenLinksAsync());

        var stored = await Context.MediaItems.AsNoTracking().SingleAsync(m => m.Id == item.Id);
        Assert.False(stored.IsBroken);
    }

    [Fact]
    public async Task CheckBrokenLinksAsync_WithoutThePermission_DeniesAndLeavesLinksUntouched()
    {
        var item = SeedMediaItem("https://example.test/recurso-broken");
        var session = StubCurrentUserService.WithSession("moderadora-sin-bandera", ModeratorName);
        var service = CreateService(session);

        await AssertDenied(() => service.CheckBrokenLinksAsync());

        var stored = await Context.MediaItems.AsNoTracking().SingleAsync(m => m.Id == item.Id);
        Assert.False(stored.IsBroken);
    }

    [Fact]
    public async Task CheckBrokenLinksAsync_WithThePermission_MarksTheBrokenLink()
    {
        var item = SeedMediaItem("https://example.test/recurso-broken");
        var service = CreateService(SessionWithPermission(ModeratorPermission.CanApproveMedia));

        var report = await service.CheckBrokenLinksAsync();

        Assert.Equal(1, report.BrokenCount);
        var stored = await Context.MediaItems.AsNoTracking().SingleAsync(m => m.Id == item.Id);
        Assert.True(stored.IsBroken);
    }
}

/// <summary>Eventos lúdicos: sin sesión, con permiso insuficiente o con la cuenta suspendida no se escribe.</summary>
public class BoardGameEventWriteGuardTests : AdministrativeWriteGuardTestBase
{
    private static CreateBoardGameEventRequest CreateRequest() => new(
        Title: "Feria de prueba",
        Description: "Feria lúdica de prueba.",
        ImageUrl: "https://example.test/cartel.jpg",
        StartDate: new DateOnly(2026, 10, 1),
        EndDate: new DateOnly(2026, 10, 3),
        Location: "Madrid",
        Country: "España");

    private BoardGameEventService CreateService(ISessionPermissionGuard? guard = null)
        => new(new SqliteBoardGameEventRepository(Context), guard);

    [Fact]
    public async Task CreateEventAsync_WithoutSession_DeniesAndPersistsNothing()
    {
        var service = CreateService(CreateGuard(Anonymous()));

        await AssertDenied(() => service.CreateEventAsync(CreateRequest()));

        Assert.Empty(await Context.BoardGameEvents.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateEventAsync_WithSuspendedAccount_DeniesEvenWithLiveCookie()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanManageEvents));
        await SuspendAsync(ModeratorId);
        var service = CreateService(CreateGuard(LiveCookie()));

        await AssertDenied(() => service.CreateEventAsync(CreateRequest()));

        Assert.Empty(await Context.BoardGameEvents.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateEventAsync_WithThePermission_PersistsTheEvent()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanManageEvents));
        var service = CreateService(CreateGuard(LiveCookie()));

        var created = await service.CreateEventAsync(CreateRequest());

        Assert.Equal("Feria de prueba", created.Title);
        var stored = await Context.BoardGameEvents.AsNoTracking().SingleAsync();
        Assert.Equal("Feria de prueba", stored.Title);
    }

    [Fact]
    public async Task DeleteEventAsync_WithoutThePermission_DeniesAndKeepsTheEvent()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanApproveMedia));
        var evt = new BoardGameEvent(
            title: "Feria existente",
            description: "Evento sembrado para la prueba.",
            imageUrl: "https://example.test/cartel.jpg",
            startDate: new DateOnly(2026, 10, 1),
            endDate: new DateOnly(2026, 10, 3),
            location: "Madrid",
            websiteUrl: "https://example.test/feria",
            organizer: "Asociación Lúdica",
            isOfficial: true,
            country: "España");
        Context.BoardGameEvents.Add(evt);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var service = CreateService(CreateGuard(LiveCookie()));

        await AssertDenied(() => service.DeleteEventAsync(evt.Id));

        Assert.True(await Context.BoardGameEvents.AsNoTracking().AnyAsync(e => e.Id == evt.Id));
    }
}

/// <summary>Novedades editoriales: la creación manual revalida permiso de moderación de contenido.</summary>
public class WeeklyReleaseWriteGuardTests : AdministrativeWriteGuardTestBase
{
    private CreateWeeklyReleaseRequest CreateRequest() => new(
        Title: "Lanzamiento de prueba",
        Publisher: "Editorial Local",
        ReleaseDate: new DateOnly(2026, 10, 2),
        GameId: Game.Id);

    private WeeklyReleaseService CreateService(ISessionPermissionGuard? guard = null)
        => new(new SqliteWeeklyReleaseRepository(Context), guard);

    [Fact]
    public async Task CreateReleaseAsync_WithoutSession_DeniesAndPersistsNothing()
    {
        var service = CreateService(CreateGuard(Anonymous()));

        await AssertDenied(() => service.CreateReleaseAsync(CreateRequest()));

        Assert.Empty(await Context.WeeklyReleases.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateReleaseAsync_WithSuspendedAccount_DeniesEvenWithLiveCookie()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanApproveMedia));
        await SuspendAsync(ModeratorId);
        var service = CreateService(CreateGuard(LiveCookie()));

        await AssertDenied(() => service.CreateReleaseAsync(CreateRequest()));

        Assert.Empty(await Context.WeeklyReleases.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateReleaseAsync_WithoutThePermission_DeniesAndPersistsNothing()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanEditGames));
        var service = CreateService(CreateGuard(LiveCookie()));

        await AssertDenied(() => service.CreateReleaseAsync(CreateRequest()));

        Assert.Empty(await Context.WeeklyReleases.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateReleaseAsync_WithThePermission_PersistsTheRelease()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanApproveMedia));
        var service = CreateService(CreateGuard(LiveCookie()));

        var created = await service.CreateReleaseAsync(CreateRequest());

        Assert.Equal("Lanzamiento de prueba", created.Title);
        var stored = await Context.WeeklyReleases.AsNoTracking().SingleAsync();
        Assert.Equal("Editorial Local", stored.Publisher);
    }
}

/// <summary>Sorteos comunitarios: crear y promover exigen el permiso de moderación de contenido.</summary>
public class GiveawayWriteGuardTests : AdministrativeWriteGuardTestBase
{
    private static CreateGiveawayRequest CreateRequest() => new(
        Title: "Sorteo de prueba",
        Organizer: "Editorial Local",
        Collaborator: null,
        Url: "https://example.test/sorteo",
        Platform: GiveawayPlatform.Instagram,
        DeadlineAt: DateTimeOffset.UtcNow.AddDays(7));

    private GiveawayService CreateService(ISessionPermissionGuard? guard = null)
        => new(new SqliteGiveawayRepository(Context), guard);

    private async Task<Giveaway> SeedGiveawayAsync()
    {
        var giveaway = new Giveaway(
            title: "Sorteo existente",
            organizer: "Editorial Local",
            url: "https://example.test/sorteo-existente",
            platform: GiveawayPlatform.Instagram,
            deadlineAt: DateTimeOffset.UtcNow.AddDays(3),
            isPromoted: false);

        Context.Giveaways.Add(giveaway);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return giveaway;
    }

    [Fact]
    public async Task CreateOrMergeGiveawayAsync_WithoutSession_DeniesAndPersistsNothing()
    {
        var service = CreateService(CreateGuard(Anonymous()));

        await AssertDenied(() => service.CreateOrMergeGiveawayAsync(CreateRequest()));

        Assert.Empty(await Context.Giveaways.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task SetPromotedAsync_WithSuspendedAccount_DeniesEvenWithLiveCookie()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanApproveMedia));
        await SuspendAsync(ModeratorId);
        var giveaway = await SeedGiveawayAsync();
        var service = CreateService(CreateGuard(LiveCookie()));

        await AssertDenied(() => service.SetPromotedAsync(giveaway.Id, isPromoted: true));

        var stored = await Context.Giveaways.AsNoTracking().SingleAsync(g => g.Id == giveaway.Id);
        Assert.False(stored.IsPromoted);
    }

    [Fact]
    public async Task CreateOrMergeGiveawayAsync_WithThePermission_PersistsTheGiveaway()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanApproveMedia));
        var service = CreateService(CreateGuard(LiveCookie()));

        var created = await service.CreateOrMergeGiveawayAsync(CreateRequest());

        Assert.Equal("Sorteo de prueba", created.Title);
        var stored = await Context.Giveaways.AsNoTracking().SingleAsync();
        Assert.Equal("Editorial Local", stored.Organizer);
    }

    [Fact]
    public async Task SetPromotedAsync_AfterRevocationBetweenOperations_DeniesAndKeepsTheGiveaway()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanApproveMedia));
        var giveaway = await SeedGiveawayAsync();
        var service = CreateService(CreateGuard(LiveCookie()));

        await service.SetPromotedAsync(giveaway.Id, isPromoted: true);
        await RevokeAsync(ModeratorId, ModeratorPermission.CanApproveMedia);

        await AssertDenied(() => service.SetPromotedAsync(giveaway.Id, isPromoted: false));

        var stored = await Context.Giveaways.AsNoTracking().SingleAsync(g => g.Id == giveaway.Id);
        Assert.True(stored.IsPromoted);
    }
}
