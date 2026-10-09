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
    [InlineData("Combo Games edition", true)]
    [InlineData("First edition", true)]
    [InlineData("2nd edition", true)]
    [InlineData("Retail edition", true)]
    [InlineData("Deluxe edition", true)]
    [InlineData("Kickstarter edition", true)]
    [InlineData("Multilingual edition", true)]
    [InlineData("International edition", true)]
    [InlineData("ENG/GER/FRE/SPA edition", true)]
    [InlineData("ENG/SPA edition", true)]
    [InlineData("ES/EN edition", true)]
    [InlineData("CAT/ENG/ITA/POR/SPA edition", true)]
    [InlineData("EN/FR/GE/IT/NL/SP edition", true)]
    [InlineData("EN/JA/KO Cube box edition", true)]
    [InlineData("Chilean/Colombian edition", true)]
    [InlineData("Print & Play edition", true)]
    [InlineData("Z-Man Spanish edition", true)]
    [InlineData("Z-Man edition", true)]
    [InlineData("Iberian edition", true)]
    [InlineData("Spanish", true)]
    [InlineData("Español", true)]
    [InlineData("Alta Tensión", false)]
    [InlineData("Ciudadelas", false)]
    [InlineData("Queen Alice", false)]
    [InlineData("Ark Nova: Mundos Marinos", false)]
    [InlineData("Terraforming Mars: Preludio", false)]
    [InlineData("Pandemic Legacy: Segunda temporada", false)]
    public void IsGenericEditionTitle_ShouldClassifyCorrectly(string title, bool expectedGeneric)
    {
        bool isGeneric = BggRawSnapshotParser.IsGenericEditionTitle(title);
        Assert.Equal(expectedGeneric, isGeneric);
    }

    [Theory]
    [InlineData("Spanish edition", null)]
    [InlineData("Edición en español", null)]
    [InlineData("Angry Lion Korean edition", null)]
    [InlineData("ENG/GER/FRE/SPA edition", null)]
    [InlineData("ENG/SPA edition", null)]
    [InlineData("CAT/ENG/ITA/POR/SPA edition", null)]
    [InlineData("Multilingual edition", null)]
    [InlineData("Retail edition", null)]
    [InlineData("Z-Man Spanish edition", null)]
    [InlineData("Iberian edition", null)]
    [InlineData("Print & Play edition", null)]
    [InlineData("Alta Tensión", "Alta Tensión")]
    [InlineData("Alta Tensión (Edición en español)", "Alta Tensión")]
    [InlineData("Alta Tensión - Spanish edition", "Alta Tensión")]
    [InlineData("Ciudadelas: Edición Deluxe", "Ciudadelas")]
    [InlineData("Queen Alice - ENG/GER/FRE/SPA edition", "Queen Alice")]
    [InlineData("Ark Nova: Mundo Marino - Spanish edition (2024)", "Ark Nova: Mundo Marino")]
    [InlineData("Código 5 - Spanish edition (2026)", "Código 5")]
    [InlineData("Código 5 - Spanish edition", "Código 5")]
    [InlineData("Spanish edition (2024)", null)]
    [InlineData("Pandemic Legacy: Segunda temporada", "Pandemic Legacy: Segunda temporada")]
    public void CleanVersionTitle_ShouldStripSuffixOrReturnNull(string rawTitle, string? expectedCleaned)
    {
        string? cleaned = BggRawSnapshotParser.CleanVersionTitle(rawTitle);
        Assert.Equal(expectedCleaned, cleaned);
    }

    [Fact]
    public void ExtractSpanishVersionInfoFromJson_ShouldExtractPandemicLegacySegundaTemporadaAndDevir_WhenVersionHasZManAndDevirEditions()
    {
        const string xml = @"
<items>
  <item type=""boardgame"" id=""221107"">
    <name type=""primary"" value=""Pandemic Legacy: Season 2"" />
    <name type=""alternate"" value=""Pandemic Legacy: Segunda temporada"" />
    <versions>
      <item type=""boardgameversion"" id=""375764"">
        <name type=""primary"" value=""Devir Spanish edition"" />
        <canonicalname value=""Pandemic Legacy: Segunda temporada"" />
        <link type=""boardgamepublisher"" id=""2366"" value=""Devir"" />
        <link type=""language"" value=""Spanish"" />
      </item>
      <item type=""boardgameversion"" id=""428178"">
        <name type=""primary"" value=""Z-Man Spanish edition"" />
        <canonicalname value=""Pandemic Legacy: Segunda temporada"" />
        <link type=""boardgamepublisher"" id=""538"" value=""Z-Man Games"" />
        <link type=""language"" value=""Spanish"" />
      </item>
    </versions>
  </item>
</items>";
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(xml);

        var result = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(json);

        Assert.NotNull(result);
        Assert.Equal("Pandemic Legacy: Segunda temporada", result.Title);
        Assert.Equal("Devir", result.Publisher);
    }

    [Fact]
    public void ExtractSpanishVersionInfoFromJson_ShouldExtractBeaconPatrol_WhenVersionNameIsIberianEditionWithCanonicalName()
    {
        const string xml = @"
<items>
  <item type=""boardgame"" id=""362976"">
    <name type=""primary"" value=""Beacon Patrol"" />
    <versions>
      <item type=""boardgameversion"" id=""700000"">
        <name type=""primary"" value=""Iberian edition"" />
        <canonicalname value=""Beacon Patrol"" />
        <link type=""boardgamepublisher"" id=""2366"" value=""Devir"" />
        <link type=""language"" value=""Spanish"" />
      </item>
    </versions>
  </item>
</items>";
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(xml);

        var result = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(json);

        Assert.NotNull(result);
        Assert.Equal("Beacon Patrol", result.Title);
        Assert.Equal("Devir", result.Publisher);
    }

    [Fact]
    public void ExtractSpanishVersionInfoFromJson_ShouldExtractCodigo5AndLudilo_WhenVersionIsSpanishEditionOfGotFive()
    {
        const string xml = @"
<items>
  <item type=""boardgame"" id=""453526"">
    <name type=""primary"" value=""Got Five!"" />
    <versions>
      <item type=""boardgameversion"" id=""778899"">
        <name type=""primary"" value=""Código 5 - Spanish edition (2026)"" />
        <link type=""boardgamepublisher"" id=""12345"" value=""Lúdilo"" />
        <link type=""language"" id=""2194"" value=""Spanish"" />
        <productcode>83162</productcode>
      </item>
    </versions>
  </item>
</items>";
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(xml);

        var result = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(json);

        Assert.NotNull(result);
        Assert.Equal("Código 5", result.Title);
        Assert.Equal("Lúdilo", result.Publisher);
    }

    [Fact]
    public void ExtractSpanishVersionInfoFromJson_ShouldReturnNullTitle_WhenVersionIsEngGerFreSpaEdition()
    {
        const string xml = @"
<items>
  <item type=""boardgame"" id=""456236"">
    <name type=""primary"" value=""Queen Alice"" />
    <versions>
      <item type=""boardgameversion"" id=""776699"">
        <name type=""primary"" value=""ENG/GER/FRE/SPA edition"" />
        <link type=""boardgamepublisher"" id=""56127"" value=""Combo Games (II)"" />
        <link type=""language"" id=""2184"" value=""English"" />
        <link type=""language"" id=""2194"" value=""Spanish"" />
      </item>
    </versions>
  </item>
</items>";
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(xml);

        var result = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(json);

        Assert.NotNull(result);
        Assert.Null(result.Title); // No debe asumir 'ENG/GER/FRE/SPA edition' como título comercial en español
        Assert.Equal("Combo Games (II)", result.Publisher);
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

    [Fact]
    public void ExtractSpanishVersionInfoFromJson_ShouldExtractMundoMarino_WhenVersionHasYearSuffix()
    {
        const string xml = @"
<items>
  <item type=""boardgame"" id=""368966"">
    <name type=""primary"" value=""Ark Nova: Marine Worlds"" />
    <versions>
      <item type=""boardgameversion"" id=""711122"">
        <name type=""primary"" value=""Ark Nova: Mundo Marino - Spanish edition (2024)"" />
        <link type=""boardgamepublisher"" id=""34501"" value=""Maldito Games"" />
        <link type=""language"" value=""Spanish"" />
      </item>
    </versions>
  </item>
</items>";
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(xml);

        var result = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(json);

        Assert.NotNull(result);
        Assert.Equal("Ark Nova: Mundo Marino", result.Title);
        Assert.Equal("Maldito Games", result.Publisher);
    }

    [Fact]
    public void ExtractSpanishVersionInfoFromJson_ShouldExtractCoverAndThumbnail_WhenPresentInVersion()
    {
        const string xml = @"
<items>
  <item type=""boardgame"" id=""1001"">
    <name type=""primary"" value=""Terraforming Mars"" />
    <image>https://cf.geekdo-images.com/original_cover.jpg</image>
    <thumbnail>https://cf.geekdo-images.com/original_thumb.jpg</thumbnail>
    <versions>
      <item type=""boardgameversion"" id=""2001"">
        <name type=""primary"" value=""Terraforming Mars"" />
        <image>https://cf.geekdo-images.com/spanish_cover.jpg</image>
        <thumbnail>https://cf.geekdo-images.com/spanish_thumb.jpg</thumbnail>
        <link type=""boardgamepublisher"" value=""Maldito Games"" />
        <link type=""language"" value=""Spanish"" />
      </item>
    </versions>
  </item>
</items>";
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(xml);

        var result = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(json);

        Assert.NotNull(result);
        Assert.Equal("https://cf.geekdo-images.com/spanish_cover.jpg", result.CoverImageUrl);
        Assert.Equal("https://cf.geekdo-images.com/spanish_thumb.jpg", result.ThumbnailUrl);
        Assert.Equal("Maldito Games", result.Publisher);
    }

    [Fact]
    public void ExtractSpanishVersionInfoFromJson_ShouldNormalizeProtocolRelativeUrl_WhenStartingWithDoubleSlash()
    {
        const string xml = @"
<items>
  <item type=""boardgame"" id=""1002"">
    <name type=""primary"" value=""Root"" />
    <versions>
      <item type=""boardgameversion"" id=""2002"">
        <name type=""primary"" value=""Root"" />
        <image>//cf.geekdo-images.com/root_es.jpg</image>
        <thumbnail>//cf.geekdo-images.com/root_es_thumb.jpg</thumbnail>
        <link type=""boardgamepublisher"" value=""2Tomatoes Games"" />
        <link type=""language"" value=""Spanish"" />
      </item>
    </versions>
  </item>
</items>";
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(xml);

        var result = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(json);

        Assert.NotNull(result);
        Assert.Equal("https://cf.geekdo-images.com/root_es.jpg", result.CoverImageUrl);
        Assert.Equal("https://cf.geekdo-images.com/root_es_thumb.jpg", result.ThumbnailUrl);
    }

    [Fact]
    public void ExtractSpanishVersionInfoFromJson_ShouldConsolidateCoverImage_WhenBestEanCandidateLacksCoverButAnotherHasIt()
    {
        const string xml = @"
<items>
  <item type=""boardgame"" id=""1003"">
    <name type=""primary"" value=""Dune: Imperium"" />
    <versions>
      <item type=""boardgameversion"" id=""3001"">
        <name type=""primary"" value=""Dune: Imperium"" />
        <barcode value=""8435407626515"" />
        <link type=""boardgamepublisher"" value=""Asmodee"" />
        <link type=""language"" value=""Spanish"" />
      </item>
      <item type=""boardgameversion"" id=""3002"">
        <name type=""primary"" value=""Dune: Imperium"" />
        <image>https://cf.geekdo-images.com/dune_es.jpg</image>
        <thumbnail>https://cf.geekdo-images.com/dune_es_thumb.jpg</thumbnail>
        <link type=""boardgamepublisher"" value=""Asmodee"" />
        <link type=""language"" value=""Spanish"" />
      </item>
    </versions>
  </item>
</items>";
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(xml);

        var result = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(json);

        Assert.NotNull(result);
        Assert.Equal("8435407626515", result.Ean);
        Assert.Equal("https://cf.geekdo-images.com/dune_es.jpg", result.CoverImageUrl);
        Assert.Equal("https://cf.geekdo-images.com/dune_es_thumb.jpg", result.ThumbnailUrl);
    }
}

