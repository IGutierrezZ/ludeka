using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using SkiaSharp;

namespace Ludeka.Infrastructure.Services;

/// <summary>
/// Motor de optimización de imágenes en memoria (Zero-Disk) basado en SkiaSharp (licencia MIT).
/// Convierte y redimensiona proporcionalmente a WebP preservando la fidelidad visual y el aspect ratio.
/// </summary>
public class SkiaSharpImageOptimizationService : IImageOptimizationService
{
    public Task<byte[]> ConvertToWebpAsync(
        Stream inputStream,
        int maxWidth,
        int quality = 82,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inputStream);

        if (inputStream.CanSeek)
        {
            inputStream.Position = 0;
        }

        using var memoryStream = new MemoryStream();
        inputStream.CopyTo(memoryStream);
        memoryStream.Position = 0;

        using var originalBitmap = SKBitmap.Decode(memoryStream);
        if (originalBitmap == null)
        {
            throw new InvalidOperationException("No se pudo decodificar la imagen proporcionada. El formato puede estar dañado o no ser compatible.");
        }

        int origWidth = originalBitmap.Width;
        int origHeight = originalBitmap.Height;

        int targetWidth = origWidth;
        int targetHeight = origHeight;

        if (maxWidth > 0 && origWidth > maxWidth)
        {
            double scale = (double)maxWidth / origWidth;
            targetWidth = maxWidth;
            targetHeight = (int)Math.Max(1, Math.Round(origHeight * scale));
        }

        SKBitmap bitmapToEncode = originalBitmap;
        SKBitmap? resizedBitmap = null;

        if (targetWidth != origWidth || targetHeight != origHeight)
        {
            var imageInfo = new SKImageInfo(targetWidth, targetHeight, originalBitmap.ColorType, originalBitmap.AlphaType);
            resizedBitmap = originalBitmap.Resize(imageInfo, SKSamplingOptions.Default);
            if (resizedBitmap != null)
            {
                bitmapToEncode = resizedBitmap;
            }
        }

        try
        {
            using var skImage = SKImage.FromBitmap(bitmapToEncode);
            var clampedQuality = Math.Clamp(quality, 1, 100);
            using var data = skImage.Encode(SKEncodedImageFormat.Webp, clampedQuality);

            if (data == null)
            {
                throw new InvalidOperationException("Error al codificar la imagen en formato WebP.");
            }

            return Task.FromResult(data.ToArray());
        }
        finally
        {
            resizedBitmap?.Dispose();
        }
    }

    public Task<ImageDimensions> GetDimensionsAsync(
        Stream inputStream,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inputStream);

        if (inputStream.CanSeek)
        {
            inputStream.Position = 0;
        }

        using var codec = SKCodec.Create(inputStream);
        if (codec != null)
        {
            return Task.FromResult(new ImageDimensions(codec.Info.Width, codec.Info.Height));
        }

        if (inputStream.CanSeek)
        {
            inputStream.Position = 0;
        }

        using var bitmap = SKBitmap.Decode(inputStream);
        if (bitmap == null)
        {
            throw new InvalidOperationException("No se pudieron leer las dimensiones de la imagen.");
        }

        return Task.FromResult(new ImageDimensions(bitmap.Width, bitmap.Height));
    }
}
