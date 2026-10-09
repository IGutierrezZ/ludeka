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

public class ArrakisReleasesExtractorTests
{
    private const string SampleHomeWithMixedItemsHtml = @"
    <section class=""elementor-section"">
        <!-- Ítem 1: Ya disponible (debe ser DESCARTADO según regla de usuario) -->
        <div class=""elementor-column"">
            <h2 class=""elementor-heading-title""><a href=""https://arrakisgames.com/underwater-cities"">UNDERWATER CITIES (Reimpresión)</a></h2>
            <div class=""elementor-widget-image"">
                <img src=""https://arrakisgames.com/wp-content/uploads/Underwater-Front.jpg"" />
            </div>
            <div class=""elementor-widget-text-editor"">
                <p>Ya disponible!</p>
            </div>
        </div>

        <!-- Ítem 2: Reimpresión futura en noviembre (debe ser INCLUIDO) -->
        <div class=""elementor-column"">
            <h2 class=""elementor-heading-title""><a href=""https://arrakisgames.com/spirit-island/"">SPIRIT ISLAND (Reimpresión)</a></h2>
            <div class=""elementor-widget-image"">
                <img src=""https://arrakisgames.com/wp-content/uploads/Spirit_Producto.png"" />
            </div>
            <div class=""elementor-widget-text-editor"">
                <p>En Noviembre</p>
            </div>
        </div>

        <!-- Ítem 3: Novedad futura con mes y año explícito (debe ser INCLUIDO) -->
        <div class=""elementor-column"">
            <h2 class=""elementor-heading-title""><a href=""https://arrakisgames.com/el-valle-de-los-mercaderes/"">EL VALLE DE LOS MERCADERES</a></h2>
            <div class=""elementor-widget-image"">
                <img src=""https://arrakisgames.com/wp-content/uploads/Dale-of-Merchants.webp"" />
            </div>
            <div class=""elementor-widget-text-editor"">
                <p>Junio 2027</p>
            </div>
        </div>

        <!-- Ítem 4: Próximamente (debe ser INCLUIDO) -->
        <div class=""elementor-column"">
            <h2 class=""elementor-heading-title""><a href=""https://arrakisgames.com/orleans-big-box/"">ORLEANS BIG BOX (Reimpresión)</a></h2>
            <div class=""elementor-widget-image"">
                <img src=""https://arrakisgames.com/wp-content/uploads/OBB_portada.png"" />
            </div>
            <div class=""elementor-widget-text-editor"">
                <p>Próximamente</p>
            </div>
        </div>

        <!-- Ítem de cabecera del sistema (debe ser IGNORADO) -->
        <div class=""elementor-column"">
            <h2 class=""elementor-heading-title"">Síguenos</h2>
            <div class=""elementor-widget-text-editor""><p>Redes sociales</p></div>
        </div>
    </section>";

    private const string SampleFichaSpiritIslandHtml = @"
    <article class=""post"">
        <h2 class=""elementor-heading-title"">Reimpresión: Noviembre 2026</h2>
        <div class=""woocommerce"">
            <img src=""https://arrakisgames.com/wp-content/uploads/2024/12/Spirit_Producto.png"" class=""attachment-woocommerce_thumbnail"" alt=""Spirit Island"" />
            <h2 class=""woocommerce-loop-product__title"">Spirit Island</h2>
        </div>
        <ul class=""elementor-icon-list-items"">
            <li class=""elementor-icon-list-item""><span class=""elementor-icon-list-text"">Autor: R. Eric Reuss</span></li>
            <li class=""elementor-icon-list-item""><span class=""elementor-icon-list-text"">Jugadores: De 1 a 4</span></li>
            <li class=""elementor-icon-list-item""><span class=""elementor-icon-list-text"">PVPr: 84.95€</span></li>
            <li class=""elementor-icon-list-item""><span class=""elementor-icon-list-text"">EAN: 8421005001106</span></li>
        </ul>
        <ul class=""elementor-icon-list-items"">
            <li class=""elementor-icon-list-item"">
                <a href=""https://boardgamegeek.com/boardgame/162886/spirit-island"" target=""_blank"">
                    <span class=""elementor-icon-list-text"">Enlace a la BGG</span>
                </a>
            </li>
        </ul>
    </article>";

