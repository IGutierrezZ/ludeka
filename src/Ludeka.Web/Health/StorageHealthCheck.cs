using Ludeka.Application.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Ludeka.Web.Health;

/// <summary>
/// Salud observable del almacén de medios REALMENTE seleccionado (INC-48, PR3, diseño D4), con
/// la misma precedencia de tres vías que D1 (<see cref="MediaOptions.ResolveLocalStoragePath"/>):
/// Cloudflare R2 con credenciales válidas, disco local configurado, o memoria volátil. Sustituye
/// a la versión anterior, que extraía un directorio con una expresión regular sobre
/// <c>Data Source=</c> de la cadena de conexión de la BASE DE DATOS — sin relación alguna con el
/// almacenamiento de medios.
/// </summary>
public class StorageHealthCheck : IHealthCheck
{
    private readonly CloudflareR2Options _r2Options;
    private readonly MediaOptions _mediaOptions;
    private readonly IHostEnvironment? _environment;

    public StorageHealthCheck(
        IOptions<CloudflareR2Options> r2Options,
        IOptions<MediaOptions> mediaOptions,
        IHostEnvironment? environment = null)
    {
        _r2Options = r2Options.Value;
        _mediaOptions = mediaOptions.Value;
        _environment = environment;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // INC-48 (PR3), diseño D4: sonda la vía de medios REALMENTE seleccionada, con la misma
        // precedencia de tres vías que D1 (LudekaServiceCollectionExtensions), en vez de parsear
        // "Data Source=" de la cadena de conexión de la base de datos, sin relación con los medios.
        if (_r2Options.HasValidCredentials)
        {
            var data = new Dictionary<string, object>
            {
                { "mode", "r2" },
                { "bucket", _r2Options.BucketName }
            };

            return Task.FromResult(HealthCheckResult.Healthy("Almacenamiento de medios configurado en Cloudflare R2.", data));
        }

        var localPath = _mediaOptions.ResolveLocalStoragePath(_environment?.ContentRootPath);
        if (localPath is not null)
        {
            return Task.FromResult(ProbeLocalStorage(localPath));
        }

        var degradedData = new Dictionary<string, object> { { "mode", "memory" } };
        return Task.FromResult(HealthCheckResult.Degraded(
            "El almacén de medios es volátil: no hay Cloudflare R2 ni ruta local configurados.",
            data: degradedData));
    }

    private static HealthCheckResult ProbeLocalStorage(string targetDirectory)
    {
        try
        {
            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // Probar escritura y lectura temporal para certificar permisos de volumen Docker
            var probeFile = Path.Combine(targetDirectory, $".healthcheck_probe_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probeFile, "ludeka-health-probe");
            var content = File.ReadAllText(probeFile);
            File.Delete(probeFile);

            if (content != "ludeka-health-probe")
            {
                return HealthCheckResult.Unhealthy("Error de verificación en la sonda de almacenamiento: contenido inconsistente.");
            }

            var data = new Dictionary<string, object>
            {
                { "mode", "disk" },
                { "directory", targetDirectory },
                { "writable", true }
            };

            return HealthCheckResult.Healthy("Almacenamiento de medios accesible en disco con permisos de lectura y escritura.", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Error de permisos o acceso al sistema de archivos de medios.", ex);
        }
    }
}
