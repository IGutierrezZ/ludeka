using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Microsoft.Extensions.Hosting;

namespace Ludeka.Infrastructure.Services;

/// <summary>
/// Almacena imágenes de juegos en el sistema de archivos físico local bajo 'wwwroot/images/games/'.
/// Valida extensiones admitidas (.jpg, .jpeg, .png, .webp) y tamaño seguro (<= 5 MB),
/// generando nombres canónicos higienizados por slug.
/// </summary>
public class PhysicalFileImageStorageService : IImageStorageService
{
    private readonly string _gamesDirectory;
    private readonly string _baseImagesDirectory;
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public PhysicalFileImageStorageService(IHostEnvironment? env = null, string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            // INC-48 (PR1a), diseño D1: SaveGameCoverAsync devuelve la URL "/images/games/{fichero}"
            // (línea 85), así que la portada debe escribirse en la subcarpeta "games" de la ruta
            // configurada. Antes se escribía en la raíz de customPath y quedaba inalcanzable por esa
            // URL (404). SaveToFolderAsync y UploadOptimizedImageAsync ya usan _baseImagesDirectory
            // sin este defecto.
            _gamesDirectory = Path.Combine(customPath, "games");
            _baseImagesDirectory = customPath;
        }
        else if (env != null && !string.IsNullOrWhiteSpace(env.ContentRootPath))
        {
            _baseImagesDirectory = Path.Combine(env.ContentRootPath, "wwwroot", "images");
            _gamesDirectory = Path.Combine(_baseImagesDirectory, "games");
        }
        else
        {
            _baseImagesDirectory = Path.Combine(AppContext.BaseDirectory, "wwwroot", "images");
            _gamesDirectory = Path.Combine(_baseImagesDirectory, "games");
        }
    }

    public Task<GameImageUploadResult> SaveGameCoverAsync(
        string slug,
        Stream contentStream,
        string originalFileName,
        string contentType,
        CancellationToken ct = default)
    {
        return SaveGameImageAsync(slug, "cover", contentStream, originalFileName, contentType, ct);
    }

    public async Task<GameImageUploadResult> SaveGameImageAsync(
        string slug,
        string slot,
        Stream contentStream,
        string originalFileName,
        string contentType,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return new GameImageUploadResult(false, null, "El slug del juego no puede estar vacío.");

        if (contentStream == null || contentStream.Length == 0)
            return new GameImageUploadResult(false, null, "El archivo proporcionado está vacío.");

        if (contentStream.Length > MaxFileSizeBytes)
            return new GameImageUploadResult(false, null, $"El tamaño del archivo ({contentStream.Length / 1024 / 1024} MB) excede el máximo permitido de 5 MB.");

        var ext = Path.GetExtension(originalFileName)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
            return new GameImageUploadResult(false, null, $"Formato no admitido ('{ext}'). Solo se admiten archivos .jpg, .jpeg, .png y .webp.");

        try
        {
            if (!Directory.Exists(_gamesDirectory))
            {
                Directory.CreateDirectory(_gamesDirectory);
            }

            var cleanSlug = Core.Entities.Game.GenerateSlug(slug);
            var normalizedSlot = string.IsNullOrWhiteSpace(slot) ? "cover" : slot.Trim().ToLowerInvariant();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var fileName = $"{cleanSlug}-{normalizedSlot}-{timestamp}{ext}";
            var fullPath = Path.Combine(_gamesDirectory, fileName);

            if (contentStream.CanSeek)
            {
                contentStream.Position = 0;
            }

            using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await contentStream.CopyToAsync(fileStream, ct);
            }

            var relativeUrl = $"/images/games/{fileName}";
            return new GameImageUploadResult(true, relativeUrl, null);
        }
        catch (Exception ex)
        {
            return new GameImageUploadResult(false, null, $"Error al guardar el archivo en disco: {ex.Message}");
        }
    }

    public async Task<GameImageUploadResult> SaveEventPosterAsync(
        string eventSlugOrId,
        Stream contentStream,
        string originalFileName,
        string contentType,
        CancellationToken ct = default)
    {
        return await SaveToFolderAsync("events", eventSlugOrId, "poster", contentStream, originalFileName, ct);
    }

    public async Task<GameImageUploadResult> SaveCommunityImageAsync(
        string subfolder,
        string identifier,
        Stream contentStream,
        string originalFileName,
        string contentType,
        CancellationToken ct = default)
    {
        var cleanSubfolder = string.IsNullOrWhiteSpace(subfolder) ? "community" : subfolder.Trim().ToLowerInvariant();
        return await SaveToFolderAsync(cleanSubfolder, identifier, "img", contentStream, originalFileName, ct);
    }

    private async Task<GameImageUploadResult> SaveToFolderAsync(
        string subfolder,
        string identifier,
        string prefix,
        Stream contentStream,
        string originalFileName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return new GameImageUploadResult(false, null, "El identificador no puede estar vacío.");

        if (contentStream == null || contentStream.Length == 0)
            return new GameImageUploadResult(false, null, "El archivo proporcionado está vacío.");

        if (contentStream.Length > MaxFileSizeBytes)
            return new GameImageUploadResult(false, null, $"El tamaño del archivo ({contentStream.Length / 1024 / 1024} MB) excede el máximo permitido de 5 MB.");

        var ext = Path.GetExtension(originalFileName)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
            return new GameImageUploadResult(false, null, $"Formato no admitido ('{ext}'). Solo se admiten archivos .jpg, .jpeg, .png y .webp.");

        try
        {
            var targetDir = Path.Combine(_baseImagesDirectory, subfolder);
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            var cleanSlug = Core.Entities.Game.GenerateSlug(identifier);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var fileName = $"{cleanSlug}-{prefix}-{timestamp}{ext}";
            var fullPath = Path.Combine(targetDir, fileName);

            if (contentStream.CanSeek)
            {
                contentStream.Position = 0;
            }

            using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await contentStream.CopyToAsync(fileStream, ct);
            }

            var relativeUrl = $"/images/{subfolder}/{fileName}";
            return new GameImageUploadResult(true, relativeUrl, null);
        }
        catch (Exception ex)
        {
            return new GameImageUploadResult(false, null, $"Error al guardar el archivo en disco: {ex.Message}");
        }
    }

    public Task<GameImageUploadResult> ValidateCoverUrlAsync(string imageUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return Task.FromResult(new GameImageUploadResult(false, null, "La URL no puede estar vacía."));

        var trimmed = imageUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            if (trimmed.StartsWith("/images/", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new GameImageUploadResult(true, trimmed, null));
            }

            return Task.FromResult(new GameImageUploadResult(false, null, "La URL debe ser absoluta con protocolo http:// o https:// (o ruta relativa /images/...)."));
        }

        return Task.FromResult(new GameImageUploadResult(true, trimmed, null));
    }

    public async Task<string> UploadOptimizedImageAsync(
        Stream inputStream,
        string objectKey,
        int maxWidth = 1000,
        int quality = 82,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inputStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        var fullPath = Path.Combine(_baseImagesDirectory, objectKey.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (inputStream.CanSeek)
        {
            inputStream.Position = 0;
        }

        using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await inputStream.CopyToAsync(fileStream, ct);
        }

        return GetPublicUrl(objectKey);
    }

    public async Task<Core.ValueObjects.ImageVariantUrls> UploadGameImageVariantsAsync(
        Stream rawImageStream,
        int bggId,
        string imageType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rawImageStream);
        if (bggId <= 0) throw new ArgumentOutOfRangeException(nameof(bggId), "El BggId debe ser positivo.");

        var normalizedType = imageType?.Trim().ToLowerInvariant() ?? "cover";
        using var memoryStream = new MemoryStream();
        await rawImageStream.CopyToAsync(memoryStream, ct);

        if (normalizedType is "cover" or "boxartfront")
        {
            memoryStream.Position = 0;
            var fullKey = $"games/{bggId}/cover.webp";
            var fullUrl = await UploadOptimizedImageAsync(memoryStream, fullKey, maxWidth: 1000, quality: 82, ct);

            memoryStream.Position = 0;
            var thumbKey = $"games/{bggId}/cover_thumb.webp";
            var thumbUrl = await UploadOptimizedImageAsync(memoryStream, thumbKey, maxWidth: 400, quality: 80, ct);

            return new Core.ValueObjects.ImageVariantUrls(fullUrl, thumbUrl);
        }

        memoryStream.Position = 0;
        var customKey = $"games/{bggId}/{normalizedType}.webp";
        var customUrl = await UploadOptimizedImageAsync(memoryStream, customKey, maxWidth: 1000, quality: 82, ct);

        return new Core.ValueObjects.ImageVariantUrls(customUrl, customUrl);
    }

    public Task<bool> DeleteImageAsync(string objectKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        var fullPath = Path.Combine(_baseImagesDirectory, objectKey.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public string GetPublicUrl(string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        var cleanKey = objectKey.TrimStart('/');
        return $"/images/{cleanKey}";
    }
}
