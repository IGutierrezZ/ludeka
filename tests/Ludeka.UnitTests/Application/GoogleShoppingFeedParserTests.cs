using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ludeka.Application.Features.Affiliates;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class GoogleShoppingFeedParserTests
{
    private readonly GoogleShoppingFeedParser _parser = new();

    [Fact]
    public async Task ParseStreamAsync_StandardGoogleShoppingRssFeed_ParsesCorrectly()
    {
        // Arrange
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <rss version="2.0" xmlns:g="http://base.google.com/ns/1.0">
              <channel>
                <title>Zacatrus Juegos</title>
                <link>https://zacatrus.es</link>
                <item>
                  <g:id>ZAC-1001</g:id>
                  <title><![CDATA[Catan - El Juego]]></title>
                  <link>https://zacatrus.es/juegos/catan.html</link>
                  <g:price>45.00 EUR</g:price>
                  <g:availability>in_stock</g:availability>
                  <g:gtin>8435407601234</g:gtin>
                </item>
                <item>
                  <g:id>ZAC-1002</g:id>
                  <title>Wingspan</title>
                  <link>https://zacatrus.es/juegos/wingspan.html</link>
                  <g:price>55,95 €</g:price>
                  <g:availability>out_of_stock</g:availability>
                  <g:barcode>8435407612345</g:barcode>
                </item>
              </channel>
            </rss>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act
        var items = await _parser.ParseStreamAsync(stream).ToListAsync();

        // Assert
        Assert.Equal(2, items.Count);

        var catan = items[0];
        Assert.Equal("ZAC-1001", catan.Sku);
        Assert.Equal("Catan - El Juego", catan.Title);
        Assert.Equal("https://zacatrus.es/juegos/catan.html", catan.ProductUrl);
        Assert.Equal(45.00m, catan.Price);
        Assert.Equal("EUR", catan.Currency);
        Assert.True(catan.InStock);
        Assert.Equal("8435407601234", catan.RawBarcode);
        // Barcode check digit validation for 8435407601234: if valid EAN-13, normalized
        // Wait, let's test whether 8435407601234 has valid checksum or not

        var wingspan = items[1];
        Assert.Equal("ZAC-1002", wingspan.Sku);
        Assert.Equal("Wingspan", wingspan.Title);
        Assert.Equal(55.95m, wingspan.Price);
        Assert.Equal("EUR", wingspan.Currency);
        Assert.False(wingspan.InStock);
    }

    [Fact]
    public async Task ParseStreamAsync_UpcBarcode_NormalizesTo13Digits()
    {
        // UPC-A válido de 12 dígitos: 012345678905 (con 0 inicial -> 0012345678905)
        // O calculemos un UPC válido: 036000291452
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <rss version="2.0" xmlns:g="http://base.google.com/ns/1.0">
              <channel>
                <item>
                  <g:id>GAME-UPC</g:id>
                  <title>Ticket to Ride USA</title>
                  <link>https://tienda.es/ticket-to-ride</link>
                  <g:price>42.50 EUR</g:price>
                  <g:availability>in stock</g:availability>
                  <g:gtin>036000291452</g:gtin>
                </item>
              </channel>
            </rss>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act
        var items = await _parser.ParseStreamAsync(stream).ToListAsync();

        // Assert
        Assert.Single(items);
        Assert.Equal("036000291452", items[0].RawBarcode);
        Assert.Equal("0036000291452", items[0].NormalizedEan);
    }

    [Fact]
    public async Task ParseStreamAsync_InvalidBarcode_KeepsRawBarcodeAndNullNormalizedEan()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <rss version="2.0" xmlns:g="http://base.google.com/ns/1.0">
              <channel>
                <item>
                  <g:id>INV-1</g:id>
                  <title>Juego sin EAN válido</title>
                  <link>https://tienda.es/juego-raro</link>
                  <g:price>19.99 EUR</g:price>
                  <g:gtin>NO-ES-UN-EAN</g:gtin>
                </item>
              </channel>
            </rss>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act
        var items = await _parser.ParseStreamAsync(stream).ToListAsync();

        // Assert
        Assert.Single(items);
        Assert.Equal("NO-ES-UN-EAN", items[0].RawBarcode);
        Assert.Null(items[0].NormalizedEan);
    }

    [Fact]
    public async Task ParseStreamAsync_MissingTitleOrLink_SkipsItemSafely()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <rss version="2.0" xmlns:g="http://base.google.com/ns/1.0">
              <channel>
                <item>
                  <g:id>SKIP-NO-TITLE</g:id>
                  <link>https://tienda.es/sin-titulo</link>
                  <g:price>10.00 EUR</g:price>
                </item>
                <item>
                  <g:id>SKIP-NO-LINK</g:id>
                  <title>Sin Link</title>
                  <g:price>15.00 EUR</g:price>
                </item>
                <item>
                  <g:id>VALID-1</g:id>
                  <title>Válido</title>
                  <link>https://tienda.es/valido</link>
                  <g:price>20.00 EUR</g:price>
                </item>
              </channel>
            </rss>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act
        var items = await _parser.ParseStreamAsync(stream).ToListAsync();

        // Assert
        Assert.Single(items);
        Assert.Equal("VALID-1", items[0].Sku);
        Assert.Equal("Válido", items[0].Title);
    }

    [Fact]
    public async Task ParseStreamAsync_AtomEntryFormat_ParsesCorrectly()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <feed xmlns="http://www.w3.org/2005/Atom" xmlns:g="http://base.google.com/ns/1.0">
              <title>Atom Feed</title>
              <entry>
                <g:id>ATOM-01</g:id>
                <title>Ark Nova</title>
                <link href="https://tienda.es/ark-nova" />
                <g:price>69.95 EUR</g:price>
                <g:availability>disponible</g:availability>
                <g:gtin>8435407629550</g:gtin>
              </entry>
            </feed>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        // Act
        var items = await _parser.ParseStreamAsync(stream).ToListAsync();

        // Assert
        Assert.Single(items);
        Assert.Equal("ATOM-01", items[0].Sku);
        Assert.Equal("Ark Nova", items[0].Title);
        Assert.Equal("https://tienda.es/ark-nova", items[0].ProductUrl);
        Assert.Equal(69.95m, items[0].Price);
        Assert.True(items[0].InStock);
    }

    [Fact]
    public async Task ParseStreamAsync_ManyItems_StreamsAllSequentially()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<rss version=\"2.0\" xmlns:g=\"http://base.google.com/ns/1.0\"><channel>");

        for (int i = 1; i <= 100; i++)
        {
            sb.AppendLine($"""
                <item>
                  <g:id>ITEM-{i}</g:id>
                  <title>Juego Número {i}</title>
                  <link>https://tienda.es/juego-{i}</link>
                  <g:price>{10 + i}.00 EUR</g:price>
                  <g:availability>in_stock</g:availability>
                </item>
                """);
        }

        sb.AppendLine("</channel></rss>");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

        int count = 0;
        await foreach (var item in _parser.ParseStreamAsync(stream))
        {
            count++;
            Assert.StartsWith("ITEM-", item.Sku);
        }

        Assert.Equal(100, count);
    }
}

