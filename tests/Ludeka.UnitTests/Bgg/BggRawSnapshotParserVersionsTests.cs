using System.Text.Json;
using Ludeka.Application.Features.Bgg;
using Ludeka.Infrastructure.Bgg;
using Xunit;

namespace Ludeka.UnitTests.Bgg;

public class BggRawSnapshotParserVersionsTests
{
    private const string SamplePowerGridXmlWithVersions = @"
<items termsofuse=""https://boardgamegeek.com/xmlapi/termsofuse"">
  <item type=""boardgame"" id=""2651"">
    <thumbnail>https://cf.geekdo-images.com/thumb.jpg</thumbnail>
    <image>https://cf.geekdo-images.com/image.jpg</image>
    <name type=""primary"" sortindex=""1"" value=""Power Grid"" />
    <name type=""alternate"" sortindex=""1"" value=""Alta Tensión"" />
    <name type=""alternate"" sortindex=""1"" value=""Funkenschlag"" />
    <yearpublished value=""2004"" />
    <link type=""boardgamedesigner"" id=""84"" value=""Friedemann Friese"" />
    <link type=""boardgamepublisher"" id=""2222"" value=""Edge Entertainment"" />
    <versions>
      <item type=""boardgameversion"" id=""21951"">
        <name type=""primary"" sortindex=""1"" value=""Funkenschlag"" />
        <yearpublished value=""2004"" />
        <link type=""boardgameversion"" id=""2651"" value=""Power Grid"" inbound=""true"" />
        <link type=""boardgamepublisher"" id=""37"" value=""2F-Spiele"" />
        <link type=""language"" id=""2194"" value=""German"" />
      </item>
      <item type=""boardgameversion"" id=""21950"">
        <name type=""primary"" sortindex=""1"" value=""Alta Tensión"" />
        <yearpublished value=""2008"" />
        <productcode value=""EDGPG01"" />
        <barcode value=""8435407626515"" />
        <link type=""boardgameversion"" id=""2651"" value=""Power Grid"" inbound=""true"" />
        <link type=""boardgamepublisher"" id=""2222"" value=""Edge Entertainment"" />
        <link type=""language"" id=""2195"" value=""Spanish"" />
      </item>
    </versions>
  </item>
</items>";

    private const string SampleXmlWithoutSpanishVersion = @"
<items>
  <item type=""boardgame"" id=""100"">
    <name type=""primary"" sortindex=""1"" value=""German Only Game"" />
    <versions>
      <item type=""boardgameversion"" id=""101"">
        <name type=""primary"" sortindex=""1"" value=""Deutsches Spiel"" />
        <link type=""language"" id=""2194"" value=""German"" />
      </item>
    </versions>
  </item>
</items>";

    private const string SampleXmlWithoutVersions = @"
<items>
  <item type=""boardgame"" id=""200"">
    <name type=""primary"" sortindex=""1"" value=""Game Without Versions"" />
  </item>
</items>";

    [Fact]
    public void HasVersionsFromJson_ShouldReturnTrue_WhenVersionsExist()
    {
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(SamplePowerGridXmlWithVersions);
        bool hasVersions = BggRawSnapshotParser.HasVersionsFromJson(json);
        Assert.True(hasVersions);
    }

    [Fact]
    public void HasVersionsFromJson_ShouldReturnFalse_WhenNoVersionsNode()
    {
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(SampleXmlWithoutVersions);
        bool hasVersions = BggRawSnapshotParser.HasVersionsFromJson(json);
        Assert.False(hasVersions);
    }

    [Fact]
    public void ExtractSpanishVersionInfoFromJson_ShouldExtractAltaTensionAndEan_FromPowerGrid()
    {
        // Arrange
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(SamplePowerGridXmlWithVersions);

        // Act
        var result = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Alta Tensión", result.Title);
        Assert.Equal("Edge Entertainment", result.Publisher);
        Assert.Equal(2008, result.YearPublished);
        Assert.Equal("8435407626515", result.Ean);
        Assert.Equal("EDGPG01", result.ProductCode);
    }

