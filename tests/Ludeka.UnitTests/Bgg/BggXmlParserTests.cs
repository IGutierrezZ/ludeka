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

    [Theory]
    [InlineData("Thematic Games", GameStyle.Ameritrash)]
    [InlineData("Wargames", GameStyle.Ameritrash)]
    [InlineData("Party Games", GameStyle.PartyGame)]
    [InlineData("Children's Games", GameStyle.PartyGame)]
    [InlineData("Abstract Games", GameStyle.FillerAbstract)]
    [InlineData("Strategy Games", GameStyle.Eurogame)]
    public void InferGameDna_Subdomains_InferCorrectGameStyle(string subdomain, GameStyle expectedStyle)
    {
        // Arrange
        string xml = $@"
        <item type=""boardgame"" id=""100"">
            <link type=""boardgamesubdomain"" id=""1"" value=""{subdomain}"" />
        </item>";
        var element = XElement.Parse(xml);

        // Act
        var (_, style, _) = BggXmlParser.InferGameDna(element, []);

        // Assert
        Assert.Equal(expectedStyle, style);
    }

    [Theory]
    [InlineData("boardgamecategory", "Miniatures", GameStyle.Ameritrash)]
    [InlineData("boardgamecategory", "Zombies", GameStyle.Ameritrash)]
    [InlineData("boardgamemechanic", "Dungeon Crawl", GameStyle.Ameritrash)]
    [InlineData("boardgamecategory", "Trivia", GameStyle.PartyGame)]
    [InlineData("boardgamecategory", "Word Game", GameStyle.PartyGame)]
    [InlineData("boardgamemechanic", "Legacy Game", GameStyle.NarrativeCampaign)]
    [InlineData("boardgamemechanic", "Campaign", GameStyle.NarrativeCampaign)]
    [InlineData("boardgamecategory", "Abstract Strategy", GameStyle.FillerAbstract)]
    public void InferGameDna_CategoriesAndMechanics_InferCorrectGameStyle(string linkType, string linkValue, GameStyle expectedStyle)
    {
        // Arrange
        string xml = $@"
        <item type=""boardgame"" id=""200"">
            <link type=""{linkType}"" id=""1"" value=""{linkValue}"" />
        </item>";
        var element = XElement.Parse(xml);

        // Act
        var (_, style, _) = BggXmlParser.InferGameDna(element, []);

        // Assert
        Assert.Equal(expectedStyle, style);
    }

    [Theory]
    [InlineData("Cooperative Game", ConfrontationType.Cooperative)]
    [InlineData("Semi-Cooperative Game", ConfrontationType.SemiCooperative)]
    [InlineData("Traitor Game", ConfrontationType.HiddenRolesOrTeams)]
    [InlineData("Secret Identity", ConfrontationType.HiddenRolesOrTeams)]
    [InlineData("Worker Placement", ConfrontationType.Competitive)]
    public void InferGameDna_ConfrontationMechanics_InferCorrectConfrontation(string mechanic, ConfrontationType expectedConfrontation)
    {
        // Arrange
        string xml = $@"
        <item type=""boardgame"" id=""300"">
            <link type=""boardgamemechanic"" id=""1"" value=""{mechanic}"" />
        </item>";
        var element = XElement.Parse(xml);

        // Act
        var (confrontation, _, _) = BggXmlParser.InferGameDna(element, []);

        // Assert
        Assert.Equal(expectedConfrontation, confrontation);
    }
}

