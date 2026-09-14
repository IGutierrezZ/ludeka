using System;
using System.IO;
using System.Threading.Tasks;
using Ludeka.Infrastructure.Services;
using SkiaSharp;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class SkiaSharpImageOptimizationTests
{
    private readonly SkiaSharpImageOptimizationService _optimizer = new();

    private static MemoryStream CreateSyntheticImageStream(int width, int height, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.DarkSlateBlue);

        using var paint = new SKPaint
        {
            Color = SKColors.OrangeRed,
            IsAntialias = true,
            StrokeWidth = 4
        };
        canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) / 4f, paint);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);

        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task ConvertToWebpAsync_WhenImageExceedsMaxWidth_ScalesProportionally()
    {
        // Arrange: 1600x1200 (aspect ratio 4:3)
        using var input = CreateSyntheticImageStream(1600, 1200);

        // Act: Redimensionar a máx 1000px
        var webpBytes = await _optimizer.ConvertToWebpAsync(input, maxWidth: 1000, quality: 82);

        // Assert: El resultado debe ser un WebP válido de 1000x750
        Assert.NotNull(webpBytes);
        Assert.True(webpBytes.Length > 0);

        using var resultStream = new MemoryStream(webpBytes);
        var dimensions = await _optimizer.GetDimensionsAsync(resultStream);

        Assert.Equal(1000, dimensions.Width);
        Assert.Equal(750, dimensions.Height);
    }

    [Fact]
    public async Task ConvertToWebpAsync_WhenImageSmallerThanMaxWidth_DoesNotUpscale()
    {
        // Arrange: 400x300
        using var input = CreateSyntheticImageStream(400, 300);

        // Act: Solicitar maxWidth de 1000px
        var webpBytes = await _optimizer.ConvertToWebpAsync(input, maxWidth: 1000, quality: 80);

        // Assert: No se amplía artificialmente, mantiene 400x300
        using var resultStream = new MemoryStream(webpBytes);
        var dimensions = await _optimizer.GetDimensionsAsync(resultStream);

        Assert.Equal(400, dimensions.Width);
        Assert.Equal(300, dimensions.Height);
    }

    [Fact]
    public async Task ConvertToWebpAsync_ProducesValidWebpHeader()
    {
        // Arrange
        using var input = CreateSyntheticImageStream(500, 500);

        // Act
        var webpBytes = await _optimizer.ConvertToWebpAsync(input, maxWidth: 500);

        // Assert: Comprobar firmas mágicas "RIFF" en byte 0..3 y "WEBP" en byte 8..11
        Assert.True(webpBytes.Length >= 12);
        var riff = System.Text.Encoding.ASCII.GetString(webpBytes, 0, 4);
        var webp = System.Text.Encoding.ASCII.GetString(webpBytes, 8, 4);

        Assert.Equal("RIFF", riff);
        Assert.Equal("WEBP", webp);
    }

    [Fact]
    public async Task GetDimensionsAsync_AccuratelyReadsOriginalDimensions()
    {
        // Arrange
        using var input = CreateSyntheticImageStream(850, 425);

        // Act
        var dimensions = await _optimizer.GetDimensionsAsync(input);

        // Assert
        Assert.Equal(850, dimensions.Width);
        Assert.Equal(425, dimensions.Height);
    }

    [Fact]
    public async Task ConvertToWebpAsync_ThrowsOnNullStream()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _optimizer.ConvertToWebpAsync(null!, maxWidth: 1000));
    }

    [Fact]
    public async Task ConvertToWebpAsync_ThrowsOnCorruptedStream()
    {
        var corruptData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        using var stream = new MemoryStream(corruptData);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _optimizer.ConvertToWebpAsync(stream, maxWidth: 1000));
    }
}
