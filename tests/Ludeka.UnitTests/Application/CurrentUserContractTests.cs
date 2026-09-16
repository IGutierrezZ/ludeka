using System;
using System.Linq;
using System.Reflection;
using Ludeka.Application.Contracts;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// Contrato sin conmutadores de INC-46 (F3): la identidad deja de poder cambiarse en caliente
/// desde el contrato y la compilación es la garantía de que no quedan puntos de uso.
/// La superficie queda congelada a la identidad derivada de la sesión.
/// </summary>
public class CurrentUserContractTests
{
    [Fact]
    public void Contract_ShouldNotDeclareRoleOrUserSwitchers()
    {
        var methodNames = typeof(ICurrentUserService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(method => method.Name)
            .ToArray();

        Assert.DoesNotContain("SwitchRole", methodNames);
        Assert.DoesNotContain("SwitchUser", methodNames);
    }

    [Fact]
    public void PermissionCheck_ShouldHaveNoDefaultImplementation()
    {
        var permissionCheck = typeof(ICurrentUserService)
            .GetMethod(nameof(ICurrentUserService.HasPermission));

        Assert.NotNull(permissionCheck);
        Assert.True(
            permissionCheck!.IsAbstract,
            "HasPermission no debe conceder permisos por defecto desde el contrato.");
    }

    [Fact]
    public void Contract_ShouldExposeOnlyTheSessionIdentitySurface()
    {
        var properties = typeof(ICurrentUserService)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["IsFoundingTeam", "Roles", "UserId", "UserName"], properties);

        var methods = typeof(ICurrentUserService)
            .GetMethods()
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["HasPermission", "IsInRole"], methods);
    }
}
