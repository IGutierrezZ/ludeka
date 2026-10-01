using System.Linq;
using System.Xml.Linq;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Bgg;
using Xunit;

namespace Ludeka.UnitTests.Bgg;

public class BggXmlParserExpansionTests
{
    [Fact]
    public void ParseItem_ShouldSetGameTypeExpansion_WhenTypeIsBoardgameexpansion()
    {
        string xml = @"<item type=""boardgameexpansion"" id=""342035"">
            <name type=""primary"" sortindex=""1"" value=""Dune: Imperium – Rise of Ix"" />
            <yearpublished value=""2022"" />
            <minplayers value=""1"" />
            <maxplayers value=""4"" />
            <playingtime value=""120"" />
            <minage value=""14"" />
        </item>";

        var element = XElement.Parse(xml);
        var game = BggXmlParser.ParseItem(element);

        Assert.NotNull(game);
        Assert.Equal(GameType.Expansion, game.Type);
        Assert.True(game.IsExpansion);
        Assert.Equal(342035, game.BggId);
    }

    [Fact]
    public void ParseItem_ShouldSetGameTypeBaseGame_WhenTypeIsBoardgame()
    {
        string xml = @"<item type=""boardgame"" id=""13"">
            <name type=""primary"" sortindex=""1"" value=""Catan"" />
            <yearpublished value=""1995"" />
        </item>";

        var element = XElement.Parse(xml);
        var game = BggXmlParser.ParseItem(element);

        Assert.NotNull(game);
        Assert.Equal(GameType.BaseGame, game.Type);
        Assert.False(game.IsExpansion);
    }

    [Fact]
    public void ExtractInboundBaseGameBggId_ShouldReturnBaseGameId_WhenInboundExpansionLinkExists()
    {
        string xml = @"<item type=""boardgameexpansion"" id=""342035"">
            <link type=""boardgameexpansion"" id=""316554"" value=""Dune: Imperium"" inbound=""true"" />
            <link type=""boardgamedesigner"" id=""101"" value=""Paul Dennen"" />
        </item>";

        var element = XElement.Parse(xml);
        int? baseId = BggXmlParser.ExtractInboundBaseGameBggId(element);

        Assert.NotNull(baseId);
        Assert.Equal(316554, baseId.Value);
    }

    [Fact]
    public void ExtractInboundBaseGameBggId_ShouldReturnNull_WhenNoInboundLink()
    {
        string xml = @"<item type=""boardgame"" id=""13"">
            <link type=""boardgameexpansion"" id=""2807"" value=""Catan: 5-6 Player Extension"" />
        </item>";

        var element = XElement.Parse(xml);
        int? baseId = BggXmlParser.ExtractInboundBaseGameBggId(element);

        Assert.Null(baseId);
    }

    [Fact]
    public void ExtractOutboundExpansionBggIds_ShouldReturnAllExpansionIdsExcludingInbound()
    {
        string xml = @"<item type=""boardgame"" id=""266192"">
            <name type=""primary"" value=""Wingspan"" />
            <link type=""boardgameexpansion"" id=""290448"" value=""Wingspan: European Expansion"" />
            <link type=""boardgameexpansion"" id=""300580"" value=""Wingspan: Oceania Expansion"" />
            <link type=""boardgameexpansion"" id=""366161"" value=""Wingspan: Asia"" />
        </item>";

        var element = XElement.Parse(xml);
        var outboundIds = BggXmlParser.ExtractOutboundExpansionBggIds(element);

        Assert.Equal(3, outboundIds.Count);
        Assert.Contains(290448, outboundIds);
        Assert.Contains(300580, outboundIds);
        Assert.Contains(366161, outboundIds);
    }

    [Fact]
    public void ExtractExpansionLinks_ShouldDistinguishInboundAndOutbound()
    {
        string xml = @"<item type=""boardgameexpansion"" id=""300580"">
            <link type=""boardgameexpansion"" id=""266192"" value=""Wingspan"" inbound=""true"" />
            <link type=""boardgameexpansion"" id=""999999"" value=""Wingspan Promo Pack"" />
        </item>";

        var element = XElement.Parse(xml);
        var links = BggXmlParser.ExtractExpansionLinks(element);

        Assert.Equal(2, links.Count);

        var inbound = links.FirstOrDefault(l => l.IsInbound);
        Assert.NotNull(inbound);
        Assert.Equal(266192, inbound.BggId);
        Assert.Equal("Wingspan", inbound.Title);

        var outbound = links.FirstOrDefault(l => !l.IsInbound);
        Assert.NotNull(outbound);
        Assert.Equal(999999, outbound.BggId);
    }
}
