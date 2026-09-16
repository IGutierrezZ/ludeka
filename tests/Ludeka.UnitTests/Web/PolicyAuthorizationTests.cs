using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Ludeka.Web.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Evaluación real de las 11 políticas de permiso de INC-46 contra el AppUser de la sesión:
/// anónimos y cuentas comunitarias denegados, moderador solo con el flag exacto, fundador
/// permitido y cuentas suspendidas denegadas aunque conserven permisos.
/// </summary>
public class PolicyAuthorizationTests : IAsyncLifetime
{
    /// <summary>Las 11 políticas de permiso del diseño, congeladas aquí para detectar ausencias.</summary>
    private static readonly string[] PermissionPolicies =
    [
        "PermisoEditarFichas",
        "PermisoAprobarMedios",
        "PermisoResolverReportes",
        "PermisoGestionarEditores",
        "PermisoGestionarCreadores",
        "PermisoGestionarTiendas",
        "PermisoPublicarInstagram",
        "PermisoVerAuditoria",
        "PermisoGestionarUsuarios",
        "PermisoGestionarEventos",
        "PermisoGestionarNotificaciones"
    ];

    private SqliteConnection _connection = null!;
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<LudekaDbContext>(options => options.UseSqlite(_connection));
        services.AddScoped<IUserRepository, SqliteUserRepository>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization(AuthorizationPolicies.Configure);

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<LudekaDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task SeedUserAsync(AppUser user)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LudekaDbContext>();
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
    }

    private async Task<bool> AuthorizeAsync(string policyName, string? userId)
    {
        using var scope = _provider.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        var principal = userId is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)], "TestScheme"));

        var result = await authorization.AuthorizeAsync(principal, resource: null, policyName);
        return result.Succeeded;
    }

    public static TheoryData<string> AllPolicies()
    {
        var data = new TheoryData<string>();
        foreach (var policy in PermissionPolicies) data.Add(policy);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllPolicies))]
    public async Task Anonymous_ShouldBeDenied(string policyName)
    {
        Assert.False(await AuthorizeAsync(policyName, userId: null));
    }

    [Theory]
    [MemberData(nameof(AllPolicies))]
    public async Task CommunityUser_ShouldBeDenied(string policyName)
    {
        await SeedUserAsync(new AppUser("jugador-comunidad", "Jugador", "jugador@ludeka.es"));

        Assert.False(await AuthorizeAsync(policyName, "jugador-comunidad"));
    }

    [Fact]
    public async Task Moderator_ShouldOnlyBeAllowedByThePoliciesMatchingItsFlags()
    {
        await SeedUserAsync(new AppUser(
            "laura_mod", "Laura Moderadora", "laura@ludeka.es",
            UserRole.Moderator, ModeratorPermission.CanApproveMedia));

        Assert.True(await AuthorizeAsync("PermisoAprobarMedios", "laura_mod"));
        Assert.False(await AuthorizeAsync("PermisoResolverReportes", "laura_mod"));
        Assert.False(await AuthorizeAsync("PermisoGestionarUsuarios", "laura_mod"));
        Assert.False(await AuthorizeAsync("PermisoGestionarEventos", "laura_mod"));
        Assert.False(await AuthorizeAsync("PermisoGestionarNotificaciones", "laura_mod"));
    }

    [Theory]
    [MemberData(nameof(AllPolicies))]
    public async Task FoundingTeam_ShouldBeAllowedEveryPolicy(string policyName)
    {
        await SeedUserAsync(new AppUser("carlos_fundador", "Carlos Fundador", "carlos@ludeka.es", UserRole.FoundingTeam));

        Assert.True(await AuthorizeAsync(policyName, "carlos_fundador"));
    }

    [Theory]
    [MemberData(nameof(AllPolicies))]
    public async Task SuspendedAccount_ShouldBeDeniedEvenWithPermissions(string policyName)
    {
        await SeedUserAsync(new AppUser(
            "susana_suspendida", "Susana", "susana@ludeka.es",
            UserRole.Moderator, ModeratorPermission.All, UserStatus.Suspended));

        Assert.False(await AuthorizeAsync(policyName, "susana_suspendida"));
    }

    [Theory]
    [InlineData("PermisoEditarFichas", ModeratorPermission.CanEditGames)]
    [InlineData("PermisoAprobarMedios", ModeratorPermission.CanApproveMedia)]
    [InlineData("PermisoResolverReportes", ModeratorPermission.CanResolveReports)]
    [InlineData("PermisoGestionarEditores", ModeratorPermission.CanManagePublishers)]
    [InlineData("PermisoGestionarCreadores", ModeratorPermission.CanManageCreators)]
    [InlineData("PermisoGestionarTiendas", ModeratorPermission.CanManageStoreLinks)]
    [InlineData("PermisoPublicarInstagram", ModeratorPermission.CanPublishInstagram)]
    [InlineData("PermisoVerAuditoria", ModeratorPermission.CanViewAuditLog)]
    [InlineData("PermisoGestionarUsuarios", ModeratorPermission.CanManageUsers)]
    [InlineData("PermisoGestionarEventos", ModeratorPermission.CanManageEvents)]
    [InlineData("PermisoGestionarNotificaciones", ModeratorPermission.CanManageNotifications)]
    public async Task EachPolicy_ShouldBeGrantedByItsExactFlag(string policyName, ModeratorPermission permission)
    {
        var userId = $"mod-{permission}";
        await SeedUserAsync(new AppUser(userId, userId, $"{userId}@ludeka.es", UserRole.Moderator, permission));

        Assert.True(await AuthorizeAsync(policyName, userId));
    }

    [Fact]
    public async Task NewPermissionPolicies_ShouldNotCrossGrantEachOther()
    {
        await SeedUserAsync(new AppUser(
            "nuria_eventos", "Nuria", "nuria@ludeka.es",
            UserRole.Moderator, ModeratorPermission.CanManageEvents));

        Assert.True(await AuthorizeAsync("PermisoGestionarEventos", "nuria_eventos"));
        Assert.False(await AuthorizeAsync("PermisoGestionarNotificaciones", "nuria_eventos"));
    }

    [Fact]
    public async Task Policies_ShouldReflectHotRevocationBecauseTheHandlerReloadsTheAppUser()
    {
        await SeedUserAsync(new AppUser(
            "marta_media", "Marta", "marta@ludeka.es",
            UserRole.Moderator, ModeratorPermission.CanApproveMedia));
        Assert.True(await AuthorizeAsync("PermisoAprobarMedios", "marta_media"));

        // Revocación fuera de banda, como haría UserManagementService en otro circuito
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LudekaDbContext>();
            var user = await db.AppUsers.SingleAsync(u => u.Id == "marta_media");
            user.RevokePermission(ModeratorPermission.CanApproveMedia);
            await db.SaveChangesAsync();
        }

        Assert.False(await AuthorizeAsync("PermisoAprobarMedios", "marta_media"));
    }

    [Fact]
    public void PolicyNames_ShouldExposeTheElevenPermissionPoliciesPlusTheModeratorRolePolicy()
    {
        Assert.Equal(PermissionPolicies.Length, AuthorizationPolicies.PermissionPolicies.Count);
        Assert.All(PermissionPolicies, name => Assert.True(AuthorizationPolicies.PermissionPolicies.ContainsKey(name)));
        Assert.False(string.IsNullOrWhiteSpace(AuthorizationPolicies.RolModerador));
    }
}
