using System;
using System.IO;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using SkiaSharp;

namespace Ludeka.Infrastructure.Services;

/// <summary>
/// Compositor de carátulas para sorteos en proporción horizontal 16:9 basado en SkiaSharp.
/// Adapta capturas móviles o fotos verticales mediante fondo difuminado, centrado nítido
/// y recorte defensivo de barras de notificaciones y navegación del sistema operativo.
/// </summary>
public class SkiaSharpGiveawayCoverComposer : IGiveawayCoverComposer
{
    public byte[] ComposeHorizontalCover(
        byte[] originalImageBytes,
        NormalizedBoundingBoxDto? cropBox = null,
        int targetWidth = 1280,
        int targetHeight = 720)
    {
        ArgumentNullException.ThrowIfNull(originalImageBytes);

        if (originalImageBytes.Length == 0)
            throw new ArgumentException("Los bytes de la imagen no pueden estar vacíos.", nameof(originalImageBytes));

        if (targetWidth <= 0) targetWidth = 1280;
        if (targetHeight <= 0) targetHeight = 720;

        using var originalBitmap = SKBitmap.Decode(originalImageBytes);
        if (originalBitmap == null)
        {
            throw new InvalidOperationException("No se pudo decodificar la imagen de entrada para componer la carátula.");
        }

        // 1. Recorte inteligente o defensivo
        using var croppedBitmap = ExtractUsableRegion(originalBitmap, cropBox);

        // 2. Composición sobre lienzo 16:9
        var canvasInfo = new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvasBitmap = new SKBitmap(canvasInfo);
        using var canvas = new SKCanvas(canvasBitmap);

        // 2.1 Fondo base oscuro
        canvas.Clear(new SKColor(11, 15, 23)); // #0B0F17

        // 2.2 Fondo desenfocado (Blur background)
        DrawBlurredBackground(canvas, croppedBitmap, targetWidth, targetHeight);

        // 2.3 Capa oscura semitransparente sobre el fondo para contraste editorial
        using (var overlayPaint = new SKPaint
        {
            Color = new SKColor(11, 15, 23, 170), // ~67% opacidad
            Style = SKPaintStyle.Fill
        })
        {
            canvas.DrawRect(new SKRect(0, 0, targetWidth, targetHeight), overlayPaint);
        }

        // 2.4 Imagen central nítida con sombra y esquinas redondeadas
        DrawCenteredForeground(canvas, croppedBitmap, targetWidth, targetHeight);

        // 2.5 Badge sutil de marca Ludeka en esquina
        DrawBrandingBadge(canvas, targetWidth, targetHeight);

        canvas.Flush();

        // 3. Codificación a WebP (85% calidad)
        using var image = SKImage.FromBitmap(canvasBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Webp, 85);
        if (data == null)
        {
            throw new InvalidOperationException("Error al codificar la carátula compuesta en formato WebP.");
        }

        return data.ToArray();
    }

    private static SKBitmap ExtractUsableRegion(SKBitmap source, NormalizedBoundingBoxDto? cropBox)
    {
        int srcWidth = source.Width;
        int srcHeight = source.Height;

        // Si se proveen coordenadas válidas de bounding box (0..1000)
        if (cropBox != null && cropBox.IsValid)
        {
            int x = Math.Clamp((cropBox.XMin * srcWidth) / 1000, 0, srcWidth - 1);
            int y = Math.Clamp((cropBox.YMin * srcHeight) / 1000, 0, srcHeight - 1);
            int w = Math.Clamp(((cropBox.XMax - cropBox.XMin) * srcWidth) / 1000, 1, srcWidth - x);
            int h = Math.Clamp(((cropBox.YMax - cropBox.YMin) * srcHeight) / 1000, 1, srcHeight - y);

            var subset = new SKBitmap();
            if (source.ExtractSubset(subset, new SKRectI(x, y, x + w, y + h)))
            {
                return subset;
            }
            subset.Dispose();
        }

        // Si es una captura de pantalla vertical de teléfono móvil (~9:19.5 o 9:20, ratio > 1.8)
        float ratio = srcHeight / (float)srcWidth;
        if (ratio > 1.8f)
        {
            int cropTop = (int)Math.Round(srcHeight * 0.045f);    // Descarte barra de estado (~4.5%)
            int cropBottom = (int)Math.Round(srcHeight * 0.055f); // Descarte barra de navegación (~5.5%)
            int newHeight = srcHeight - cropTop - cropBottom;

            if (newHeight > 50)
            {
                var subset = new SKBitmap();
                if (source.ExtractSubset(subset, new SKRectI(0, cropTop, srcWidth, cropTop + newHeight)))
                {
                    return subset;
                }
                subset.Dispose();
            }
        }

        // Devolver copia completa
        return source.Copy();
    }

