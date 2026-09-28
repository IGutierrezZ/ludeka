using System;
using System.Threading.Tasks;
using Ludeka.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas unitarias para <see cref="ExternalLoginEvents.HandleRemoteFailureAsync"/> y el registro
/// del evento <see cref="RemoteAuthenticationEvents.OnRemoteFailure"/> (evita 500 al cancelar OAuth).
/// </summary>
public class ExternalLoginEventsRemoteFailureTests
{
    private const string ProviderName = "Facebook";

    private static RemoteFailureContext BuildContext(AuthenticationProperties? properties, Exception? failure = null)
    {
        var scheme = new AuthenticationScheme(ProviderName, ProviderName, typeof(RemoteAuthenticationHandler<RemoteAuthenticationOptions>));
        var options = new RemoteAuthenticationOptions();
        var httpContext = new DefaultHttpContext();
        failure ??= new Exception("access_denied");

        return new RemoteFailureContext(httpContext, scheme, options, failure)
        {
            Properties = properties
        };
    }

    [Fact]
    public void ConfigureExternalLogin_ShouldRegisterBothTicketReceivedAndRemoteFailureEvents()
    {
        // Arrange
        var options = new RemoteAuthenticationOptions();

        // Act
        options.ConfigureExternalLogin(ProviderName);

        // Assert
        Assert.NotNull(options.Events.OnTicketReceived);
        Assert.NotNull(options.Events.OnRemoteFailure);
    }

    [Fact]
    public async Task HandleRemoteFailureAsync_WhenFailureOccursDuringAccountLinking_ShouldHandleResponseAndRedirectToConnectionsWithCancellationNotice()
    {
        // Arrange: desafío emitido desde la pantalla de vinculación de cuentas con intención marcada.
        var properties = new AuthenticationProperties();
        ExternalLoginIntent.MarkLink(properties, "user-42");
        var context = BuildContext(properties);

        // Act
        await ExternalLoginEvents.HandleRemoteFailureAsync(ProviderName, context);

        // Assert: la respuesta se marca como gestionada (evita AuthenticationFailureException 500).
        Assert.NotNull(context.Result);
        Assert.True(context.Result!.Handled);
        Assert.Equal(StatusCodes.Status302Found, context.HttpContext.Response.StatusCode);
        Assert.Equal(
            AccountConnectionRoutes.PageWithCancellation,
            context.HttpContext.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task HandleRemoteFailureAsync_WhenFailureOccursDuringAccountLinkingWithRedirectUriFallback_ShouldHandleResponseAndRedirectToConnections()
    {
        // Arrange: desafío que tiene RedirectUri hacia la pantalla de conexiones (fallback sin intent explícito).
        var properties = new AuthenticationProperties { RedirectUri = AccountConnectionRoutes.Page };
        var context = BuildContext(properties);

        // Act
        await ExternalLoginEvents.HandleRemoteFailureAsync(ProviderName, context);

        // Assert
        Assert.NotNull(context.Result);
        Assert.True(context.Result!.Handled);
        Assert.Equal(StatusCodes.Status302Found, context.HttpContext.Response.StatusCode);
        Assert.Equal(
            AccountConnectionRoutes.PageWithCancellation,
            context.HttpContext.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task HandleRemoteFailureAsync_WhenFailureOccursDuringNormalLogin_ShouldHandleResponseAndRedirectToLoginWithCancellationNotice()
    {
        // Arrange: desafío emitido desde el login convencional (sin intención de vinculación).
        var properties = new AuthenticationProperties { RedirectUri = "/" };
        var context = BuildContext(properties);

        // Act
        await ExternalLoginEvents.HandleRemoteFailureAsync(ProviderName, context);

        // Assert: se gestiona la respuesta y redirige al login con aviso cerrado de cancelación.
        Assert.NotNull(context.Result);
        Assert.True(context.Result!.Handled);
        Assert.Equal(StatusCodes.Status302Found, context.HttpContext.Response.StatusCode);
        Assert.Equal(
            AccountConnectionRoutes.LoginWithCancellation,
            context.HttpContext.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task HandleRemoteFailureAsync_WhenPropertiesAreNull_ShouldHandleResponseAndRedirectToLogin()
    {
        // Arrange: el proveedor no devolvió o no se pudo desencriptar el state/properties.
        var context = BuildContext(null);

        // Act
        await ExternalLoginEvents.HandleRemoteFailureAsync(ProviderName, context);

        // Assert
        Assert.NotNull(context.Result);
        Assert.True(context.Result!.Handled);
        Assert.Equal(StatusCodes.Status302Found, context.HttpContext.Response.StatusCode);
        Assert.Equal(
            AccountConnectionRoutes.LoginWithCancellation,
            context.HttpContext.Response.Headers.Location.ToString());
    }
}
