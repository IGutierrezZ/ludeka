using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace Ludeka.Web;

/// <summary>
/// Construcción de las <see cref="ForwardedHeadersOptions"/> de <c>Ludeka.Web</c> (INC-52,
/// especificación <c>reverse-proxy-forwarded-headers</c>, diseño D2-D5): el proceso corre
/// siempre detrás de un proxy inverso de salto único que termina TLS (mapeo de dominio directo
/// en Cloud Run; Nginx en el despliegue VPS), y sin este middleware <c>Request.Scheme</c> se
/// queda en <c>http</c> aunque llegue la cabecera <c>X-Forwarded-Proto: https</c>. Función pura,
/// sin dependencias de arranque: mismo patrón que <see cref="WebStartupGuards.Evaluate"/> y
/// <see cref="MediaStorageWarnings.GetConfigurationWarnings"/>.
/// </summary>
public static class ForwardedHeadersConfiguration
{
    /// <summary>Devuelve las opciones de reenvío de cabeceras para la tubería HTTP de
    /// producción. Solo procesa <see cref="ForwardedHeaders.XForwardedProto"/> (D2): ni
    /// <c>XForwardedHost</c> (suplantación de host, que ninguna capa detendría) ni
    /// <c>XForwardedFor</c> (nadie lee la IP remota hoy). Las listas de confianza se vacían
    /// explícitamente (D4) porque el origen del proxy (Cloud Run) no es una IP conocida de
    /// antemano, y <see cref="ForwardedHeadersOptions.ForwardLimit"/> se fija en 1 (D5) por el
    /// salto único del mapeo de dominio directo.</summary>
    public static ForwardedHeadersOptions Build()
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1,
        };

        // Propiedad vigente en .NET 10; KnownNetworks está obsoleta ("Obsolete, please use
        // KnownIPNetworks instead").
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        return options;
    }
}
