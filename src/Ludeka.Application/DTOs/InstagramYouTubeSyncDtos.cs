using System;
using System.Collections.Generic;
using Ludeka.Core.Enums;

namespace Ludeka.Application.DTOs;

/// <summary>
/// Elemento multimedia de vídeo/Reel obtenido desde la cuenta oficial de Instagram de Ludeka.
/// </summary>
public sealed record InstagramAccountMediaDto(
    string Id,
    string MediaType,
    string? MediaProductType,
    string MediaUrl,
    string Permalink,
    string? Caption,
    DateTimeOffset Timestamp);

/// <summary>
/// Mención de cuenta de Instagram (@handle) resuelta contra el directorio de Ludeka.
/// </summary>
public sealed record ResolvedMentionDto(
    string InstagramHandle,
    string OfficialName,
    string? YouTubeHandle,
    string? YouTubeChannelUrl,
    string? LudekaProfileUrl,
    string ReplacementText);

/// <summary>
/// Metadatos adaptados (título, descripción y menciones resueltas) listos para publicar un YouTube Short.
/// </summary>
public sealed record YouTubeAdaptedMetadataDto(
    string Title,
    string Description,
    IReadOnlyList<ResolvedMentionDto> ResolvedMentions);

/// <summary>
/// Resultado de una subida de YouTube Short.
/// </summary>
public sealed record YouTubeShortUploadResultDto(
    string VideoId,
    string ShortUrl,
    bool IsQuotaExceeded = false,
    string? ErrorMessage = null);

/// <summary>
/// Resultado consolidado de un ciclo de sincronización de Reels de Instagram a YouTube Shorts.
/// </summary>
public sealed record InstagramYouTubeSyncResultDto(
    int DiscoveredReelsCount,
    int NewlyEnqueuedCount,
    int UploadedCount,
    int FailedCount,
    int SkippedCount,
    bool StoppedByQuotaLimit);

/// <summary>
/// DTO de lectura para el panel administrativo de Reels sincronizados a YouTube Shorts.
/// </summary>
public sealed record InstagramReelSyncItemDto(
    Guid Id,
    string InstagramMediaId,
    string InstagramPermalink,
    string? Caption,
    DateTimeOffset MediaPublishedAtUtc,
    InstagramReelSyncStatus Status,
    string? GeneratedYouTubeTitle,
    string? GeneratedYouTubeDescription,
    string? YouTubeVideoId,
    string? YouTubeUrl,
    int AttemptCount,
    string? LastError,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SyncedAtUtc);
