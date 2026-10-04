using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ludeka.Application.Contracts;

public interface IAffiliateClickService
{
    /// <summary>
    /// Resuelve la URL de destino de compra para un juego y tienda, registra el clic de forma asíncrona
    /// y devuelve la URL enriquecida con parámetros de afiliado.
    /// Si no existe enlace específico pero es una tienda conocida, genera la búsqueda asistida.
    /// </summary>
    Task<string?> ResolveAndTrackRedirectAsync(
        string gameSlug,
        string storeSlug,
        string? country = null,
        CancellationToken ct = default);

    /// <summary>
    /// Registra un clic para una URL directa de tienda (validando dominio seguro) y la devuelve enriquecida.
    /// </summary>
    Task<string?> TrackDirectRedirectAsync(
        Guid? gameId,
        string? gameSlug,
        string gameTitle,
        string storeName,
        string targetUrl,
        string? country = null,
        CancellationToken ct = default);
}
