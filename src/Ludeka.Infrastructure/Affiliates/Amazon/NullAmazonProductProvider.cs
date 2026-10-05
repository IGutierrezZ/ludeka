using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;

namespace Ludeka.Infrastructure.Affiliates.Amazon;

/// <summary>
/// Proveedor nulo que se utiliza cuando no hay ningún proveedor de Amazon configurado o activo.
/// </summary>
public class NullAmazonProductProvider : IAmazonProductProvider
{
    public Task<string?> LookupAsinByEanAsync(string ean, CancellationToken ct = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<AmazonProductPriceResult?> GetPriceAndStockAsync(string asin, CancellationToken ct = default)
    {
        return Task.FromResult<AmazonProductPriceResult?>(null);
    }
}
