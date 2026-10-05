using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Application.Features.Affiliates;

/// <summary>
/// Parser para endpoints públicos de catálogo de tiendas Shopify (/products.json).
/// </summary>
public class ShopifyJsonCatalogParser : IShopifyJsonCatalogParser
{
    private static readonly Regex ImageEanRegex = new(@"(?<!\d)(84\d{11}|\d{13})(?!\d)", RegexOptions.Compiled);
    private readonly IAffiliateUrlResolver? _affiliateUrlResolver;

    public ShopifyJsonCatalogParser(IAffiliateUrlResolver? affiliateUrlResolver = null)
    {
        _affiliateUrlResolver = affiliateUrlResolver;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<FeedProductItem> ParseStreamAsync(
        Stream stream,
        CancellationToken ct = default)
    {
        return ParseStreamAsync(stream, storeBaseUrl: null, ct);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<FeedProductItem> ParseStreamAsync(
        Stream stream,
        string? storeBaseUrl = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        string? cleanBase = NormalizeBaseUrl(storeBaseUrl);

        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        if (!doc.RootElement.TryGetProperty("products", out var productsElem) ||
            productsElem.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var product in productsElem.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();

            if (!product.TryGetProperty("title", out var titleElem))
                continue;

            string title = titleElem.GetString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
                continue;

            string handle = product.TryGetProperty("handle", out var handleElem)
                ? handleElem.GetString()?.Trim() ?? string.Empty
                : string.Empty;

            string productUrl = string.IsNullOrWhiteSpace(cleanBase)
                ? $"/products/{handle}"
                : $"{cleanBase}/products/{handle}";

            if (_affiliateUrlResolver != null)
            {
                productUrl = _affiliateUrlResolver.ResolveAffiliateUrl(productUrl);
            }

            // Candidato de EAN en las URLs de imágenes (común en tiendas españolas como Cuarto de Juegos)
            string? imageCandidateEan = ExtractEanFromImages(product);

            if (!product.TryGetProperty("variants", out var variantsElem) ||
                variantsElem.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var variant in variantsElem.EnumerateArray())
            {
                decimal price = 0m;
                if (variant.TryGetProperty("price", out var priceElem))
                {
                    var priceStr = priceElem.GetString();
                    if (!string.IsNullOrWhiteSpace(priceStr))
                    {
                        decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out price);
                    }
                }

                bool available = variant.TryGetProperty("available", out var availElem) && availElem.GetBoolean();

                string? sku = variant.TryGetProperty("sku", out var skuElem) ? skuElem.GetString()?.Trim() : null;
                string? barcode = variant.TryGetProperty("barcode", out var barcodeElem) ? barcodeElem.GetString()?.Trim() : null;

                string? rawBarcode = null;
                string? normalizedEan = null;

                // 1. Prioridad: campo barcode nativo si contiene EAN-13 válido
                if (!string.IsNullOrWhiteSpace(barcode))
                {
                    rawBarcode = barcode;
                    if (BarcodeValidator.TryNormalizeEan13(barcode, out var normBarcode))
                    {
                        normalizedEan = normBarcode;
                    }
                }

                // 2. Prioridad: campo SKU si coincide con un EAN-13 válido (ej. Ludus Belli)
                if (normalizedEan == null && !string.IsNullOrWhiteSpace(sku))
                {
                    if (BarcodeValidator.TryNormalizeEan13(sku, out var normSku))
                    {
                        normalizedEan = normSku;
                        rawBarcode ??= sku;
                    }
                    else if (rawBarcode == null)
                    {
                        rawBarcode = sku;
                    }
                }

                // 3. Prioridad: EAN-13 embebido en la imagen principal
                if (normalizedEan == null && !string.IsNullOrWhiteSpace(imageCandidateEan))
                {
                    normalizedEan = imageCandidateEan;
                    rawBarcode ??= imageCandidateEan;
                }

                string effectiveSku = !string.IsNullOrWhiteSpace(sku)
                    ? sku
                    : (variant.TryGetProperty("id", out var vIdElem) ? vIdElem.ToString() : handle);

                yield return new FeedProductItem(
                    Sku: effectiveSku,
                    Title: title,
                    RawBarcode: rawBarcode,
                    NormalizedEan: normalizedEan,
                    Price: price,
                    Currency: "EUR",
                    InStock: available,
                    ProductUrl: productUrl);
            }
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<FeedProductItem> ParsePaginatedAsync(
        string storeFeedUrl,
        HttpClient httpClient,
        int maxPages = 100,
        int delayBetweenPagesMs = 150,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(storeFeedUrl);
        ArgumentNullException.ThrowIfNull(httpClient);

        string cleanBase = NormalizeBaseUrl(storeFeedUrl) ?? storeFeedUrl.Trim().TrimEnd('/');

        for (int page = 1; page <= maxPages; page++)
        {
            ct.ThrowIfCancellationRequested();

            string pageUrl = $"{cleanBase}/products.json?limit=250&page={page}";

            using var request = new HttpRequestMessage(HttpMethod.Get, pageUrl);
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Ludeka/1.0");

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            }
            catch
            {
                break;
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    break;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                int itemsInPage = 0;

                await foreach (var item in ParseStreamAsync(stream, cleanBase, ct).ConfigureAwait(false))
                {
                    itemsInPage++;
                    yield return item;
                }

                if (itemsInPage == 0)
                {
                    break;
                }
            }

            if (delayBetweenPagesMs > 0)
            {
                await Task.Delay(delayBetweenPagesMs, ct).ConfigureAwait(false);
            }
        }
    }

    private static string? ExtractEanFromImages(JsonElement product)
    {
        if (!product.TryGetProperty("images", out var imagesElem) || imagesElem.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var img in imagesElem.EnumerateArray())
        {
            if (img.TryGetProperty("src", out var srcElem) && srcElem.GetString() is { Length: > 0 } src)
            {
                var match = ImageEanRegex.Match(src);
                if (match.Success && BarcodeValidator.TryNormalizeEan13(match.Value, out var normImg))
                {
                    return normImg;
                }
            }
        }

        return null;
    }

    private static string? NormalizeBaseUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return null;

        var trimmed = rawUrl.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return uri.GetLeftPart(UriPartial.Authority);
        }

        return trimmed.TrimEnd('/');
    }
}
