using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using Ludeka.Application.Features.Identity;
using Ludeka.Core.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Ludeka.Web.Authentication;

/// <summary>
/// Eventos de los esquemas sociales (INC-46 F2): extraen únicamente los claims del proveedor,
/// delegan la vinculación en <see cref="IExternalLoginService"/> y firman la cookie de sesión
/// de Ludeka con la identidad resuelta.
/// </summary>
public static class ExternalLoginEvents
{
    /// <summary>Claim con la marca de correo verificado del proveedor.</summary>
    public const string EmailVerifiedClaim = "ludeka:email_verified";

    /// <summary>Claim con la máscara de permisos como snapshot de interfaz (nunca decide autorización).</summary>
    public const string PermissionsClaim = "ludeka:permissions";

    /// <summary>
    /// Suscribe el evento de ticket recibido de un esquema externo. El handler del proveedor
    /// firma después la cookie de sesión con el principal devuelto.
    /// </summary>
    public static void ConfigureExternalLogin(this RemoteAuthenticationOptions options, string providerName)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Events.OnTicketReceived = context => HandleTicketReceivedAsync(providerName, context);
    }

    public static async Task HandleTicketReceivedAsync(string providerName, TicketReceivedContext context)
    {
        var externalPrincipal = context.Principal;
        if (externalPrincipal is null)
        {
            context.Fail("El proveedor no entregó una identidad utilizable.");
            return;
        }

        var providerKey = externalPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? externalPrincipal.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(providerKey))
        {
            context.Fail("El proveedor no entregó un identificador de usuario utilizable.");
            return;
        }

        var email = FindEmail(externalPrincipal);
        var displayName = externalPrincipal.FindFirstValue(ClaimTypes.Name) ?? externalPrincipal.FindFirstValue("name");
        var emailVerified = IsEmailVerified(externalPrincipal);

        var loginService = context.HttpContext.RequestServices.GetRequiredService<IExternalLoginService>();

        // Incremento 49: bifurcación de vinculación. Si el desafío marcó la intención en Items, esta
        // rama sustituye por completo al camino de acceso: nunca aprovisiona un AppUser nuevo (diseño §D1).
        if (ExternalLoginIntent.TryReadLink(context.Properties, out var intendedUserId))
        {
            var sessionUserId = await ResolveSessionUserIdAsync(context);

            // Reconfirmación (diseño §D1): la sesión que vuelve del proveedor debe ser exactamente
            // la misma que emitió la intención. Sin esto, la comprobación del paso 2 (userId capturado
            // en servidor al emitir el desafío) quedaría sin efecto en el retorno.
            if (!string.Equals(intendedUserId, sessionUserId, StringComparison.OrdinalIgnoreCase))
            {
                context.HandleResponse();
                context.Response.Redirect(string.IsNullOrWhiteSpace(sessionUserId)
                    ? AccountConnectionRoutes.LoginWithLinkWithoutSession
                    : AccountConnectionRoutes.PageWithSessionChanged);
                return;
            }

            var linkResult = await loginService.LinkAsync(
                intendedUserId, providerName, providerKey, email, emailVerified, context.HttpContext.RequestAborted);

            if (linkResult.Outcome != ExternalLoginLinkOutcome.Linked)
            {
                context.HandleResponse();
                context.Response.Redirect(AccountConnectionRoutes.PageWithResult(linkResult.Outcome));
                return;
            }

            context.Principal = BuildSessionPrincipal(linkResult.User);
            context.Properties ??= new AuthenticationProperties();
            context.Properties.IsPersistent = true;
            context.Properties.AllowRefresh = true;
            context.ReturnUri = AccountConnectionRoutes.PageWithResult(linkResult.Outcome);
            return;
        }

        try
        {
            var user = await loginService.ResolveAsync(
                providerName,
                providerKey,
                email,
                emailVerified,
                displayName,
                context.HttpContext.RequestAborted);

            context.Principal = BuildSessionPrincipal(user);
            context.Properties ??= new AuthenticationProperties();
            context.Properties.IsPersistent = true;
            context.Properties.AllowRefresh = true;
        }
        catch (ExternalLoginCollisionException)
        {
            // INC-49, partición 2a/2b de ResolveAsync (diseño §2.2/§3.1): el correo verificado
            // coincide con una cuenta que ya tiene otro proveedor vinculado. Nunca se fusiona en
            // silencio: se informa del conflicto y se dirige a iniciar sesión con el método ya
            // usado. Distinto del rechazo de vinculación (bifurcación de arriba): aquí SÍ existe
            // la salida hacia Ajustes → Conexiones.
            context.HandleResponse();
            context.Response.Redirect(AccountConnectionRoutes.LoginWithAccountCollision);
        }
    }

    /// <summary>Construye el principal de la cookie de sesión propia de Ludeka.</summary>
    public static ClaimsPrincipal BuildSessionPrincipal(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(PermissionsClaim, ((int)user.Permissions).ToString(CultureInfo.InvariantCulture))
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, ExternalAuthenticationSchemes.SessionCookieScheme));
    }

    private static string? FindEmail(ClaimsPrincipal? principal)
    {
        if (principal is null) return null;

        return principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email")
            ?? principal.FindFirstValue("urn:discord:user:email")
            ?? principal.FindFirstValue("urn:google:email");
    }

    private static bool IsEmailVerified(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(EmailVerifiedClaim);
        return claim is not null && bool.TryParse(claim.Value, out var verified) && verified;
    }

    private static async Task<string?> ResolveSessionUserIdAsync(TicketReceivedContext context)
    {
        var directUser = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(directUser))
        {
            return directUser;
        }

        var authService = context.HttpContext.RequestServices.GetService<IAuthenticationService>();
        if (authService is not null)
        {
            var authResult = await context.HttpContext.AuthenticateAsync(ExternalAuthenticationSchemes.SessionCookieScheme);
            if (authResult.Succeeded && authResult.Principal is not null)
            {
                return authResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            }
        }

        return null;
    }
}
