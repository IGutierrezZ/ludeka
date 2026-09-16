using System;
using System.IO;
using Ludeka.Web.Authentication;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Contrato de la protección de rutas de INC-46 (F2): orden del pipeline, páginas con su política
/// y endpoints públicos explícitos. Se lee el código fuente (convención del repositorio) porque el
/// comportamiento real se comprueba con el arranque y las sondas descritas en la verificación.
/// </summary>
public class AuthorizationPipelineContractTests
{
    public static TheoryData<string, string, string> ProtectedPages => new()
    {
        { "UserManagement.razor", "/admin/usuarios", AuthorizationPolicies.PermisoGestionarUsuarios },
        { "AuditLogViewer.razor", "/admin/auditoria", AuthorizationPolicies.PermisoVerAuditoria },
        { "CatalogQueueAdmin.razor", "/admin/cola-catalogacion", AuthorizationPolicies.PermisoEditarFichas },
        { "InstagramModeration.razor", "/admin/instagram", AuthorizationPolicies.PermisoPublicarInstagram },
        { "SocialInboxModeration.razor", "/admin/ingesta-social", AuthorizationPolicies.PermisoAprobarMedios },
        { "MonitoredAccountsDirectory.razor", "/admin/canales-monitorizados", AuthorizationPolicies.PermisoAprobarMedios },
        { "MediaModeration.razor", "/moderacion-media", AuthorizationPolicies.PermisoAprobarMedios },
        { "GameReportsModeration.razor", "/moderacion/reportes", AuthorizationPolicies.PermisoResolverReportes },
        { "AdminNotifications.razor", "/admin/notificaciones", AuthorizationPolicies.PermisoGestionarNotificaciones },
        { "EventsManagement.razor", "/admin/eventos", AuthorizationPolicies.PermisoGestionarEventos },
    };

    [Fact]
    public void Pipeline_ShouldAuthenticateAndAuthorizeBeforeAntiforgery()
    {
        var source = ReadSource("src/Ludeka.Web/Program.cs");

        var authentication = source.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
        var authorization = source.IndexOf("app.UseAuthorization();", StringComparison.Ordinal);
        var antiforgery = source.IndexOf("app.UseAntiforgery();", StringComparison.Ordinal);
        var https = source.IndexOf("app.UseHttpsRedirection();", StringComparison.Ordinal);

        Assert.True(https >= 0, "Program.cs no llama a UseHttpsRedirection().");
        Assert.True(authentication > https, "UseAuthentication() debe ir después de UseHttpsRedirection().");
        Assert.True(authorization > authentication, "UseAuthorization() debe ir después de UseAuthentication().");
        Assert.True(antiforgery > authorization, "UseAntiforgery() debe ir después de UseAuthorization().");
    }

    [Theory]
    [MemberData(nameof(ProtectedPages))]
    public void ProtectedPage_ShouldDeclareItsAuthorizePolicyAndRoute(string pageFile, string route, string policy)
    {
        var source = ReadSource($"src/Ludeka.Web/Components/Pages/{pageFile}");

        Assert.Contains($"@page \"{route}\"", source, StringComparison.Ordinal);
        Assert.Contains($"@attribute [Authorize(Policy = AuthorizationPolicies.{policy})]", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("MediaModeration.razor", "/moderacion/multimedia")]
    [InlineData("MediaModeration.razor", "/admin/multimedia")]
    [InlineData("MediaModeration.razor", "/admin/moderacion-medios")]
    [InlineData("GameReportsModeration.razor", "/admin/reportes")]
    public void AliasRoute_ShouldLiveInAPageThatIsAlreadyProtected(string pageFile, string alias)
    {
        var source = ReadSource($"src/Ludeka.Web/Components/Pages/{pageFile}");

        Assert.Contains($"@page \"{alias}\"", source, StringComparison.Ordinal);
        Assert.Contains("@attribute [Authorize(Policy = AuthorizationPolicies.", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/ready")]
    [InlineData("/login/external")]
    [InlineData("/logout")]
    public void PublicEndpoint_ShouldDeclareAllowAnonymous(string route)
    {
        var source = ReadSource("src/Ludeka.Web/Program.cs");

        var index = source.IndexOf($"\"{route}\"", StringComparison.Ordinal);
        Assert.True(index > 0, $"No se encontró el endpoint {route} en Program.cs.");

        var nextEndpoint = source.IndexOf("app.Map", index + route.Length, StringComparison.Ordinal);
        var block = nextEndpoint > index ? source[index..nextEndpoint] : source[index..];
        Assert.Contains(".AllowAnonymous()", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Routes_ShouldUseAuthorizeRouteViewWithRedirectToLogin()
    {
        var routes = ReadSource("src/Ludeka.Web/Components/Routes.razor");
        Assert.Contains("<AuthorizeRouteView", routes, StringComparison.Ordinal);
        Assert.Contains("<NotAuthorized>", routes, StringComparison.Ordinal);
        Assert.Contains("<RedirectToLogin", routes, StringComparison.Ordinal);
        Assert.DoesNotContain("<RouteView", routes, StringComparison.Ordinal);

        var redirect = ReadSource("src/Ludeka.Web/Components/Shared/RedirectToLogin.razor");
        Assert.Contains("RendererInfo.IsInteractive", redirect, StringComparison.Ordinal);
        Assert.Contains("ExternalAuthenticationSchemes.LoginPath", redirect, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionGuard_ShouldForceAFullReloadWhenTheSessionIsInvalidated()
    {
        // INC-46 F3: el guardián del circuito escucha la invalidación y recarga la página completa
        // para que el pipeline y el circuito reevalúen la identidad sin esperar al cierre de sesión.
        var guard = ReadSource("src/Ludeka.Web/Components/Shared/SessionGuard.razor");
        Assert.Contains("IUserSessionInvalidator", guard, StringComparison.Ordinal);
        Assert.Contains("Invalidated +=", guard, StringComparison.Ordinal);
        Assert.Contains("forceLoad: true", guard, StringComparison.Ordinal);
        Assert.Contains("RendererInfo.IsInteractive", guard, StringComparison.Ordinal);
        Assert.Contains("Dispose", guard, StringComparison.Ordinal);

        var layout = ReadSource("src/Ludeka.Web/Components/Layout/MainLayout.razor");
        Assert.Contains("<SessionGuard />", layout, StringComparison.Ordinal);
    }

    private static string ReadSource(string relativePath)
    {
        var path = Path.Combine(GetRepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"No se encontró el archivo fuente: {relativePath}");
        return File.ReadAllText(path);
    }

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Ludeka.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
