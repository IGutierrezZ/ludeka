using System;
using System.Collections.Generic;
using AspNet.Security.OAuth.Discord;
using Ludeka.Application.Features.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AuthenticationOptions = Ludeka.Application.Features.Identity.AuthenticationOptions;

namespace Ludeka.Web.Authentication;

/// <summary>
/// Registro dirigido por configuración (INC-46) de la cookie de sesión propia de Ludeka y de los
/// esquemas sociales de Google, Discord y Facebook. Un proveedor solo se registra si está
/// habilitado y tiene credenciales; en caso contrario se emite un aviso y la aplicación arranca igual.
/// </summary>
public static class ExternalAuthenticationSchemes
{
    /// <summary>Esquema de la cookie de sesión propia de Ludeka.</summary>
    public const string SessionCookieScheme = CookieAuthenticationDefaults.AuthenticationScheme;

    /// <summary>Nombre de la cookie de sesión emitida tras un acceso social correcto.</summary>
    public const string SessionCookieName = "ludeka.session";

    /// <summary>Ruta de inicio de sesión.</summary>
    public const string LoginPath = "/login";

    /// <summary>Ruta de cierre de sesión.</summary>
    public const string LogoutPath = "/logout";

    public const string GoogleCallbackPath = "/signin-google";
    public const string DiscordCallbackPath = "/signin-discord";
    public const string FacebookCallbackPath = "/signin-facebook";

    /// <summary>
    /// Registra la cookie de sesión, los esquemas sociales utilizables, el estado de autenticación
    /// en cascada para Blazor y los servicios de autorización.
    /// </summary>
    public static AuthenticationBuilder AddLudekaAuthentication(this IServiceCollection services, AuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        var builder = services.AddAuthentication(SessionCookieScheme);

        builder.AddCookie(SessionCookieScheme, cookie =>
        {
            cookie.Cookie.Name = SessionCookieName;
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            cookie.Cookie.SameSite = SameSiteMode.Lax;
            cookie.SlidingExpiration = true;
            cookie.ExpireTimeSpan = TimeSpan.FromMinutes(options.Cookie.EffectiveExpireMinutes);
            cookie.LoginPath = new PathString(LoginPath);
            cookie.LogoutPath = new PathString(LogoutPath);
            cookie.AccessDeniedPath = new PathString(LoginPath);
        });

        foreach (var registration in GetEnabledProviders(options))
        {
            RegisterProvider(builder, registration);
        }

        services.AddAuthorization(AuthorizationPolicies.Configure);
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddCascadingAuthenticationState();

        return builder;
    }

    /// <summary>
    /// Proveedores que deben registrarse con la configuración actual: habilitados y con credenciales.
    /// </summary>
    public static IReadOnlyList<ExternalProviderRegistration> GetEnabledProviders(AuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var registrations = new List<ExternalProviderRegistration>();
        foreach (var name in ExternalProviderNames.All)
        {
            if (!options.Providers.TryGetValue(name, out var provider) || !provider.IsUsable)
            {
                continue;
            }

            registrations.Add(new ExternalProviderRegistration(
                name,
                SchemeFor(name),
                CallbackPathFor(name),
                provider.EffectiveClientId!,
                provider.EffectiveClientSecret!));
        }

        return registrations;
    }

    /// <summary>
    /// Avisos de configuración para proveedores habilitados sin credenciales. La aplicación
    /// arranca igual, pero el esquema no se registra.
    /// </summary>
    public static IReadOnlyList<string> GetConfigurationWarnings(AuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var warnings = new List<string>();
        foreach (var name in ExternalProviderNames.All)
        {
            if (!options.Providers.TryGetValue(name, out var provider) || !provider.Enabled || provider.HasCredentials)
            {
                continue;
            }

            warnings.Add(
                $"El proveedor de autenticación '{name}' está habilitado pero no tiene ClientId/ClientSecret configurados; " +
                "su esquema no se registrará y su botón no aparecerá en la pantalla de acceso.");
        }

        return warnings;
    }

    /// <summary>
    /// Aviso agregado de "cero proveedores utilizables" (INC-52, PR5, diseño D10), hermano de
    /// <see cref="GetConfigurationWarnings"/> — no lo modifica ni reutiliza su lista, porque su
    /// tipo de retorno está fijado por una prueba vigente. Devuelve <see langword="null"/> si algún
    /// proveedor cumple <see cref="ExternalProviderOptions.IsUsable"/>; si no, un aviso cuya
    /// severidad depende del entorno: en Production nadie puede iniciar sesión, incluido el
    /// Administrador Fundador, así que se registra como <see cref="LogLevel.Error"/>. Fuera de
    /// Production se registra como <see cref="LogLevel.Warning"/>.
    /// </summary>
    public static AuthenticationStartupNotice? GetNoUsableProviderNotice(AuthenticationOptions options, string? environmentName)
    {
        ArgumentNullException.ThrowIfNull(options);

        foreach (var name in ExternalProviderNames.All)
        {
            if (options.Providers.TryGetValue(name, out var provider) && provider.IsUsable)
            {
                return null;
            }
        }

        var isProduction = string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
        var level = isProduction ? LogLevel.Error : LogLevel.Warning;
        var message =
            "Ningún proveedor de autenticación social está operativo: Google, Discord y Facebook están " +
            "deshabilitados o sin credenciales completas. Nadie puede iniciar sesión, incluido el " +
            "Administrador Fundador. Revisa Authentication:Providers:{Proveedor}:Enabled y su " +
            "ClientId/ClientSecret (AppId/AppSecret en Facebook).";

        return new AuthenticationStartupNotice(level, message);
    }

