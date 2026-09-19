using System.IO;
using Ludeka.Application.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Ludeka.Web.Extensions;

/// <summary>
/// INC-48 (PR1b), diseño D2: sirve por HTTP el fallback de medios en disco físico bajo el prefijo
/// público fijo "/images". <c>MapStaticAssets()</c> (<c>Program.cs</c>) solo sirve el manifiesto de
/// activos generado en tiempo de compilación; no sirve ficheros escritos en tiempo de ejecución por
/// <c>PhysicalFileImageStorageService</c> (INC-48, PR1a), que es exactamente el vacío que este
/// middleware resuelve.
/// </summary>
public static class MediaStaticFilesExtensions
{
    /// <summary>
    /// Registra <c>UseStaticFiles</c> apuntando a <paramref name="options"/>.LocalStoragePath bajo el
    /// prefijo "/images". Devuelve <paramref name="app"/> intacta si no hay ruta local configurada:
    /// en ese caso el almacenamiento activo es Cloudflare R2 o memoria (precedencia de tres vías de
    /// PR1a), ninguno de los cuales escribe ficheros en este proceso, así que no hay nada que servir.
    /// </summary>
    public static IApplicationBuilder UseLudekaMediaFiles(
        this IApplicationBuilder app,
        MediaOptions options,
        IHostEnvironment environment)
    {
        if (!options.HasLocalStoragePath)
        {
            return app;
        }

        var mediaRoot = ResolveMediaRoot(options.LocalStoragePath, environment);

        // PhysicalFileImageStorageService ya crea el directorio al escribir (diseño D1), pero este
        // middleware se registra al arrancar el proceso, potencialmente antes de que exista ninguna
        // imagen: un despliegue en frío con la ruta configurada pero aún vacía no debe impedir que la
        // tubería se construya.
        Directory.CreateDirectory(mediaRoot);

        return app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(mediaRoot),
            RequestPath = "/images"

            // ServeUnknownFileTypes se deja en su valor por defecto (false) a propósito (matriz de
            // amenazas del diseño, fila "Exposición de rutas"): activarlo serviría cualquier
            // extensión de fichero como binario genérico bajo /images, ampliando la superficie
            // expuesta sin necesidad — las portadas ya usan extensiones conocidas (.jpg/.jpeg/.png/.webp).
        });
    }

    /// <summary>
    /// Resuelve <paramref name="localStoragePath"/> contra <see cref="IHostEnvironment.ContentRootPath"/>
    /// cuando es relativa. Debe permanecer equivalente a la resolución que ya hace
    /// <c>LudekaServiceCollectionExtensions.ResolveMediaStoragePath</c> (INC-48, PR1a) para el mismo
    /// valor de configuración: si divergieran, las imágenes se escribirían en un directorio y se
    /// intentarían servir desde otro, reproduciendo el defecto 404 que PR1a corrigió para la ruta de
    /// escritura.
    /// <para>
    /// ⚠️ NINGUNA prueba cruza hoy las dos resoluciones. <c>MediaStaticFilesDeliveryTests</c> construye
    /// <c>PhysicalFileImageStorageService</c> con una ruta ya absoluta, o escribe el fichero por su
    /// cuenta, así que nunca pasa por la resolución de PR1a. Una divergencia NO se detectaría en la
    /// suite: si tocas esta resolución, cambia también la de Ludeka.Infrastructure a mano.
    /// </para>
    /// </summary>
    private static string ResolveMediaRoot(string localStoragePath, IHostEnvironment environment)
    {
        if (Path.IsPathRooted(localStoragePath) || string.IsNullOrWhiteSpace(environment.ContentRootPath))
        {
            return localStoragePath;
        }

        return Path.Combine(environment.ContentRootPath, localStoragePath);
    }
}
