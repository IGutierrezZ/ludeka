namespace Ludeka.Core.Enums;

/// <summary>
/// Tipo de acción registrada en la bitácora de auditoría editorial.
/// </summary>
public enum AuditAction
{
    /// <summary>
    /// Alta o creación de una nueva entidad.
    /// </summary>
    Created,

    /// <summary>
    /// Modificación o actualización de campos de una entidad.
    /// </summary>
    Updated,

    /// <summary>
    /// Eliminación o baja de una entidad.
    /// </summary>
    Deleted,

    /// <summary>
    /// Cambio de estado operativo (ej. reporte resuelto, cuenta suspendida).
    /// </summary>
    StatusChanged,

    /// <summary>
    /// Cambio de rol de usuario (ej. comunitario a moderador).
    /// </summary>
    RoleChanged,

    /// <summary>
    /// Modificación de la máscara granular de permisos de moderación.
    /// </summary>
    PermissionsChanged,

    /// <summary>
    /// Publicación oficial en redes sociales o canales externos.
    /// </summary>
    Published,

    /// <summary>
    /// Vinculación explícita de una identidad externa con la cuenta del equipo fundador.
    /// </summary>
    LinkedFounderIdentity,

    /// <summary>
    /// Vinculación manual de un proveedor de identidad externo desde la sesión activa (INC-49).
    /// </summary>
    LinkedProvider,

    /// <summary>
    /// Desvinculación manual de un proveedor de identidad externo desde la sesión activa (INC-49).
    /// </summary>
    UnlinkedProvider
}
