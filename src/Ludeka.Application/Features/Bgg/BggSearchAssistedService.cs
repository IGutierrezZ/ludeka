using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Ludeka.Application.Features.Bgg;

public class BggSearchAssistedService : IBggSearchAssistedService
{
    private readonly IBggClient _bggClient;
    private readonly IGameRepository _gameRepo;
    private readonly IUserCollectionRepository _collectionRepo;
    private readonly IPendingBggImportRepository _pendingRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAiGameSummaryService? _aiSummaryService;
    private readonly Microsoft.Extensions.Logging.ILogger<BggSearchAssistedService>? _logger;

    public BggSearchAssistedService(
        IBggClient bggClient,
        IGameRepository gameRepo,
        IUserCollectionRepository collectionRepo,
        IPendingBggImportRepository pendingRepo,
        ICurrentUserService currentUserService,
        IAiGameSummaryService? aiSummaryService = null,
        Microsoft.Extensions.Logging.ILogger<BggSearchAssistedService>? logger = null)
    {
        _bggClient = bggClient;
        _gameRepo = gameRepo;
        _collectionRepo = collectionRepo;
        _pendingRepo = pendingRepo;
        _currentUserService = currentUserService;
        _aiSummaryService = aiSummaryService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return [];
        }

        var results = await _bggClient.SearchGamesAsync(query.Trim(), ct);
        var enrichedResults = new List<BggSearchResultDto>();

        if (results.Count == 0)
        {
            // Búsqueda asistida local de resiliencia: si BGG requiere token o está saturado, busca en catálogo local
            var localMatches = await _gameRepo.SearchAsync(new GameFilterCriteria(SearchTerm: query.Trim()), page: 1, pageSize: 10, ct: ct);
            foreach (var g in localMatches.Items)
            {
                enrichedResults.Add(new BggSearchResultDto(
                    g.BggId,
                    g.SpanishTitle,
                    g.YearPublished,
                    IsAlreadyCataloged: true,
                    ExistingGameSlug: g.Slug,
                    ExistingGameId: g.Id
                ));
            }
            return enrichedResults;
        }

        foreach (var r in results)
        {
            var localGame = await _gameRepo.GetByBggIdAsync(r.BggId, ct);
            if (localGame != null)
            {
                enrichedResults.Add(r with
                {
                    IsAlreadyCataloged = true,
                    ExistingGameSlug = localGame.Slug,
                    ExistingGameId = localGame.Id
                });
            }
            else
            {
                enrichedResults.Add(r);
            }
        }

        return enrichedResults;
    }

    public async Task<Guid> AddGameToCollectionAsync(int bggId, CollectionStatus status, CancellationToken ct = default)
    {
        if (bggId <= 0)
            throw new ArgumentException("El identificador BGG debe ser mayor a cero.", nameof(bggId));

        // Invariante de anonimia: añadir a la colección exige sesión antes de tocar BGG o la base.
        string userId = SessionIdentity.Require(_currentUserService);
        var existingGame = await _gameRepo.GetByBggIdAsync(bggId, ct);

        Guid gameId;
        if (existingGame == null)
        {
            // Descarga en vivo desde BGG
            var fetchedGame = await _bggClient.FetchGameByBggIdAsync(bggId, ct);
            if (fetchedGame == null)
            {
                throw new InvalidOperationException($"No se pudo descargar la información de BoardGameGeek para el identificador {bggId}.");
            }

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
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "No se pudo generar la síntesis de IA al descargar el juego #{BggId} ('{Title}') de BGG.", bggId, fetchedGame.SpanishTitle);
                }
            }

            await _gameRepo.AddRangeAsync([fetchedGame], ct);
            gameId = fetchedGame.Id;

            // Si estaba en la cola de importaciones pendientes, marcarlo como completado
            var pendingItem = await _pendingRepo.GetByBggIdAsync(bggId, ct);
            if (pendingItem != null)
            {
                pendingItem.MarkAsCompleted();
                await _pendingRepo.UpdateAsync(pendingItem, ct);
            }

            // Promover posibles ítems en espera de otros usuarios
            await _collectionRepo.PromotePendingItemsAsync(bggId, gameId, ct);
        }
        else
        {
            gameId = existingGame.Id;
        }

        // Asociar a la colección del usuario actual
        var existingUserItem = await _collectionRepo.GetByUserAndGameAsync(userId, gameId, ct);
        if (existingUserItem == null)
        {
            var newItem = new UserCollectionItem(userId, gameId, status);
            await _collectionRepo.AddAsync(newItem, ct);
        }
        else if (existingUserItem.Status != status)
        {
            existingUserItem.ChangeStatus(status);
            await _collectionRepo.UpdateAsync(existingUserItem, ct);
        }

        return gameId;
    }
}
