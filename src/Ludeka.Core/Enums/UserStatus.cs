namespace Ludeka.Core.Enums;

/// <summary>
/// Estado de actividad de una cuenta de usuario en Ludeka.
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// Cuenta activa y operativa.
    /// </summary>
    Active,

    /// <summary>
    /// Cuenta suspendida. Se inhabilitan inmediatamente todos los privilegios y accesos de moderación.
    /// </summary>
    Suspended = 1,

    /// <summary>
    /// Cuenta dada de baja con datos personales anonimizados de forma irreversible (RGPD art. 17).
    /// </summary>
    Deleted = 2
}
