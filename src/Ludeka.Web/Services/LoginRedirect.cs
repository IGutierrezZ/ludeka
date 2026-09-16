using System;
using Ludeka.Web.Authentication;
using Microsoft.AspNetCore.Components;

namespace Ludeka.Web.Services;

/// <summary>
/// Traducción de la denegación controlada por falta de sesión (INC-46, criterio 11). El render de las
/// fichas es InteractiveServer y el pipeline HTTP no se reejecuta por evento: cuando un servicio de
/// escritura deniega por anonimia, es la guarda de la interfaz la que debe llevar al acceso con una
/// navegación forzada que preserve la ruta actual como <c>ReturnUrl</c>.
/// </summary>
public static class LoginRedirect
{
    /// <summary>
    /// Indica si la excepción es una denegación controlada por falta de sesión. Solo esas se
    /// traducen en navegación: el resto de fallos deben seguir su curso normal.
    /// </summary>
    public static bool IsSessionDenied(Exception? exception) => exception is UnauthorizedAccessException;

    /// <summary>
    /// Redirige al inicio de sesión preservando la ruta actual. Si ya se está en el acceso, no hace
    /// nada para no encadenar redirecciones.
    /// </summary>
    public static void ToLogin(this NavigationManager navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);

        string? returnUrl = ResolveReturnUrl(navigation.Uri);
        if (returnUrl is null)
        {
            return;
        }

        string target = $"{ExternalAuthenticationSchemes.LoginPath}?ReturnUrl={Uri.EscapeDataString(returnUrl)}";
        navigation.NavigateTo(target, forceLoad: true);
    }

    /// <summary>
    /// Redirige al acceso solo cuando la excepción es una denegación por falta de sesión. Devuelve
    /// <c>true</c> si la navegación se disparó.
    /// </summary>
    public static bool TryRedirectToLogin(this NavigationManager navigation, Exception? exception)
    {
        if (!IsSessionDenied(exception))
        {
            return false;
        }

        navigation.ToLogin();
        return true;
    }

    /// <summary>Ruta de la petición actual, o <c>null</c> si ya es el propio acceso.</summary>
    private static string? ResolveReturnUrl(string? uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var absolute))
        {
            return null;
        }

        if (absolute.AbsolutePath.StartsWith(ExternalAuthenticationSchemes.LoginPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return absolute.PathAndQuery;
    }
}