    private const string SampleCatalogHtml = @"
    <div class=""elementor-widget-wrap"">
        <a href=""https://arrakisgames.com/aquaria/"" target=""_blank"">
            <img src=""https://arrakisgames.com/wp-content/uploads/Aquaria-portada.png"" />
        </a>
        <h2 class=""elementor-heading-title""><a href=""https://arrakisgames.com/aquaria/"" target=""_blank"">Aquaria</a></h2>
    </div>
    <div class=""elementor-widget-wrap"">
        <a href=""https://arrakisgames.com/boonlake"" target=""_blank"">
            <img src=""https://arrakisgames.com/wp-content/uploads/Boonlake_caja.jpg"" />
        </a>
        <h2 class=""elementor-heading-title""><a href=""https://arrakisgames.com/boonlake"" target=""_blank"">Boonlake</a></h2>
    </div>";

    private readonly ArrakisReleasesExtractor _extractor;

    public ArrakisReleasesExtractorTests()
    {
        var httpClient = new HttpClient(new MockHttpMessageHandler());
        _extractor = new ArrakisReleasesExtractor(httpClient, NullLogger<ArrakisReleasesExtractor>.Instance);
    }

    [Fact]
    public void ParseHtml_DiscardsAvailableItems_AndExtractsUpcomingReleasesAndReprints()
    {
        var items = _extractor.ParseHtml(SampleHomeWithMixedItemsHtml, null);

        // Underwater Cities debe ser descartado porque está marcado como "Ya disponible!"
        Assert.DoesNotContain(items, i => i.Title.Contains("Underwater Cities", StringComparison.OrdinalIgnoreCase));

        // Debe contener los otros 3 ítems
        Assert.Equal(3, items.Count);

        var spirit = items.First(i => i.Title.Equals("Spirit Island", StringComparison.OrdinalIgnoreCase));
        Assert.True(spirit.IsReprint);
        Assert.Equal("En Noviembre", spirit.TargetDateText);
        Assert.Equal("https://arrakisgames.com/spirit-island/", spirit.SourceUrl);
        Assert.Equal("Arrakis Games", spirit.Publisher);

        var valle = items.First(i => i.Title.Equals("El Valle de los Mercaderes", StringComparison.OrdinalIgnoreCase));
        Assert.False(valle.IsReprint);
        Assert.Equal("Junio 2027", valle.TargetDateText);
        Assert.Equal(new DateOnly(2027, 6, 1), valle.ReleaseDate);
        Assert.True(valle.IsMonthOnly);

        var orleans = items.First(i => i.Title.Equals("Orleans Big Box", StringComparison.OrdinalIgnoreCase));
        Assert.True(orleans.IsReprint);
        Assert.Equal("Próximamente", orleans.TargetDateText);
    }

