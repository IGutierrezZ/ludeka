using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Features.Bgg;

public class BggImportService : IBggImportService
{
    private readonly IBggClient _bggClient;
    private readonly IGameRepository _gameRepo;
    private readonly IUserCollectionRepository _collectionRepo;
    private readonly IPendingBggImportRepository _pendingRepo;
    private readonly ICurrentUserService _currentUserService;

    public BggImportService(
        IBggClient bggClient,
        IGameRepository gameRepo,
        IUserCollectionRepository collectionRepo,
        IPendingBggImportRepository pendingRepo,
        ICurrentUserService currentUserService)
    {
        _bggClient = bggClient;
        _gameRepo = gameRepo;
        _collectionRepo = collectionRepo;
        _pendingRepo = pendingRepo;
        _currentUserService = currentUserService;
    }

    public Task<BggImportResultDto> ImportUserCollectionAsync(BggImportRequest request, CancellationToken ct = default)
    {
        return ImportUserCollectionAsync(request, null, ct);
    }

    public async Task<BggImportResultDto> ImportUserCollectionAsync(
        BggImportRequest request,
        IProgress<BggImportProgressReport>? progress,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            throw new ArgumentException("El nombre de usuario de BGG no puede estar vacío.", nameof(request.Username));

        // Invariante de anonimia: importar la colección escribe identidad y exige sesión.
        string userId = SessionIdentity.Require(_currentUserService);
        progress?.Report(new BggImportProgressReport(BggImportPhase.Initializing, "Iniciando proceso de importación..."));

        var bggItems = await _bggClient.FetchUserCollectionAsync(request.Username.Trim(), progress, ct);

        int importedCount = 0;
        int enqueuedCount = 0;
        var errors = new List<string>();

        if (bggItems.Count > 0)
        {
            progress?.Report(new BggImportProgressReport(
                BggImportPhase.ProcessingItems,
                $"Cruzando {bggItems.Count} títulos de BGG con el catálogo oficial de Ludeka...",
                ItemsFound: bggItems.Count));
        }

        foreach (var bggItem in bggItems)
        {
            CollectionStatus? targetStatus = null;
            if (bggItem.IsOwned && request.ImportOwned)
            {
                targetStatus = CollectionStatus.InCollection;
            }
            else if ((bggItem.IsWishlist || bggItem.IsWantToBuy) && request.ImportWishlist)
            {
                targetStatus = CollectionStatus.WantToBuy;
            }

            bool isPlayed = bggItem.NumPlays > 0;

            if (targetStatus == null && !isPlayed) continue;

            try
            {
                var localGame = await _gameRepo.GetByBggIdAsync(bggItem.BggId, ct);
                if (localGame != null)
                {
                    // El juego YA existe en el catálogo local
                    var existingItem = await _collectionRepo.GetByUserAndGameAsync(userId, localGame.Id, ct);
                    if (existingItem == null)
                    {
                        var newItem = new UserCollectionItem(userId, localGame.Id, targetStatus, isPlayed);
                        await _collectionRepo.AddAsync(newItem, ct);
                        importedCount++;
                    }
                    else
                    {
                        bool updated = false;
                        if (targetStatus.HasValue && existingItem.Status != targetStatus.Value)
                        {
                            existingItem.ChangeStatus(targetStatus.Value);
                            updated = true;
                        }
                        if (isPlayed && !existingItem.IsPlayed)
                        {
                            existingItem.SetPlayed(true);
                            updated = true;
                        }
                        if (updated)
                        {
                            await _collectionRepo.UpdateAsync(existingItem, ct);
                        }
                    }
                }
                else
                {
                    // El juego NO existe en Ludeka: se encola
                    var existingPendingItem = await _collectionRepo.GetByUserAndBggIdAsync(userId, bggItem.BggId, ct);
                    if (existingPendingItem == null)
                    {
                        var newPendingItem = new UserCollectionItem(
                            userId: userId,
                            bggId: bggItem.BggId,
                            pendingTitle: bggItem.Title,
                            status: targetStatus,
                            isPlayed: isPlayed,
                            thumbnailUrl: bggItem.ThumbnailUrl,
                            yearPublished: bggItem.YearPublished
                        );
                        await _collectionRepo.AddAsync(newPendingItem, ct);
                        enqueuedCount++;

                        // Registrar o actualizar en PendingBggImports
                        var pendingQueueEntry = await _pendingRepo.GetByBggIdAsync(bggItem.BggId, ct);
                        if (pendingQueueEntry == null)
                        {
                            var newQueueEntry = new PendingBggImport(
                                bggId: bggItem.BggId,
                                title: bggItem.Title,
                                yearPublished: bggItem.YearPublished,
                                thumbnailUrl: bggItem.ThumbnailUrl,
                                coverImageUrl: bggItem.CoverImageUrl
                            );
                            await _pendingRepo.AddAsync(newQueueEntry, ct);
                        }
                        else
                        {
                            pendingQueueEntry.IncrementRequestCount();
                            await _pendingRepo.UpdateAsync(pendingQueueEntry, ct);
                        }
                    }
                    else if (existingPendingItem.Status != targetStatus.Value)
                    {
                        existingPendingItem.ChangeStatus(targetStatus.Value);
                        await _collectionRepo.UpdateAsync(existingPendingItem, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Error al importar {bggItem.Title} (BggId: {bggItem.BggId}): {ex.Message}");
            }
        }

        progress?.Report(new BggImportProgressReport(
            BggImportPhase.Completed,
            $"Importación finalizada. Total: {bggItems.Count} (En ludoteca: {importedCount}, En cola: {enqueuedCount})",
            ItemsFound: bggItems.Count));

        return new BggImportResultDto(
            TotalProcessed: bggItems.Count,
            ImportedToCollection: importedCount,
            EnqueuedForCataloging: enqueuedCount,
            Errors: errors
        );
    }
}
