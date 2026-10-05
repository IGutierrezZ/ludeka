using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
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
/// Proveedor oficial que implementa la especificación PA-API 5.0 de Amazon con autenticación AWS SigV4.
/// </summary>
public class OfficialAmazonPaApiProvider : IAmazonProductProvider
{
    private readonly HttpClient _httpClient;
    private readonly AmazonOptions _options;
    private readonly ILogger<OfficialAmazonPaApiProvider> _logger;

    public OfficialAmazonPaApiProvider(
        HttpClient httpClient,
        IOptions<AmazonOptions> options,
        ILogger<OfficialAmazonPaApiProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? new AmazonOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> LookupAsinByEanAsync(string ean, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ean)) return null;

        if (!IsConfigured())
        {
            _logger.LogWarning("PA-API oficial no configurada (AccessKey o SecretKey ausentes). Omitiendo búsqueda de ASIN para EAN {Ean}.", ean);
            return null;
        }

        try
        {
            var payload = new
            {
                Keywords = ean.Trim(),
                SearchIndex = "All",
                ItemCount = 1,
                PartnerTag = _options.PaApi.AssociateTag,
                PartnerType = "Associates",
                Resources = new[] { "ItemInfo.Title" }
            };

            var jsonBody = JsonSerializer.Serialize(payload);
            var responseJson = await ExecuteSignedRequestAsync("SearchItems", jsonBody, ct);
            if (string.IsNullOrWhiteSpace(responseJson)) return null;

            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("SearchResult", out var searchResult) &&
                searchResult.TryGetProperty("Items", out var items) &&
                items.ValueKind == JsonValueKind.Array &&
                items.GetArrayLength() > 0)
            {
                var firstItem = items[0];
                if (firstItem.TryGetProperty("ASIN", out var asinProp) && !string.IsNullOrWhiteSpace(asinProp.GetString()))
                {
                    return asinProp.GetString()!.Trim().ToUpperInvariant();
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            _logger.LogError(ex, "Error al consultar PA-API SearchItems para EAN {Ean}.", ean);
            return null;
        }
    }

    public async Task<AmazonProductPriceResult?> GetPriceAndStockAsync(string asin, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(asin)) return null;

        if (!IsConfigured())
        {
            _logger.LogWarning("PA-API oficial no configurada (AccessKey o SecretKey ausentes). Omitiendo consulta de precio para ASIN {Asin}.", asin);
            return null;
        }

        try
        {
            var payload = new
            {
                ItemIds = new[] { asin.Trim().ToUpperInvariant() },
                ItemIdType = "ASIN",
                PartnerTag = _options.PaApi.AssociateTag,
                PartnerType = "Associates",
                Resources = new[]
                {
                    "ItemInfo.Title",
                    "Offers.Listings.Price",
                    "Offers.Listings.Availability.Message",
                    "Offers.Listings.Availability.Type"
                }
            };

            var jsonBody = JsonSerializer.Serialize(payload);
            var responseJson = await ExecuteSignedRequestAsync("GetItems", jsonBody, ct);
            if (string.IsNullOrWhiteSpace(responseJson)) return null;

            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("ItemsResult", out var itemsResult) ||
                !itemsResult.TryGetProperty("Items", out var items) ||
                items.ValueKind != JsonValueKind.Array ||
                items.GetArrayLength() == 0)
            {
                return null;
            }

            var item = items[0];
            string? title = null;
            if (item.TryGetProperty("ItemInfo", out var info) && info.TryGetProperty("Title", out var titleProp) && titleProp.TryGetProperty("DisplayValue", out var disp))
            {
                title = disp.GetString();
            }

            string? detailUrl = item.TryGetProperty("DetailPageURL", out var urlProp) ? urlProp.GetString() : null;

            decimal? price = null;
            string currency = "€";
            bool inStock = true;

