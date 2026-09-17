using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Features.Identity;
using Ludeka.Core.Entities;
using Ludeka.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Camino normal (sin intención de vinculación) de <see cref="ExternalLoginEvents.HandleTicketReceivedAsync"/>,
/// incluida la rama 2b de <c>ResolveAsync</c> que lanza <see cref="ExternalLoginCollisionException"/>
/// (INC-49, diseño §2.2/§3.1). <see cref="ExternalLoginEventsLinkBranchTests"/> se acota, por diseño,
/// exclusivamente a la bifurcación de vinculación —su doble de <c>ResolveAsync</c> lanza si se le
/// llama—, así que este camino necesita su propio doble y su propia clase. Construye un
/// <see cref="TicketReceivedContext"/> real por el mismo motivo que la prueba hermana: no asumir el
/// contrato de <c>HandleResponse()</c>.
/// </summary>
public class ExternalLoginEventsNormalLoginBranchTests
{
    private const string ProviderName = "Google";

    private static (TicketReceivedContext Context, FakeExternalLoginService Service) BuildContext()
    {
        var scheme = new AuthenticationScheme(ProviderName, ProviderName, typeof(CookieAuthenticationHandler));
        var options = new RemoteAuthenticationOptions();

        var externalPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "google-sub-200")], "External"));

        // Propiedades sin marcar (a diferencia de ExternalLoginIntent.MarkLink): TryReadLink
        // devuelve false y el evento entra en el camino normal, no en la bifurcación de vinculación.
        var properties = new AuthenticationProperties();

        var ticket = new AuthenticationTicket(externalPrincipal, properties, ProviderName);

        var service = new FakeExternalLoginService();
        var services = new ServiceCollection();
        services.AddSingleton<IExternalLoginService>(service);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };

        return (new TicketReceivedContext(httpContext, scheme, options, ticket), service);
    }

    [Fact]
    public async Task HandleTicketReceivedAsync_WhenResolveAsyncThrowsExternalLoginCollisionException_ShouldHandleResponseAndRedirectWithoutSigningASessionCookie()
    {
        // Arrange: sin intención de vinculación, el correo verificado del acceso normal coincide con
        // una cuenta que ya tiene otro proveedor vinculado (rama 2b de ResolveAsync, INC-49).
        var (context, service) = BuildContext();
        var originalPrincipal = context.Principal;
        service.ResolveException = new ExternalLoginCollisionException("El correo ya está vinculado a otra cuenta.");

        // Act
        await ExternalLoginEvents.HandleTicketReceivedAsync(ProviderName, context);

        // Assert: se interrumpe la respuesta y se redirige exactamente al aviso de colisión; nunca se
        // fusiona en silencio (conducta titular de INC-49).
        Assert.NotNull(context.Result);
        Assert.True(context.Result!.Handled);
        Assert.Equal(StatusCodes.Status302Found, context.HttpContext.Response.StatusCode);
        Assert.Equal(
            AccountConnectionRoutes.LoginWithAccountCollision,
            context.HttpContext.Response.Headers.Location.ToString());

        // Assert: no se firma ninguna cookie de sesión (el principal externo no se sustituye), mismo
        // mecanismo de aserción que usan las ramas de fallo de la vinculación.
        Assert.Same(originalPrincipal, context.Principal);
        Assert.False(context.HttpContext.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task HandleTicketReceivedAsync_WhenResolveAsyncSucceedsWithoutLinkIntent_ShouldBuildSessionPrincipalWithoutHandlingResponse()
    {
        // Arrange: sin intención de vinculación, ResolveAsync resuelve la cuenta con normalidad
        // (regresión de control: el catch de colisión no debe alterar el camino feliz).
        var (context, service) = BuildContext();
        service.ResolveResult = new AppUser("user-9", "Jugadora Nueve", "jugadora9@ludeka.es");

        // Act
        await ExternalLoginEvents.HandleTicketReceivedAsync(ProviderName, context);

        // Assert: el camino feliz no interrumpe la respuesta (eso es exclusivo de las ramas de fallo).
        Assert.Null(context.Result);

        // Assert: se firma la cookie de sesión con la identidad resuelta.
        Assert.Equal("user-9", context.Principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.True(context.Properties.IsPersistent);
        Assert.True(context.Properties.AllowRefresh);
    }

    /// <summary>
    /// Doble de prueba de <see cref="IExternalLoginService"/> para el camino normal de login. Expone
    /// <see cref="ResolveAsync"/> configurable (devuelve <see cref="ResolveResult"/> o lanza
    /// <see cref="ResolveException"/>) y nunca espera <c>LinkAsync</c>/<c>UnlinkAsync</c>, exclusivos
    /// de la bifurcación de vinculación que cubre <see cref="ExternalLoginEventsLinkBranchTests"/>.
    /// </summary>
    private sealed class FakeExternalLoginService : IExternalLoginService
    {
        public AppUser ResolveResult { get; set; } = null!;

        public ExternalLoginCollisionException? ResolveException { get; set; }

        public Task<AppUser> ResolveAsync(
            string provider, string providerKey, string? email, bool emailVerified, string? displayName,
            CancellationToken cancellationToken = default)
            => ResolveException is not null
                ? Task.FromException<AppUser>(ResolveException)
                : Task.FromResult(ResolveResult);

        public Task<ExternalLoginLinkResult> LinkAsync(
            string userId, string provider, string providerKey, string? email, bool emailVerified,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException(
                "No se esperaba LinkAsync: estas pruebas cubren exclusivamente el camino normal de login.");

        public Task UnlinkAsync(string userId, string provider, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("No se esperaba UnlinkAsync en estas pruebas.");
    }
}
