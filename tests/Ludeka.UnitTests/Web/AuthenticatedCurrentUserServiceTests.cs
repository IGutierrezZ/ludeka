using System;
using System.Security.Claims;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Web.Authentication;
using Ludeka.Web.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Identidad de la sesión (INC-46 F3): sin sesión no hay usuario ni privilegios, el snapshot del
/// circuito manda sobre los claims y una cuenta suspendida queda sin roles ni permisos.
/// </summary>
public class AuthenticatedCurrentUserServiceTests
{
    private static AuthenticatedCurrentUserService WithoutSession()
        => new(new UserIdentitySnapshot());

    private static AuthenticatedCurrentUserService WithResolvedUser(AppUser? user)
    {
        var snapshot = new UserIdentitySnapshot();
        snapshot.Resolve(principal: null, user: user);
        return new AuthenticatedCurrentUserService(snapshot);
    }

    private static AuthenticatedCurrentUserService WithSessionCookie(AppUser user)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = ExternalLoginEvents.BuildSessionPrincipal(user) }
        };

        return new AuthenticatedCurrentUserService(new UserIdentitySnapshot(), accessor);
    }

    [Fact]
    public void WithoutSession_ShouldReportEmptyIdentityAndNoPrivilege()
    {
        var service = WithoutSession();

        Assert.Equal(string.Empty, service.UserId);
        Assert.Equal(string.Empty, service.UserName);
        Assert.Empty(service.Roles);
        Assert.False(service.IsFoundingTeam);
        Assert.False(service.IsInRole("FoundingTeam"));
        Assert.False(service.IsInRole("Moderator"));
        Assert.False(service.HasPermission(ModeratorPermission.All));
    }

    [Fact]
    public void WithFoundingTeamSession_ShouldExposeTheRoleAndEveryPermission()
    {
        var service = WithResolvedUser(new AppUser(
            "carlos_fundador", "Carlos Fundador", "carlos@ludeka.es", UserRole.FoundingTeam));

        Assert.Equal("carlos_fundador", service.UserId);
        Assert.Equal("Carlos Fundador", service.UserName);
        Assert.Equal(["FoundingTeam"], service.Roles);
        Assert.True(service.IsFoundingTeam);
        Assert.True(service.IsInRole("FoundingTeam"));
        Assert.False(service.IsInRole("Moderator"));
        Assert.True(service.HasPermission(ModeratorPermission.CanManageUsers));
        Assert.True(service.HasPermission(ModeratorPermission.CanViewAuditLog));
    }

    [Fact]
    public void WithModeratorSession_ShouldGrantOnlyItsFlags()
    {
        var service = WithResolvedUser(new AppUser(
            "marta_media", "Marta", "marta@ludeka.es", UserRole.Moderator, ModeratorPermission.CanApproveMedia));

        Assert.Equal(["Moderator"], service.Roles);
        Assert.False(service.IsFoundingTeam);
        Assert.True(service.IsInRole("Moderator"));
        Assert.True(service.HasPermission(ModeratorPermission.CanApproveMedia));
        Assert.False(service.HasPermission(ModeratorPermission.CanResolveReports));
    }

    [Fact]
    public void WithCommunitySession_ShouldHaveNoModerationPrivilege()
    {
        var service = WithResolvedUser(new AppUser("jugador", "Jugador", "jugador@ludeka.es"));

        Assert.Equal(["CommunityUser"], service.Roles);
        Assert.False(service.IsFoundingTeam);
        Assert.False(service.IsInRole("Moderator"));
        Assert.False(service.HasPermission(ModeratorPermission.CanApproveMedia));
    }

    [Fact]
    public void WithSuspendedSession_ShouldKeepIdentityButDenyEveryPrivilege()
    {
        var service = WithResolvedUser(new AppUser(
            "susana_suspendida", "Susana", "susana@ludeka.es",
            UserRole.Moderator, ModeratorPermission.All, UserStatus.Suspended));

        Assert.Equal("susana_suspendida", service.UserId);
        Assert.Equal("Susana", service.UserName);
        Assert.Empty(service.Roles);
        Assert.False(service.IsFoundingTeam);
        Assert.False(service.IsInRole("Moderator"));
        Assert.False(service.HasPermission(ModeratorPermission.CanApproveMedia));
    }

    [Fact]
    public void SsrWithSessionCookie_ShouldProjectTheCookieIdentity()
    {
        var user = new AppUser(
            "laura_mod", "Laura Moderadora", "laura@ludeka.es", UserRole.Moderator, ModeratorPermission.CanEditGames);

        var service = WithSessionCookie(user);

        Assert.Equal("laura_mod", service.UserId);
        Assert.Equal("Laura Moderadora", service.UserName);
        Assert.Equal(["Moderator"], service.Roles);
        Assert.False(service.IsFoundingTeam);
        Assert.True(service.IsInRole("Moderator"));
        Assert.True(service.HasPermission(ModeratorPermission.CanEditGames));
        Assert.False(service.HasPermission(ModeratorPermission.CanApproveMedia));
    }

    [Fact]
    public void SsrWithAnonymousCookie_ShouldNotGrantAnyPrivilege()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        var service = new AuthenticatedCurrentUserService(new UserIdentitySnapshot(), accessor);

        Assert.Equal(string.Empty, service.UserId);
        Assert.Empty(service.Roles);
        Assert.False(service.IsInRole("Moderator"));
        Assert.False(service.HasPermission(ModeratorPermission.All));
    }

    [Fact]
    public void WithoutSession_ShouldIgnoreNullAndBlankRoleNames()
    {
        var service = WithoutSession();

        Assert.False(service.IsInRole(null!));
        Assert.False(service.IsInRole("   "));
    }
}
