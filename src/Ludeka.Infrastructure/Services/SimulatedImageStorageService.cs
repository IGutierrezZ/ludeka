using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Infrastructure.Services;

/// <summary>
/// Almacén de medios simulado en memoria para entornos de desarrollo y pruebas automatizadas (Zero-Cloud).
/// Ejecuta la optimización real con SkiaSharp para validar el pipeline WebP sin requerir credenciales de Cloudflare.
/// </summary>
public class SimulatedImageStorageService : IImageStorageService
{
    private readonly IImageOptimizationService _optimizationService;
    private readonly CloudflareR2Options _options;
    private readonly ILogger<SimulatedImageStorageService> _logger;
    private readonly ConcurrentDictionary<string, byte[]> _memoryStore = new(StringComparer.OrdinalIgnoreCase);

    public SimulatedImageStorageService(
        IImageOptimizationService optimizationService,
        IOptions<CloudflareR2Options> options,
        ILogger<SimulatedImageStorageService> logger)
    {
        _optimizationService = optimizationService ?? throw new ArgumentNullException(nameof(optimizationService));
        _options = options?.Value ?? new CloudflareR2Options();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Consulta si un objeto simulado existe en el almacén en memoria.
    /// </summary>
    public bool HasObject(string objectKey) => _memoryStore.ContainsKey(objectKey);

    /// <summary>
    /// Obtiene los bytes simulados de un objeto almacenado en memoria.
    /// </summary>
    public byte[]? GetObjectBytes(string objectKey) => _memoryStore.TryGetValue(objectKey, out var bytes) ? bytes : null;

    /// <summary>
    /// Número de archivos almacenados actualmente en memoria.
    /// </summary>
    public int StoredFilesCount => _memoryStore.Count;

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
        _memoryStore[objectKey] = webpBytes;

        _logger.LogInformation("[Simulated R2] Imagen optimizada guardada en memoria: {Key} ({Bytes} bytes)", objectKey, webpBytes.Length);
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

        memoryStream.Position = 0;
        var customKey = $"games/{bggId}/{normalizedType}.webp";
        var customUrl = await UploadOptimizedImageAsync(memoryStream, customKey, maxWidth: 1000, quality: 82, ct);

        return new ImageVariantUrls(customUrl, customUrl);
    }

    public Task<bool> DeleteImageAsync(string objectKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        var removed = _memoryStore.TryRemove(objectKey, out _);
        _logger.LogInformation("[Simulated R2] Eliminación de {Key}: {Result}", objectKey, removed ? "éxito" : "no encontrado");
        return Task.FromResult(removed);
    }

    public string GetPublicUrl(string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        var baseUrl = _options.PublicCdnBaseUrl.TrimEnd('/');
        var key = objectKey.TrimStart('/');
        return $"{baseUrl}/{key}";
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
            return new GameImageUploadResult(false, null, ex.Message);
        }
    }

    public Task<GameImageUploadResult> ValidateCoverUrlAsync(string imageUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return Task.FromResult(new GameImageUploadResult(false, null, "La URL no puede estar vacía."));

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return Task.FromResult(new GameImageUploadResult(false, null, "La URL no tiene un formato válido."));

        return Task.FromResult(new GameImageUploadResult(true, imageUrl, null));
    }
}
