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
}
