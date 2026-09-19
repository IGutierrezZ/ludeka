using Ludeka.Application.Options;

namespace Ludeka.Web;

/// <summary>
/// Aviso explícito de almacén de medios degradado (INC-48, spec `media-storage-precedence`,
/// requisito 3). En <c>Production</c> sin credenciales válidas de Cloudflare R2, el almacén activo
/// (disco local o memoria) puede no sobrevivir a un reinicio del proceso; la aplicación arranca
/// igual, pero el operador debe verlo en los logs de arranque. Mismo patrón que
/// <see cref="Ludeka.Web.Authentication.ExternalAuthenticationSchemes.GetConfigurationWarnings"/>:
/// función estática pura, sin dependencias de arranque.
/// </summary>
public static class MediaStorageWarnings
{
    public static IReadOnlyList<string> GetConfigurationWarnings(CloudflareR2Options options, string? environmentName)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.HasValidCredentials || !string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return new[]
        {
            "El almacén de medios activo está degradado: no hay credenciales válidas de Cloudflare R2 " +
            "configuradas en Production; las imágenes se sirven desde disco local o memoria y pueden no " +
            "sobrevivir a un reinicio del proceso."
        };
    }
}
