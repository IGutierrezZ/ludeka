using System.Net.Http;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class TelegramChannelCollectorTests
{
    private const string SampleTelegramHtml = """
        <!DOCTYPE html>
        <html>
        <body>
            <div class="tgme_channel_info">
                <div class="tgme_channel_info_header">Devir Iberia</div>
            </div>
            
            <div class="tgme_widget_message_wrap">
                <div class="tgme_widget_message" data-post="deviriberia/1450">
                    <div class="tgme_widget_message_user">
                        <span class="tgme_widget_message_owner_name">Devir Iberia</span>
                    </div>
                    <div class="tgme_widget_message_text">
                        ¡Ya disponible en tiendas la reimpresión de <b>Catán</b> y sus expansiones!<br/>
                        PVP recomendado: 45 €.<br/>
                        Encuéntralo en tu tienda habitual.
                    </div>
                    <div class="tgme_widget_message_photo_wrap" style="background-image:url('https://cdn.telegram.org/file/devir_catan_1450.jpg')"></div>
                    <div class="tgme_widget_message_footer">
                        <time datetime="2026-09-14T10:00:00+00:00" class="time">10:00</time>
                    </div>
                </div>
            </div>

            <div class="tgme_widget_message_wrap">
                <div class="tgme_widget_message" data-post="deviriberia/1451">
                    <div class="tgme_widget_message_text">
                        Gran sorteo de aniversario: sortemos 3 copias de Lacrimosa. Participa comentando en nuestro canal.
                    </div>
                    <time datetime="2026-09-14T15:30:00+00:00" class="time">15:30</time>
                </div>
            </div>
        </body>
        </html>
        """;

    [Fact]
    public void CanHandle_ReturnsTrue_OnlyForTelegram()
    {
        var collector = new TelegramChannelCollector(new HttpClient(), NullLogger<TelegramChannelCollector>.Instance);

        Assert.True(collector.CanHandle(SocialPlatform.Telegram));
        Assert.False(collector.CanHandle(SocialPlatform.YouTube));
        Assert.False(collector.CanHandle(SocialPlatform.Instagram));
    }

    [Theory]
    [InlineData("@deviriberia", "", "deviriberia")]
    [InlineData("deviriberia", "", "deviriberia")]
    [InlineData("", "https://t.me/s/zacatrus", "zacatrus")]
    [InlineData("", "https://t.me/malditogames", "malditogames")]
    [InlineData("@arrakis_games", "https://t.me/arrakis_games", "arrakis_games")]
    public void ExtractChannelUsername_ParsesVariousFormats(string handle, string profileUrl, string expected)
    {
        var result = TelegramChannelCollector.ExtractChannelUsername(handle, profileUrl);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseTelegramHtml_ExtractsMessagesCorrectly()
    {
        var posts = TelegramChannelCollector.ParseTelegramHtml(SampleTelegramHtml, "deviriberia", "Devir Iberia", maxItems: 5);

        Assert.Equal(2, posts.Count);

        // Al ordenar por más reciente primero, post 1451 (15:30) va antes que 1450 (10:00)
        var latest = posts[0];
        Assert.Equal("https://t.me/deviriberia/1451", latest.SourceUrl);
        Assert.Contains("Gran sorteo de aniversario", latest.Description);
        Assert.Equal(SocialPlatform.Telegram, latest.Platform);

        var first = posts[1];
        Assert.Equal("https://t.me/deviriberia/1450", first.SourceUrl);
        Assert.Contains("reimpresión de Catán", first.Description);
        Assert.Equal("https://cdn.telegram.org/file/devir_catan_1450.jpg", first.ThumbnailUrl);
        Assert.Equal("Devir Iberia", first.AuthorOrChannel);
        Assert.Equal(SocialPlatform.Telegram, first.Platform);
    }

    [Fact]
    public void ParseTelegramHtml_RespectsMaxItems()
    {
        var posts = TelegramChannelCollector.ParseTelegramHtml(SampleTelegramHtml, "deviriberia", "Devir Iberia", maxItems: 1);

        Assert.Single(posts);
    }
}