    /// <summary>Esquema de autenticación de un proveedor soportado.</summary>
    public static string SchemeFor(string providerName) => providerName switch
    {
        ExternalProviderNames.Google => GoogleDefaults.AuthenticationScheme,
        ExternalProviderNames.Discord => DiscordAuthenticationDefaults.AuthenticationScheme,
        ExternalProviderNames.Facebook => FacebookDefaults.AuthenticationScheme,
        _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, "Proveedor de autenticación no soportado.")
    };

    /// <summary>Ruta de callback registrada en el proveedor para un esquema soportado.</summary>
    public static string CallbackPathFor(string providerName) => providerName switch
    {
        ExternalProviderNames.Google => GoogleCallbackPath,
        ExternalProviderNames.Discord => DiscordCallbackPath,
        ExternalProviderNames.Facebook => FacebookCallbackPath,
        _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, "Proveedor de autenticación no soportado.")
    };

    /// <summary>Nombre visible del proveedor en la pantalla de acceso.</summary>
    public static string DisplayNameFor(string providerName) => providerName switch
    {
        ExternalProviderNames.Google => "Google",
        ExternalProviderNames.Discord => "Discord",
        ExternalProviderNames.Facebook => "Facebook",
        _ => providerName
    };

    private static void RegisterProvider(AuthenticationBuilder builder, ExternalProviderRegistration registration)
    {
        switch (registration.Name)
        {
            case ExternalProviderNames.Google:
                builder.AddGoogle(GoogleDefaults.AuthenticationScheme, google =>
                {
                    google.ClientId = registration.ClientId;
                    google.ClientSecret = registration.ClientSecret;
                    google.CallbackPath = registration.CallbackPath;
                    google.SignInScheme = SessionCookieScheme;
                    AddScope(google.Scope, "email");
                    AddScope(google.Scope, "profile");
                    google.ClaimActions.MapJsonKey(ExternalLoginEvents.EmailVerifiedClaim, "email_verified");
                    google.ConfigureExternalLogin(registration.Name);
                });
                break;

            case ExternalProviderNames.Discord:
                builder.AddDiscord(DiscordAuthenticationDefaults.AuthenticationScheme, discord =>
                {
                    discord.ClientId = registration.ClientId;
                    discord.ClientSecret = registration.ClientSecret;
                    discord.CallbackPath = registration.CallbackPath;
                    discord.SignInScheme = SessionCookieScheme;
                    AddScope(discord.Scope, "identify");
                    AddScope(discord.Scope, "email");
                    discord.ClaimActions.MapJsonKey(ExternalLoginEvents.EmailVerifiedClaim, "verified");
                    discord.ConfigureExternalLogin(registration.Name);
                });
                break;

            case ExternalProviderNames.Facebook:
                builder.AddFacebook(FacebookDefaults.AuthenticationScheme, facebook =>
                {
                    facebook.AppId = registration.ClientId;
                    facebook.AppSecret = registration.ClientSecret;
                    facebook.CallbackPath = registration.CallbackPath;
                    facebook.SignInScheme = SessionCookieScheme;
                    AddScope(facebook.Scope, "email");
                    if (!facebook.Fields.Contains("email")) facebook.Fields.Add("email");
                    if (!facebook.Fields.Contains("name")) facebook.Fields.Add("name");
                    facebook.ClaimActions.MapJsonKey(ExternalLoginEvents.EmailVerifiedClaim, "verified");
                    facebook.ConfigureExternalLogin(registration.Name);
                });
                break;
        }
    }

    private static void AddScope(ICollection<string> scopes, string scope)
    {
        foreach (var existing in scopes)
        {
            if (string.Equals(existing, scope, StringComparison.OrdinalIgnoreCase)) return;
        }

        scopes.Add(scope);
    }
}

/// <summary>Proveedor social listo para registrarse como esquema de autenticación.</summary>
/// <param name="Name">Nombre canónico del proveedor.</param>
/// <param name="Scheme">Nombre del esquema de autenticación.</param>
/// <param name="CallbackPath">Ruta de callback registrada en el proveedor.</param>
/// <param name="ClientId">Identificador de cliente OAuth.</param>
/// <param name="ClientSecret">Secreto de cliente OAuth.</param>
public sealed record ExternalProviderRegistration(
    string Name,
    string Scheme,
    string CallbackPath,
    string ClientId,
    string ClientSecret);

/// <summary>Aviso de arranque sobre el estado agregado de los proveedores de autenticación social
/// (INC-52, PR5, diseño D10).</summary>
/// <param name="Level"><see cref="LogLevel.Error"/> en Production, <see cref="LogLevel.Warning"/> en cualquier otro entorno.</param>
/// <param name="Message">Mensaje explicativo, listo para registrarse.</param>
public sealed record AuthenticationStartupNotice(LogLevel Level, string Message);
