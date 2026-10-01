using System.Text.Json;
using System.Xml.Linq;
using Ludeka.Infrastructure.Bgg;
using Xunit;

namespace Ludeka.UnitTests.Bgg;

public class BggXmlToJsonConverterTests
{
    [Fact]
    public void ConvertToJson_ShouldPreserveAttributesAndValues()
    {
        string xml = @"<item type=""boardgame"" id=""13"">
            <name type=""primary"" sortindex=""1"" value=""Catan"" />
            <yearpublished value=""1995"" />
            <description>Juego de colonización de la isla de Catán.</description>
        </item>";

        var element = XElement.Parse(xml);
        string json = BggXmlToJsonConverter.ConvertToJson(element);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("boardgame", root.GetProperty("@type").GetString());
        Assert.Equal("13", root.GetProperty("@id").GetString());
        Assert.Equal("1995", root.GetProperty("yearpublished").GetProperty("@value").GetString());
        Assert.Equal("Catan", root.GetProperty("name").GetProperty("@value").GetString());
        Assert.Contains("colonización", root.GetProperty("description").GetString());
    }

    [Fact]
    public void ConvertToJson_ShouldGroupMultipleChildElementsAsArray()
    {
        string xml = @"<item type=""boardgame"" id=""13"">
            <name type=""primary"" sortindex=""1"" value=""Catan"" />
            <name type=""alternate"" sortindex=""1"" value=""Los Colonos de Catán"" />
            <link type=""boardgameexpansion"" id=""2807"" value=""Catan: 5-6 Player Extension"" />
            <link type=""boardgameexpansion"" id=""3000"" value=""Catan: Cities &amp; Knights"" />
        </item>";

        var element = XElement.Parse(xml);
        string json = BggXmlToJsonConverter.ConvertToJson(element);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var names = root.GetProperty("name");
        Assert.Equal(JsonValueKind.Array, names.ValueKind);
        Assert.Equal(2, names.GetArrayLength());

        var links = root.GetProperty("link");
        Assert.Equal(JsonValueKind.Array, links.ValueKind);
        Assert.Equal(2, links.GetArrayLength());
        Assert.Equal("2807", links[0].GetProperty("@id").GetString());
        Assert.Equal("boardgameexpansion", links[0].GetProperty("@type").GetString());
    }

    [Fact]
    public void ConvertXmlStringToJson_ShouldHandleRootItemsDoc()
    {
        string fullXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
        <items termsofuse=""https://boardgamegeek.com/xmlapi/termsofuse"">
            <item type=""boardgameexpansion"" id=""342035"">
                <name type=""primary"" value=""Dune: Imperium – Rise of Ix"" />
                <link type=""boardgameexpansion"" id=""316554"" value=""Dune: Imperium"" inbound=""true"" />
            </item>
        </items>";

        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(fullXml);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("boardgameexpansion", root.GetProperty("@type").GetString());
        Assert.Equal("342035", root.GetProperty("@id").GetString());

        var link = root.GetProperty("link");
        Assert.Equal("316554", link.GetProperty("@id").GetString());
        Assert.Equal("true", link.GetProperty("@inbound").GetString());
    }

    [Fact]
    public void ConvertXmlStringToJson_ShouldReturnEmptyObject_WhenInputIsEmpty()
    {
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson("");
        Assert.Equal("{}", json);
    }
}
