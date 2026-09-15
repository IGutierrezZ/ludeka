using System.Net.Http;
using System.Threading.Tasks;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class InstagramFeedCollectorTests
{
    private class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public T CurrentValue { get; set; }
        public TestOptionsMonitor(T value) => CurrentValue = value;
        public T Get(string? name) => CurrentValue;
        public System.IDisposable? OnChange(System.Action<T, string?> listener) => null;
    }

    [Fact]
    public void CanHandle_ReturnsTrue_OnlyForInstagram()
    {
        var optionsMonitor = new TestOptionsMonitor<SocialCollectorOptions>(new SocialCollectorOptions());
        var collector = new InstagramFeedCollector(new HttpClient(), optionsMonitor, NullLogger<InstagramFeedCollector>.Instance);

        Assert.True(collector.CanHandle(SocialPlatform.Instagram));
        Assert.False(collector.CanHandle(SocialPlatform.YouTube));
        Assert.False(collector.CanHandle(SocialPlatform.Telegram));
    }

    [Fact]
    public void ParseInstagramProfileHtml_ExtractsPostShortcodes()
    {
        var html = """
            <html>
            <body>
                <a href="/p/C_abc12345/">Post 1</a>
                <a href="/reel/C_def67890/">Reel 1</a>
                <a href="/p/C_abc12345/">Duplicado</a>
            </body>
            </html>
            """;

        var posts = InstagramFeedCollector.ParseInstagramProfileHtml(html, "Devir", maxItems: 5);

        Assert.Equal(2, posts.Count);
        Assert.Equal("https://www.instagram.com/p/C_abc12345/", posts[0].SourceUrl);
        Assert.False(posts[0].IsVideo);

        Assert.Equal("https://www.instagram.com/p/C_def67890/", posts[1].SourceUrl);
        Assert.True(posts[1].IsVideo);
    }

    [Fact]
    public async Task CollectRecentPostsAsync_SimulatedMode_ReturnsSimulatedPosts()
    {
        var optionsMonitor = new TestOptionsMonitor<SocialCollectorOptions>(new SocialCollectorOptions { Simulate = true });
        var collector = new InstagramFeedCollector(new HttpClient(), optionsMonitor, NullLogger<InstagramFeedCollector>.Instance);

        var account = new MonitoredSocialAccount(
            name: "Devir Iberia",
            platform: SocialPlatform.Instagram,
            handleOrChannelId: "deviriberia",
            accountType: MonitoredAccountType.Publisher,
            profileUrl: "https://instagram.com/deviriberia");

        var posts = await collector.CollectRecentPostsAsync(account, maxItems: 2);

        Assert.Equal(2, posts.Count);
        Assert.All(posts, p =>
        {
            Assert.Contains("instagram.com/p/sim_deviriberia_", p.SourceUrl);
            Assert.Equal(SocialPlatform.Instagram, p.Platform);
            Assert.Equal("Devir Iberia", p.AuthorOrChannel);
        });
    }
}
