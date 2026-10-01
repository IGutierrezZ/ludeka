using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Infrastructure.Services;

/// <summary>
/// Servicio de almacenamiento de medios en Cloudflare R2 con API compatible S3 y cero costes de salida (Zero Egress).
/// </summary>
public class CloudflareR2StorageService : IImageStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly IImageOptimizationService _optimizationService;
    private readonly CloudflareR2Options _options;
    private readonly ILogger<CloudflareR2StorageService> _logger;

    public CloudflareR2StorageService(
        IImageOptimizationService optimizationService,
        IOptions<CloudflareR2Options> options,
        ILogger<CloudflareR2StorageService> logger,
        IAmazonS3? s3Client = null)
    {
        _optimizationService = optimizationService ?? throw new ArgumentNullException(nameof(optimizationService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (s3Client != null)
        {
            _s3Client = s3Client;
        }
        else
        {
            var credentials = new BasicAWSCredentials(_options.AccessKeyId, _options.SecretAccessKey);
            var config = new AmazonS3Config
            {
                ServiceURL = $"https://{_options.AccountId}.r2.cloudflarestorage.com",
                AuthenticationRegion = "auto",
                ForcePathStyle = true
            };
            _s3Client = new AmazonS3Client(credentials, config);
        }
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

        var webpBytes = await _optimizationService.ConvertToWebpAsync(inputStream, maxWidth, quality, ct);

        using var uploadStream = new MemoryStream(webpBytes);
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = uploadStream,
            ContentType = "image/webp",
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true,
            Headers =
            {
                CacheControl = "public, max-age=31536000, immutable"
            }
        };

        await _s3Client.PutObjectAsync(request, ct);
        _logger.LogInformation("Imagen optimizada subida con éxito a R2: {Key} ({Bytes} bytes)", objectKey, webpBytes.Length);

        return GetPublicUrl(objectKey);
    }

    public async Task<ImageVariantUrls> UploadGameImageVariantsAsync(
        Stream rawImageStream,
        int bggId,
        string imageType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rawImageStream);
        if (bggId <= 0) throw new ArgumentOutOfRangeException(nameof(bggId), "El BggId debe ser positivo.");

        var normalizedType = imageType?.Trim().ToLowerInvariant() ?? "cover";

        // Aseguramos poder reutilizar el stream en memoria
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

            return new ImageVariantUrls(fullUrl, thumbUrl);
        }

        if (normalizedType is "back" or "boxartback" or "boxback")
        {
            memoryStream.Position = 0;
            var backKey = $"games/{bggId}/back.webp";
            var backUrl = await UploadOptimizedImageAsync(memoryStream, backKey, maxWidth: 1000, quality: 82, ct);

            return new ImageVariantUrls(backUrl, backUrl);
        }

        if (normalizedType is "table" or "gameplay" or "creative" or "components")
        {
            memoryStream.Position = 0;
            var tableKey = $"games/{bggId}/table.webp";
            var tableUrl = await UploadOptimizedImageAsync(memoryStream, tableKey, maxWidth: 1200, quality: 82, ct);

            return new ImageVariantUrls(tableUrl, tableUrl);
        }

        // Tipo personalizado o no reconocido
        memoryStream.Position = 0;
        var customKey = $"games/{bggId}/{normalizedType}.webp";
        var customUrl = await UploadOptimizedImageAsync(memoryStream, customKey, maxWidth: 1000, quality: 82, ct);

        return new ImageVariantUrls(customUrl, customUrl);
    }

    public async Task<bool> DeleteImageAsync(string objectKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        try
        {
            await _s3Client.DeleteObjectAsync(_options.BucketName, objectKey, ct);
            _logger.LogInformation("Imagen eliminada de R2: {Key}", objectKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar imagen de R2: {Key}", objectKey);
            return false;
        }
    }

    public string GetPublicUrl(string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        var baseUrl = _options.PublicCdnBaseUrl.TrimEnd('/');
        var key = objectKey.TrimStart('/');
        return $"{baseUrl}/{key}";
    }

    // --- Métodos de compatibilidad con interfaz existente ---

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
        try
        {
            var normalizedSlot = string.IsNullOrWhiteSpace(slot) ? "cover" : slot.Trim().ToLowerInvariant();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var objectKey = $"games/{slug}/{normalizedSlot}-{timestamp}.webp";
            int maxWidth = normalizedSlot is "table" ? 1200 : 1000;
            var publicUrl = await UploadOptimizedImageAsync(contentStream, objectKey, maxWidth: maxWidth, quality: 82, ct);
            return new GameImageUploadResult(true, publicUrl, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar imagen '{Slot}' en R2 para {Slug}", slot, slug);
            return new GameImageUploadResult(false, null, ex.Message);
        }
    }

    public async Task<GameImageUploadResult> SaveEventPosterAsync(
        string eventSlugOrId,
        Stream contentStream,
        string originalFileName,
        string contentType,
        CancellationToken ct = default)
    {
        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var objectKey = $"events/{eventSlugOrId}-{timestamp}.webp";
            var publicUrl = await UploadOptimizedImageAsync(contentStream, objectKey, maxWidth: 1200, quality: 82, ct);
            return new GameImageUploadResult(true, publicUrl, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar póster de evento en R2 para {Event}", eventSlugOrId);
            return new GameImageUploadResult(false, null, ex.Message);
        }
    }

    public async Task<GameImageUploadResult> SaveCommunityImageAsync(
        string subfolder,
        string identifier,
        Stream contentStream,
        string originalFileName,
        string contentType,
        CancellationToken ct = default)
    {
        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var folder = string.IsNullOrWhiteSpace(subfolder) ? "community" : subfolder.Trim('/', '\\');
            var objectKey = $"{folder}/{identifier}-{timestamp}.webp";
            var publicUrl = await UploadOptimizedImageAsync(contentStream, objectKey, maxWidth: 1000, quality: 82, ct);
            return new GameImageUploadResult(true, publicUrl, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar imagen de comunidad en R2");
            return new GameImageUploadResult(false, null, ex.Message);
        }
    }

    public Task<GameImageUploadResult> ValidateCoverUrlAsync(string imageUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return Task.FromResult(new GameImageUploadResult(false, null, "La URL no puede estar vacía."));

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return Task.FromResult(new GameImageUploadResult(false, null, "La URL no tiene un formato válido (debe comenzar con http o https)."));

        return Task.FromResult(new GameImageUploadResult(true, imageUrl, null));
    }
}
