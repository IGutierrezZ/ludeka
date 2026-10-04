using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Servicio de búsqueda quirúrgica y moderación de vídeos de YouTube en tiempo real.
/// </summary>
public interface IYouTubeSearchService
{
    Task<IReadOnlyList<YouTubeSearchResultDto>> SearchVideosForGameAsync(Guid gameId, CancellationToken ct = default);
    Task<IReadOnlyList<YouTubeSearchResultDto>> SearchQuickOverviewsAsync(string gameTitle, CancellationToken ct = default);
    Task<IReadOnlyList<YouTubeSearchResultDto>> SearchTutorialsAsync(string gameTitle, CancellationToken ct = default);
    Task<IReadOnlyList<YouTubeSearchResultDto>> SearchPlaythroughsAsync(string gameTitle, Guid? gameId = null, CancellationToken ct = default);
    Task<IReadOnlyList<YouTubeSearchResultDto>> SearchConsolidatedCandidatesAsync(string gameTitle, Guid? gameId = null, CancellationToken ct = default);
    Task<MediaItemDto> IngestVideoAsync(YouTubeIngestRequestDto request, CancellationToken ct = default);
    Task<IReadOnlyList<MediaItemDto>> AutoSuggestAndIngestForGameAsync(Guid gameId, bool autoApprove = false, CancellationToken ct = default);
    Task<IReadOnlyList<MediaItemDto>> AutoSuggestAndIngestConsolidatedForGameAsync(Guid gameId, bool autoApprove = false, CancellationToken ct = default);
}
