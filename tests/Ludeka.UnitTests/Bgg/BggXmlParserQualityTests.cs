using System.Linq;
using System.Xml.Linq;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Bgg;
using Xunit;

namespace Ludeka.UnitTests.Bgg;

public class BggXmlParserQualityTests
{
    private const string SampleRichXml = @"
<items termsofuse=""https://boardgamegeek.com/xmlapi/termsofuse"">
  <item type=""boardgame"" id=""13"">
    <name type=""primary"" sortindex=""1"" value=""Catan"" />
    <name type=""alternate"" sortindex=""1"" value=""Catán (Edición en español)"" />
    <minplayers value=""3"" />
    <maxplayers value=""4"" />
    <minplaytime value=""60"" />
    <maxplaytime value=""90"" />
    <playingtime value=""75"" />
    <poll name=""suggested_numplayers"" title=""User Suggested Number of Players"" totalvotes=""2400"">
      <results numplayers=""1"">
        <result value=""Best"" numvotes=""10"" />
        <result value=""Recommended"" numvotes=""50"" />
        <result value=""Not Recommended"" numvotes=""1800"" />
      </results>
      <results numplayers=""2"">
        <result value=""Best"" numvotes=""40"" />
        <result value=""Recommended"" numvotes=""300"" />
        <result value=""Not Recommended"" numvotes=""1500"" />
      </results>
      <results numplayers=""3"">
        <result value=""Best"" numvotes=""600"" />
        <result value=""Recommended"" numvotes=""1200"" />
        <result value=""Not Recommended"" numvotes=""100"" />
      </results>
      <results numplayers=""4"">
        <result value=""Best"" numvotes=""1850"" />
        <result value=""Recommended"" numvotes=""400"" />
        <result value=""Not Recommended"" numvotes=""50"" />
      </results>
    </poll>
    <link type=""boardgamepublisher"" id=""26"" value=""Devir"" />
    <link type=""boardgamepublisher"" id=""10"" value=""Kosmos"" />
  </item>
</items>";

    [Fact]
    public void ParseQualityMetadata_ExtractsScalabilityAndDurations()
    {
        var doc = XDocument.Parse(SampleRichXml);
        var item = doc.Root?.Element("item");
        Assert.NotNull(item);

        var quality = BggXmlParser.ParseQualityMetadata(item);

        Assert.Equal(60, quality.MinPlayTime);
        Assert.Equal(90, quality.MaxPlayTime);
        Assert.Equal(75, quality.PlayingTime);
        Assert.Equal(3, quality.MinPlayers);
        Assert.Equal(4, quality.MaxPlayers);
        Assert.NotEmpty(quality.Scalability);

        var fourPlayers = quality.Scalability.FirstOrDefault(s => s.PlayerCount == 4);
        Assert.NotNull(fourPlayers);
        Assert.Equal(ScalabilityStatus.MustPlay, fourPlayers.Status);

        Assert.Equal("Devir Iberia", quality.SpanishPublisher);
    }

    [Fact]
    public void ParseScalability_WhenPollMissing_AppliesDeterministicFallback()
    {
        const string xmlWithoutPoll = @"
<item type=""boardgame"" id=""9999"">
  <minplayers value=""2"" />
  <maxplayers value=""4"" />
</item>";

        var item = XElement.Parse(xmlWithoutPoll);
        var scalability = BggXmlParser.ParseScalability(item, 2, 4);

        Assert.NotEmpty(scalability);
        Assert.Equal(3, scalability.Count); // 2, 3, 4

        // Entre 2 y 4 son recomendados por fallback determinista
        Assert.All(scalability, s => Assert.Equal(ScalabilityStatus.Recommended, s.Status));
    }

    [Fact]
    public void ParseScalability_SinglePlayerCountGame_FallbackSetsMustPlay()
    {
        const string xmlSoloGame = @"
<item type=""boardgame"" id=""8888"">
  <minplayers value=""2"" />
  <maxplayers value=""2"" />
</item>";

        var item = XElement.Parse(xmlSoloGame);
        var scalability = BggXmlParser.ParseScalability(item, 2, 2);

        Assert.Single(scalability);
        Assert.Equal(2, scalability[0].PlayerCount);
        Assert.Equal(ScalabilityStatus.MustPlay, scalability[0].Status);
    }

    [Fact]
    public void InferFootprint_CategorizesAccurately()
    {
        var smallItem = XElement.Parse(@"<item><link type=""boardgamecategory"" value=""Card Game"" /></item>");
        Assert.Equal(TableFootprint.SmallTable, BggXmlParser.InferFootprint(smallItem, 15, 20));

        var standardItem = XElement.Parse(@"<item><link type=""boardgamecategory"" value=""Family Game"" /></item>");
        Assert.Equal(TableFootprint.StandardTable, BggXmlParser.InferFootprint(standardItem, 45, 60));

        var monsterItem = XElement.Parse(@"<item><link type=""boardgamecategory"" value=""Wargame"" /></item>");
        Assert.Equal(TableFootprint.TableMonster, BggXmlParser.InferFootprint(monsterItem, 120, 180));
    }
}
