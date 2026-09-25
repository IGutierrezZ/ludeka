using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Contrato de servicio para consultar y persistir las preferencias visuales y configuración de cada usuario.
/// </summary>
public interface IUserPreferenceService
{
    /// <summary>
    /// Obtiene el tema de color preferido para el usuario indicado.
    /// Si el usuario no tiene preferencia guardada, devuelve el tema por defecto ('charcoal').
    /// </summary>
    Task<string> GetUserThemeAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Guarda o actualiza el tema de color preferido para el usuario.
    /// </summary>
    Task SetUserThemeAsync(string userId, string theme, CancellationToken ct = default);

    /// <summary>
    /// Obtiene el objeto completo de preferencias del usuario.
    /// </summary>
    Task<UserPreferenceDto> GetUserPreferenceAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene el país preferido del usuario, o null si prefiere ver todo sin filtrar.
    /// </summary>
    Task<string?> GetUserCountryAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Guarda o actualiza el país preferido del usuario.
    /// </summary>
    Task SetUserCountryAsync(string userId, string? country, CancellationToken ct = default);

    /// <summary>
    /// Obtiene si el usuario ha configurado su perfil público como oculto/privado.
    /// Por defecto es false (público).
    /// </summary>
    Task<bool> IsPublicProfileHiddenAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Establece si el perfil público del usuario está oculto o visible.
    /// </summary>
    Task SetPublicProfileHiddenAsync(string userId, bool hide, CancellationToken ct = default);
}
