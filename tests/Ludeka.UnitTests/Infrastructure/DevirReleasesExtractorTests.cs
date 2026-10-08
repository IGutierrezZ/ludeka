using System.Linq;
using Ludeka.Infrastructure.Extractors;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class DevirReleasesExtractorTests
{
    private const string SampleDevirHtml = @"
    <div class=""mgz-element-inner"">
        <div class=""mgz-single-image-wrapper"">
            <img class=""mgz-hover-main"" src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/wysiwyg/Proximos-lanzamientos/8436625615992-1200-frontflat.jpg"" alt=""Vileborn"" />
        </div>
        <p style=""text-align: center;""><span><strong>2026</strong></span></p>
        <p style=""text-align: center;""><span><strong>VILEBORN</strong></span></p>
    </div>
    <div class=""mgz-element-inner"">
        <div class=""mgz-single-image-wrapper"">
            <img class=""mgz-hover-main"" src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/wysiwyg/Proximos-lanzamientos/dagger.jpg"" alt=""Daggerheart"" />
        </div>
        <p style=""text-align: center;""><span><strong>2027</strong></span></p>
        <p style=""text-align: center;""><span><strong>DAGGERHEART</strong></span></p>
    </div>
    <div class=""mgz-element-inner"">
        <div class=""mgz-single-image-wrapper"">
            <a href=""https://devir.es/en-el-abismo"">
                <img class=""mgz-hover-main"" src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/wysiwyg/Proximos-lanzamientos/8436625616159-1200.jpg"" alt=""En el Abismo"" />
            </a>
        </div>
        <p style=""text-align: center;""><span><strong>Noviembre 2026</strong></span></p>
        <p style=""text-align: center;""><span><strong>EN EL ABISMO</strong></span></p>
    </div>
    ";

    [Fact]
    public void ParseHtml_ExtractsOnlyReleasesWithClosedCalendarMonthAndCapturesProductUrl()
    {
        var extractor = new DevirReleasesExtractor(new System.Net.Http.HttpClient(), NullLogger<DevirReleasesExtractor>.Instance);

        var results = extractor.ParseHtml(SampleDevirHtml);

        // Vileborn (solo 2026) y Daggerheart (solo 2027) se descartan por no tener mes cerrado anunciado.
        // Solo En el Abismo (Noviembre 2026) debe extraerse.
        Assert.Single(results);

        var abismo = results.First();
        Assert.Equal("EN EL ABISMO", abismo.Title);
        Assert.Equal("Devir", abismo.Publisher);
        Assert.Equal("8436625616159", abismo.Ean);
        Assert.Equal("Noviembre 2026", abismo.TargetDateText);
        Assert.NotNull(abismo.ReleaseDate);
        Assert.Equal(2026, abismo.ReleaseDate!.Value.Year);
        Assert.Equal(11, abismo.ReleaseDate!.Value.Month);
        Assert.Equal("https://devir.es/en-el-abismo", abismo.SourceUrl);
    }

    [Fact]
    public void ParseHtml_ExtractsDetailedReleasesWithPriceAndDiscardsRoleplayingGames()
    {
        var sampleHtml = @"
        <p style=""text-align: center;""><span style=""font-size: 38px;""><strong>Noviembre 2026 - Juegos de mesa</strong></span></p>
        <div class=""mgz-element-column"">
            <div class=""mgz-single-image-wrapper"">
                <a href=""https://devir.es/hanging-gardens"">
                    <img src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/wysiwyg/product/8436625610973-1200-face3d.jpg"" alt=""Hanging Gardens"" />
                </a>
            </div>
        </div>
        <div class=""mgz-element-column"">
            <div class=""mgz-element-text"">
                <p><span style=""font-size: 24px;""><strong><span>THE HANGING GARDENS</span></strong></span> <strong>NOVEDAD</strong></p>
                <p><strong>Juego de mesa</strong><br /><strong>Precio:</strong> 25€</p>
            </div>
        </div>
        <div class=""mgz-element-column"">
            <div class=""mgz-single-image-wrapper"">
                <a href=""https://devir.es/lacrimosa"">
                    <img src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/wysiwyg/product/8436625611111-1200.jpg"" alt=""Lacrimosa"" />
                </a>
            </div>
        </div>
        <div class=""mgz-element-column"">
            <div class=""mgz-element-text"">
                <p><span style=""font-size: 24px;""><strong><span>LACRIMOSA</span></strong></span> <strong>REIMPRESIÓN</strong></p>
                <p><strong>Juego de mesa</strong><br /><strong>Precio:</strong> 65,50 €</p>
            </div>
        </div>
        <p style=""text-align: center;""><span style=""font-size: 38px;""><strong>Noviembre 2026 - Juegos de rol</strong></span></p>
        <div class=""mgz-element-column"">
            <div class=""mgz-single-image-wrapper"">
                <img src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/wysiwyg/product/8436625619999-1200.jpg"" alt=""Rol Book"" />
            </div>
        </div>
        <div class=""mgz-element-column"">
            <div class=""mgz-element-text"">
                <p><span style=""font-size: 24px;""><strong><span>EL ANILLO ÚNICO</span></strong></span></p>
                <p><strong>Juego de rol</strong><br /><strong>Precio:</strong> 50€</p>
            </div>
        </div>
        ";

        var extractor = new DevirReleasesExtractor(new System.Net.Http.HttpClient(), NullLogger<DevirReleasesExtractor>.Instance);
        var results = extractor.ParseHtml(sampleHtml);

        // Solo los 2 juegos de mesa deben extraerse, el de rol debe descartarse
        Assert.Equal(2, results.Count);
        var gardens = results.First(r => r.Title == "THE HANGING GARDENS");
        Assert.Equal("Devir", gardens.Publisher);
        Assert.Equal("8436625610973", gardens.Ean);
        Assert.Equal(25m, gardens.EstimatedPvp);
        Assert.False(gardens.IsReprint);
        Assert.True(gardens.IsMonthOnly);
        Assert.Equal("https://devir.es/hanging-gardens", gardens.SourceUrl);

        var lacrimosa = results.First(r => r.Title == "LACRIMOSA");
        Assert.Equal("Devir", lacrimosa.Publisher);
        Assert.Equal("8436625611111", lacrimosa.Ean);
        Assert.Equal(65.50m, lacrimosa.EstimatedPvp);
        Assert.True(lacrimosa.IsReprint);
        Assert.True(lacrimosa.IsMonthOnly);
    }

    [Fact]
    public void ParseHtml_DiscardsInDevelopmentSectionsAndNonBoardgames()
    {
        var html = @"
        <p style=""text-align: center;""><span style=""font-size: 38px;""><strong>Diciembre 2026 - Juegos de mesa</strong></span></p>
        <div class=""mgz-element-column"">
            <div class=""mgz-single-image-wrapper"">
                <a href=""https://devir.es/red-cathedral-expansion"">
                    <img src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436625612222-1200-face3d.jpg"" alt=""Red Cathedral Expansion"" />
                </a>
            </div>
        </div>
        <div class=""mgz-element-column"">
            <div class=""mgz-element-text"">
                <p><span style=""font-size: 24px;""><strong><span>THE RED CATHEDRAL EXPANSION</span></strong></span></p>
                <p><strong>Juego de mesa</strong><br /><strong>Precio:</strong> 30€</p>
            </div>
        </div>
        <p style=""text-align: center;""><span style=""font-size: 38px;""><strong>EN DESARROLLO - JUEGOS DE MESA - 2027</strong></span></p>
        <div class=""mgz-element-inner"">
            <div class=""mgz-single-image-wrapper"">
                <img class=""mgz-hover-main"" src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/wysiwyg/Proximos-lanzamientos/8436625619999-1200.jpg"" alt=""Dogs of War"" />
            </div>
            <p style=""text-align: center;""><span><strong>2027</strong></span></p>
            <p style=""text-align: center;""><span><strong>DOGS OF WAR</strong></span></p>
        </div>
        <p style=""text-align: center;""><span style=""font-size: 38px;""><strong>EN DESARROLLO - JUEGOS DE ROL - 2027</strong></span></p>
        <div class=""mgz-element-inner"">
            <div class=""mgz-single-image-wrapper"">
                <img class=""mgz-hover-main"" src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/wysiwyg/Proximos-lanzamientos/dagger.jpg"" alt=""Daggerheart"" />
            </div>
            <p style=""text-align: center;""><span><strong>2027</strong></span></p>
            <p style=""text-align: center;""><span><strong>DAGGERHEART</strong></span></p>
        </div>
        ";

        var extractor = new DevirReleasesExtractor(new System.Net.Http.HttpClient(), NullLogger<DevirReleasesExtractor>.Instance);
        var results = extractor.ParseHtml(html);

        Assert.Single(results);
        var item = results.First();
        Assert.Equal("THE RED CATHEDRAL EXPANSION", item.Title);
        Assert.Equal("https://devir.es/red-cathedral-expansion", item.SourceUrl);
        Assert.Equal(2026, item.ReleaseDate!.Value.Year);
        Assert.Equal(12, item.ReleaseDate!.Value.Month);
    }

    [Fact]
    public void ParseHtml_PrioritizesFace3dImageOverFlatCover()
    {
        var html = @"
        <p style=""text-align: center;""><span style=""font-size: 38px;""><strong>Noviembre 2026 - Juegos de mesa</strong></span></p>
        <div class=""mgz-element-column"">
            <div class=""mgz-single-image-wrapper"">
                <a href=""https://devir.es/salton-sea"">
                    <img src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436625615555-1200-front.jpg"" alt=""Salton Sea 2D"" />
                </a>
            </div>
            <div class=""mgz-single-image-wrapper"">
                <a href=""https://devir.es/salton-sea"">
                    <img src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436625615555-1200-face3d.jpg"" alt=""Salton Sea 3D"" />
                </a>
            </div>
        </div>
        <div class=""mgz-element-column"">
            <div class=""mgz-element-text"">
                <p><span style=""font-size: 24px;""><strong><span>SALTON SEA</span></strong></span></p>
                <p><strong>Juego de mesa</strong><br /><strong>Precio:</strong> 35€</p>
            </div>
        </div>
        ";

        var extractor = new DevirReleasesExtractor(new System.Net.Http.HttpClient(), NullLogger<DevirReleasesExtractor>.Instance);
        var results = extractor.ParseHtml(html);

        Assert.Single(results);
        var item = results.First();
        Assert.Equal("SALTON SEA", item.Title);
        Assert.Contains("face3d", item.CoverImageUrl);
        Assert.Equal("https://devir.es/salton-sea", item.SourceUrl);
    }

    [Fact]
    public void ParseHtml_DiscardsPastMonths_WhenReferenceDateIsProvided()
    {
        var html = @"
        <p style=""text-align: center;""><span style=""font-size: 38px;""><strong>Septiembre 2026 - Juegos de mesa</strong></span></p>
        <div class=""mgz-element-column"">
            <div class=""mgz-element-text"">
                <p><span style=""font-size: 24px;""><strong><span>JUEGO PASADO</span></strong></span></p>
                <p><strong>Juego de mesa</strong><br /><strong>Precio:</strong> 20€</p>
            </div>
        </div>
        <p style=""text-align: center;""><span style=""font-size: 38px;""><strong>Octubre 2026 - Juegos de mesa</strong></span></p>
        <div class=""mgz-element-column"">
            <div class=""mgz-element-text"">
                <p><span style=""font-size: 24px;""><strong><span>JUEGO PRESENTE</span></strong></span></p>
                <p><strong>Juego de mesa</strong><br /><strong>Precio:</strong> 30€</p>
            </div>
        </div>
        <p style=""text-align: center;""><span style=""font-size: 38px;""><strong>Noviembre 2026 - Juegos de mesa</strong></span></p>
        <div class=""mgz-element-column"">
            <div class=""mgz-element-text"">
                <p><span style=""font-size: 24px;""><strong><span>JUEGO FUTURO</span></strong></span></p>
                <p><strong>Juego de mesa</strong><br /><strong>Precio:</strong> 40€</p>
            </div>
        </div>
        ";

        var extractor = new DevirReleasesExtractor(new System.Net.Http.HttpClient(), NullLogger<DevirReleasesExtractor>.Instance);
        var refDate = new System.DateOnly(2026, 10, 1);
        var results = extractor.ParseHtml(html, refDate);

        // Septiembre 2026 debe ser descartado; Octubre y Noviembre deben mantenerse
        Assert.Equal(2, results.Count);
        Assert.DoesNotContain(results, r => r.Title == "JUEGO PASADO");
        Assert.Contains(results, r => r.Title == "JUEGO PRESENTE");
        Assert.Contains(results, r => r.Title == "JUEGO FUTURO");
    }

    [Fact]
    public void ParseHtml_NeverProducesAuthorOrRoleplayingDuplicatesFromDetailedSections()
    {
        var html = @"
        <p style=""text-align: center;""><span style=""font-size: 38px;""><strong>Octubre 2026 - Juegos de mesa</strong></span></p>
        <div class=""mgz-element-inner"">
            <div class=""mgz-element-column"">
                <div class=""mgz-single-image-wrapper"">
                    <a href=""https://devir.es/the-hanging-gardens"">
                        <img src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/wysiwyg/Proximos-lanzamientos/8436625610973-1200-face3d.jpg"" alt=""The Hanging Gardens"" />
                    </a>
                </div>
            </div>
            <div class=""mgz-element-column"">
                <div class=""mgz-element-text"">
                    <p><span style=""font-size: 24px;""><strong><span>THE HANGING GARDENS</span></strong></span></p>
                    <p><strong>19 de octubre</strong></p>
                    <p><strong>Autor: </strong>Seiji Kanai</p>
                    <p><strong>Ilustrador: </strong>Toko</p>
                    <p><strong>Juego de mesa</strong><br /><strong>Precio:</strong> 25€</p>
                </div>
            </div>
        </div>
        ";

        var extractor = new DevirReleasesExtractor(new System.Net.Http.HttpClient(), NullLogger<DevirReleasesExtractor>.Instance);
        var refDate = new System.DateOnly(2026, 10, 1);
        var results = extractor.ParseHtml(html, refDate);

        // Debe haber EXACTAMENTE 1 resultado, correspondiente al juego real
        Assert.Single(results);
        var item = results.First();
        Assert.Equal("THE HANGING GARDENS", item.Title);
        Assert.Equal(25m, item.EstimatedPvp);
        Assert.Equal("8436625610973", item.Ean);

        // Jamás debe existir una tarjeta espuria con 'Autor:'
        Assert.DoesNotContain(results, r => r.Title.StartsWith("Autor", System.StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, r => r.Title.StartsWith("Ilustrador", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ParseProductGalleryHtml_ExtractsFace3dComponentsAndBackCover()
    {
        var sampleProductHtml = @"
        <!DOCTYPE html>
        <html>
        <head><title>The Hanging Gardens - Devir Iberia</title></head>
        <body>
            <div class=""product attribute sku"">
                <strong class=""type"">Referencia</strong>
                <div class=""value"" itemprop=""sku"">8436625610973</div>
            </div>
            <div class=""price-box price-final_price"" data-role=""priceBox"" data-product-id=""4521"">
                <span class=""price"">25,00&nbsp;€</span>
            </div>
            <script type=""text/x-magento-init"">
            {
                ""[data-gallery-role=gallery-placeholder]"": {
                    ""mage/gallery/gallery"": {
                        ""mixins"":[""magnifier/magnify""],
                        ""data"": [
                            {
                                ""thumb"": ""https://devir.es/media/catalog/product/cache/thumb/8436625610973-1200-face3d.jpg"",
                                ""img"": ""https://devir.es/media/catalog/product/cache/medium/8436625610973-1200-face3d.jpg"",
                                ""full"": ""https://devir.es/media/catalog/product/8436625610973-1200-face3d.jpg"",
                                ""isMain"": true,
                                ""type"": ""image""
                            },
                            {
                                ""thumb"": ""https://devir.es/media/catalog/product/cache/thumb/8436625610973-1200-components1.jpg"",
                                ""img"": ""https://devir.es/media/catalog/product/cache/medium/8436625610973-1200-components1.jpg"",
                                ""full"": ""https://devir.es/media/catalog/product/8436625610973-1200-components1.jpg"",
                                ""isMain"": false,
                                ""type"": ""image""
                            },
                            {
                                ""thumb"": ""https://devir.es/media/catalog/product/cache/thumb/8436625610973-1200-backflat.jpg"",
                                ""img"": ""https://devir.es/media/catalog/product/cache/medium/8436625610973-1200-backflat.jpg"",
                                ""full"": ""https://devir.es/media/catalog/product/8436625610973-1200-backflat.jpg"",
                                ""isMain"": false,
                                ""type"": ""image""
                            },
                            {
                                ""thumb"": ""https://devir.es/media/catalog/product/cache/thumb/8436625610973-1200-frontflat.jpg"",
                                ""img"": ""https://devir.es/media/catalog/product/cache/medium/8436625610973-1200-frontflat.jpg"",
                                ""full"": ""https://devir.es/media/catalog/product/8436625610973-1200-frontflat.jpg"",
                                ""isMain"": false,
                                ""type"": ""image""
                            }
                        ]
                    }
                }
            }
            </script>
        </body>
        </html>";

        var extractor = new DevirReleasesExtractor(new System.Net.Http.HttpClient(), NullLogger<DevirReleasesExtractor>.Instance);
        var gallery = extractor.ParseProductGalleryHtml(sampleProductHtml);

        Assert.NotNull(gallery);
        Assert.Equal("https://devir.es/media/catalog/product/8436625610973-1200-face3d.jpg", gallery.CoverImageUrl);
        Assert.Equal("https://devir.es/media/catalog/product/8436625610973-1200-components1.jpg", gallery.TableImageUrl);
        Assert.Equal("https://devir.es/media/catalog/product/8436625610973-1200-backflat.jpg", gallery.BackCoverImageUrl);
        Assert.Equal("https://devir.es/media/catalog/product/8436625610973-1200-frontflat.jpg", gallery.FrontFlatImageUrl);
        Assert.Equal("8436625610973", gallery.Ean);
        Assert.Equal(25.00m, gallery.Pvp);
    }

    [Fact]
    public void ParseCatalogPageHtml_ExtractsCatalogItemsAndDetectsNextPage()
    {
        const string sampleCatalogHtml = @"
        <ol class=""products list items product-items"">
            <li class=""item product product-item"">
                <div class=""product-item-info"">
                    <a href=""https://devir.es/bichos-polilla-tramposa"" class=""product photo product-item-photo"" tabindex=""-1"">
                        <span class=""product-image-container"">
                            <span class=""product-image-wrapper"">
                                <img class=""product-image-photo"" src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436017221138-1200-face3d-copy.jpg"" alt=""Polilla&#x20;Tramposa"" />
                            </span>
                        </span>
                    </a>
                    <div class=""product details product-item-details"">
                        <strong class=""product name product-item-name"">
                            <a class=""product-item-link"" href=""https://devir.es/bichos-polilla-tramposa"">Polilla Tramposa</a>
                        </strong>
                    </div>
                </div>
            </li>
            <li class=""item product product-item"">
                <div class=""product-item-info"">
                    <a href=""https://devir.es/castle-party"" class=""product photo product-item-photo"" tabindex=""-1"">
                        <span class=""product-image-container"">
                            <span class=""product-image-wrapper"">
                                <img class=""product-image-photo"" src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436589622340-1200-face3d-copy.jpg"" alt=""Castle Party"" />
                            </span>
                        </span>
                    </a>
                </div>
            </li>
        </ol>
        <ul class=""items pages-items"">
            <li class=""item pages-item-next"">
                <a class=""action  next"" href=""https://devir.es/catalogo/juegos-de-mesa?p=2"">Siguiente</a>
            </li>
        </ul>";

        var extractor = new DevirReleasesExtractor(new System.Net.Http.HttpClient(), NullLogger<DevirReleasesExtractor>.Instance);
        var result = extractor.ParseCatalogPageHtml(sampleCatalogHtml);

        Assert.NotNull(result);
        Assert.True(result.HasNextPage);
        Assert.Equal(2, result.Items.Count);

        var item1 = result.Items[0];
        Assert.Equal("https://devir.es/bichos-polilla-tramposa", item1.ProductUrl);
        Assert.Equal("Polilla Tramposa", item1.Title);
        Assert.Equal("8436017221138", item1.Ean);
        Assert.Equal("https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436017221138-1200-face3d-copy.jpg", item1.CoverImageUrl);

        var item2 = result.Items[1];
        Assert.Equal("https://devir.es/castle-party", item2.ProductUrl);
        Assert.Equal("Castle Party", item2.Title);
        Assert.Equal("8436589622340", item2.Ean);
    }

    private class MockHttpMessageHandler : System.Net.Http.HttpMessageHandler
    {
        private readonly System.Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> _handler;

        public MockHttpMessageHandler(System.Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request,
            System.Threading.CancellationToken cancellationToken)
        {
            return System.Threading.Tasks.Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task ExtractCatalogPageAsync_RetriesOn403AndSucceedsOnSecondAttempt()
    {
        int callCount = 0;
        System.Uri? capturedReferer = null;

        var handler = new MockHttpMessageHandler(req =>
        {
            callCount++;
            capturedReferer = req.Headers.Referrer;
            if (callCount == 1)
            {
                return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Forbidden);
            }

            return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(@"
                <li class=""item product product-item"">
                    <div class=""product-item-info"">
                        <a href=""https://devir.es/paris"" class=""product photo product-item-photo"">
                            <span class=""product-image-container"">
                                <img class=""product-image-photo"" src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436017221138-1200-face3d.jpg"" alt=""Paris"" />
                            </span>
                        </a>
                    </div>
                </li>")
            };
        });

        var client = new System.Net.Http.HttpClient(handler);
        var extractor = new DevirReleasesExtractor(client, NullLogger<DevirReleasesExtractor>.Instance);

        var result = await extractor.ExtractCatalogPageAsync(page: 2);

        Assert.Equal(2, callCount);
        Assert.True(result.Success);
        Assert.Single(result.Items);
        Assert.Equal("Paris", result.Items[0].Title);
        Assert.NotNull(capturedReferer);
        Assert.Equal("https://devir.es/catalogo/juegos-de-mesa", capturedReferer.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task ExtractCatalogPageAsync_ReturnsFailureWhenBothAttemptsReturn403()
    {
        int callCount = 0;
        var handler = new MockHttpMessageHandler(req =>
        {
            callCount++;
            return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Forbidden);
        });

        var client = new System.Net.Http.HttpClient(handler);
        var extractor = new DevirReleasesExtractor(client, NullLogger<DevirReleasesExtractor>.Instance);

        var result = await extractor.ExtractCatalogPageAsync(page: 2);

        Assert.Equal(2, callCount);
        Assert.False(result.Success);
        Assert.Empty(result.Items);
    }
}