            if (item.TryGetProperty("Offers", out var offers) &&
                offers.TryGetProperty("Listings", out var listings) &&
                listings.ValueKind == JsonValueKind.Array &&
                listings.GetArrayLength() > 0)
            {
                var listing = listings[0];
                if (listing.TryGetProperty("Price", out var priceObj))
                {
                    if (priceObj.TryGetProperty("Amount", out var amtProp) && amtProp.TryGetDecimal(out var amt))
                    {
                        price = amt;
                    }
                    if (priceObj.TryGetProperty("Currency", out var currProp) && !string.IsNullOrWhiteSpace(currProp.GetString()))
                    {
                        currency = currProp.GetString() == "EUR" ? "€" : currProp.GetString()!;
                    }
                }

                if (listing.TryGetProperty("Availability", out var availObj) &&
                    availObj.TryGetProperty("Type", out var availType))
                {
                    var typeStr = availType.GetString();
                    inStock = !string.Equals(typeStr, "OutOfStock", StringComparison.OrdinalIgnoreCase);
                }
            }

            if (!price.HasValue) return null;

            return new AmazonProductPriceResult(
                asin: asin.Trim().ToUpperInvariant(),
                price: price.Value,
                currency: currency,
                inStock: inStock,
                title: title,
                productUrl: detailUrl
            );
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            _logger.LogError(ex, "Error al consultar PA-API GetItems para ASIN {Asin}.", asin);
            return null;
        }
    }

    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(_options.PaApi.AccessKey) &&
               !string.IsNullOrWhiteSpace(_options.PaApi.SecretKey) &&
               !string.IsNullOrWhiteSpace(_options.PaApi.AssociateTag);
    }

    public async Task<string?> ExecuteSignedRequestAsync(string operation, string jsonBody, CancellationToken ct)
    {
        var host = string.IsNullOrWhiteSpace(_options.PaApi.Host) ? "webservices.amazon.es" : _options.PaApi.Host;
        var region = string.IsNullOrWhiteSpace(_options.PaApi.Region) ? "eu-west-1" : _options.PaApi.Region;
        var service = "ProductAdvertisingAPI";
        var target = $"com.amazon.paapi5.v1.ProductAdvertisingAPIv1.{operation}";
        var path = $"/paapi5/{operation.ToLowerInvariant()}";

        var now = DateTimeOffset.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var requestUri = new Uri($"https://{host}{path}");
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);

        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        request.Headers.Add("host", host);
        request.Headers.Add("x-amz-date", amzDate);
        request.Headers.Add("x-amz-target", target);

        // Calcular firma AWS SigV4
        var payloadHash = ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(jsonBody)));
        var canonicalHeaders = $"content-type:application/json; charset=utf-8\nhost:{host}\nx-amz-date:{amzDate}\nx-amz-target:{target}\n";
        var signedHeaders = "content-type;host;x-amz-date;x-amz-target";
        var canonicalRequest = $"POST\n{path}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";

        var credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";

        var signingKey = GetSignatureKey(_options.PaApi.SecretKey, dateStamp, region, service);
        var signature = ToHex(HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(stringToSign)));

        var authHeader = $"AWS4-HMAC-SHA256 Credential={_options.PaApi.AccessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
        request.Headers.TryAddWithoutValidation("Authorization", authHeader);

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("PA-API devolvió código HTTP {StatusCode} en {Operation}.", response.StatusCode, operation);
            return null;
        }

        return await response.Content.ReadAsStringAsync(ct);
    }

    private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
    {
        var kSecret = Encoding.UTF8.GetBytes("AWS4" + key);
        var kDate = HMACSHA256.HashData(kSecret, Encoding.UTF8.GetBytes(dateStamp));
        var kRegion = HMACSHA256.HashData(kDate, Encoding.UTF8.GetBytes(regionName));
        var kService = HMACSHA256.HashData(kRegion, Encoding.UTF8.GetBytes(serviceName));
        return HMACSHA256.HashData(kService, Encoding.UTF8.GetBytes("aws4_request"));
    }

    private static string ToHex(byte[] data)
    {
        var sb = new StringBuilder(data.Length * 2);
        foreach (var b in data)
        {
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }
}
