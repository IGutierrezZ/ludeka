using System.IO;

namespace Ludeka.Application.Options;

/// <summary>
/// Configuración del respaldo de medios en disco local (INC-48, diseño D1). Cuando no hay
/// credenciales válidas de Cloudflare R2 pero hay una ruta local configurada, el almacenamiento de
/// medios se sirve desde disco físico en vez de memoria volátil.
/// </summary>
public class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>
    /// Ruta local (absoluta o relativa a <c>ContentRootPath</c>) donde se guardan los medios cuando
    /// no hay credenciales válidas de Cloudflare R2. Vacía por defecto (sin respaldo en disco).
    /// Clave de entorno: <c>Media__LocalStoragePath</c>.
    /// </summary>
    public string LocalStoragePath { get; set; } = string.Empty;

    /// <summary>
    /// Indica si hay una ruta local de almacenamiento de medios configurada de forma no vacía.
    /// </summary>
    public bool HasLocalStoragePath => !string.IsNullOrWhiteSpace(LocalStoragePath);

    /// <summary>
    /// Resuelve <see cref="LocalStoragePath"/> contra <paramref name="contentRootPath"/> cuando es
    /// relativa. Único punto de esta regla (INC-48, PR2, tarea 3.9): antes vivía duplicada, sin
    /// ninguna prueba que las cruzara, en <c>LudekaServiceCollectionExtensions</c>
    /// (Ludeka.Infrastructure, ruta de escritura) y en <c>MediaStaticFilesExtensions</c>
    /// (Ludeka.Web, ruta de lectura servida por HTTP); ambos sitios llaman ahora a este método.
    /// </summary>
    /// <param name="contentRootPath">Normalmente <c>IHostEnvironment.ContentRootPath</c>. Se recibe
    /// como <see langword="string"/>, no como <c>IHostEnvironment</c>, para no introducir una
    /// dependencia de hosting en <c>Ludeka.Application</c>.</param>
    /// <returns><see langword="null"/> si no hay ninguna ruta local configurada
    /// (<see cref="HasLocalStoragePath"/> es <see langword="false"/>); <see cref="LocalStoragePath"/>
    /// tal cual si es absoluta o si <paramref name="contentRootPath"/> es nulo o vacío; en caso
    /// contrario, <see cref="LocalStoragePath"/> combinada con <paramref name="contentRootPath"/>.</returns>
    public string? ResolveLocalStoragePath(string? contentRootPath)
    {
        if (!HasLocalStoragePath)
        {
            return null;
        }

        if (Path.IsPathRooted(LocalStoragePath) || string.IsNullOrWhiteSpace(contentRootPath))
        {
            return LocalStoragePath;
        }

        return Path.Combine(contentRootPath, LocalStoragePath);
    }
}
