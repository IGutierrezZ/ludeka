using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Options;
using Ludeka.Infrastructure.Affiliates.Amazon;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class OfficialAmazonPaApiProviderTests
{
    private class TestHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Responder(request));
        }
    }

    private static OfficialAmazonPaApiProvider CreateProvider(
        TestHttpMessageHandler handler,
        string accessKey = "TESTACCESSKEY",
        string secretKey = "TESTSECRETKEY",
        string associateTag = "ludeka-21")
    {
        var httpClient = new HttpClient(handler);
        var options = Microsoft.Extensions.Options.Options.Create(new AmazonOptions
        {
            PaApi = new AmazonPaApiOptions
            {
                AccessKey = accessKey,
                SecretKey = secretKey,
                AssociateTag = associateTag,
                Region = "eu-west-1",
                Host = "webservices.amazon.es"
            }
        });

        return new OfficialAmazonPaApiProvider(httpClient, options, NullLogger<OfficialAmazonPaApiProvider>.Instance);
    }

    [Fact]
    public void IsConfigured_WhenKeysAreMissing_ReturnsFalse()
    {
        var provider = CreateProvider(new TestHttpMessageHandler(), accessKey: "", secretKey: "");
        Assert.False(provider.IsConfigured());
    }

    [Fact]
    public void IsConfigured_WhenKeysArePresent_ReturnsTrue()
    {
        var provider = CreateProvider(new TestHttpMessageHandler());
        Assert.True(provider.IsConfigured());
    }

    [Fact]
    public async Task LookupAsinByEanAsync_WhenNotConfigured_ReturnsNullWithoutCallingHttp()
    {
        bool called = false;
        var handler = new TestHttpMessageHandler
        {
            Responder = _ => { called = true; return new HttpResponseMessage(HttpStatusCode.OK); }
        };
        var provider = CreateProvider(handler, accessKey: "");

        var result = await provider.LookupAsinByEanAsync("8435407626492");

        Assert.Null(result);
        Assert.False(called);
    }

    [Fact]
    public async Task GetPriceAndStockAsync_WhenNotConfigured_ReturnsNullWithoutCallingHttp()
    {
        bool called = false;
        var handler = new TestHttpMessageHandler
        {
            Responder = _ => { called = true; return new HttpResponseMessage(HttpStatusCode.OK); }
        };
        var provider = CreateProvider(handler, accessKey: "");

        var result = await provider.GetPriceAndStockAsync("B07MZT757D");

        Assert.Null(result);
        Assert.False(called);
    }

    [Fact]
    public async Task GetPriceAndStockAsync_WithValidPaApiResponse_SignsRequestAndParsesPrice()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new TestHttpMessageHandler
        {
            Responder = req =>
            {
                capturedRequest = req;
                var json = """
                {
                    "ItemsResult": {
                        "Items": [
                            {
                                "ASIN": "B07MZT757D",
                                "DetailPageURL": "https://www.amazon.es/dp/B07MZT757D?tag=ludeka-21",
                                "ItemInfo": {
                                    "Title": { "DisplayValue": "Wingspan" }
                                },
                                "Offers": {
                                    "Listings": [
                                        {
                                            "Price": { "Amount": 55.00, "Currency": "EUR" },
                                            "Availability": { "Type": "Now", "Message": "En stock" }
                                        }
                                    ]
                                }
                            }
                        ]
                    }
                }
                """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
        };

        var provider = CreateProvider(handler);

        var result = await provider.GetPriceAndStockAsync("B07MZT757D");

        Assert.NotNull(result);
        Assert.Equal("B07MZT757D", result.Asin);
        Assert.Equal(55.00m, result.Price);
        Assert.Equal("€", result.Currency);
        Assert.True(result.InStock);
        Assert.Equal("Wingspan", result.Title);

        // Validar cabeceras de firma AWS v4
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("x-amz-date"));
        Assert.True(capturedRequest.Headers.Contains("x-amz-target"));
        Assert.True(capturedRequest.Headers.Contains("Authorization"));
        var auth = string.Join(" ", capturedRequest.Headers.GetValues("Authorization"));
        Assert.StartsWith("AWS4-HMAC-SHA256 Credential=TESTACCESSKEY", auth);
        Assert.Contains("Signature=", auth);
    }

    [Fact]
    public async Task LookupAsinByEanAsync_WithValidSearchResult_ParsesAsin()
    {
        var handler = new TestHttpMessageHandler
        {
            Responder = _ =>
            {
                var json = """
                {
                    "SearchResult": {
                        "Items": [
                            {
                                "ASIN": "B07MZT757D",
                                "ItemInfo": {
                                    "Title": { "DisplayValue": "Wingspan" }
                                }
                            }
                        ]
                    }
                }
                """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
        };

        var provider = CreateProvider(handler);

        var asin = await provider.LookupAsinByEanAsync("8435407626492");

        Assert.Equal("B07MZT757D", asin);
    }
}
