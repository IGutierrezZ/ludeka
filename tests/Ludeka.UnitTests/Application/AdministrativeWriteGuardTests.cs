using System;
using System.Linq;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
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
