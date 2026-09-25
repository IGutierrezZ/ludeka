using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Repositorio de tokens efímeros para acceso por Magic Link.
/// </summary>
public interface IMagicLinkTokenRepository
{
    /// <summary>
    /// Persiste un nuevo token de acceso por correo.
    /// </summary>
    Task AddAsync(MagicLinkToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un token a partir de su hash SHA-256 verificando que no esté consumido y que no haya expirado en <paramref name="nowUtc"/>.
    /// </summary>
    Task<MagicLinkToken?> GetValidByTokenHashAsync(string tokenHash, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un token por su identificador único.
    /// </summary>
    Task<MagicLinkToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza el estado del token (consumo atómico).
    /// </summary>
    Task UpdateAsync(MagicLinkToken token, CancellationToken cancellationToken = default);
}
