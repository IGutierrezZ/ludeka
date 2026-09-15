using System.Net.Http;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class RssBlogFeedCollectorTests
{
    private const string SampleRss2Xml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0">
            <channel>
                <title>Blog de Zacatrus</title>
                <link>https://zacatrus.es/blog</link>
                <description>Novedades y noticias del mundo lúdico</description>
                <item>
                    <title>Presentamos Virus! Halloween, la nueva expansión</title>
                    <link>https://zacatrus.es/blog/virus-halloween-expansion</link>
                    <pubDate>Mon, 14 Sep 2026 09:00:00 +0200</pubDate>
                    <description>Llega la expansión más terrorífica y divertida de Virus!. Prepárate para nuevas cartas de contagio.</description>
                    <enclosure url="https://zacatrus.es/media/virus_halloween.jpg" type="image/jpeg" />
                </item>
                <item>
                    <title>Crónica del Festival Internacional de Juegos de Córdoba</title>
                    <link>https://zacatrus.es/blog/festival-cordoba-2026</link>
                    <pubDate>Fri, 11 Sep 2026 14:00:00 +0200</pubDate>
                    <description>Os contamos todo lo vivido en el festival lúdico del año.</description>
                </item>
            </channel>
        </rss>
        """;

    private const string SampleAtomXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <feed xmlns="http://www.w3.org/2005/Atom">
            <title>Maldito Games Noticias</title>
            <entry>
                <title>Lanzamiento de Terraforming Mars: Automa</title>
                <link href="https://malditogames.com/noticias/terraforming-mars-automa"/>
                <id>urn:uuid:12345</id>
                <updated>2026-09-13T10:00:00Z</updated>
                <summary>Ya puedes jugar en solitario con la nueva expansión oficial Automa.</summary>
            </entry>
        </feed>
        """;

    [Fact]
    public void CanHandle_ReturnsTrue_ForRssFeedAndWebsite()
    {
        var collector = new RssBlogFeedCollector(new HttpClient(), NullLogger<RssBlogFeedCollector>.Instance);

        Assert.True(collector.CanHandle(SocialPlatform.RssFeed));
        Assert.True(collector.CanHandle(SocialPlatform.Website));
        Assert.False(collector.CanHandle(SocialPlatform.YouTube));
        Assert.False(collector.CanHandle(SocialPlatform.Instagram));
    }

    [Fact]
    public void ParseFeedXml_Rss2_ExtractsItemsCorrectly()
    {
        var posts = RssBlogFeedCollector.ParseFeedXml(SampleRss2Xml, "Zacatrus", SocialPlatform.RssFeed, maxItems: 5);

        Assert.Equal(2, posts.Count);

        var first = posts[0];
        Assert.Equal("Presentamos Virus! Halloween, la nueva expansión", first.Title);
        Assert.Equal("https://zacatrus.es/blog/virus-halloween-expansion", first.SourceUrl);
        Assert.Contains("Virus!", first.Description);
        Assert.Equal("https://zacatrus.es/media/virus_halloween.jpg", first.ThumbnailUrl);
        Assert.Equal(SocialPlatform.RssFeed, first.Platform);
        Assert.False(first.IsVideo);
    }

    [Fact]
    public void ParseFeedXml_Atom_ExtractsEntriesCorrectly()
    {
        var posts = RssBlogFeedCollector.ParseFeedXml(SampleAtomXml, "Maldito Games", SocialPlatform.RssFeed, maxItems: 5);

        Assert.Single(posts);

        var first = posts[0];
        Assert.Equal("Lanzamiento de Terraforming Mars: Automa", first.Title);
        Assert.Equal("https://malditogames.com/noticias/terraforming-mars-automa", first.SourceUrl);
        Assert.Contains("Automa", first.Description);
        Assert.Equal(SocialPlatform.RssFeed, first.Platform);
    }
}
