using System.Linq;
using System.Xml.Linq;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Bgg;
using Xunit;

namespace Ludeka.UnitTests.Bgg;

public class BggXmlParserTests
{
    private const string SampleWingspanXml = @"
<items termsofuse=""https://boardgamegeek.com/xmlapi/termsofuse"">
  <item type=""boardgame"" id=""266192"">
    <thumbnail>https://cf.geekdo-images.com/thumb.jpg</thumbnail>
    <image>https://cf.geekdo-images.com/image.jpg</image>
    <name type=""primary"" sortindex=""1"" value=""Wingspan"" />
    <name type=""alternate"" sortindex=""1"" value=""Wingspan (Edición en español)"" />
    <description>Wingspan is a competitive bird-collection board game.&amp;#10;</description>
    <yearpublished value=""2019"" />
    <minplayers value=""1"" />
    <maxplayers value=""5"" />
    <poll name=""suggested_numplayers"" title=""User Suggested Number of Players"" totalvotes=""1250"">
      <results numplayers=""1"">
        <result value=""Best"" numvotes=""210"" />
        <result value=""Recommended"" numvotes=""580"" />
        <result value=""Not Recommended"" numvotes=""95"" />
      </results>
      <results numplayers=""2"">
        <result value=""Best"" numvotes=""850"" />
        <result value=""Recommended"" numvotes=""390"" />
        <result value=""Not Recommended"" numvotes=""35"" />
      </results>
      <results numplayers=""3"">
        <result value=""Best"" numvotes=""920"" />
        <result value=""Recommended"" numvotes=""310"" />
        <result value=""Not Recommended"" numvotes=""20"" />
      </results>
      <results numplayers=""4"">
        <result value=""Best"" numvotes=""420"" />
        <result value=""Recommended"" numvotes=""680"" />
        <result value=""Not Recommended"" numvotes=""90"" />
      </results>
      <results numplayers=""5"">
        <result value=""Best"" numvotes=""70"" />
        <result value=""Recommended"" numvotes=""340"" />
        <result value=""Not Recommended"" numvotes=""620"" />
      </results>
    </poll>
    <poll name=""suggested_playerage"" title=""User Suggested Player Age"" totalvotes=""180"">
      <results>
        <result value=""8"" numvotes=""25"" />
        <result value=""10"" numvotes=""95"" />
        <result value=""12"" numvotes=""40"" />
        <result value=""14"" numvotes=""20"" />
      </results>
    </poll>
    <poll name=""language_dependence"" title=""Language Dependence"" totalvotes=""95"">
      <results>
        <result level=""1"" value=""No necessary in-game text"" numvotes=""2"" />
        <result level=""2"" value=""Some necessary text - easily memorized or small crib sheet"" numvotes=""15"" />
        <result level=""3"" value=""Moderate in-game text - need deciphering or cards"" numvotes=""70"" />
        <result level=""4"" value=""Extensive use of text - massive conversion needed to be playable"" numvotes=""8"" />
      </results>
    </poll>
    <playingtime value=""70"" />
    <minplaytime value=""40"" />
    <maxplaytime value=""70"" />
    <minage value=""10"" />
    <link type=""boardgamedesigner"" id=""1111"" value=""Elizabeth Hargrave"" />
    <link type=""boardgamepublisher"" id=""2222"" value=""Maldito Games"" />
    <statistics page=""1"">
      <ratings>
        <usersrated value=""85000"" />
        <average value=""8.056"" />
        <bayesaverage value=""7.96"" />
        <ranks>
          <rank type=""subtype"" id=""1"" name=""boardgame"" friendlyname=""Board Game Rank"" value=""28"" bayesaverage=""7.96"" />
        </ranks>
      </ratings>
    </statistics>
  </item>
</items>";

    [Fact]
    public void ParseGameXml_ShouldExtractGameDetailsAccurately()
    {
        // Arrange
        var doc = XDocument.Parse(SampleWingspanXml);

        // Act
        var game = BggXmlParser.ParseItem(doc.Root!.Element("item")!);

        // Assert
        Assert.NotNull(game);
        Assert.Equal(266192, game.BggId);
        Assert.Equal("Wingspan", game.OriginalTitle);
        Assert.Equal("Wingspan", game.SpanishTitle);
        Assert.Equal(2019, game.YearPublished);
        Assert.Equal("Elizabeth Hargrave", game.Designer);
        Assert.Equal("Maldito Games", game.Publisher);
        Assert.Equal(28, game.BggRank);
        Assert.True(game.BggRating >= 8.0);
    }

