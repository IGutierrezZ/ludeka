using System;
using System.Linq;
using Ludeka.Application.Features.Admin;
using Ludeka.Core.Enums;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// Las insignias de permisos de la gestión de usuarios deben nombrar cada bandera declarada: una
/// bandera concedida sin nombre visible es una concesión invisible para quien administra. La prueba
/// enumera el contrato del propio enum, de modo que ampliarlo sin nombrar la bandera vuelve a rojo.
/// </summary>
public class PermissionNamesTests
{
    private static readonly ModeratorPermission[] DeclaredFlags = Enum.GetValues<ModeratorPermission>()
        .Where(flag => flag != ModeratorPermission.None && flag != ModeratorPermission.All)
        .ToArray();

    [Fact]
    public void GetPermissionNames_ForAModerator_ShouldNameEveryDeclaredFlag()
    {
        Assert.Equal(12, DeclaredFlags.Length);

        foreach (var flag in DeclaredFlags)
        {
            var names = UserManagementService.GetPermissionNames(flag, UserRole.Moderator);

            Assert.Single(names);
            Assert.False(
                string.IsNullOrWhiteSpace(names[0]),
                $"La bandera {flag} se concede sin un nombre visible.");
        }
    }

    [Fact]
    public void GetPermissionNames_ForTheFullMask_ShouldReturnTwelveDistinctNames()
    {
        var names = UserManagementService.GetPermissionNames(ModeratorPermission.All, UserRole.Moderator);

        Assert.Equal(DeclaredFlags.Length, names.Count);
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void GetPermissionNames_ForFoundingTeamOrCommunity_ShouldKeepItsCurrentBehavior()
    {
        Assert.Equal(
            ["Acceso Total (Mesa Fundadora)"],
            UserManagementService.GetPermissionNames(ModeratorPermission.All, UserRole.FoundingTeam));

        Assert.Empty(UserManagementService.GetPermissionNames(ModeratorPermission.All, UserRole.CommunityUser));
    }

    [Fact]
    public void GetPermissionNames_ForAnEmptyMask_ShouldReturnNothing()
    {
        Assert.Empty(UserManagementService.GetPermissionNames(ModeratorPermission.None, UserRole.Moderator));
    }
}
