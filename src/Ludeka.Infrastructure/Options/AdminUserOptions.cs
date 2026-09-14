namespace Ludeka.Infrastructure.Options;

/// <summary>
/// Opciones de configuración para el usuario Administrador Fundador inicial garantizado.
/// </summary>
public class AdminUserOptions
{
    public const string SectionName = "AdminUser";

    /// <summary>
    /// Identificador único del usuario administrador inicial.
    /// </summary>
    public string Id { get; set; } = "admin-fundador";

    /// <summary>
    /// Nombre para mostrar del usuario administrador inicial.
    /// </summary>
    public string UserName { get; set; } = "Administrador Ludeka";

    /// <summary>
    /// Correo electrónico del usuario administrador inicial.
    /// </summary>
    public string Email { get; set; } = "admin@ludeka.es";

    /// <summary>
    /// País de origen del administrador para asignación territorial.
    /// </summary>
    public string? Country { get; set; } = "España";
}
