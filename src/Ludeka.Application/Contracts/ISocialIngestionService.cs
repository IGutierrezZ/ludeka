using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

public interface ISocialIngestionService
{
    /// <summary>Alta exprés desde la interfaz: exige sesión y permiso de moderación de medios.</summary>
    Task<SocialInboxItemDto> IngestFromUrlAsync(string url, string? manualCaption = null, CancellationToken ct = default);

    /// <summary>Alta exprés multimodal: admite URL, portada y bases (texto o captura).</summary>
    Task<SocialInboxItemDto> IngestMultimodalAsync(SocialExpressMultimodalInputDto input, CancellationToken ct = default);

    /// <summary>
    /// Alta exprés del recolector en segundo plano (INC-46, W1). Es una ruta de sistema sin sesión:
    /// la usan los servicios hospedados que descubren publicaciones, nunca los manejadores de la interfaz.
    /// </summary>
    Task<SocialInboxItemDto> IngestFromCollectorAsync(string url, string? manualCaption = null, CancellationToken ct = default);

    Task<SocialInboxItemDto> IngestManualAdvancedAsync(SocialInboxManualInputDto input, CancellationToken ct = default);
    Task<SocialInboxItemDto> UpdateItemAsync(SocialInboxUpdateDto dto, CancellationToken ct = default);
    Task<SocialInboxItemDto> ReanalyzeWithAiAsync(Guid inboxItemId, CancellationToken ct = default);
    Task<int> PurgeSimulatedItemsAsync(CancellationToken ct = default);
    Task<Guid> ApproveAndPublishAsync(Guid inboxItemId, string reviewerUserId, CancellationToken ct = default);
    Task RejectItemAsync(Guid inboxItemId, string reason, string reviewerUserId, CancellationToken ct = default);
    Task<IReadOnlyList<SocialInboxItemDto>> GetPendingItemsAsync(SocialSubmissionType? typeFilter = null, CancellationToken ct = default);
    Task<IReadOnlyList<SocialInboxItemDto>> GetAllItemsAsync(SocialInboxStatus? statusFilter = null, SocialSubmissionType? typeFilter = null, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<SocialInboxItemDto?> GetItemByIdAsync(Guid id, CancellationToken ct = default);
    Task<int> GetPendingCountAsync(CancellationToken ct = default);
}
