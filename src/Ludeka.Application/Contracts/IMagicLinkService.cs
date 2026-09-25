using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Features.Identity;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Servicio para solicitud, emisión y consumo atómico de enlaces mágicos de acceso por correo.
/// </summary>
public interface IMagicLinkService
{
    /// <summary>
    /// Genera un token efímero de alta entropía, persiste su hash SHA-256 y envía el correo con el enlace de acceso.
    /// </summary>
    Task<MagicLinkRequestResult> RequestMagicLinkAsync(
        string email,
        string? returnUrl = null,
        string? targetUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica y consume atómicamente un token recibido por parámetro de URL, resolviendo o aprovisionando el usuario.
    /// </summary>
    Task<MagicLinkVerifyResult> VerifyAndConsumeAsync(
        string rawToken,
        CancellationToken cancellationToken = default);
}
