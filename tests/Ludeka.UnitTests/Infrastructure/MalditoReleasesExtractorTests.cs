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
}
