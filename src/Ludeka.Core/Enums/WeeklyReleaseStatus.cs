namespace Ludeka.Core.Enums;

/// <summary>
/// Estado del ciclo de vida y moderación de una novedad o lanzamiento editorial.
/// </summary>
public enum WeeklyReleaseStatus
{
    /// <summary>
    /// Aprobado y publicado en la cartelera pública de /novedades.
    /// </summary>
    Published = 1,

    /// <summary>
    /// Pendiente de moderación (requiere revisión y/o enlace de BGG sugerido por IA).
    /// </summary>
    PendingModeration = 2,

    /// <summary>
    /// Rechazado o descartado por moderación.
    /// </summary>
    Rejected = 3
}
