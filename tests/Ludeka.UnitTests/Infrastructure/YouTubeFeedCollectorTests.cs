using System;
using System.Linq;
using System.Net.Http;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class YouTubeFeedCollectorTests
{
    private const string SampleAtomXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <feed xmlns="http://www.w3.org/2005/Atom" 
              xmlns:yt="http://www.youtube.com/xml/schemas/2015" 
              xmlns:media="http://search.yahoo.com/mrss/">
            <title>El Troquel - Juegos de Mesa</title>
            <entry>
                <id>yt:video:dQw4w9WgXcQ</id>
                <yt:videoId>dQw4w9WgXcQ</yt:videoId>
                <title>Tutorial y Partida Completa a Ark Nova en Español</title>
                <link rel="alternate" href="https://www.youtube.com/watch?v=dQw4w9WgXcQ"/>
                <author>
                    <name>El Troquel</name>
                </author>
                <published>2026-09-14T18:30:00+00:00</published>
                <updated>2026-09-14T19:00:00+00:00</updated>
                <media:group>
                    <media:title>Tutorial y Partida Completa a Ark Nova en Español</media:title>
                    <media:description>Hoy explicamos cómo jugar a Ark Nova, el gran eurogame de zoológicos.</media:description>
                    <media:thumbnail url="https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg" width="480" height="360"/>
                </media:group>
            </entry>
            <entry>
                <id>yt:video:9bZkp7q19f0</id>
                <yt:videoId>9bZkp7q19f0</yt:videoId>
                <title>Top 5 Juegos Ligeros para Fiestas</title>
                <link rel="alternate" href="https://www.youtube.com/watch?v=9bZkp7q19f0"/>
                <author>
                    <name>El Troquel</name>
                </author>
                <published>2026-09-12T12:00:00+00:00</published>
                <media:group>
                    <media:description>Nuestra selección de party games para jugar con no iniciados.</media:description>
                    <media:thumbnail url="https://i.ytimg.com/vi/9bZkp7q19f0/hqdefault.jpg"/>
                </media:group>
            </entry>
        </feed>
        """;

    [Fact]
    public void CanHandle_ReturnsTrue_OnlyForYouTube()
    {
        var collector = new YouTubeFeedCollector(new HttpClient(), NullLogger<YouTubeFeedCollector>.Instance);

        Assert.True(collector.CanHandle(SocialPlatform.YouTube));
        Assert.False(collector.CanHandle(SocialPlatform.Instagram));
        Assert.False(collector.CanHandle(SocialPlatform.Telegram));
        Assert.False(collector.CanHandle(SocialPlatform.RssFeed));
    }

    [Fact]
    public void ParseFeedXml_ExtractsVideosCorrectly()
    {
        var posts = YouTubeFeedCollector.ParseFeedXml(SampleAtomXml, "Fallback Canal", maxItems: 5);

        Assert.Equal(2, posts.Count);

        var first = posts[0];
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", first.SourceUrl);
        Assert.Equal("Tutorial y Partida Completa a Ark Nova en Español", first.Title);
        Assert.Equal("El Troquel", first.AuthorOrChannel);
        Assert.Contains("Ark Nova", first.Description);
        Assert.Equal("https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg", first.ThumbnailUrl);
        Assert.True(first.IsVideo);
        Assert.Equal(SocialPlatform.YouTube, first.Platform);
        Assert.Equal(2026, first.PublishedAt.Year);
        Assert.Equal(9, first.PublishedAt.Month);
    }

    [Fact]
    public void ParseFeedXml_RespectsMaxItems()
    {
        var posts = YouTubeFeedCollector.ParseFeedXml(SampleAtomXml, "Fallback Canal", maxItems: 1);

        Assert.Single(posts);
        Assert.Equal("Tutorial y Partida Completa a Ark Nova en Español", posts[0].Title);
    }

    [Fact]
    public void ParseFeedXml_ReturnsEmpty_WhenXmlIsEmptyOrInvalid()
    {
        var postsEmpty = YouTubeFeedCollector.ParseFeedXml(string.Empty, "Canal", 5);
        Assert.Empty(postsEmpty);
    }
}
