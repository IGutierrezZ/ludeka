using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class OpenGraphSocialMetadataExtractorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExtractFromUrlAsync_NullOrEmptyUrl_ReturnsNull(string? url)
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var client = new HttpClient(handler);
        var extractor = new OpenGraphSocialMetadataExtractor(client, NullLogger<OpenGraphSocialMetadataExtractor>.Instance);

        var result = await extractor.ExtractFromUrlAsync(url!);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExtractFromUrlAsync_YouTubeUrl_WithValidOEmbed_ExtractsMetadata()
    {
        var oembedJson = """
        {
            "title": "Tutorial Catan en 5 Minutos",
            "author_name": "Mesa Lúdica",
            "thumbnail_url": "https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg"
        }
        """;

        var handler = new FakeHttpMessageHandler((req, _) =>
        {
            if (req.RequestUri != null && req.RequestUri.AbsoluteUri.Contains("youtube.com/oembed"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(oembedJson, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var client = new HttpClient(handler);
        var extractor = new OpenGraphSocialMetadataExtractor(client, NullLogger<OpenGraphSocialMetadataExtractor>.Instance);

        var result = await extractor.ExtractFromUrlAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ");

        Assert.NotNull(result);
        Assert.Equal(SocialPlatform.YouTube, result.Platform);
        Assert.Equal("dQw4w9WgXcQ", result.VideoId);
        Assert.True(result.IsVideo);
        Assert.Equal("Tutorial Catan en 5 Minutos", result.Title);
        Assert.Equal("Mesa Lúdica", result.AuthorOrChannel);
        Assert.Equal("Tutorial Catan en 5 Minutos", result.Description);
        Assert.Equal("https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg", result.ImageUrl);
    }

    [Fact]
    public async Task ExtractFromUrlAsync_YouTubeUrl_WhenOEmbedFails_FallsBackToDefaultThumbnailAndId()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var client = new HttpClient(handler);
        var extractor = new OpenGraphSocialMetadataExtractor(client, NullLogger<OpenGraphSocialMetadataExtractor>.Instance);

        var result = await extractor.ExtractFromUrlAsync("https://youtu.be/dQw4w9WgXcQ");

        Assert.NotNull(result);
        Assert.Equal(SocialPlatform.YouTube, result.Platform);
        Assert.Equal("dQw4w9WgXcQ", result.VideoId);
        Assert.True(result.IsVideo);
        Assert.Null(result.Title);
        Assert.Null(result.AuthorOrChannel);
        Assert.Equal("https://img.youtube.com/vi/dQw4w9WgXcQ/hqdefault.jpg", result.ImageUrl);
    }

    [Theory]
    [InlineData("https://www.youtube.com/shorts/abc123XYZ09", "abc123XYZ09")]
    [InlineData("https://www.youtube.com/embed/abc123XYZ09", "abc123XYZ09")]
    [InlineData("https://www.youtube.com/v/abc123XYZ09", "abc123XYZ09")]
    public async Task ExtractFromUrlAsync_YouTubeAlternateUrlFormats_ExtractsVideoId(string url, string expectedId)
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var client = new HttpClient(handler);
        var extractor = new OpenGraphSocialMetadataExtractor(client, NullLogger<OpenGraphSocialMetadataExtractor>.Instance);

        var result = await extractor.ExtractFromUrlAsync(url);

        Assert.NotNull(result);
        Assert.Equal(expectedId, result.VideoId);
        Assert.True(result.IsVideo);
        Assert.Equal($"https://img.youtube.com/vi/{expectedId}/hqdefault.jpg", result.ImageUrl);
    }

    [Fact]
    public async Task ExtractFromUrlAsync_InstagramPost_ExtractsAuthorAndOpenGraphMetadata()
    {
        var html = """
        <!DOCTYPE html>
        <html>
        <head>
            <meta property="og:title" content="Devir Iberia on Instagram: '¡Ya está aquí la nueva expansión!'" />
            <meta property="og:image" content="https://cdn.instagram.com/p/image123.jpg" />
            <meta property="og:description" content="Descubre todos los componentes y mecánicas." />
        </head>
        <body></body>
        </html>
        """;

        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            }));

        var client = new HttpClient(handler);
        var extractor = new OpenGraphSocialMetadataExtractor(client, NullLogger<OpenGraphSocialMetadataExtractor>.Instance);

        var result = await extractor.ExtractFromUrlAsync("https://www.instagram.com/p/Cxyz123/");

        Assert.NotNull(result);
        Assert.Equal(SocialPlatform.Instagram, result.Platform);
        Assert.False(result.IsVideo);
        Assert.Equal("Devir Iberia", result.AuthorOrChannel);
        Assert.Equal("Devir Iberia on Instagram: '¡Ya está aquí la nueva expansión!'", result.Title);
        Assert.Equal("https://cdn.instagram.com/p/image123.jpg", result.ImageUrl);
        Assert.Equal("Descubre todos los componentes y mecánicas.", result.Description);
    }

    [Theory]
    [InlineData("https://www.instagram.com/reel/Cxyz123/")]
    [InlineData("https://www.instagram.com/tv/Cxyz123/")]
    public async Task ExtractFromUrlAsync_InstagramReelOrTv_MarksAsVideo(string url)
    {
        var html = """
        <html>
        <head><meta property="og:title" content="Reel Lúdico" /></head>
        </html>
        """;

        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            }));

        var client = new HttpClient(handler);
        var extractor = new OpenGraphSocialMetadataExtractor(client, NullLogger<OpenGraphSocialMetadataExtractor>.Instance);

        var result = await extractor.ExtractFromUrlAsync(url);

        Assert.NotNull(result);
        Assert.Equal(SocialPlatform.Instagram, result.Platform);
        Assert.True(result.IsVideo);
    }

    [Fact]
    public async Task ExtractFromUrlAsync_GenericWebsite_WithFullOpenGraph_ExtractsMetadata()
    {
        var html = """
        <!DOCTYPE html>
        <html>
        <head>
            <meta property="og:title" content="Reseña de Ark Nova" />
            <meta property="og:description" content="Construye tu propio zoológico moderno." />
            <meta property="og:image" content="https://ejemplo.com/ark-nova.png" />
        </head>
        </html>
        """;

        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            }));

        var client = new HttpClient(handler);
        var extractor = new OpenGraphSocialMetadataExtractor(client, NullLogger<OpenGraphSocialMetadataExtractor>.Instance);

        var result = await extractor.ExtractFromUrlAsync("https://ejemplo.com/articulos/ark-nova");

        Assert.NotNull(result);
        Assert.Equal(SocialPlatform.Website, result.Platform);
        Assert.Equal("Reseña de Ark Nova", result.Title);
        Assert.Equal("Construye tu propio zoológico moderno.", result.Description);
        Assert.Equal("https://ejemplo.com/ark-nova.png", result.ImageUrl);
        Assert.False(result.IsVideo);
    }

    [Fact]
    public async Task ExtractFromUrlAsync_GenericWebsite_FallbackToTitleTagWhenOgTitleMissing()
    {
        var html = """
        <!DOCTYPE html>
        <html>
        <head>
            <title>Novedades Lúdicas de Octubre</title>
        </head>
        </html>
        """;

        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            }));

        var client = new HttpClient(handler);
        var extractor = new OpenGraphSocialMetadataExtractor(client, NullLogger<OpenGraphSocialMetadataExtractor>.Instance);

        var result = await extractor.ExtractFromUrlAsync("https://ejemplo.com/novedades");

        Assert.NotNull(result);
        Assert.Equal("Novedades Lúdicas de Octubre", result.Title);
        Assert.Null(result.ImageUrl);
        Assert.Null(result.Description);
    }

    [Fact]
    public async Task ExtractFromUrlAsync_HtmlWithEntities_DecodesHtmlEntities()
    {
        var html = """
        <html>
        <head>
            <meta property="og:title" content="Dungeons &amp; Lasers &quot;Dragons&quot; &#39;Special&#39;" />
            <meta property="og:description" content="Partida &lt;1&gt; &amp; m&aacute;s" />
        </head>
        </html>
        """;

        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            }));

        var client = new HttpClient(handler);
        var extractor = new OpenGraphSocialMetadataExtractor(client, NullLogger<OpenGraphSocialMetadataExtractor>.Instance);

        var result = await extractor.ExtractFromUrlAsync("https://ejemplo.com/dragons");

        Assert.NotNull(result);
        Assert.Equal("Dungeons & Lasers \"Dragons\" 'Special'", result.Title);
        Assert.Equal("Partida <1> & más", result.Description);
    }

    [Fact]
    public async Task ExtractFromUrlAsync_HttpErrorStatus_ReturnsPlatformWithNullMetadata()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var client = new HttpClient(handler);
        var extractor = new OpenGraphSocialMetadataExtractor(client, NullLogger<OpenGraphSocialMetadataExtractor>.Instance);

        var result = await extractor.ExtractFromUrlAsync("https://www.instagram.com/p/missing/");

        Assert.NotNull(result);
        Assert.Equal(SocialPlatform.Instagram, result.Platform);
        Assert.Null(result.Title);
        Assert.Null(result.ImageUrl);
    }

    [Fact]
    public async Task ExtractFromUrlAsync_HttpThrowsException_HandlesGracefully()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            throw new HttpRequestException("Error de conexión DNS"));

        var client = new HttpClient(handler);
        var extractor = new OpenGraphSocialMetadataExtractor(client, NullLogger<OpenGraphSocialMetadataExtractor>.Instance);

        var result = await extractor.ExtractFromUrlAsync("https://ejemplo.com/failing");

        Assert.NotNull(result);
        Assert.Equal(SocialPlatform.Website, result.Platform);
        Assert.Null(result.Title);
    }

    [Theory]
    [InlineData("https://twitter.com/juegosdemesa/status/123", SocialPlatform.Twitter)]
    [InlineData("https://x.com/juegosdemesa/status/123", SocialPlatform.Twitter)]
    [InlineData("https://www.tiktok.com/@ludeka/video/123", SocialPlatform.TikTok)]
    public async Task ExtractFromUrlAsync_DetectsOtherPlatforms(string url, SocialPlatform expectedPlatform)
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><head><title>Test</title></head></html>")
            }));

        var client = new HttpClient(handler);
        var extractor = new OpenGraphSocialMetadataExtractor(client, NullLogger<OpenGraphSocialMetadataExtractor>.Instance);

        var result = await extractor.ExtractFromUrlAsync(url);

        Assert.NotNull(result);
        Assert.Equal(expectedPlatform, result.Platform);
    }

    [Fact]
    public async Task ExtractFromUrlAsync_InstagramBlockedLoginHtml_DoesNotTreatAsValidContentAndDisablesVideoFlag()
    {
        // Arrange: HTML devuelto por Instagram en servidor anónimo (sólo etiqueta <title>Instagram</title>)
        var blockedHtml = "<html><head><title>Instagram</title></head><body><h1>Login</h1></body></html>";
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(blockedHtml, Encoding.UTF8, "text/html")
            }));

        var client = new HttpClient(handler);
        var extractor = new OpenGraphSocialMetadataExtractor(client, NullLogger<OpenGraphSocialMetadataExtractor>.Instance);

        // Act
        var result = await extractor.ExtractFromUrlAsync("https://www.instagram.com/reel/C-test123/");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SocialPlatform.Instagram, result.Platform);
        Assert.Null(result.Title);
        Assert.Null(result.ImageUrl);
        Assert.False(result.IsVideo);
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
