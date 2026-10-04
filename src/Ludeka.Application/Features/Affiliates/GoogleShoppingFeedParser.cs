using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Ludeka.Application.Contracts;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Application.Features.Affiliates;

/// <summary>
/// Parser en streaming de alta eficiencia para feeds en formato Google Shopping (RSS 2.0 / Atom XML).
/// Procesa ítems secuencialmente mediante XmlReader manteniendo un consumo de memoria constante O(1).
/// </summary>
public class GoogleShoppingFeedParser : IFeedParser
{
    private static readonly Regex PriceRegex = new(@"[\d\.,]+", RegexOptions.Compiled);
    private readonly IAffiliateUrlResolver? _affiliateUrlResolver;

    public GoogleShoppingFeedParser(IAffiliateUrlResolver? affiliateUrlResolver = null)
    {
        _affiliateUrlResolver = affiliateUrlResolver;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<FeedProductItem> ParseStreamAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var settings = new XmlReaderSettings
        {
            Async = true,
            IgnoreWhitespace = true,
            DtdProcessing = DtdProcessing.Prohibit,
            CloseInput = false
        };

        using var reader = XmlReader.Create(stream, settings);

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();

            if (reader.NodeType == XmlNodeType.Element &&
                (string.Equals(reader.LocalName, "item", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(reader.LocalName, "entry", StringComparison.OrdinalIgnoreCase)))
            {
                using var subtree = reader.ReadSubtree();
                var element = await XElement.LoadAsync(subtree, LoadOptions.None, ct).ConfigureAwait(false);
                var item = ParseItemElement(element);
                if (item != null)
                {
                    yield return item;
                }
            }
        }
    }

    private FeedProductItem? ParseItemElement(XElement element)
    {
        var elements = element.Elements().ToList();

        // 1. Título
        string? title = elements
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "title", StringComparison.OrdinalIgnoreCase))
            ?.Value?.Trim();

        if (string.IsNullOrWhiteSpace(title))
            return null;

        // 2. URL de producto
        string? productUrl = elements
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "link", StringComparison.OrdinalIgnoreCase))
            ?.Value?.Trim();

        if (string.IsNullOrWhiteSpace(productUrl))
        {
            // En feeds Atom, <link href="..." /> puede venir como atributo
            var linkElem = elements.FirstOrDefault(e => string.Equals(e.Name.LocalName, "link", StringComparison.OrdinalIgnoreCase));
            productUrl = linkElem?.Attribute("href")?.Value?.Trim();
        }

        if (string.IsNullOrWhiteSpace(productUrl))
            return null;

        // 3. SKU / ID
        string? sku = elements
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "id", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(e.Name.LocalName, "sku", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(e.Name.LocalName, "mpn", StringComparison.OrdinalIgnoreCase))
            ?.Value?.Trim();

        if (string.IsNullOrWhiteSpace(sku))
        {
            sku = productUrl;
        }

        // 4. Código de barras / GTIN / EAN
        string? rawBarcode = elements
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "gtin", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(e.Name.LocalName, "barcode", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(e.Name.LocalName, "ean", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(e.Name.LocalName, "upc", StringComparison.OrdinalIgnoreCase))
            ?.Value?.Trim();

        string? normalizedEan = null;
        if (!string.IsNullOrWhiteSpace(rawBarcode))
        {
            if (BarcodeValidator.TryNormalizeEan13(rawBarcode, out var norm))
            {
                normalizedEan = norm;
            }
        }

        // 5. Precio y Divisa
        string? rawPrice = elements
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "price", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(e.Name.LocalName, "sale_price", StringComparison.OrdinalIgnoreCase))
            ?.Value?.Trim();

        var (price, currency) = ParsePriceAndCurrency(rawPrice);

        // 6. Disponibilidad (Stock)
        string? rawAvailability = elements
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "availability", StringComparison.OrdinalIgnoreCase))
            ?.Value?.Trim();

        bool inStock = ParseAvailability(rawAvailability);

        return new FeedProductItem(
            Sku: sku,
            Title: title,
            RawBarcode: rawBarcode,
            NormalizedEan: normalizedEan,
            Price: price,
            Currency: currency,
            InStock: inStock,
            ProductUrl: productUrl);
    }

    private static (decimal Price, string Currency) ParsePriceAndCurrency(string? rawPrice)
    {
        if (string.IsNullOrWhiteSpace(rawPrice))
            return (0m, "EUR");

        string currency = "EUR";
        string upper = rawPrice.ToUpperInvariant();
        if (upper.Contains("USD") || upper.Contains('$'))
            currency = "USD";
        else if (upper.Contains("GBP") || upper.Contains('£'))
            currency = "GBP";
        else if (upper.Contains("EUR") || upper.Contains('€'))
            currency = "EUR";

        var match = PriceRegex.Match(rawPrice);
        if (!match.Success)
            return (0m, currency);

        string numText = match.Value;

        // Si contiene coma y punto (ej. 1,234.50 o 1.234,50)
        if (numText.Contains(',') && numText.Contains('.'))
        {
            int commaIdx = numText.IndexOf(',');
            int dotIdx = numText.IndexOf('.');
            if (commaIdx < dotIdx)
            {
                // Formato 1,234.50 (coma es miles, punto es decimal)
                numText = numText.Replace(",", string.Empty);
            }
            else
            {
                // Formato 1.234,50 (punto es miles, coma es decimal)
                numText = numText.Replace(".", string.Empty).Replace(',', '.');
            }
        }
        else if (numText.Contains(','))
        {
            // Solo coma decimal: "45,00" -> "45.00"
            numText = numText.Replace(',', '.');
        }

        if (decimal.TryParse(numText, NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price))
        {
            return (price, currency);
        }

        return (0m, currency);
    }

    private static bool ParseAvailability(string? availability)
    {
        if (string.IsNullOrWhiteSpace(availability))
            return true;

        string lower = availability.Trim().ToLowerInvariant();

        if (lower is "out of stock" or "out_of_stock" or "agotado" or "no disponible" or "false" or "0")
            return false;

        if (lower is "in stock" or "in_stock" or "disponible" or "en stock" or "true" or "1" or "preorder" or "backorder")
            return true;

        return !lower.Contains("out") && !lower.Contains("agotado");
    }
}
