using System;
using System.IO;
using System.Threading.Tasks;
using Ludeka.Application.Options;
using Ludeka.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class ImageStorageNamingTests
{
    private readonly SimulatedImageStorageService _service;

    public ImageStorageNamingTests()
    {
        var optimizer = new SkiaSharpImageOptimizationService();
        var options = Options.Create(new CloudflareR2Options
        {
            AccountId = "test-account",
            AccessKeyId = "test-key",
            SecretAccessKey = "test-secret",
            BucketName = "ludeka-media",
            PublicCdnBaseUrl = "https://cdn.ludeka.com",
            Simulate = true
        });
        _service = new SimulatedImageStorageService(optimizer, options, NullLogger<SimulatedImageStorageService>.Instance);
    }

    private static MemoryStream CreateDummyImageStream()
    {
        using var bitmap = new SKBitmap(200, 200);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.AliceBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 80);

        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task UploadGameImageVariantsAsync_CoverType_GeneratesCoverAndCoverThumbKeys()
    {
        using var stream = CreateDummyImageStream();

        var result = await _service.UploadGameImageVariantsAsync(stream, 342942, "cover");

        Assert.Equal("https://cdn.ludeka.com/games/342942/cover.webp", result.FullUrl);
        Assert.Equal("https://cdn.ludeka.com/games/342942/cover_thumb.webp", result.ThumbUrl);
        Assert.True(_service.HasObject("games/342942/cover.webp"));
        Assert.True(_service.HasObject("games/342942/cover_thumb.webp"));
    }

    [Fact]
    public async Task UploadGameImageVariantsAsync_BackType_GeneratesBackKey()
    {
        using var stream = CreateDummyImageStream();

        var result = await _service.UploadGameImageVariantsAsync(stream, 342942, "back");

        Assert.Equal("https://cdn.ludeka.com/games/342942/back.webp", result.FullUrl);
        Assert.Equal("https://cdn.ludeka.com/games/342942/back.webp", result.ThumbUrl);
        Assert.True(_service.HasObject("games/342942/back.webp"));
    }

    [Fact]
    public async Task UploadGameImageVariantsAsync_TableType_GeneratesTableKey()
    {
        using var stream = CreateDummyImageStream();

        var result = await _service.UploadGameImageVariantsAsync(stream, 167791, "table");

        Assert.Equal("https://cdn.ludeka.com/games/167791/table.webp", result.FullUrl);
        Assert.Equal("https://cdn.ludeka.com/games/167791/table.webp", result.ThumbUrl);
        Assert.True(_service.HasObject("games/167791/table.webp"));
    }

    [Theory]
    [InlineData("https://cdn.ludeka.com", "games/123/cover.webp", "https://cdn.ludeka.com/games/123/cover.webp")]
    [InlineData("https://cdn.ludeka.com/", "/games/123/cover.webp", "https://cdn.ludeka.com/games/123/cover.webp")]
    [InlineData("https://media.ludeka.app/", "social/2026/09/banner.webp", "https://media.ludeka.app/social/2026/09/banner.webp")]
    public void GetPublicUrl_HandlesSlashesCorrectly(string baseUrl, string objectKey, string expected)
    {
        var optimizer = new SkiaSharpImageOptimizationService();
        var options = Options.Create(new CloudflareR2Options
        {
            PublicCdnBaseUrl = baseUrl,
            Simulate = true
        });
        var service = new SimulatedImageStorageService(optimizer, options, NullLogger<SimulatedImageStorageService>.Instance);

        var url = service.GetPublicUrl(objectKey);

        Assert.Equal(expected, url);
    }
}
