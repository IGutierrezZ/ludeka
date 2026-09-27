using System;
using System.IO;
using Ludeka.Application.DTOs;
using Ludeka.Infrastructure.Services;
using SkiaSharp;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class SkiaSharpGiveawayCoverComposerTests
{
    private readonly SkiaSharpGiveawayCoverComposer _composer = new();

    private static byte[] CreateSyntheticImage(int width, int height)
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
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    [Fact]
    public void ComposeHorizontalCover_VerticalImage_Produces16x9Composite()
    {
        // Arrange: Imagen vertical tipo post/historia (1080x1920)
        var inputBytes = CreateSyntheticImage(1080, 1920);

        // Act
        var resultBytes = _composer.ComposeHorizontalCover(inputBytes);

        // Assert
        Assert.NotNull(resultBytes);
        Assert.NotEmpty(resultBytes);

        using var resultBitmap = SKBitmap.Decode(resultBytes);
        Assert.NotNull(resultBitmap);
        Assert.Equal(1280, resultBitmap.Width);
        Assert.Equal(720, resultBitmap.Height);
    }

    [Fact]
    public void ComposeHorizontalCover_MobileScreenshotTallRatio_AppliesDefensiveCropAndOutputs16x9()
    {
        // Arrange: Pantallazo móvil panorámico muy alto (1080x2400 -> ratio > 1.8)
        var inputBytes = CreateSyntheticImage(1080, 2400);

        // Act
        var resultBytes = _composer.ComposeHorizontalCover(inputBytes, cropBox: null);

        // Assert
        Assert.NotNull(resultBytes);
        using var resultBitmap = SKBitmap.Decode(resultBytes);
        Assert.NotNull(resultBitmap);
        Assert.Equal(1280, resultBitmap.Width);
        Assert.Equal(720, resultBitmap.Height);
    }

    [Fact]
    public void ComposeHorizontalCover_WithExplicitBoundingBox_CropsAndComposes()
    {
        // Arrange: Imagen con bounding box delimitado por IA
        var inputBytes = CreateSyntheticImage(1000, 2000);
        var cropBox = new NormalizedBoundingBoxDto(
            YMin: 80,
            XMin: 50,
            YMax: 920,
            XMax: 950);

        // Act
        var resultBytes = _composer.ComposeHorizontalCover(inputBytes, cropBox);

        // Assert
        Assert.NotNull(resultBytes);
        using var resultBitmap = SKBitmap.Decode(resultBytes);
        Assert.NotNull(resultBitmap);
        Assert.Equal(1280, resultBitmap.Width);
        Assert.Equal(720, resultBitmap.Height);
    }

    [Fact]
    public void ComposeHorizontalCover_SquareImage_Produces16x9Composite()
    {
        // Arrange: Imagen cuadrada típica de post (1080x1080)
        var inputBytes = CreateSyntheticImage(1080, 1080);

        // Act
        var resultBytes = _composer.ComposeHorizontalCover(inputBytes);

        // Assert
        Assert.NotNull(resultBytes);
        using var resultBitmap = SKBitmap.Decode(resultBytes);
        Assert.NotNull(resultBitmap);
        Assert.Equal(1280, resultBitmap.Width);
        Assert.Equal(720, resultBitmap.Height);
    }

    [Fact]
    public void ComposeHorizontalCover_NullOrEmptyBytes_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _composer.ComposeHorizontalCover([]));
        Assert.Throws<ArgumentNullException>(() => _composer.ComposeHorizontalCover(null!));
    }
}
