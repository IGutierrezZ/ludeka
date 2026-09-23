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

        // INC-52 (Fase 3), design.md D1: UseForwardedHeaders debe ir como PRIMER middleware de
        // la tubería, antes del bloque de no-desarrollo (UseExceptionHandler/UseHsts) y, por
        // tanto, antes de UseHttpsRedirection.
        var forwardedHeaders = source.IndexOf("app.UseForwardedHeaders(", StringComparison.Ordinal);
        var exceptionHandler = source.IndexOf("app.UseExceptionHandler(", StringComparison.Ordinal);
        var authentication = source.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
        var authorization = source.IndexOf("app.UseAuthorization();", StringComparison.Ordinal);
        var antiforgery = source.IndexOf("app.UseAntiforgery();", StringComparison.Ordinal);
        var https = source.IndexOf("app.UseHttpsRedirection();", StringComparison.Ordinal);

        Assert.True(https >= 0, "Program.cs no llama a UseHttpsRedirection().");
        Assert.True(forwardedHeaders >= 0, "Program.cs no llama a UseForwardedHeaders().");
        Assert.True(forwardedHeaders < https, "UseForwardedHeaders() debe ir antes de UseHttpsRedirection().");
        Assert.True(forwardedHeaders < exceptionHandler, "UseForwardedHeaders() debe ir antes de UseExceptionHandler().");
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
    public void LinkAccountEndpoint_ShouldDeclareRequireAuthorization()
    {
        // INC-49: a diferencia de /login/external (AllowAnonymous), el desafío de vinculación exige
        // sesión iniciada. Mismo idioma de lectura de fuente que PublicEndpoint_ShouldDeclareAllowAnonymous.
        var source = ReadSource("src/Ludeka.Web/Program.cs");

        var index = source.IndexOf("\"/cuenta/conexiones/vincular\"", StringComparison.Ordinal);
        Assert.True(index > 0, "No se encontró el endpoint /cuenta/conexiones/vincular en Program.cs.");

        var nextEndpoint = source.IndexOf("app.Map", index + "/cuenta/conexiones/vincular".Length, StringComparison.Ordinal);
        var block = nextEndpoint > index ? source[index..nextEndpoint] : source[index..];
        Assert.Contains(".RequireAuthorization()", block, StringComparison.Ordinal);
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
        Assert.Contains("ToLogin(", redirect, StringComparison.Ordinal);
    }

    [Fact]
    public void RedirectToLogin_ShouldPreserveReturnUrlWithLoginRedirectSemantics()
    {
        // INC-50, diseño D2: el camino interactivo reutiliza LoginRedirect.ToLogin (escape +
        // guard ResolveReturnUrl) en lugar de construir la navegación a mano, de modo que la
        // ReturnUrl se preserva con el mismo formato que el desafío del middleware en SSR.
        var marcadorViejo = "NavigateTo(ExternalAuthenticationSchemes.LoginPath";
        var redirect = ReadSource("src/Ludeka.Web/Components/Shared/RedirectToLogin.razor");
        Assert.Contains("ToLogin(", redirect, StringComparison.Ordinal);
        Assert.DoesNotContain(marcadorViejo, redirect, StringComparison.Ordinal);
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

    [Fact]
    public void MainLayout_ShouldMountTheAccountEmailNotice()
    {
        // INC-49 PR #6: el aviso de correo no verificado se monta en la cabecera de toda la
        // aplicación. Mismo idioma de lectura de fuente que
        // SessionGuard_ShouldForceAFullReloadWhenTheSessionIsInvalidated: solo se afirma la
        // presencia del componente, no su posición exacta dentro del fichero.
        var layout = ReadSource("src/Ludeka.Web/Components/Layout/MainLayout.razor");
        Assert.Contains("<AccountEmailNotice />", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicProfile_ShouldNeverReferenceTheUnverifiedEmailNotice()
    {
        // INC-49: spec account-provider-connections, escenario "El aviso nunca aparece en el
        // perfil público". Pin de regresión hacia adelante: hoy ya pasa trivialmente porque
        // PublicProfile.razor no conoce ninguna de estas tres piezas.
        var profile = ReadSource("src/Ludeka.Web/Components/Pages/PublicProfile.razor");
        Assert.DoesNotContain("AccountEmailNotice", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("IAccountConnectionsService", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("HasVerifiedProviderEmailAsync", profile, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountEmailNotice_ShouldSubscribeToConnectionsInvalidatedAndDisposeCleanly()
    {
        // Corrección del orquestador (INC-49 PR #6): IAccountConnectionsService cachea por todo el
        // circuito (diseño §D6), así que el aviso de cabecera debe refrescarse cuando esa caché se
        // invalida tras una desvinculación, sin depender de una recarga completa de página. Mismo
        // idioma de contrato de fuente que SessionGuard_ShouldForceAFullReloadWhenTheSessionIsInvalidated,
        // que exige exactamente la misma disciplina de suscripción/baja para no filtrar memoria por circuito.
        var notice = ReadSource("src/Ludeka.Web/Components/Shared/AccountEmailNotice.razor");

        Assert.Contains("@implements IDisposable", notice, StringComparison.Ordinal);
        Assert.Contains("Connections.Invalidated +=", notice, StringComparison.Ordinal);
        Assert.Contains("Connections.Invalidated -=", notice, StringComparison.Ordinal);
        Assert.Contains("RendererInfo.IsInteractive", notice, StringComparison.Ordinal);
        Assert.Contains("HasVerifiedProviderEmailAsync", notice, StringComparison.Ordinal);
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
