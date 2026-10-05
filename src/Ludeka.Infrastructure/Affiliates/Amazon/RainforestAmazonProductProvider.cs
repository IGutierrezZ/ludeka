using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Infrastructure.Affiliates.Amazon;

/// <summary>
/// Proveedor puente que consulta la API de Rainforest para resolver identificadores y precios de Amazon España.
/// </summary>
public class RainforestAmazonProductProvider : IAmazonProductProvider
{
    private readonly HttpClient _httpClient;
    private readonly AmazonOptions _options;
    private readonly ILogger<RainforestAmazonProductProvider> _logger;

    public RainforestAmazonProductProvider(
        HttpClient httpClient,
        IOptions<AmazonOptions> options,
        ILogger<RainforestAmazonProductProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? new AmazonOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> LookupAsinByEanAsync(string ean, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ean)) return null;

        var apiKey = _options.Bridge.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Rainforest API Key no configurada. Omitiendo búsqueda de ASIN para EAN {Ean}.", ean);
            return null;
        }

        try
        {
            var baseUrl = string.IsNullOrWhiteSpace(_options.Bridge.BaseUrl)
                ? "https://api.rainforestapi.com"
                : _options.Bridge.BaseUrl.TrimEnd('/');

            var domain = string.IsNullOrWhiteSpace(_options.Bridge.AmazonDomain)
                ? "amazon.es"
                : _options.Bridge.AmazonDomain;

            var requestUrl = $"{baseUrl}/request?api_key={Uri.EscapeDataString(apiKey)}&type=search&search_term={Uri.EscapeDataString(ean.Trim())}&amazon_domain={Uri.EscapeDataString(domain)}";

            using var response = await _httpClient.GetAsync(requestUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Rainforest API devolvió HTTP {StatusCode} al buscar EAN {Ean}.", response.StatusCode, ean);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            if (root.TryGetProperty("search_results", out var searchResults) &&
                searchResults.ValueKind == JsonValueKind.Array &&
                searchResults.GetArrayLength() > 0)
            {
                var first = searchResults[0];
                if (first.TryGetProperty("asin", out var asinProp) && !string.IsNullOrWhiteSpace(asinProp.GetString()))
                {
                    return asinProp.GetString()!.Trim().ToUpperInvariant();
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            _logger.LogError(ex, "Error al consultar Rainforest API para resolver EAN {Ean}.", ean);
            return null;
        }
    }

    public async Task<AmazonProductPriceResult?> GetPriceAndStockAsync(string asin, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(asin)) return null;

        var apiKey = _options.Bridge.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Rainforest API Key no configurada. Omitiendo consulta de precio para ASIN {Asin}.", asin);
            return null;
        }

        try
        {
            var baseUrl = string.IsNullOrWhiteSpace(_options.Bridge.BaseUrl)
                ? "https://api.rainforestapi.com"
                : _options.Bridge.BaseUrl.TrimEnd('/');

            var domain = string.IsNullOrWhiteSpace(_options.Bridge.AmazonDomain)
                ? "amazon.es"
                : _options.Bridge.AmazonDomain;

            var requestUrl = $"{baseUrl}/request?api_key={Uri.EscapeDataString(apiKey)}&type=product&asin={Uri.EscapeDataString(asin.Trim())}&amazon_domain={Uri.EscapeDataString(domain)}";

            using var response = await _httpClient.GetAsync(requestUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Rainforest API devolvió HTTP {StatusCode} al consultar ASIN {Asin}.", response.StatusCode, asin);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            if (!root.TryGetProperty("product", out var product) || product.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? title = product.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
            string? productUrl = product.TryGetProperty("link", out var linkProp) ? linkProp.GetString() : null;

            decimal? price = null;
            string currency = "€";
            bool inStock = true;

            // 1. Intentar buybox_winner
            if (product.TryGetProperty("buybox_winner", out var buybox) && buybox.ValueKind == JsonValueKind.Object)
            {
                if (buybox.TryGetProperty("price", out var priceObj) && priceObj.ValueKind == JsonValueKind.Object)
                {
                    if (priceObj.TryGetProperty("value", out var valProp) && valProp.TryGetDecimal(out var val))
                    {
                        price = val;
                    }
                    if (priceObj.TryGetProperty("symbol", out var symProp) && !string.IsNullOrWhiteSpace(symProp.GetString()))
                    {
                        currency = symProp.GetString()!;
                    }
                }

                if (buybox.TryGetProperty("availability", out var availObj) && availObj.ValueKind == JsonValueKind.Object)
                {
                    if (availObj.TryGetProperty("type", out var typeProp))
                    {
                        var typeStr = typeProp.GetString();
                        inStock = !string.Equals(typeStr, "out_of_stock", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            // 2. Fallback a prices array o price directo si buybox_winner no tenía precio
            if (!price.HasValue && product.TryGetProperty("prices", out var pricesArray) && pricesArray.ValueKind == JsonValueKind.Array && pricesArray.GetArrayLength() > 0)
            {
                var firstPrice = pricesArray[0];
                if (firstPrice.TryGetProperty("value", out var pVal) && pVal.TryGetDecimal(out var val))
                {
                    price = val;
                }
            }

            if (!price.HasValue)
            {
                _logger.LogInformation("No se encontró información de precio en el producto de Rainforest para ASIN {Asin}.", asin);
                return null;
            }

            return new AmazonProductPriceResult(
                asin: asin.Trim().ToUpperInvariant(),
                price: price.Value,
                currency: currency,
                inStock: inStock,
                title: title,
                productUrl: productUrl
            );
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            _logger.LogError(ex, "Error al consultar Rainforest API para producto ASIN {Asin}.", asin);
            return null;
        }
    }
}
