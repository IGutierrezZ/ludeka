using System;
using System.Linq;
using Ludeka.Infrastructure.Extractors;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class MalditoReleasesExtractorTests
{
    private const string SampleHomeHtml = @"
    <div class=""pagebuilder-column"">
        <figure>
            <img src=""https://tienda.malditogames.com/media/wysiwyg/SQ_Floe.jpg"" alt="""" />
        </figure>
        <div class=""fecha-home""><p>2027</p></div>
    </div>
    <div class=""pagebuilder-column"">
        <figure>
            <img src=""https://tienda.malditogames.com/media/wysiwyg/SQ-sleeping-gods-cielos-lejanos.jpg"" alt="""" />
        </figure>
        <div class=""fecha-home""><p>2027</p></div>
    </div>
    ";

    private const string SampleCatalogHtml = @"
    <div class=""product-item-info"">
        <a class=""product-item-link"" href=""https://tienda.malditogames.com/1300-tianxia-metal-coins.html"">
            Tianxia Metal Coins
        </a>
        <span class=""price"">27,00&nbsp;€</span>
    </div>
    <div class=""product-item-info"">
        <a class=""product-item-link"" href=""https://tienda.malditogames.com/34-comprar-terraforming-mars.html"">
            Terraforming Mars
        </a>
        <span class=""price"">55,00&nbsp;€</span>
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
        <div class=""sev सामूहिक"">
            <figure>
                <img src=""https://tienda.malditogames.com/media/wysiwyg/SQ_Floe.jpg"" alt="""" />
            </figure>
            <div class=""fecha-home""><p>2027</p></div>
        </div>
    </div>
    ";

    [Fact]
    public void ParseHtml_ExtractsSeVieneTitlesAndCatalogPreorders()
    {
        var extractor = new MalditoReleasesExtractor(new System.Net.Http.HttpClient(), NullLogger<MalditoReleasesExtractor>.Instance);

        var results = extractor.ParseHtml(SampleHomeHtml, SampleCatalogHtml);

        Assert.Equal(4, results.Count);

        var floe = results.First(r => r.Title == "Floe");
        Assert.Equal("Maldito Games", floe.Publisher);
        Assert.Equal("2027", floe.TargetDateText);
        Assert.Equal("https://tienda.malditogames.com/media/wysiwyg/SQ_Floe.jpg", floe.CoverImageUrl);

        var sleepingGods = results.First(r => r.Title.Contains("Sleeping Gods"));
        Assert.Equal("Maldito Games", sleepingGods.Publisher);
        Assert.Equal("2027", sleepingGods.TargetDateText);

        var mars = results.First(r => r.Title == "Terraforming Mars");
        Assert.Equal("Maldito Games", mars.Publisher);
        Assert.Equal(55.00m, mars.EstimatedPvp);
        Assert.Equal("https://tienda.malditogames.com/34-comprar-terraforming-mars.html", mars.SourceUrl);
    }

    [Fact]
    public void ParseHtml_ExtractsSectionalGridProducts_WithDatesEanPvpAndReprintStatus()
    {
        var extractor = new MalditoReleasesExtractor(new System.Net.Http.HttpClient(), NullLogger<MalditoReleasesExtractor>.Instance);

        var results = extractor.ParseHtml(SampleFullSectionsHomeHtml, null);

        Assert.Equal(4, results.Count);

        // 1. Últimas novedades: Emblemas
        var emblemas = results.First(r => r.Title == "Emblemas");
        Assert.Equal("8436625618788", emblemas.Ean);
        Assert.Equal(20.00m, emblemas.EstimatedPvp);
        Assert.Equal("8 de octubre", emblemas.TargetDateText);
        Assert.NotNull(emblemas.ReleaseDate);
        Assert.Equal(10, emblemas.ReleaseDate!.Value.Month);
        Assert.Equal(8, emblemas.ReleaseDate!.Value.Day);
        Assert.False(emblemas.IsReprint);
        Assert.False(emblemas.IsMonthOnly);

        // 2. A puntito de llegar: Railway Boom
        var railway = results.First(r => r.Title == "Railway Boom");
        Assert.Equal("8436625619129", railway.Ean);
        Assert.Equal(52.00m, railway.EstimatedPvp);
        Assert.Equal("15 de octubre", railway.TargetDateText);
        Assert.False(railway.IsReprint);

        // 3. Volverán a estar disponibles en breve (Reimpresión): Earthborne Rangers
        var rangers = results.First(r => r.Title == "Earthborne Rangers");
        Assert.Equal("8436578818099", rangers.Ean);
        Assert.Equal(100.00m, rangers.EstimatedPvp);
        Assert.Equal("22 de octubre", rangers.TargetDateText);
        Assert.True(rangers.IsReprint);

        // 4. Lo que se viene: Floe
        var floe = results.First(r => r.Title == "Floe");
        Assert.Equal("2027", floe.TargetDateText);
        Assert.True(floe.IsMonthOnly);
        Assert.Equal(new DateOnly(2027, 1, 1), floe.ReleaseDate);
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
}
