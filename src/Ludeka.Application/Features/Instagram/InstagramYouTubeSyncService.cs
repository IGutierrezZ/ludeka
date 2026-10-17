using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Application.Features.Instagram;

/// <summary>
/// Orquestador de sincronización de Reels de Instagram al canal de YouTube de Ludeka como Shorts (INC-150).
/// Garantiza orden cronológico (primero los más antiguos del historial y luego los nuevos diarios)
/// y respeta el límite de subidas por ejecución para proteger la cuota gratuita de YouTube Data API v3.
/// </summary>
public class InstagramYouTubeSyncService : IInstagramYouTubeSyncService
{
    private readonly IInstagramApiClient _instagramClient;
    private readonly IInstagramReelSyncRepository _syncRepository;
    private readonly IInstagramToYouTubeCaptionAdapter _captionAdapter;
    private readonly IYouTubeShortsUploadClient _youTubeUploadClient;
    private readonly InstagramYouTubeSyncOptions _options;
    private readonly ILogger<InstagramYouTubeSyncService> _logger;

    public InstagramYouTubeSyncService(
        IInstagramApiClient instagramClient,
        IInstagramReelSyncRepository syncRepository,
        IInstagramToYouTubeCaptionAdapter captionAdapter,
        IYouTubeShortsUploadClient youTubeUploadClient,
        IOptions<InstagramYouTubeSyncOptions> options,
        ILogger<InstagramYouTubeSyncService> logger)
    {
        _instagramClient = instagramClient ?? throw new ArgumentNullException(nameof(instagramClient));
        _syncRepository = syncRepository ?? throw new ArgumentNullException(nameof(syncRepository));
        _captionAdapter = captionAdapter ?? throw new ArgumentNullException(nameof(captionAdapter));
        _youTubeUploadClient = youTubeUploadClient ?? throw new ArgumentNullException(nameof(youTubeUploadClient));
        _options = options?.Value ?? new InstagramYouTubeSyncOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InstagramYouTubeSyncResultDto> SyncReelsToYouTubeAsync(
        int? maxUploads = null,
        CancellationToken ct = default)
    {
        int effectiveMaxUploads = Math.Clamp(maxUploads ?? _options.MaxUploadsPerRun, 1, 6);
        int fetchLimit = Math.Clamp(_options.MaxFetchReels, 1, 100);

        // 1. Consultar los Reels de la cuenta oficial de Instagram de Ludeka
        var accountReels = await _instagramClient.GetAccountReelsAsync(fetchLimit, ct);

        // Ordenar de más antiguo a más reciente para preservar el orden cronológico del historial
        var orderedReels = accountReels
            .OrderBy(r => r.Timestamp)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToList();

        var mediaUrlByIgId = orderedReels
            .GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().MediaUrl, StringComparer.OrdinalIgnoreCase);

        int newlyEnqueued = 0;

        // 2. Registrar nuevos Reels descubiertos en la tabla de control
        foreach (var reel in orderedReels)
        {
            var existing = await _syncRepository.GetByInstagramMediaIdAsync(reel.Id, ct);
            if (existing == null)
            {
                var newItem = new InstagramReelSyncItem(
                    instagramMediaId: reel.Id,
                    instagramPermalink: reel.Permalink,
                    caption: reel.Caption,
                    mediaPublishedAtUtc: reel.Timestamp);

                await _syncRepository.AddAsync(newItem, ct);
                newlyEnqueued++;
            }
        }

        // 3. Obtener los pendientes de subida (en orden cronológico de publicación) hasta el tope diario seguro
        var pendingItems = await _syncRepository.GetPendingForUploadAsync(
            maxItems: effectiveMaxUploads,
            maxAttempts: _options.MaxRetryAttempts,
            ct: ct);

        int uploadedCount = 0;
        int failedCount = 0;
        int skippedCount = 0;
        bool stoppedByQuota = false;

        foreach (var item in pendingItems)
        {
            ct.ThrowIfCancellationRequested();

            if (!mediaUrlByIgId.TryGetValue(item.InstagramMediaId, out var freshMediaUrl) ||
                string.IsNullOrWhiteSpace(freshMediaUrl))
            {
                _logger.LogWarning("El Reel {MediaId} ya no devuelve una media_url activa en el feed de Instagram. Se omite en este ciclo.", item.InstagramMediaId);
                skippedCount++;
                continue;
            }

            try
            {
                // 4. Adaptar título y descripción con el motor híbrido (Directorio Ludeka + Gemini + Pie determinista)
                var adapted = await _captionAdapter.AdaptCaptionForYouTubeShortAsync(item.Caption, ct);
                item.UpdateAdaptedMetadata(adapted.Title, adapted.Description);

                // 5. Subir el flujo MP4 a YouTube Shorts
                var uploadResult = await _youTubeUploadClient.UploadShortFromUrlAsync(
                    mediaUrl: freshMediaUrl,
                    title: adapted.Title,
                    description: adapted.Description,
                    ct: ct);

                if (uploadResult.IsQuotaExceeded)
                {
                    item.MarkFailed(uploadResult.ErrorMessage ?? "Cuota diaria de YouTube agotada.");
                    await _syncRepository.UpdateAsync(item, ct);
                    failedCount++;
                    stoppedByQuota = true;
                    break;
                }

                item.MarkUploaded(uploadResult.VideoId, uploadResult.ShortUrl);
                await _syncRepository.UpdateAsync(item, ct);
                uploadedCount++;

                _logger.LogInformation(
                    "Reel {InstagramMediaId} sincronizado con éxito a YouTube Short {YouTubeVideoId} ({Title}).",
                    item.InstagramMediaId,
                    uploadResult.VideoId,
                    adapted.Title);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error al sincronizar el Reel {InstagramMediaId} a YouTube Shorts.", item.InstagramMediaId);
                item.MarkFailed(ex.Message);
                await _syncRepository.UpdateAsync(item, ct);
                failedCount++;
            }
        }

        return new InstagramYouTubeSyncResultDto(
            DiscoveredReelsCount: orderedReels.Count,
            NewlyEnqueuedCount: newlyEnqueued,
            UploadedCount: uploadedCount,
            FailedCount: failedCount,
            SkippedCount: skippedCount,
            StoppedByQuotaLimit: stoppedByQuota);
    }

    public async Task<IReadOnlyList<InstagramReelSyncItemDto>> GetSyncItemsAsync(
        InstagramReelSyncStatus? statusFilter = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        var items = await _syncRepository.GetAllAsync(statusFilter, limit, ct);
        return items.Select(MapToDto).ToList();
    }

    private static InstagramReelSyncItemDto MapToDto(InstagramReelSyncItem item) =>
        new(
            Id: item.Id,
            InstagramMediaId: item.InstagramMediaId,
            InstagramPermalink: item.InstagramPermalink,
            Caption: item.Caption,
            MediaPublishedAtUtc: item.MediaPublishedAtUtc,
            Status: item.Status,
            GeneratedYouTubeTitle: item.GeneratedYouTubeTitle,
            GeneratedYouTubeDescription: item.GeneratedYouTubeDescription,
            YouTubeVideoId: item.YouTubeVideoId,
            YouTubeUrl: item.YouTubeUrl,
            AttemptCount: item.AttemptCount,
            LastError: item.LastError,
            CreatedAtUtc: item.CreatedAtUtc,
            SyncedAtUtc: item.SyncedAtUtc);
}
