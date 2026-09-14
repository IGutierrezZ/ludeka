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

public class SimulatedImageStorageServiceTests
{
    private readonly SimulatedImageStorageService _service;

    public SimulatedImageStorageServiceTests()
    {
        var optimizer = new SkiaSharpImageOptimizationService();
        var options = Options.Create(new CloudflareR2Options
        {
            AccountId = "mock-account",
            AccessKeyId = "mock-key",
            SecretAccessKey = "mock-secret",
            BucketName = "ludeka-media",
            PublicCdnBaseUrl = "https://cdn.ludeka.com",
            Simulate = true
        });
        _service = new SimulatedImageStorageService(optimizer, options, NullLogger<SimulatedImageStorageService>.Instance);
    }

    private static MemoryStream CreateTestImage(int width = 300, int height = 300)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.LightGreen);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 85);

        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task UploadOptimizedImageAsync_StoresBytesAndReturnsPublicUrl()
    {
        using var stream = CreateTestImage(500, 500);

        var url = await _service.UploadOptimizedImageAsync(stream, "social/test-card.webp", maxWidth: 400);

        Assert.Equal("https://cdn.ludeka.com/social/test-card.webp", url);
        Assert.True(_service.HasObject("social/test-card.webp"));
        Assert.NotNull(_service.GetObjectBytes("social/test-card.webp"));
    }

    [Fact]
    public async Task DeleteImageAsync_RemovesFromInternalStore()
    {
        using var stream = CreateTestImage();
        await _service.UploadOptimizedImageAsync(stream, "test/to-delete.webp");

        Assert.True(_service.HasObject("test/to-delete.webp"));

        var deleted = await _service.DeleteImageAsync("test/to-delete.webp");
        Assert.True(deleted);
        Assert.False(_service.HasObject("test/to-delete.webp"));
    }

    [Fact]
    public async Task SaveGameCoverAsync_BackwardsCompatibility_SavesOptimizedWebp()
    {
        using var stream = CreateTestImage();

        var result = await _service.SaveGameCoverAsync("ark-nova", stream, "cover.png", "image/png");

        Assert.True(result.Success);
        Assert.NotNull(result.RelativePath);
        Assert.StartsWith("https://cdn.ludeka.com/games/ark-nova/cover-", result.RelativePath);
        Assert.EndsWith(".webp", result.RelativePath);
    }

    [Fact]
    public async Task SaveEventPosterAsync_BackwardsCompatibility_SavesOptimizedWebp()
    {
        using var stream = CreateTestImage();

        var result = await _service.SaveEventPosterAsync("interocio-2026", stream, "poster.jpg", "image/jpeg");

        Assert.True(result.Success);
        Assert.NotNull(result.RelativePath);
        Assert.StartsWith("https://cdn.ludeka.com/events/interocio-2026-", result.RelativePath);
        Assert.EndsWith(".webp", result.RelativePath);
    }

    [Fact]
    public async Task SaveCommunityImageAsync_BackwardsCompatibility_SavesOptimizedWebp()
    {
        using var stream = CreateTestImage();

        var result = await _service.SaveCommunityImageAsync("photos", "table-setup-1", stream, "setup.png", "image/png");

        Assert.True(result.Success);
        Assert.NotNull(result.RelativePath);
        Assert.StartsWith("https://cdn.ludeka.com/photos/table-setup-1-", result.RelativePath);
        Assert.EndsWith(".webp", result.RelativePath);
    }

    [Fact]
    public async Task ValidateCoverUrlAsync_ValidHttpUrl_ReturnsSuccess()
    {
        var result = await _service.ValidateCoverUrlAsync("https://cdn.ludeka.com/games/123/cover.webp");

        Assert.True(result.Success);
        Assert.Equal("https://cdn.ludeka.com/games/123/cover.webp", result.RelativePath);
    }

    [Fact]
    public async Task ValidateCoverUrlAsync_EmptyOrInvalidUrl_ReturnsFailure()
    {
        var emptyResult = await _service.ValidateCoverUrlAsync("");
        Assert.False(emptyResult.Success);

        var invalidResult = await _service.ValidateCoverUrlAsync("ftp://not-allowed");
        Assert.False(invalidResult.Success);
    }
}