    [Fact]
    public void ExtractSpanishVersionInfoFromJson_ShouldReturnNull_WhenNoSpanishVersionExists()
    {
        // Arrange
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(SampleXmlWithoutSpanishVersion);

        // Act
        var result = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(json);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ExtractSpanishVersionInfoFromJson_ShouldFallbackToValidEanFromProductCode_WhenBarcodeMissing()
    {
        // Arrange: barcode missing but productcode has an EAN-13
        const string xml = @"
<items>
  <item type=""boardgame"" id=""300"">
    <name type=""primary"" value=""Test Game"" />
    <versions>
      <item type=""boardgameversion"" id=""301"">
        <name type=""primary"" value=""Juego de Prueba"" />
        <productcode value=""8436017220100"" />
        <link type=""language"" value=""Spanish"" />
      </item>
    </versions>
  </item>
</items>";
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(xml);

        // Act
        var result = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Juego de Prueba", result.Title);
        Assert.Equal("8436017220100", result.Ean);
    }

    [Fact]
    public void ExtractSpanishVersionInfoFromJson_ShouldReturnNull_WhenKoreanVersionHasBggLanguageId2195()
    {
        // Caso de regresión crítico: en BGG XMLAPI2 el ID 2195 corresponde a Korean.
        // Nunca debe ser identificado como versión en español.
        const string xml = @"
<items>
  <item type=""boardgame"" id=""342942"">
    <name type=""primary"" value=""Ark Nova: Marine Worlds"" />
    <versions>
      <item type=""boardgameversion"" id=""654321"">
        <name type=""primary"" value=""Angry Lion Korean edition"" />
        <barcode value=""8809641480501"" />
        <link type=""boardgamepublisher"" id=""9999"" value=""Angry Lion Games"" />
        <link type=""language"" id=""2195"" value=""Korean"" />
      </item>
    </versions>
  </item>
</items>";
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(xml);

        var result = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(json);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("Spanish edition", true)]
    [InlineData("Spanish Edition", true)]
    [InlineData("Korean edition", true)]
    [InlineData("Angry Lion Korean edition", true)]
    [InlineData("Edición en español", true)]
    [InlineData("Edición española", true)]
    [InlineData("Edición castellano", true)]
    [InlineData("Devir Spanish edition", true)]
    [InlineData("Maldito Games edition", true)]
    [InlineData("First edition", true)]
    [InlineData("Spanish", true)]
    [InlineData("Español", true)]
    [InlineData("Alta Tensión", false)]
    [InlineData("Ciudadelas", false)]
    [InlineData("Ark Nova: Mundos Marinos", false)]
    [InlineData("Terraforming Mars: Preludio", false)]
    public void IsGenericEditionTitle_ShouldClassifyCorrectly(string title, bool expectedGeneric)
    {
        bool isGeneric = BggRawSnapshotParser.IsGenericEditionTitle(title);
        Assert.Equal(expectedGeneric, isGeneric);
    }

    [Theory]
    [InlineData("Spanish edition", null)]
    [InlineData("Edición en español", null)]
    [InlineData("Angry Lion Korean edition", null)]
    [InlineData("Alta Tensión", "Alta Tensión")]
    [InlineData("Alta Tensión (Edición en español)", "Alta Tensión")]
    [InlineData("Alta Tensión - Spanish edition", "Alta Tensión")]
    [InlineData("Ciudadelas: Edición Deluxe", "Ciudadelas")]
    public void CleanVersionTitle_ShouldStripSuffixOrReturnNull(string rawTitle, string? expectedCleaned)
    {
        string? cleaned = BggRawSnapshotParser.CleanVersionTitle(rawTitle);
        Assert.Equal(expectedCleaned, cleaned);
    }

    [Fact]
    public void ExtractSpanishVersionInfoFromJson_ShouldExtractEanAndPublisher_WithNullTitle_WhenVersionIsGenericSpanishEdition()
    {
        const string xml = @"
<items>
  <item type=""boardgame"" id=""400"">
    <name type=""primary"" value=""Dune: Imperium"" />
    <versions>
      <item type=""boardgameversion"" id=""401"">
        <name type=""primary"" value=""Spanish edition"" />
        <barcode value=""8436017220209"" />
        <link type=""boardgamepublisher"" id=""2222"" value=""Dire Wolf / Asmodee"" />
        <link type=""language"" value=""Spanish"" />
      </item>
    </versions>
  </item>
</items>";
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(xml);

        var result = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(json);

        Assert.NotNull(result);
        Assert.Null(result.Title); // Título no debe ser "Spanish edition"
        Assert.Equal("Dire Wolf / Asmodee", result.Publisher);
        Assert.Equal("8436017220209", result.Ean);
    }

    [Fact]
    public void ExtractSpanishVersionInfoFromJson_ShouldMergeTitleFromLocalizedCandidate_WithEanFromGenericCandidate()
    {
        const string xml = @"
<items>
  <item type=""boardgame"" id=""500"">
    <name type=""primary"" value=""Power Grid"" />
    <versions>
      <item type=""boardgameversion"" id=""501"">
        <name type=""primary"" value=""Spanish edition"" />
        <barcode value=""8435407626515"" />
        <link type=""boardgamepublisher"" id=""2222"" value=""Edge Entertainment"" />
        <link type=""language"" value=""Spanish"" />
      </item>
      <item type=""boardgameversion"" id=""502"">
        <name type=""primary"" value=""Alta Tensión"" />
        <link type=""language"" value=""Spanish"" />
      </item>
    </versions>
  </item>
</items>";
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(xml);

        var result = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(json);

        Assert.NotNull(result);
        Assert.Equal("Alta Tensión", result.Title); // Enriquecido desde la versión localizada
        Assert.Equal("Edge Entertainment", result.Publisher);
        Assert.Equal("8435407626515", result.Ean); // Enriquecido desde la versión con EAN
    }
}

