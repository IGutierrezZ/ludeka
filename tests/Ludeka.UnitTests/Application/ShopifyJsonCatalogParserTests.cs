using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Affiliates;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class ShopifyJsonCatalogParserTests
{
    private readonly ShopifyJsonCatalogParser _parser = new();

    [Fact]
    public async Task ParseStreamAsync_LudusBelliPayload_ExtractsEanFromSku()
    {
        // Fixture real de Ludus Belli donde el SKU es el EAN-13
        const string json = """
        {
          "products": [
            {
              "id": 10435606348105,
              "title": "Custodian Dreadnought",
              "handle": "custodian-dreadnought",
              "vendor": "Games Workshop",
              "product_type": "Juegos de Miniaturas - Wargames",
              "variants": [
                {
                  "id": 55510319333705,
                  "title": "Default Title",
                  "sku": "5011921285839",
                  "price": "57.60",
                  "compare_at_price": "64.00",
                  "available": true
                }
              ],
              "images": [
                {
                  "src": "https://cdn.shopify.com/s/files/box.jpg"
                }
              ]
            }
          ]
        }
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var items = await _parser.ParseStreamAsync(stream, "https://ludusbelli.com").ToListAsync();

        Assert.Single(items);
        var item = items[0];
        Assert.Equal("Custodian Dreadnought", item.Title);
        Assert.Equal("https://ludusbelli.com/products/custodian-dreadnought", item.ProductUrl);
        Assert.Equal(57.60m, item.Price);
        Assert.Equal("EUR", item.Currency);
        Assert.True(item.InStock);
        Assert.Equal("5011921285839", item.NormalizedEan);
        Assert.Equal("5011921285839", item.RawBarcode);
    }

    [Fact]
    public async Task ParseStreamAsync_CuartoDeJuegosPayload_ExtractsEanFromImageFilename()
    {
        // Fixture real de Cuarto de Juegos donde el SKU es nulo pero la imagen contiene el EAN-13
        const string json = """
        {
          "products": [
            {
              "id": 16222568350021,
              "title": "Time Bomb: Moriarty vs Sherlock",
              "handle": "time-bomb-moriarty-vs-sherlock",
              "vendor": "Devir",
              "product_type": "Juego de mesa",
              "variants": [
                {
                  "id": 59553679147333,
                  "title": "Default Title",
                  "sku": null,
                  "price": "10.80",
                  "compare_at_price": "12.00",
                  "available": true
                }
              ],
              "images": [
                {
                  "src": "https://cdn.shopify.com/s/files/1/0900/files/8436625611079-1200-face3d.jpg?v=1790944759"
                }
              ]
            }
          ]
        }
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var items = await _parser.ParseStreamAsync(stream, "https://cuartodejuegos.es").ToListAsync();

        Assert.Single(items);
        var item = items[0];
        Assert.Equal("Time Bomb: Moriarty vs Sherlock", item.Title);
        Assert.Equal("https://cuartodejuegos.es/products/time-bomb-moriarty-vs-sherlock", item.ProductUrl);
        Assert.Equal(10.80m, item.Price);
        Assert.True(item.InStock);
        Assert.Equal("8436625611079", item.NormalizedEan);
    }

    [Fact]
    public async Task ParseStreamAsync_OutOfStockProduct_ParsesCorrectly()
    {
        const string json = """
        {
          "products": [
            {
              "id": 9999,
              "title": "Catán Clásico",
              "handle": "catan-clasico",
              "variants": [
                {
                  "id": 1111,
                  "title": "Default Title",
                  "sku": "8436017220100",
                  "price": "42.00",
                  "available": false
                }
              ]
            }
          ]
        }
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var items = await _parser.ParseStreamAsync(stream, "https://tienda.es").ToListAsync();

        Assert.Single(items);
        var item = items[0];
        Assert.False(item.InStock);
        Assert.Equal(42.00m, item.Price);
        Assert.Equal("8436017220100", item.NormalizedEan);
    }

    [Fact]
    public async Task ParseStreamAsync_EmptyProductsList_YieldsZeroItems()
    {
        const string json = """{ "products": [] }""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var items = await _parser.ParseStreamAsync(stream, "https://tienda.es").ToListAsync();

        Assert.Empty(items);
    }

    [Fact]
    public async Task ParsePaginatedAsync_StopsWhenPageIsEmpty()
    {
        var mockHandler = new TestHttpMessageHandler(request =>
        {
            if (request.RequestUri!.Query.Contains("page=1"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "products": [
                        {
                          "id": 1,
                          "title": "Juego 1",
                          "handle": "juego-1",
                          "variants": [{ "id": 10, "price": "19.99", "available": true }]
                        }
                      ]
                    }
                    """, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "products": [] }""", Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(mockHandler);
        var items = await _parser.ParsePaginatedAsync(
            "https://tienda.es/products.json",
            httpClient,
            maxPages: 5,
            delayBetweenPagesMs: 0).ToListAsync();

        Assert.Single(items);
        Assert.Equal("Juego 1", items[0].Title);
        Assert.Equal("https://tienda.es/products/juego-1", items[0].ProductUrl);
    }

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
