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
}
