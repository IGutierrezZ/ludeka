using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Application.Features.Bgg;

public class BggCatalogQueueService : IBggCatalogQueueService
{
    private const string DenialMessage =
        "Se requiere el permiso de moderación 'CanEditGames' para procesar la cola de catalogación.";

    private readonly IPendingBggImportRepository _pendingRepo;
    private readonly IBggClient _bggClient;
    private readonly IGameRepository _gameRepo;
    private readonly IUserCollectionRepository _collectionRepo;
    private readonly IAiGameSummaryService? _aiSummaryService;
    private readonly ISessionPermissionGuard? _permissionGuard;

    public BggCatalogQueueService(
        IPendingBggImportRepository pendingRepo,
        IBggClient bggClient,
        IGameRepository gameRepo,
        IUserCollectionRepository collectionRepo,
        IAiGameSummaryService? aiSummaryService = null,
        ISessionPermissionGuard? permissionGuard = null)
    {
        _pendingRepo = pendingRepo;
        _bggClient = bggClient;
        _gameRepo = gameRepo;
        _collectionRepo = collectionRepo;
        _aiSummaryService = aiSummaryService;
        _permissionGuard = permissionGuard;
    }

    /// <summary>
    /// Revalida sesión y permiso releyendo el <c>AppUser</c> actual (INC-46, W1): la cola se procesa
    /// desde el panel administrativo y desde la pestaña pública «Cola comunitaria».
    /// </summary>
    private Task RequirePermissionAsync(CancellationToken ct)
        => _permissionGuard is null
            ? Task.CompletedTask
            : _permissionGuard.RequireAsync(ModeratorPermission.CanEditGames, DenialMessage, ct);

    public async Task<IReadOnlyList<CatalogQueueItemDto>> GetTopPendingQueueAsync(int limit = 50, CancellationToken ct = default)
    {
        var items = await _pendingRepo.GetTopPendingAsync(limit, ct);
        return items.Select(i => new CatalogQueueItemDto(
            i.Id,
            i.BggId,
            i.Title,
            i.YearPublished,
            i.ThumbnailUrl,
            i.RequestedCount,
            i.Status,
            i.CreatedAt,
            i.ProcessedAt,
            i.ErrorMessage,
            i.Origin,
            i.ExtractedTitle
        )).ToList();
    }

    public async Task<int> GetTotalPendingCountAsync(CancellationToken ct = default)
    {
        return await _pendingRepo.GetTotalPendingCountAsync(ct);
    }

    public async Task<ProcessQueueResultDto> ProcessPendingQueueBatchAsync(int batchSize = 20, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        var topPending = await _pendingRepo.GetTopPendingAsync(batchSize, ct);
        if (topPending.Count == 0)
        {
            return new ProcessQueueResultDto(0, 0, 0, []);
        }

        int successCount = 0;
        int failedCount = 0;
        var catalogedTitles = new List<string>();

        foreach (var pending in topPending)
        {
            pending.MarkAsProcessing();
            await _pendingRepo.UpdateAsync(pending, ct);

            try
            {
                var fetchedGame = await _bggClient.FetchGameByBggIdAsync(pending.BggId, ct);
                if (fetchedGame == null)
                {
                    throw new InvalidOperationException($"BGG no devolvió información para el juego #{pending.BggId}. Puede que el juego no exista o que el token de API no tenga permisos suficientes.");
                }

                // Asegurar persistencia del juego en el catálogo local
                var existingGame = await _gameRepo.GetByBggIdAsync(pending.BggId, ct);
                Guid gameId;
                if (existingGame == null)
                {
                    if (_aiSummaryService != null)
                    {
                        try
                        {
                            var summaryDto = await _aiSummaryService.GenerateSummaryAsync(fetchedGame, ct);
                            fetchedGame.SetAiSummary(new AiGameSummary(
                                summaryDto.GeneralVerdict,
                                summaryDto.ScalabilitySummary,
                                summaryDto.AgeSummary,
                                summaryDto.FootprintSummary,
                                summaryDto.Model,
                                summaryDto.GeneratedAt ?? DateTime.UtcNow
                            ));
                        }
                        catch
                        {
                            // Salvaguarda para no bloquear la importación de catálogo si falla la síntesis
                        }
                    }

                    await _gameRepo.AddRangeAsync([fetchedGame], ct);
                    gameId = fetchedGame.Id;
                }
                else
                {
                    gameId = existingGame.Id;
                    if (existingGame.AiSummary == null && _aiSummaryService != null)
                    {
                        try
                        {
                            var summaryDto = await _aiSummaryService.GenerateSummaryAsync(existingGame, ct);
                            existingGame.SetAiSummary(new AiGameSummary(
                                summaryDto.GeneralVerdict,
                                summaryDto.ScalabilitySummary,
                                summaryDto.AgeSummary,
                                summaryDto.FootprintSummary,
                                summaryDto.Model,
                                summaryDto.GeneratedAt ?? DateTime.UtcNow
                            ));
                            await _gameRepo.UpdateAsync(existingGame, ct);
                        }
                        catch
                        {
                            // Salvaguarda
                        }
                    }
                }

                // Promoción atómica de todas las colecciones de usuario en espera
                await _collectionRepo.PromotePendingItemsAsync(pending.BggId, gameId, ct);

                pending.MarkAsCompleted();
                await _pendingRepo.UpdateAsync(pending, ct);

                catalogedTitles.Add(fetchedGame.SpanishTitle);
                successCount++;
            }
            catch (Exception ex)
            {
                pending.MarkAsFailed(ex.Message);
                await _pendingRepo.UpdateAsync(pending, ct);
                failedCount++;
            }
        }

        return new ProcessQueueResultDto(
            ProcessedCount: topPending.Count,
            SuccessCount: successCount,
            FailedCount: failedCount,
            CatalogedGameTitles: catalogedTitles
        );
    }

    public async Task ResetFailedItemsAsync(CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        await _pendingRepo.ResetFailedToPendingAsync(ct);
    }
}