    private static void DrawBlurredBackground(SKCanvas canvas, SKBitmap source, int targetWidth, int targetHeight)
    {
        float scaleX = (float)targetWidth / source.Width;
        float scaleY = (float)targetHeight / source.Height;
        float scale = Math.Max(scaleX, scaleY);

        float scaledWidth = source.Width * scale;
        float scaledHeight = source.Height * scale;
        float destX = (targetWidth - scaledWidth) / 2f;
        float destY = (targetHeight - scaledHeight) / 2f;

        using var blurFilter = SKImageFilter.CreateBlur(28, 28);
        using var paint = new SKPaint
        {
            ImageFilter = blurFilter,
            IsAntialias = true
        };

        var destRect = new SKRect(destX, destY, destX + scaledWidth, destY + scaledHeight);
        canvas.DrawBitmap(source, destRect, SKSamplingOptions.Default, paint);
    }

    private static void DrawCenteredForeground(SKCanvas canvas, SKBitmap source, int targetWidth, int targetHeight)
    {
        float maxFgHeight = targetHeight - 32f; // 16px margen arriba y abajo
        float maxFgWidth = targetWidth - 64f;

        float scale = Math.Min(maxFgHeight / source.Height, maxFgWidth / source.Width);
        float fgWidth = source.Width * scale;
        float fgHeight = source.Height * scale;

        float x = (targetWidth - fgWidth) / 2f;
        float y = (targetHeight - fgHeight) / 2f;

        var fgRect = new SKRect(x, y, x + fgWidth, y + fgHeight);
        var roundRect = new SKRoundRect(fgRect, 16f, 16f);

        // Sombra proyectada difusa
        using (var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 190),
            ImageFilter = SKImageFilter.CreateDropShadow(0, 10, 20, 20, new SKColor(0, 0, 0, 210)),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        })
        {
            canvas.DrawRoundRect(roundRect, shadowPaint);
        }

        // Dibujar imagen redondeada
        canvas.Save();
        canvas.ClipRoundRect(roundRect, SKClipOperation.Intersect, antialias: true);
        using (var imagePaint = new SKPaint
        {
            IsAntialias = true
        })
        {
            canvas.DrawBitmap(source, fgRect, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), imagePaint);
        }
        canvas.Restore();

        // Borde sutil exterior
        using (var borderPaint = new SKPaint
        {
            Color = new SKColor(255, 255, 255, 30),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true
        })
        {
            canvas.DrawRoundRect(roundRect, borderPaint);
        }
    }

    private static void DrawBrandingBadge(SKCanvas canvas, int targetWidth, int targetHeight)
    {
        var badgeRect = new SKRoundRect(new SKRect(targetWidth - 110, targetHeight - 44, targetWidth - 24, targetHeight - 18), 6, 6);

        using (var bgPaint = new SKPaint
        {
            Color = new SKColor(11, 15, 23, 200),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        })
        {
            canvas.DrawRoundRect(badgeRect, bgPaint);
        }

        using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        using var font = new SKFont(typeface, 13);
        using var textPaint = new SKPaint
        {
            Color = new SKColor(249, 115, 22), // #F97316 Ludeka Brand Orange
            IsAntialias = true
        };

        var text = "LUDEKA";
        float textWidth = font.MeasureText(text);
        float x = badgeRect.Rect.MidX - (textWidth / 2f);
        float y = badgeRect.Rect.MidY + 4.5f;

        using var blob = SKTextBlob.Create(text, font);
        canvas.DrawText(blob, x, y, textPaint);
    }
}
