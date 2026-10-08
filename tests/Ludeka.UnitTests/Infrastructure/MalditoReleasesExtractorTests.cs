using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Infrastructure.Extractors;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class MalditoReleasesExtractorTests
{
    private const string SampleHomeWithPuntitoHtml = @"
    <div class=""pagebuilder-column-group"">
        <h2 class=""titulo-home"" data-content-type=""heading"">A puntito de llegar</h2>
        <div class=""novedades-home"">
            <ul class=""product-items"">
                <li class=""item product product-item"">
                    <div class=""product-item-info type1"">
                        <div class=""product photo product-item-photo"">
                            <a href=""https://tienda.malditogames.com/railway-boom.html"">
                                <img class=""product-image-photo"" src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436625619129-1200-face3d.jpg"" />
                            </a>
                        </div>
                        <div class=""product details product-item-details"">
                            <a class=""product-item-link"" href=""https://tienda.malditogames.com/railway-boom.html"">
                                Railway Boom
                            </a>
                            <span class=""fecha_home"">15 de octubre</span>
                            <span class=""price"">52,00&nbsp;&euro;</span>
                        </div>
                    </div>
                </li>
            </ul>
        </div>
    </div>
    ";

    private const string SampleFullSectionsHomeHtml = @"
    <div class=""pagebuilder-column-group"">
        <h2 class=""titulo-home"" data-content-type=""heading"">&Uacute;ltimas novedades</h2>
        <div class=""novedades-home"">
            <ul class=""product-items"">
                <li class=""item product product-item"">
                    <div class=""product-item-info type1"">
                        <div class=""product photo product-item-photo"">
                            <a href=""https://tienda.malditogames.com/emblemas.html"">
                                <img class=""product-image-photo"" src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436625618788-1200-face3d.jpg"" />
                            </a>
                        </div>
                        <div class=""product details product-item-details"">
                            <a class=""product-item-link"" href=""https://tienda.malditogames.com/emblemas.html"">
                                Emblemas
                            </a>
                            <span class=""fecha_home"">8 de octubre</span>
                            <span class=""price"">20,00&nbsp;&euro;</span>
                        </div>
                    </div>
                </li>
            </ul>
        </div>

        <h2 class=""titulo-home"" data-content-type=""heading"">A puntito de llegar</h2>
        <div class=""novedades-home"">
            <ul class=""product-items"">
                <li class=""item product product-item"">
                    <div class=""product-item-info type1"">
                        <div class=""product photo product-item-photo"">
                            <a href=""https://tienda.malditogames.com/railway-boom.html"">
                                <img class=""product-image-photo"" src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436625619129-1200-face3d.jpg"" />
                            </a>
                        </div>
                        <div class=""product details product-item-details"">
                            <a class=""product-item-link"" href=""https://tienda.malditogames.com/railway-boom.html"">
                                Railway Boom
                            </a>
                            <span class=""fecha_home"">15 de octubre</span>
                            <span class=""price"">52,00&nbsp;&euro;</span>
                        </div>
                    </div>
                </li>
            </ul>
        </div>

        <h2 class=""titulo-home"" data-content-type=""heading"">Volver&aacute;n a estar disponibles en breve</h2>
        <div class=""novedades-home"">
            <ul class=""product-items"">
                <li class=""item product product-item"">
                    <div class=""product-item-info type1"">
                        <div class=""product photo product-item-photo"">
                            <a href=""https://tienda.malditogames.com/1243-earthborne-rangers.html"">
                                <img class=""product-image-photo"" src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578818099-1200-face3d.jpg"" />
                            </a>
                        </div>
                        <div class=""product details product-item-details"">
                            <a class=""product-item-link"" href=""https://tienda.malditogames.com/1243-earthborne-rangers.html"">
                                Earthborne Rangers
                            </a>
                            <span class=""fecha_home"">22 de octubre</span>
                            <span class=""price"">100,00&nbsp;&euro;</span>
                        </div>
                    </div>
                </li>
            </ul>
        </div>

        <h2 class=""titulo-home"" data-content-type=""heading"">Lo que se viene</h2>
        <div class=""sev"">
            <figure>
                <img src=""https://tienda.malditogames.com/media/wysiwyg/SQ_Floe.jpg"" alt="""" />
            </figure>
            <div class=""fecha-home""><p>2027</p></div>
        </div>
    </div>
    ";

    private const string SampleMalditoProductPageHtml = @"
    <!doctype html>
    <html lang=""es"">
        <head>
            <meta property=""og:type"" content=""product"" />
            <meta property=""product:price:amount"" content=""115"" />
            <meta property=""product:price:currency"" content=""EUR"" />
        </head>
        <body>
            <script type=""text/x-magento-init"">
            {
                ""[data-gallery-role=gallery-placeholder]"": {
                    ""mage/gallery/gallery"": {
                        ""data"": [
                            {
                                ""thumb"": ""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578819720-1200-face3d.jpg"",
                                ""img"": ""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578819720-1200-face3d.jpg"",
                                ""full"": ""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578819720-1200-face3d.jpg"",
                                ""isMain"": true
                            },
                            {
                                ""thumb"": ""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578819720-1200-frontflat.jpg"",
                                ""img"": ""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578819720-1200-frontflat.jpg"",
                                ""full"": ""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578819720-1200-frontflat.jpg"",
                                ""isMain"": false
                            },
                            {
                                ""thumb"": ""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578819720-1200-backflat.jpg"",
                                ""img"": ""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578819720-1200-backflat.jpg"",
                                ""full"": ""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578819720-1200-backflat.jpg"",
                                ""isMain"": false
                            }
                        ]
                    }
                }
            }
            </script>
            <div class=""product-add-form"">
                <form data-product-sku=""8436578819720"" action=""/cart""></form>
            </div>
        </body>
    </html>
    ";

    private const string SampleMalditoCatalogPageHtml = @"
    <div class=""products wrapper grid products-grid"">
        <ol class=""products list items product-items"">
            <li class=""item product product-item"">
                <div class=""product-item-info"">
                    <a class=""product-item-link"" href=""https://tienda.malditogames.com/34-terraforming-mars.html"">
                        Terraforming Mars
                    </a>
                    <img class=""product-image-photo"" src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578810017-1200-face3d.jpg"" />
                </div>
            </li>
            <li class=""item product product-item"">
                <div class=""product-item-info"">
                    <a class=""product-item-link"" href=""https://tienda.malditogames.com/scythe.html"">
                        Scythe
                    </a>
                    <img class=""product-image-photo"" src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578810200-1200-face3d.jpg"" />
                </div>
            </li>
        </ol>
        <div class=""pages"">
            <a class=""action next"" href=""https://tienda.malditogames.com/juegos?p=2"">Siguiente</a>
        </div>
    </div>
    ";

    [Fact]
    public void ParseHtml_FiltersStrictly_OnlyReturnsPuntitoAndReprint_ExcludesUltimasNovedadesAndLoQueSeViene()
    {
        var extractor = new MalditoReleasesExtractor(new HttpClient(), NullLogger<MalditoReleasesExtractor>.Instance);

        var results = extractor.ParseHtml(SampleFullSectionsHomeHtml, null);

        // Se deben descartar 'Últimas novedades' (Emblemas) y 'Lo que se viene' (Floe)
        Assert.Equal(2, results.Count);

        // 1. A puntito de llegar: Railway Boom
        var railway = Assert.Single(results, r => r.Title == "Railway Boom");
        Assert.Equal("8436625619129", railway.Ean);
        Assert.Equal(52.00m, railway.EstimatedPvp);
        Assert.Equal("15 de octubre", railway.TargetDateText);
        Assert.False(railway.IsReprint);
        Assert.Equal("Maldito Games", railway.Publisher);

        // 2. Volverán a estar disponibles en breve (Reimpresión): Earthborne Rangers
        var rangers = Assert.Single(results, r => r.Title == "Earthborne Rangers");
        Assert.Equal("8436578818099", rangers.Ean);
        Assert.Equal(100.00m, rangers.EstimatedPvp);
        Assert.Equal("22 de octubre", rangers.TargetDateText);
        Assert.True(rangers.IsReprint);
        Assert.Equal("Maldito Games", rangers.Publisher);

        // Verificar explícitamente ausencias
        Assert.DoesNotContain(results, r => r.Title == "Emblemas");
        Assert.DoesNotContain(results, r => r.Title == "Floe");
    }

    [Fact]
    public void ParseProductGalleryHtml_ExtractsImagesEanAndPrice()
    {
        var extractor = new MalditoReleasesExtractor(new HttpClient(), NullLogger<MalditoReleasesExtractor>.Instance);

        var gallery = extractor.ParseProductGalleryHtml(SampleMalditoProductPageHtml);

        Assert.NotNull(gallery);
        Assert.Equal("https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578819720-1200-face3d.jpg", gallery!.CoverImageUrl);
        Assert.Equal("https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578819720-1200-backflat.jpg", gallery.BackCoverImageUrl);
        Assert.Equal("https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578819720-1200-frontflat.jpg", gallery.TableImageUrl);
        Assert.Equal("8436578819720", gallery.Ean);
        Assert.Equal(115.00m, gallery.Pvp);
    }

    [Fact]
    public void ParseCatalogPageHtml_ExtractsItemsAndNextPage()
    {
        var extractor = new MalditoReleasesExtractor(new HttpClient(), NullLogger<MalditoReleasesExtractor>.Instance);

        var result = extractor.ParseCatalogPageHtml(SampleMalditoCatalogPageHtml);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.HasNextPage);
        Assert.Equal(2, result.Items.Count);

        var terraforming = result.Items[0];
        Assert.Equal("Terraforming Mars", terraforming.Title);
        Assert.Equal("https://tienda.malditogames.com/34-terraforming-mars.html", terraforming.ProductUrl);
        Assert.Equal("8436578810017", terraforming.Ean);
    }

    [Theory]
    [InlineData("https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578818099-1200-face3d.jpg", "8436578818099")]
    [InlineData("https://tienda.malditogames.com/media/catalog/product/8/4/8436625618788.png", "8436625618788")]
    [InlineData("https://tienda.malditogames.com/media/wysiwyg/SQ_Floe.jpg", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ExtractEanFromImageUrl_ReturnsExpectedEan(string? imageUrl, string? expectedEan)
    {
        var ean = MalditoReleasesExtractor.ExtractEanFromImageUrl(imageUrl);
        Assert.Equal(expectedEan, ean);
    }

    [Theory]
    [InlineData("8 de octubre", 2026, 2026, 10, 8, false)]
    [InlineData("15 de octubre", 2026, 2026, 10, 15, false)]
    [InlineData("29 de octubre", 2026, 2026, 10, 29, false)]
    [InlineData("5 de noviembre", 2026, 2026, 11, 5, false)]
    [InlineData("2027", 2026, 2027, 1, 1, true)]
    [InlineData("Noviembre 2026", 2026, 2026, 11, 1, true)]
    public void ParseSpanishDate_ParsesCorrectly(string rawDate, int refYear, int expYear, int expMonth, int expDay, bool expMonthOnly)
    {
        var (date, isMonthOnly) = MalditoReleasesExtractor.ParseSpanishDate(rawDate, refYear);

        Assert.NotNull(date);
        Assert.Equal(expYear, date!.Value.Year);
        Assert.Equal(expMonth, date!.Value.Month);
        Assert.Equal(expDay, date!.Value.Day);
        Assert.Equal(expMonthOnly, isMonthOnly);
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task ExtractReleasesAsync_WhenHomeSucceeds_EnrichesWithProductGallery()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            var url = req.RequestUri?.ToString() ?? string.Empty;
            if (url.Contains("railway-boom.html"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleMalditoProductPageHtml)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleHomeWithPuntitoHtml)
            };
        });

        var client = new HttpClient(handler);
        var extractor = new MalditoReleasesExtractor(client, NullLogger<MalditoReleasesExtractor>.Instance);

        var results = await extractor.ExtractReleasesAsync();

        Assert.NotNull(results);
        var item = Assert.Single(results);
        Assert.Equal("Railway Boom", item.Title);
        // Debe enriquecerse con la galería (3D cover, back cover, EAN, PVP de la ficha de producto)
        Assert.Contains("face3d", item.CoverImageUrl!);
        Assert.Contains("backflat", item.BackCoverImageUrl!);
        Assert.Equal("8436578819720", item.Ean);
        Assert.Equal(115.00m, item.EstimatedPvp);
    }

    [Fact]
    public async Task ExtractReleasesAsync_WhenBothFail_ReturnsEmptyList()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        });

        var client = new HttpClient(handler);
        var extractor = new MalditoReleasesExtractor(client, NullLogger<MalditoReleasesExtractor>.Instance);

        var results = await extractor.ExtractReleasesAsync();

        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task ExtractReleasesAsync_RetriesOn403AndSucceedsOnSecondAttempt()
    {
        int homeAttempts = 0;
        var handler = new MockHttpMessageHandler(req =>
        {
            var uri = req.RequestUri?.ToString().TrimEnd('/') ?? string.Empty;
            if (uri.Equals("https://tienda.malditogames.com", StringComparison.OrdinalIgnoreCase))
            {
                homeAttempts++;
                if (homeAttempts == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.Forbidden);
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleHomeWithPuntitoHtml)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleMalditoProductPageHtml)
            };
        });

        var client = new HttpClient(handler);
        var extractor = new MalditoReleasesExtractor(client, NullLogger<MalditoReleasesExtractor>.Instance);

        var results = await extractor.ExtractReleasesAsync();

        Assert.Equal(2, homeAttempts);
        Assert.NotEmpty(results);
    }
}
