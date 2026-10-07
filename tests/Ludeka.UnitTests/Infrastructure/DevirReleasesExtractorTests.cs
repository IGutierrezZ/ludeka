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
            <img class=""mgz-hover-main"" src=""https://devirinvestments.s3.eu-west-1.amazonaws.com/img/wysiwyg/Proximos-lanzamientos/8436625616159-1200.jpg"" alt=""En el Abismo"" />
        </div>
        <p style=""text-align: center;""><span><strong>Noviembre 2026</strong></span></p>
        <p style=""text-align: center;""><span><strong>EN EL ABISMO</strong></span></p>
    </div>
    ";

    [Fact]
    public void ParseHtml_ExtractsReleasesAndEanFromImageFilename()
    {
        var extractor = new DevirReleasesExtractor(new System.Net.Http.HttpClient(), NullLogger<DevirReleasesExtractor>.Instance);

        var results = extractor.ParseHtml(SampleDevirHtml);

        Assert.Equal(3, results.Count);

        var vileborn = results.First(r => r.Title == "VILEBORN");
        Assert.Equal("Devir", vileborn.Publisher);
        Assert.Equal("8436625615992", vileborn.Ean);
        Assert.Equal("2026", vileborn.TargetDateText);
        Assert.Equal("https://devirinvestments.s3.eu-west-1.amazonaws.com/img/wysiwyg/Proximos-lanzamientos/8436625615992-1200-frontflat.jpg", vileborn.CoverImageUrl);

        var daggerheart = results.First(r => r.Title == "DAGGERHEART");
        Assert.Equal("Devir", daggerheart.Publisher);
        Assert.Null(daggerheart.Ean);
        Assert.Equal("2027", daggerheart.TargetDateText);

        var abismo = results.First(r => r.Title == "EN EL ABISMO");
        Assert.Equal("8436625616159", abismo.Ean);
        Assert.Equal("Noviembre 2026", abismo.TargetDateText);
        Assert.NotNull(abismo.ReleaseDate);
        Assert.Equal(2026, abismo.ReleaseDate!.Value.Year);
        Assert.Equal(11, abismo.ReleaseDate!.Value.Month);
    }
}
