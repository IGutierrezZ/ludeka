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

    private static async Task HandleTicketReceivedAsync(string providerName, TicketReceivedContext context)
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
}
