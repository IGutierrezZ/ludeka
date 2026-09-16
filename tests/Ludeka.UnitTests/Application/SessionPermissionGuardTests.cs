using System;
using System.Linq;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// Revalidación de permiso sobre la identidad de la sesión (INC-46, hallazgo W1): la guarda relee el
/// <c>AppUser</c> sin rastreo antes de cada operación, de modo que una cuenta suspendida o con
/// permisos revocados queda bloqueada aunque su cookie siga vigente y declare privilegios.
/// </summary>
public class SessionPermissionGuardTests : AnonymityPolicyTestBase
{
    private const ModeratorPermission AnyPermission = ModeratorPermission.CanEditGames;

    private static AppUser ModeratorWith(params ModeratorPermission[] permissions) => new(
        id: "moderadora-real",
        userName: "Moderadora Real",
        email: "moderadora@ludeka.es",
        role: UserRole.Moderator,
        permissions: permissions.Aggregate(ModeratorPermission.None, (mask, next) => mask | next));

    private void Seed(AppUser user)
    {
        Context.AppUsers.Add(user);
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    private ISessionPermissionGuard CreateGuard(ICurrentUserService currentUser)
        => new SessionPermissionGuard(currentUser, new SqliteUserRepository(Context));

    /// <summary>Cookie vigente de una moderadora: declara rol y todos los permisos.</summary>
    private static StubCurrentUserService LiveCookie(string userId = "moderadora-real") => new()
    {
        UserId = userId,
        UserName = "Moderadora Real",
        Roles = ["Moderator"],
        Permissions = ModeratorPermission.All
    };

    [Fact]
    public async Task RequireAsync_WithoutSession_Denies()
    {
        Seed(ModeratorWith(AnyPermission));
        var guard = CreateGuard(Anonymous());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => guard.RequireAsync(AnyPermission, "Operación administrativa denegada."));
    }

    [Fact]
    public async Task RequireAsync_WithLiveCookieOfAnAccountThatNoLongerExists_Denies()
    {
        var guard = CreateGuard(LiveCookie("cuenta-eliminada"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => guard.RequireAsync(AnyPermission, "Operación administrativa denegada."));
    }

    [Fact]
    public async Task RequireAsync_WithSuspendedAccount_DeniesEvenWithPrivilegedCookie()
    {
        var user = ModeratorWith(AnyPermission);
        Seed(user);

        user.UpdateStatus(UserStatus.Suspended);
        Context.AppUsers.Update(user);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var guard = CreateGuard(LiveCookie());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => guard.RequireAsync(AnyPermission, "Operación administrativa denegada."));
    }

    [Fact]
    public async Task RequireAsync_WithRevokedPermission_DeniesEvenIfTheCookieStillDeclaresIt()
    {
        Seed(ModeratorWith(ModeratorPermission.CanEditGames));
        var guard = CreateGuard(LiveCookie());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => guard.RequireAsync(ModeratorPermission.CanApproveMedia, "Operación administrativa denegada."));
    }

    [Fact]
    public async Task RequireAsync_WithCommunityUser_Denies()
    {
        Seed(new AppUser(
            id: "jugadora-real",
            userName: "Jugadora Real",
            email: "jugadora@ludeka.es",
            role: UserRole.CommunityUser));
        var guard = CreateGuard(LiveCookie("jugadora-real"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => guard.RequireAsync(AnyPermission, "Operación administrativa denegada."));
    }

    [Fact]
    public async Task RequireAsync_WithTheExactPermission_Allows()
    {
        Seed(ModeratorWith(AnyPermission));
        var guard = CreateGuard(LiveCookie());

        await guard.RequireAsync(AnyPermission, "Operación administrativa denegada.");
    }

    [Fact]
    public async Task RequireAsync_WithFoundingTeam_AllowsEveryPermission()
    {
        Seed(new AppUser(
            id: "fundadora-real",
            userName: "Fundadora Real",
            email: "fundadora@ludeka.es",
            role: UserRole.FoundingTeam));
        var guard = CreateGuard(LiveCookie("fundadora-real"));

        await guard.RequireAsync(ModeratorPermission.CanManageUsers, "Operación administrativa denegada.");
    }

    [Fact]
    public async Task RequireAsync_WhenPermissionIsRevokedBetweenOperations_BlocksTheNextOperation()
    {
        var user = ModeratorWith(ModeratorPermission.CanEditGames, ModeratorPermission.CanApproveMedia);
        Seed(user);
        var guard = CreateGuard(LiveCookie());

        await guard.RequireAsync(ModeratorPermission.CanApproveMedia, "Operación administrativa denegada.");

        user.RevokePermission(ModeratorPermission.CanApproveMedia);
        Context.AppUsers.Update(user);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => guard.RequireAsync(ModeratorPermission.CanApproveMedia, "Operación administrativa denegada."));
    }
}
