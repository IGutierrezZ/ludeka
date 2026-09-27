using System;
using System.Collections.Generic;
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
/// Bifurcación de vinculación en <see cref="ExternalLoginEvents.HandleTicketReceivedAsync"/>
/// (INC-49, diseño §D1). Construye un <see cref="TicketReceivedContext"/> real —el diseño exige no
/// asumir el contrato de <see cref="M:Microsoft.AspNetCore.Authentication.HandleRequestContext`1.HandleResponse"/>—
/// en lugar de simular su comportamiento.
/// </summary>
public class ExternalLoginEventsLinkBranchTests
{
    private const string ProviderName = "Google";

    private static (TicketReceivedContext Context, FakeExternalLoginService Service) BuildContext(
        string providerKey,
        string intendedUserId,
        ClaimsPrincipal sessionUser)
    {
        var scheme = new AuthenticationScheme(ProviderName, ProviderName, typeof(CookieAuthenticationHandler));
        var options = new RemoteAuthenticationOptions();

        var externalPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, providerKey)], "External"));

        var properties = new AuthenticationProperties();
        ExternalLoginIntent.MarkLink(properties, intendedUserId);

        var ticket = new AuthenticationTicket(externalPrincipal, properties, ProviderName);

        var service = new FakeExternalLoginService();
        var services = new ServiceCollection();
        services.AddSingleton<IExternalLoginService>(service);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            User = sessionUser
        };

        return (new TicketReceivedContext(httpContext, scheme, options, ticket), service);
    }

    [Fact]
    public async Task HandleTicketReceivedAsync_WhenSessionMatchesTheMarkedIntent_ShouldLinkAndRebuildThePrincipal()
    {
        // Arrange: la sesión que vuelve del proveedor es la misma que emitió la intención.
        var sessionUser = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-1")], "ludeka.session"));
        var (context, service) = BuildContext("google-sub-99", "user-1", sessionUser);

        var linkedUser = new AppUser("user-1", "Jugadora Uno", "jugadora1@ludeka.es");
        service.LinkResult = new ExternalLoginLinkResult(ExternalLoginLinkOutcome.Linked, ProviderName, linkedUser);

        // Act
        await ExternalLoginEvents.HandleTicketReceivedAsync(ProviderName, context);

        // Assert: se invoca LinkAsync con el UserId de la sesión (nunca con el providerKey).
        var call = Assert.Single(service.LinkCalls);
        Assert.Equal("user-1", call.UserId);
        Assert.Equal(ProviderName, call.Provider);
        Assert.Equal("google-sub-99", call.ProviderKey);

        // Assert: el principal se reconstruye con la identidad de Ludeka resuelta.
        Assert.Equal("user-1", context.Principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.True(context.Properties.IsPersistent);
        Assert.True(context.Properties.AllowRefresh);

        // Assert: el camino feliz no interrumpe la respuesta (eso es exclusivo de las ramas de fallo).
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task HandleTicketReceivedAsync_WhenThereIsNoSessionOnReturn_ShouldHandleResponseAndRedirectWithoutLinking()
    {
        // Arrange: la sesión que emitió la intención expiró o se cerró antes de volver del proveedor.
        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
        var (context, service) = BuildContext("google-sub-100", "user-1", anonymousUser);
        var originalPrincipal = context.Principal;

        // Act
        await ExternalLoginEvents.HandleTicketReceivedAsync(ProviderName, context);

        // Assert: se interrumpe la respuesta con la redirección exacta de D1, sin vincular nada.
        Assert.NotNull(context.Result);
        Assert.True(context.Result!.Handled);
        Assert.Equal(StatusCodes.Status302Found, context.HttpContext.Response.StatusCode);
        Assert.Equal(AccountConnectionRoutes.LoginWithLinkWithoutSession, context.HttpContext.Response.Headers.Location.ToString());
        Assert.Empty(service.LinkCalls);

        // Assert: no se firma ninguna cookie de sesión (el principal externo no se sustituye).
        Assert.Same(originalPrincipal, context.Principal);
        Assert.False(context.HttpContext.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task HandleTicketReceivedAsync_WhenTheReturningSessionBelongsToAnotherUser_ShouldHandleResponseAndRedirectWithoutLinking()
    {
        // Arrange: quien vuelve del proveedor tiene sesión, pero de una cuenta distinta a la que
        // emitió el desafío de vinculación.
        var otherSessionUser = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-2")], "ludeka.session"));
        var (context, service) = BuildContext("google-sub-101", "user-1", otherSessionUser);
        var originalPrincipal = context.Principal;

        // Act
        await ExternalLoginEvents.HandleTicketReceivedAsync(ProviderName, context);

        // Assert: se interrumpe la respuesta con la redirección exacta de D1, sin vincular nada.
        Assert.NotNull(context.Result);
        Assert.True(context.Result!.Handled);
        Assert.Equal(StatusCodes.Status302Found, context.HttpContext.Response.StatusCode);
        Assert.Equal(AccountConnectionRoutes.PageWithSessionChanged, context.HttpContext.Response.Headers.Location.ToString());
        Assert.Empty(service.LinkCalls);

        // Assert: tampoco se vincula al UserId marcado ni se firma una sesión para el nuevo usuario.
        Assert.Same(originalPrincipal, context.Principal);
        Assert.False(context.HttpContext.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task HandleTicketReceivedAsync_WhenLinkAsyncDoesNotLink_ShouldHandleResponseAndRedirectWithoutRebuildingThePrincipal()
    {
        // Arrange: la sesión coincide con la intención, pero LinkAsync rechaza el par (ya pertenece
        // a otra cuenta). El resultado NO es Linked: no debe reconstruirse el principal de sesión.
        var sessionUser = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-1")], "ludeka.session"));
        var (context, service) = BuildContext("google-sub-102", "user-1", sessionUser);
        var originalPrincipal = context.Principal;

        var requester = new AppUser("user-1", "Jugadora Uno", "jugadora1@ludeka.es");
        service.LinkResult = new ExternalLoginLinkResult(
            ExternalLoginLinkOutcome.RejectedOwnedByAnotherAccount, ProviderName, requester);

        // Act
        await ExternalLoginEvents.HandleTicketReceivedAsync(ProviderName, context);

        // Assert: se llamó a LinkAsync (a diferencia de las dos ramas anteriores), pero el resultado
        // no vinculado interrumpe la respuesta en vez de firmar sesión.
        Assert.Single(service.LinkCalls);
        Assert.NotNull(context.Result);
        Assert.True(context.Result!.Handled);
        Assert.Equal(StatusCodes.Status302Found, context.HttpContext.Response.StatusCode);
        Assert.Equal(
            AccountConnectionRoutes.PageWithResult(ExternalLoginLinkOutcome.RejectedOwnedByAnotherAccount),
            context.HttpContext.Response.Headers.Location.ToString());
        Assert.Same(originalPrincipal, context.Principal);
        Assert.False(context.HttpContext.Response.Headers.ContainsKey("Set-Cookie"));
    }

    /// <summary>
    /// Doble de prueba de <see cref="IExternalLoginService"/> para aislar la bifurcación del
    /// evento del acceso real a datos. Registra cada llamada a <see cref="LinkAsync"/> sin
    /// persistir nada.
    /// </summary>
    private sealed class FakeExternalLoginService : IExternalLoginService
    {
        public List<(string UserId, string Provider, string ProviderKey, string? Email, bool EmailVerified)> LinkCalls { get; } = new();

        public ExternalLoginLinkResult LinkResult { get; set; } = null!;

        public Task<AppUser> ResolveAsync(
            string provider, string providerKey, string? email, bool emailVerified, string? displayName,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(
                "No se esperaba ResolveAsync: estas pruebas cubren exclusivamente la bifurcación de vinculación.");

        public Task<ExternalLoginLinkResult> LinkAsync(
            string userId, string provider, string providerKey, string? email, bool emailVerified,
            CancellationToken cancellationToken = default)
        {
            LinkCalls.Add((userId, provider, providerKey, email, emailVerified));
            return Task.FromResult(LinkResult);
        }

        public Task UnlinkAsync(string userId, string provider, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("No se esperaba UnlinkAsync en estas pruebas.");
    }

    [Fact]
    public async Task HandleTicketReceivedAsync_WhenHttpContextUserIsAnonymous_ShouldAuthenticateFromCookieScheme()
    {
        // Arrange: en tiempo de ejecución real, AuthenticationMiddleware no ha poblado HttpContext.User
        // porque los IAuthenticationRequestHandler se ejecutan antes. El usuario se recupera autenticando
        // explícitamente el SessionCookieScheme.
        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
        var scheme = new AuthenticationScheme(ProviderName, ProviderName, typeof(CookieAuthenticationHandler));
        var options = new RemoteAuthenticationOptions();

        var externalPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "google-sub-200")], "External"));

        var properties = new AuthenticationProperties();
        ExternalLoginIntent.MarkLink(properties, "user-cookie-1");

        var ticket = new AuthenticationTicket(externalPrincipal, properties, ProviderName);

        var service = new FakeExternalLoginService();
        var linkedUser = new AppUser("user-cookie-1", "Jugador Cookie", "cookie@ludeka.es");
        service.LinkResult = new ExternalLoginLinkResult(ExternalLoginLinkOutcome.Linked, ProviderName, linkedUser);

        var cookieUser = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-cookie-1")], ExternalAuthenticationSchemes.SessionCookieScheme));

        var services = new ServiceCollection();
        services.AddSingleton<IExternalLoginService>(service);
        services.AddSingleton<IAuthenticationService>(new MockAuthenticationService(cookieUser));

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            User = anonymousUser
        };

        var context = new TicketReceivedContext(httpContext, scheme, options, ticket);

        // Act
        await ExternalLoginEvents.HandleTicketReceivedAsync(ProviderName, context);

        // Assert: se vinculó con éxito usando la identidad recuperada del esquema de sesión.
        var call = Assert.Single(service.LinkCalls);
        Assert.Equal("user-cookie-1", call.UserId);
        Assert.Equal("user-cookie-1", context.Principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Null(context.Result);
    }

    private sealed class MockAuthenticationService : IAuthenticationService
    {
        private readonly ClaimsPrincipal _principal;

        public MockAuthenticationService(ClaimsPrincipal principal) => _principal = principal;

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            if (string.Equals(scheme, ExternalAuthenticationSchemes.SessionCookieScheme, StringComparison.OrdinalIgnoreCase))
            {
                var ticket = new AuthenticationTicket(_principal, scheme!);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }

            return Task.FromResult(AuthenticateResult.NoResult());
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