    [Fact]
    public void ParseGameXml_ShouldExtractScalabilityTrafficLightAndIdeal()
    {
        // Arrange
        var doc = XDocument.Parse(SampleWingspanXml);

        // Act
        var game = BggXmlParser.ParseItem(doc.Root!.Element("item")!);

        // Assert
        Assert.NotNull(game);
        Assert.Equal(5, game.Scalability.Count);

        var twoPlayers = game.Scalability.First(s => s.PlayerCount == 2);
        Assert.Equal(ScalabilityStatus.MustPlay, twoPlayers.Status);

        var fivePlayers = game.Scalability.First(s => s.PlayerCount == 5);
        Assert.Equal(ScalabilityStatus.NotRecommended, fivePlayers.Status);

        Assert.Equal("Ideal: 2-3 jugadores", game.IdealPlayerCountText);
    }

    [Fact]
    public void ParseGameXml_ShouldExtractAgeAndLanguageDependence()
    {
        // Arrange
        var doc = XDocument.Parse(SampleWingspanXml);

        // Act
        var game = BggXmlParser.ParseItem(doc.Root!.Element("item")!);

        // Assert
        Assert.NotNull(game);
        Assert.Equal(10, game.Age.BoxAge);
        Assert.Equal(10, game.Age.CommunityAge);
        Assert.Equal(LanguageDependence.High, game.Language); // Level 3/4 = High
    }

    [Fact]
    public void ParseGameXml_ShouldExtractRealPlayTimesAndPerPlayerEstimate()
    {
        // Arrange
        var doc = XDocument.Parse(SampleWingspanXml);

        // Act
        var game = BggXmlParser.ParseItem(doc.Root!.Element("item")!);

        // Assert
        Assert.NotNull(game);
        Assert.Equal(40, game.Duration.MinMinutes);
        Assert.Equal(70, game.Duration.MaxMinutes);
        // avgPlayers = (1 + 5) / 2 = 3. avgTime = (40 + 70) / 2 = 55. estPerPlayer = 55 / 3 = 18 min
        Assert.Equal(18, game.Duration.EstimatedPerPlayerMinutes);
    }

    [Fact]
    public void ParseGameXml_WithoutPollVotes_ShouldApplyDeterministicScalabilityFallback()
    {
        // Arrange: XML sin votos en la encuesta
        const string xmlWithoutVotes = @"
<items>
  <item type=""boardgame"" id=""999999"">
    <name type=""primary"" value=""Quick Match"" />
    <minplayers value=""2"" />
    <maxplayers value=""4"" />
    <minplaytime value=""15"" />
    <maxplaytime value=""20"" />
    <playingtime value=""20"" />
    <poll name=""suggested_numplayers"" title=""User Suggested Number of Players"" totalvotes=""0"">
    </poll>
  </item>
</items>";
        var doc = XDocument.Parse(xmlWithoutVotes);

        // Act
        var game = BggXmlParser.ParseItem(doc.Root!.Element("item")!);

        // Assert
        Assert.NotNull(game);
        Assert.Equal(3, game.Scalability.Count); // 2, 3, 4
        Assert.All(game.Scalability, s => Assert.Equal(ScalabilityStatus.Recommended, s.Status));
    }

    [Theory]
    [InlineData("Card Game", 30, TableFootprint.SmallTable)]
    [InlineData("Miniatures", 120, TableFootprint.TableMonster)]
    [InlineData("Economic", 180, TableFootprint.TableMonster)]
    [InlineData("Economic", 90, TableFootprint.StandardTable)]
    public void ParseGameXml_ShouldInferTableFootprintAccurately(string category, int maxPlayTime, TableFootprint expectedFootprint)
    {
        // Arrange
        string xml = $@"
<items>
  <item type=""boardgame"" id=""12345"">
    <name type=""primary"" value=""Test Game"" />
    <minplayers value=""2"" />
    <maxplayers value=""4"" />
    <minplaytime value=""{maxPlayTime / 2}"" />
    <maxplaytime value=""{maxPlayTime}"" />
    <playingtime value=""{maxPlayTime}"" />
    <link type=""boardgamecategory"" id=""1"" value=""{category}"" />
  </item>
</items>";
        var doc = XDocument.Parse(xml);

        // Act
        var game = BggXmlParser.ParseItem(doc.Root!.Element("item")!);

        // Assert
        Assert.NotNull(game);
        Assert.Equal(expectedFootprint, game.Footprint);
    }
}
