using System;
using Ludeka.Application.Contracts;
using Ludeka.Web.Authentication;
using Ludeka.Web.Services;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Traducción de la denegación por falta de sesión (INC-46, F4): en el circuito interactivo el
/// pipeline HTTP no se reejecuta por evento, así que la guarda de los servicios debe llevar al
/// acceso con una navegación forzada que preserve la ruta actual.
/// </summary>
public class LoginRedirectTests
{
    private sealed class RecordingNavigationManager : NavigationManager
    {
        public RecordingNavigationManager(string uri) => Initialize("https://ludeka.test/", uri);

        public string? LastTarget { get; private set; }

        public bool? LastForceLoad { get; private set; }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            LastTarget = uri;
            LastForceLoad = forceLoad;
        }
    }

    private static RecordingNavigationManager At(string uri) => new(uri);

    [Fact]
    public void TryRedirectToLogin_WithSessionDenial_NavigatesToLoginPreservingTheCurrentRoute()
    {
        var navigation = At("https://ludeka.test/juegos/catan?tab=reglas");

        var redirected = navigation.TryRedirectToLogin(new UnauthorizedAccessException(SessionIdentity.SessionRequiredMessage));

        Assert.True(redirected);
        Assert.Equal("/login?ReturnUrl=%2Fjuegos%2Fcatan%3Ftab%3Dreglas", navigation.LastTarget);
        Assert.True(navigation.LastForceLoad);
    }

    [Fact]
    public void TryRedirectToLogin_WithAnyOtherFailure_DoesNotNavigate()
    {
        var navigation = At("https://ludeka.test/juegos/catan");

        var redirected = navigation.TryRedirectToLogin(new InvalidOperationException("otro fallo"));

        Assert.False(redirected);
        Assert.Null(navigation.LastTarget);
    }

    [Fact]
    public void TryRedirectToLogin_WithoutException_DoesNotNavigate()
    {
        var navigation = At("https://ludeka.test/juegos/catan");

        Assert.False(navigation.TryRedirectToLogin(null));
        Assert.Null(navigation.LastTarget);
    }

    [Fact]
    public void ToLogin_FromTheLoginPage_DoesNotChainARedirectLoop()
    {
        var navigation = At("https://ludeka.test/login?ReturnUrl=%2Fmi-ludoteca");

        navigation.ToLogin();

        Assert.Null(navigation.LastTarget);
    }

    [Fact]
    public void ToLogin_FromTheLibraryRoot_PreservesThePathInTheReturnUrl()
    {
        var navigation = At("https://ludeka.test/mi-ludoteca");

        navigation.ToLogin();

        Assert.Equal("/login?ReturnUrl=%2Fmi-ludoteca", navigation.LastTarget);
        Assert.True(navigation.LastForceLoad);
    }

    [Fact]
    public void IsSessionDenied_RecognizesOnlyTheControlledDenial()
    {
        Assert.True(LoginRedirect.IsSessionDenied(new UnauthorizedAccessException(SessionIdentity.SessionRequiredMessage)));
        Assert.False(LoginRedirect.IsSessionDenied(new ArgumentException("identidad vacía")));
    }
}
