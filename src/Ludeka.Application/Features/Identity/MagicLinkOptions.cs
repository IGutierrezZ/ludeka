namespace Ludeka.Application.Features.Identity;

/// <summary>
/// Opciones de configuración para el acceso por Magic Link.
/// </summary>
public class MagicLinkOptions
{
    public const string SectionName = "Authentication:MagicLink";

    /// <summary>
    /// Minutos de validez del enlace mágico antes de expirar (por defecto: 15 minutos).
    /// </summary>
    public int TokenLifetimeMinutes { get; set; } = 15;

    /// <summary>
    /// Dirección de correo del remitente.
    /// </summary>
    public string SenderEmail { get; set; } = "hola@ludeka.es";

    /// <summary>
    /// Nombre visible del remitente.
    /// </summary>
    public string SenderName { get; set; } = "Ludeka";

    /// <summary>
    /// URL base para componer el enlace si no se deriva de la petición HTTP.
    /// </summary>
    public string? BaseUrl { get; set; }
}