    [Fact]
    public void ParseHtml_RealElementorLayout_ExtractsAllUpcomingGamesAndThumbs()
    {
        var html = @"
        <h2 class=""elementor-heading-title elementor-size-default"">Próximos lanzamientos</h2>
        <section class=""elementor-section elementor-top-section elementor-element"">
            <div class=""elementor-column elementor-col-25 elementor-top-column elementor-element"">
                <div class=""elementor-widget-wrap elementor-element-populated"">
                    <section class=""elementor-section elementor-inner-section elementor-element"">
                        <div class=""elementor-column elementor-col-100 elementor-inner-column elementor-element"">
                            <div class=""elementor-widget-container"">
                                <h2 class=""elementor-heading-title elementor-size-default""><a href=""https://arrakisgames.com/pilgrims-curiosas-aventuras/"">PILGRIMS, CURIOSAS AVENTURAS</a></h2>
                            </div>
                        </div>
                    </section>
                    <div class=""elementor-element elementor-widget elementor-widget-image"">
                        <div class=""elementor-widget-container"">
                            <a href=""https://arrakisgames.com/pilgrims-curiosas-aventuras/"" target=""_blank"">
                                <img decoding=""async"" src=""https://arrakisgames.com/wp-content/uploads/elementor/thumbs/Pilgrims_portada_web.png"" />
                            </a>
                        </div>
                    </div>
                    <div class=""elementor-element elementor-widget elementor-widget-text-editor"">
                        <div class=""elementor-widget-container"">
                            <p>En Noviembre</p>
                        </div>
                    </div>
                </div>
            </div>
            <div class=""elementor-column elementor-col-25 elementor-top-column elementor-element"">
                <div class=""elementor-widget-wrap elementor-element-populated"">
                    <section class=""elementor-section elementor-inner-section elementor-element"">
                        <div class=""elementor-column elementor-col-100 elementor-inner-column elementor-element"">
                            <div class=""elementor-widget-container"">
                                <h2 class=""elementor-heading-title elementor-size-default""><a href=""https://arrakisgames.com/spirit-island/"">SPIRIT ISLAND (Reimpresión)</a></h2>
                            </div>
                        </div>
                    </section>
                    <div class=""elementor-element elementor-widget elementor-widget-image"">
                        <div class=""elementor-widget-container"">
                            <img decoding=""async"" src=""https://arrakisgames.com/wp-content/uploads/elementor/thumbs/SpiritIsland-Cover.jpg"" />
                        </div>
                    </div>
                    <div class=""elementor-element elementor-widget elementor-widget-text-editor"">
                        <div class=""elementor-widget-container"">
                            <p>En Noviembre</p>
                        </div>
                    </div>
                </div>
            </div>
            <div class=""elementor-column elementor-col-25 elementor-top-column elementor-element"">
                <div class=""elementor-widget-wrap elementor-element-populated"">
                    <section class=""elementor-section elementor-inner-section elementor-element"">
                        <div class=""elementor-column elementor-col-100 elementor-inner-column elementor-element"">
                            <div class=""elementor-widget-container"">
                                <h2 class=""elementor-heading-title elementor-size-default"">MYTHOLOGIES</h2>
                            </div>
                        </div>
                    </section>
                    <div class=""elementor-element elementor-widget elementor-widget-image"">
                        <div class=""elementor-widget-container"">
                            <img decoding=""async"" src=""https://arrakisgames.com/wp-content/uploads/elementor/thumbs/Mythologies.jpg"" />
                        </div>
                    </div>
                    <div class=""elementor-element elementor-widget elementor-widget-text-editor"">
                        <div class=""elementor-widget-container"">
                            <p>Próximamente</p>
                        </div>
                    </div>
                </div>
            </div>
        </section>";

        var items = _extractor.ParseHtml(html, null);

        Assert.Equal(3, items.Count);

        var pilgrims = items.First(i => i.Title.Contains("Pilgrims", StringComparison.OrdinalIgnoreCase));
        Assert.False(pilgrims.IsReprint);
        Assert.Equal("En Noviembre", pilgrims.TargetDateText);
        Assert.Equal("https://arrakisgames.com/pilgrims-curiosas-aventuras/", pilgrims.SourceUrl);
        Assert.Equal("https://arrakisgames.com/wp-content/uploads/elementor/thumbs/Pilgrims_portada_web.png", pilgrims.CoverImageUrl);

        var spirit = items.First(i => i.Title.Contains("Spirit Island", StringComparison.OrdinalIgnoreCase));
        Assert.True(spirit.IsReprint);
        Assert.Equal("En Noviembre", spirit.TargetDateText);
        Assert.Equal("https://arrakisgames.com/wp-content/uploads/elementor/thumbs/SpiritIsland-Cover.jpg", spirit.CoverImageUrl);

        var myth = items.First(i => i.Title.Contains("Mythologies", StringComparison.OrdinalIgnoreCase));
        Assert.False(myth.IsReprint);
        Assert.Equal("Próximamente", myth.TargetDateText);
        Assert.Equal("https://arrakisgames.com/wp-content/uploads/elementor/thumbs/Mythologies.jpg", myth.CoverImageUrl);
    }

    [Fact]
    public void ParseProductFicha_ExtractsEanPvpBggAndCover()
    {
        var details = _extractor.ParseProductFichaHtml(SampleFichaSpiritIslandHtml, "https://arrakisgames.com/spirit-island/");

        Assert.NotNull(details);
        Assert.Equal("8421005001106", details.Ean);
        Assert.Equal(84.95m, details.Pvp);
        Assert.Equal(162886, details.BggId);
        Assert.Equal("https://boardgamegeek.com/boardgame/162886/spirit-island", details.BggUrl);
        Assert.Equal("https://arrakisgames.com/wp-content/uploads/2024/12/Spirit_Producto.png", details.CoverImageUrl);
        Assert.Contains("Noviembre 2026", details.StatusText);
    }

    [Fact]
    public void ParseCatalogHtml_ExtractsProductLinks()
    {
        var items = _extractor.ParseCatalogHtml(SampleCatalogHtml);

        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.Title == "Aquaria" && i.ProductUrl.Contains("aquaria"));
        Assert.Contains(items, i => i.Title == "Boonlake" && i.ProductUrl.Contains("boonlake"));
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html></html>")
            });
        }
    }
}
