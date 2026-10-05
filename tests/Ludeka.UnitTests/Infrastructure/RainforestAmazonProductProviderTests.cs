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

public class RainforestAmazonProductProviderTests
{
    private class TestHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Responder(request));
        }
    }

    private static RainforestAmazonProductProvider CreateProvider(TestHttpMessageHandler handler, string? apiKey = "test-rainforest-key")
    {
        var httpClient = new HttpClient(handler);
        var options = Microsoft.Extensions.Options.Options.Create(new AmazonOptions
        {
            Bridge = new AmazonBridgeOptions
            {
                ApiKey = apiKey ?? string.Empty,
                BaseUrl = "https://api.rainforestapi.com",
                AmazonDomain = "amazon.es"
            }
        });

        return new RainforestAmazonProductProvider(httpClient, options, NullLogger<RainforestAmazonProductProvider>.Instance);
    }

    [Fact]
    public async Task LookupAsinByEanAsync_WithValidSearchResults_ReturnsNormalizedAsin()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            Responder = req =>
            {
                Assert.Contains("search_term=8435407626492", req.RequestUri!.Query);
                Assert.Contains("amazon_domain=amazon.es", req.RequestUri!.Query);

                var json = """
                {
                    "request_info": { "success": true },
                    "search_results": [
                        { "position": 1, "title": "Wingspan", "asin": "b07mzt757d" }
                    ]
                }
                """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
        };

        var provider = CreateProvider(handler);

        // Act
        var asin = await provider.LookupAsinByEanAsync("8435407626492");

        // Assert
        Assert.Equal("B07MZT757D", asin);
    }

    [Fact]
    public async Task LookupAsinByEanAsync_WhenApiKeyIsEmpty_ReturnsNullWithoutCallingHttp()
    {
        // Arrange
        bool httpCalled = false;
        var handler = new TestHttpMessageHandler
        {
            Responder = _ =>
            {
                httpCalled = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        };

        var provider = CreateProvider(handler, apiKey: "");

        // Act
        var asin = await provider.LookupAsinByEanAsync("8435407626492");

        // Assert
        Assert.Null(asin);
        Assert.False(httpCalled);
    }

    [Fact]
    public async Task LookupAsinByEanAsync_WhenHttpFails_ReturnsNullGracefully()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        };

        var provider = CreateProvider(handler);

        // Act
        var asin = await provider.LookupAsinByEanAsync("8435407626492");

        // Assert
        Assert.Null(asin);
    }

    [Fact]
    public async Task GetPriceAndStockAsync_WithValidProductBuybox_ReturnsParsedPriceAndStock()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            Responder = req =>
            {
                Assert.Contains("asin=B07MZT757D", req.RequestUri!.Query);

                var json = """
                {
                    "request_info": { "success": true },
                    "product": {
                        "title": "Wingspan Edición Española",
                        "asin": "B07MZT757D",
                        "link": "https://www.amazon.es/dp/B07MZT757D",
                        "buybox_winner": {
                            "price": {
                                "value": 52.50,
                                "currency": "EUR",
                                "symbol": "€"
                            },
                            "availability": {
                                "type": "in_stock",
                                "raw": "En stock."
                            }
                        }
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

        // Act
        var result = await provider.GetPriceAndStockAsync("B07MZT757D");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("B07MZT757D", result.Asin);
        Assert.Equal(52.50m, result.Price);
        Assert.Equal("€", result.Currency);
        Assert.True(result.InStock);
        Assert.Equal("Wingspan Edición Española", result.Title);
        Assert.Equal("https://www.amazon.es/dp/B07MZT757D", result.ProductUrl);
    }

    [Fact]
    public async Task GetPriceAndStockAsync_WhenOutOfStock_ReturnsInStockFalse()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            Responder = _ =>
            {
                var json = """
                {
                    "product": {
                        "title": "Juego descatalogado",
                        "asin": "B07MZT757D",
                        "buybox_winner": {
                            "price": { "value": 99.00 },
                            "availability": { "type": "out_of_stock" }
                        }
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

        // Act
        var result = await provider.GetPriceAndStockAsync("B07MZT757D");

        // Assert
        Assert.NotNull(result);
        Assert.False(result.InStock);
        Assert.Equal(99.00m, result.Price);
    }

    [Fact]
    public async Task GetPriceAndStockAsync_WhenNoPriceAvailable_ReturnsNull()
    {
        // Arrange
        var handler = new TestHttpMessageHandler
        {
            Responder = _ =>
            {
                var json = """
                {
                    "product": {
                        "title": "Juego sin ofertas activas",
                        "asin": "B07MZT757D"
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

        // Act
        var result = await provider.GetPriceAndStockAsync("B07MZT757D");

        // Assert
        Assert.Null(result);
    }
}
