using System.Linq;
using System.Xml.Linq;
using Ludeka.Infrastructure.Bgg;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class BggSleeveParserTests
{
    [Theory]
    [InlineData("Standard: 63.5 x 88 mm", 63.5, 88.0, 0, "Standard Card Game")]
    [InlineData("Chimera 57 x 89 mm (110 cards)", 57.0, 89.0, 110, "Chimera / USA")]
    [InlineData("Mini European: 44x68mm", 44.0, 68.0, 0, "Mini Euro")]
    [InlineData("Tarot (70 x 120 mm) 84 cartas", 70.0, 120.0, 84, "Tarot Grande")]
    public void ParseSingleSleeve_ExtractsDimensionsAndQuantities(string text, double expectedW, double expectedH, int expectedQty, string expectedFormat)
    {
        var sleeve = BggSleeveParser.ParseSingleSleeve(text);

        Assert.NotNull(sleeve);
        Assert.Equal(expectedW, sleeve.WidthMm);
        Assert.Equal(expectedH, sleeve.HeightMm);
        Assert.Equal(expectedQty, sleeve.CardCount);
        Assert.Equal(expectedFormat, sleeve.FormatName);
    }

    [Fact]
    public void ParseSingleSleeve_ReturnsZeroCardCount_WhenNoQuantitySpecified()
    {
        var sleeve = BggSleeveParser.ParseSingleSleeve("Standard: 63.5 x 88 mm");
        Assert.NotNull(sleeve);
        Assert.Equal(0, sleeve.CardCount);
    }

    [Theory]
    [InlineData("2.5 x 3.5")]
    [InlineData("10 x 20 mm")]
    [InlineData("500 x 800 mm")]
    public void ParseSingleSleeve_ReturnsNull_WhenDimensionsArePlausibilityViolated(string text)
    {
        var sleeve = BggSleeveParser.ParseSingleSleeve(text);
        Assert.Null(sleeve);
    }

    [Theory]
    [InlineData("2.5\" x 3.5\"", true, 63.5, 88.9)]
    [InlineData("2.5 x 3.5 inches", true, 63.5, 88.9)]
    [InlineData("0.5\" x 1.0\"", false, 0, 0)]
    public void ParseSingleSleeve_ConvertsInchesToMm_OrRejectsIfInvalid(string text, bool isValid, double expectedW, double expectedH)
    {
        var sleeve = BggSleeveParser.ParseSingleSleeve(text);
        if (isValid)
        {
            Assert.NotNull(sleeve);
            Assert.InRange(sleeve.WidthMm, expectedW - 0.1, expectedW + 0.1);
            Assert.InRange(sleeve.HeightMm, expectedH - 0.1, expectedH + 0.1);
        }
        else
        {
            Assert.Null(sleeve);
        }
    }

    [Fact]
    public void ParseSingleSleeve_RespectsExplicitQuantity_WhenGiven()
    {
        var sleeve = BggSleeveParser.ParseSingleSleeve("56 x 87 mm", "73");

        Assert.NotNull(sleeve);
        Assert.Equal(56.0, sleeve.WidthMm);
        Assert.Equal(87.0, sleeve.HeightMm);
        Assert.Equal(73, sleeve.CardCount);
    }

    [Fact]
    public void ParseSleeves_ParsesMultipleSleeveLinks_FromXml()
    {
        string xml = @"
        <item type=""boardgame"" id=""174430"">
            <link type=""boardgamecardsleeve"" id=""101"" value=""Mini European: 44 x 68 mm"" qty=""73"" />
            <link type=""boardgamecardsleeve"" id=""102"" value=""Large: 65 x 100 mm"" qty=""12"" />
            <link type=""boardgamedesigner"" id=""201"" value=""Antoine Bauza"" />
        </item>";

        var element = XElement.Parse(xml);
        var sleeves = BggSleeveParser.ParseSleeves(element);

        Assert.Equal(2, sleeves.Count);

        var miniEuro = sleeves.FirstOrDefault(s => s.WidthMm == 44.0);
        Assert.NotNull(miniEuro);
        Assert.Equal(68.0, miniEuro.HeightMm);
        Assert.Equal(73, miniEuro.CardCount);
        Assert.Equal("Mini Euro", miniEuro.FormatName);

        var large = sleeves.FirstOrDefault(s => s.WidthMm == 65.0);
        Assert.NotNull(large);
        Assert.Equal(100.0, large.HeightMm);
        Assert.Equal(12, large.CardCount);
        Assert.Equal("Tarot / 7 Wonders", large.FormatName);
    }
}
