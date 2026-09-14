namespace Ludeka.Application.Contracts;

/// <summary>
/// Proporciona resolución y enriquecimiento contextual de enlaces de compra con parámetros de afiliación privados.
/// </summary>
public interface IAffiliateUrlResolver
{
    /// <summary>
    /// Transforma una URL de producto o tienda incorporando los parámetros de afiliación correspondientes
    /// sin alterar la URL original si no coincide con ninguna regla configurada.
    /// </summary>
    /// <param name="rawUrl">URL original del producto en la tienda.</param>
    /// <param name="storeName">Nombre opcional de la tienda para optimizar la coincidencia.</param>
    /// <returns>URL normalizada y enriquecida con los parámetros de afiliado.</returns>
    string ResolveAffiliateUrl(string rawUrl, string? storeName = null);
}
