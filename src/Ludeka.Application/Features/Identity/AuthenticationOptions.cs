using System;
using System.Collections.Generic;

namespace Ludeka.Application.Features.Identity;

/// <summary>
/// Opciones de autenticación de Ludeka (INC-46): cookie de sesión propia y proveedores sociales
/// dirigidos por configuración. Un proveedor solo se registra si está habilitado y tiene credenciales.
/// </summary>
public sealed class AuthenticationOptions
{
    /// <summary>Nombre de la sección de configuración: <c>Authentication</c>.</summary>
    public const string SectionName = "Authentication";

    /// <summary>Opciones de la cookie de sesión de Ludeka.</summary>
    public CookieSessionOptions Cookie { get; set; } = new();

    /// <summary>Proveedores sociales indexados por nombre (<c>Google</c>, <c>Discord</c>, <c>Facebook</c>).</summary>
    public Dictionary<string, ExternalProviderOptions> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Configuración de la cookie de sesión que Ludeka emite tras un acceso social correcto.
/// </summary>
public sealed class CookieSessionOptions
{
    /// <summary>Caducidad por defecto de la cookie de sesión: 30 días.</summary>
    public const int DefaultExpireMinutes = 60 * 24 * 30;

    /// <summary>Minutos de vigencia de la cookie de sesión con caducidad deslizante.</summary>
    public int ExpireMinutes { get; set; } = DefaultExpireMinutes;

    /// <summary>Caducidad efectiva, ignorando valores no positivos de configuración.</summary>
    public int EffectiveExpireMinutes => ExpireMinutes > 0 ? ExpireMinutes : DefaultExpireMinutes;
}

/// <summary>
/// Credenciales y estado de un proveedor social. Google y Discord usan <c>ClientId</c>/<c>ClientSecret</c>;
/// Facebook usa <c>AppId</c>/<c>AppSecret</c>.
/// </summary>
public sealed class ExternalProviderOptions
{
    /// <summary>Indica si el maintainer habilitó el proveedor por configuración.</summary>
    public bool Enabled { get; set; }

    /// <summary>Identificador de cliente OAuth (Google, Discord).</summary>
    public string? ClientId { get; set; }

    /// <summary>Secreto de cliente OAuth (Google, Discord).</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Identificador de aplicación (Facebook).</summary>
    public string? AppId { get; set; }

    /// <summary>Secreto de aplicación (Facebook).</summary>
    public string? AppSecret { get; set; }

    /// <summary>Identificador efectivo, admitiendo la nomenclatura de Facebook.</summary>
    public string? EffectiveClientId => FirstNonEmpty(ClientId, AppId);

    /// <summary>Secreto efectivo, admitiendo la nomenclatura de Facebook.</summary>
    public string? EffectiveClientSecret => FirstNonEmpty(ClientSecret, AppSecret);

    /// <summary>Indica si el proveedor tiene credenciales completas.</summary>
    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(EffectiveClientId) && !string.IsNullOrWhiteSpace(EffectiveClientSecret);

    /// <summary>Indica si el proveedor debe registrarse: habilitado y con credenciales.</summary>
    public bool IsUsable => Enabled && HasCredentials;

    private static string? FirstNonEmpty(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first)) return first!.Trim();
        return string.IsNullOrWhiteSpace(second) ? null : second!.Trim();
    }
}

/// <summary>
/// Nombres canónicos de los proveedores sociales soportados, alineados con la sección de configuración
/// <c>Authentication:Providers</c>.
/// </summary>
public static class ExternalProviderNames
{
    public const string Google = "Google";
    public const string Discord = "Discord";
    public const string Facebook = "Facebook";

    /// <summary>Proveedores soportados, en el orden de la pantalla de acceso.</summary>
    public static readonly IReadOnlyList<string> All = [Google, Discord, Facebook];
}
