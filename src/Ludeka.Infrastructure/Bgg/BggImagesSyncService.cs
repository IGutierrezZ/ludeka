using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Bgg;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Infrastructure.Bgg;

/// <summary>
/// Servicio para sincronizar y enriquecer medios de imágenes comunitarias (portada, contraportada, mesa)
/// para los juegos más relevantes de BoardGameGeek (Top 3.000).
/// </summary>
public class BggImagesSyncService : IBggImagesSyncService
{
    private readonly IDbContextFactory<LudekaDbContext> _contextFactory;
    private readonly IBggRawSnapshotRepository _snapshotRepo;
    private readonly IGeekDoImagesClient _geekDoClient;
    private readonly IImageStorageService _imageStorageService;
    private readonly CloudflareR2Options _r2Options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<BggImagesSyncService> _logger;

    public BggImagesSyncService(
        IDbContextFactory<LudekaDbContext> contextFactory,
        IBggRawSnapshotRepository snapshotRepo,
        IGeekDoImagesClient geekDoClient,
        IImageStorageService imageStorageService,
        IOptions<CloudflareR2Options> r2Options,
        HttpClient httpClient,
        ILogger<BggImagesSyncService> logger)
    {
        _contextFactory = contextFactory;
        _snapshotRepo = snapshotRepo;
        _geekDoClient = geekDoClient;
        _imageStorageService = imageStorageService;
        _r2Options = r2Options?.Value ?? new CloudflareR2Options();
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BggImagesSyncResultDto> SyncTopRankedImagesBatchAsync(
        int afterRank = 0,
        int batchSize = 25,
        int maxRank = 3000,
        int delayMs = 800,
        CancellationToken ct = default)
    {
        if (batchSize <= 0) batchSize = 25;
        if (maxRank <= 0) maxRank = 3000;

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var games = await context.Games
            .Where(g => g.BggRank != null && g.BggRank > afterRank && g.BggRank <= maxRank)
            .OrderBy(g => g.BggRank)
            .Take(batchSize)
            .ToListAsync(ct);

        if (games.Count == 0)
        {
            return new BggImagesSyncResultDto(
                EvaluatedCount: 0,
                UpdatedCount: 0,
                SkippedCount: 0,
                FailedCount: 0,
                LastRankProcessed: afterRank,
                HasMore: false,
                Message: $"No se encontraron más juegos con BggRank > {afterRank} y <= {maxRank}."
            );
        }

        var bggIds = games
            .Where(g => g.BggId > 0)
            .Select(g => g.BggId)
            .Distinct()
            .ToList();

        var snapshots = await _snapshotRepo.GetSnapshotsByBggIdsAsync(bggIds, ct);
        var snapshotsMap = snapshots.ToDictionary(s => s.BggId, s => s);

        int evaluatedCount = 0;
        int updatedCount = 0;
        int skippedCount = 0;
        int failedCount = 0;
        int lastRank = afterRank;

        foreach (var game in games)
        {
            if (ct.IsCancellationRequested) break;

            if (game.BggRank.HasValue && game.BggRank.Value > lastRank)
            {
                lastRank = game.BggRank.Value;
            }

            if (game.BggId <= 0)
            {
                skippedCount++;
                continue;
            }

            int bggId = game.BggId;
            snapshotsMap.TryGetValue(bggId, out var snapshot);
            BggSpanishVersionInfoDto? vInfo = snapshot != null
                ? BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(snapshot.RawJson)
                : null;

            bool isCorruptedCover = IsCorruptedOrSimulatedCover(game.CoverImageUrl);
            bool isMissingBack = string.IsNullOrWhiteSpace(game.BackCoverImageUrl);
            bool isMissingTable = string.IsNullOrWhiteSpace(game.TableImageUrl);
            bool hasSpanishCoverAvailable = vInfo != null &&
                                            !string.IsNullOrWhiteSpace(vInfo.CoverImageUrl) &&
                                            game.CoverImageUrl != vInfo.CoverImageUrl;

            if (!isCorruptedCover && !isMissingBack && !isMissingTable && !hasSpanishCoverAvailable)
            {
                skippedCount++;
                continue;
            }

            evaluatedCount++;

            try
            {
                GeekDoGalleryImagesDto? gallery = null;
                if (isMissingBack || isMissingTable || isCorruptedCover)
                {
                    if (delayMs > 0)
                    {
                        await Task.Delay(delayMs, ct);
                    }

                    gallery = await _geekDoClient.GetTopVotedImagesAsync(bggId, ct);
                }

                string? targetCover = game.CoverImageUrl;
                string? targetThumb = game.ThumbnailUrl;
                string? targetBack = game.BackCoverImageUrl;
                string? targetTable = game.TableImageUrl;

                // 1. Portada frontal: Prioridad absoluta para edición en español
                if (vInfo != null && !string.IsNullOrWhiteSpace(vInfo.CoverImageUrl))
                {
                    targetCover = vInfo.CoverImageUrl;
                    if (!string.IsNullOrWhiteSpace(vInfo.ThumbnailUrl))
                    {
                        targetThumb = vInfo.ThumbnailUrl;
                    }
                }
                else if (isCorruptedCover || string.IsNullOrWhiteSpace(targetCover))
                {
                    if (gallery != null && !string.IsNullOrWhiteSpace(gallery.FrontCoverUrl))
                    {
                        targetCover = gallery.FrontCoverUrl;
                    }
                    else if (snapshot != null)
                    {
                        var (rootCover, rootThumb) = BggRawSnapshotParser.ExtractRootImagesFromJson(snapshot.RawJson);
                        if (!string.IsNullOrWhiteSpace(rootCover))
                        {
                            targetCover = rootCover;
                            targetThumb ??= rootThumb;
                        }
                    }
                }

                // 2. Contraportada
                if (string.IsNullOrWhiteSpace(targetBack) && gallery != null && !string.IsNullOrWhiteSpace(gallery.BackCoverUrl))
                {
                    targetBack = gallery.BackCoverUrl;
                }

                // 3. Fotografía en mesa / componentes
                if (string.IsNullOrWhiteSpace(targetTable) && gallery != null && !string.IsNullOrWhiteSpace(gallery.TableOrGameplayUrl))
                {
                    targetTable = gallery.TableOrGameplayUrl;
                }

                // Persistencia: Cloudflare R2 si está configurado, o CDN directo de BGG (Zero-Cloud)
                if (_r2Options.HasValidCredentials)
                {
                    if (!string.IsNullOrWhiteSpace(targetCover) && !IsR2Url(targetCover))
                    {
                        using var s = await DownloadImageStreamAsync(targetCover, ct);
                        if (s != null)
                        {
                            var variants = await _imageStorageService.UploadGameImageVariantsAsync(s, bggId, "cover", ct);
                            targetCover = variants.FullUrl;
                            targetThumb = variants.ThumbUrl;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(targetBack) && !IsR2Url(targetBack))
                    {
                        using var s = await DownloadImageStreamAsync(targetBack, ct);
                        if (s != null)
                        {
                            var variants = await _imageStorageService.UploadGameImageVariantsAsync(s, bggId, "back", ct);
                            targetBack = variants.FullUrl;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(targetTable) && !IsR2Url(targetTable))
                    {
                        using var s = await DownloadImageStreamAsync(targetTable, ct);
                        if (s != null)
                        {
                            var variants = await _imageStorageService.UploadGameImageVariantsAsync(s, bggId, "table", ct);
                            targetTable = variants.FullUrl;
                        }
                    }
                }

                bool changed = game.CoverImageUrl != targetCover ||
                               game.ThumbnailUrl != targetThumb ||
                               game.BackCoverImageUrl != targetBack ||
                               game.TableImageUrl != targetTable;

                if (changed)
                {
                    game.UpdateMediaUrls(targetCover, targetThumb, targetBack, targetTable);
                    updatedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogWarning(ex, "Fallo al sincronizar imágenes comunitarias para #{BggId} ('{Title}'): {Message}",
                    bggId, game.SpanishTitle, ex.Message);
            }
        }

        if (updatedCount > 0)
        {
            await context.SaveChangesAsync(ct);
        }

        bool hasMore = games.Count == batchSize && lastRank < maxRank;

        return new BggImagesSyncResultDto(
            EvaluatedCount: evaluatedCount,
            UpdatedCount: updatedCount,
            SkippedCount: skippedCount,
            FailedCount: failedCount,
            LastRankProcessed: lastRank,
            HasMore: hasMore,
            Message: $"Lote completado hasta rango #{lastRank}: {updatedCount} actualizados, {skippedCount} omitidos, {failedCount} fallidos."
        );
    }

    private static bool IsCorruptedOrSimulatedCover(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        return url.Contains(".r2.dev/games/", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("/images/game-placeholder.svg", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsR2Url(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!string.IsNullOrWhiteSpace(_r2Options.PublicCdnBaseUrl) &&
            url.StartsWith(_r2Options.PublicCdnBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return url.Contains(".r2.cloudflarestorage.com", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Stream?> DownloadImageStreamAsync(string imageUrl, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, imageUrl);
            req.Headers.Add("User-Agent", "Ludeka/1.0");

            var response = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) return null;

            var ms = new MemoryStream();
            await response.Content.CopyToAsync(ms, ct);
            ms.Position = 0;
            return ms;
        }
        catch
        {
            return null;
        }
    }
}
