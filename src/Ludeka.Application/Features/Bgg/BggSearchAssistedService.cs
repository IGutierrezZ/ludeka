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
    private readonly IBggRawSnapshotRepository? _snapshotRepo;
    private readonly ILogger<BggSearchAssistedService>? _logger;

    public BggSearchAssistedService(
        IBggClient bggClient,
        IGameRepository gameRepo,
        IUserCollectionRepository collectionRepo,
        IPendingBggImportRepository pendingRepo,
        ICurrentUserService currentUserService,
        IAiGameSummaryService? aiSummaryService = null,
        IBggRawSnapshotRepository? snapshotRepo = null,
        ILogger<BggSearchAssistedService>? logger = null)
    {
        _bggClient = bggClient;
        _gameRepo = gameRepo;
        _collectionRepo = collectionRepo;
        _pendingRepo = pendingRepo;
        _currentUserService = currentUserService;
        _aiSummaryService = aiSummaryService;
        _snapshotRepo = snapshotRepo;
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

            // 1. Auto-vinculación bidireccional inmediata
            await TryAutoLinkBidirectionalAsync(fetchedGame, ct);

            // 2. Generación de síntesis general de juego
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

                // 3. Si es una expansión, generar automáticamente el aporte de expansión
                if (fetchedGame.Type == GameType.Expansion)
                {
                    try
                    {
                        Game? baseGame = null;
                        if (fetchedGame.BaseGameId.HasValue)
                        {
                            baseGame = await _gameRepo.GetByIdAsync(fetchedGame.BaseGameId.Value, ct);
                        }

                        var aporteDto = await _aiSummaryService.GenerateExpansionAporteAsync(fetchedGame, baseGame, ct);
                        fetchedGame.SetExpansionAporte(
                            aporteDto.Necessity,
                            aporteDto.ImpactTags,
                            aporteDto.WhatItBringsSummary,
                            aporteDto.ExtraPlayerCount,
                            aporteDto.ExtraDurationMinutes
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "No se pudo generar la síntesis de aporte de expansión con IA para #{BggId} ('{Title}').", bggId, fetchedGame.SpanishTitle);
                    }
                }
            }

            try
            {
                await _gameRepo.AddRangeAsync([fetchedGame], ct);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error al guardar en catálogo el juego BGG #{BggId} ('{Title}').", bggId, fetchedGame.SpanishTitle);
                throw;
            }
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

            // Auto-vinculación oportunista para expansiones huérfanas preexistentes o juegos base
            if ((existingGame.Type == GameType.Expansion && !existingGame.BaseGameId.HasValue) ||
                existingGame.Type == GameType.BaseGame)
            {
                Guid? beforeBase = existingGame.BaseGameId;
                await TryAutoLinkBidirectionalAsync(existingGame, ct);
                if (existingGame.BaseGameId != beforeBase)
                {
                    await _gameRepo.UpdateAsync(existingGame, ct);
                }
            }
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

    private async Task TryAutoLinkBidirectionalAsync(Game game, CancellationToken ct)
    {
        if (_snapshotRepo == null) return;

        try
        {
            var snapshot = await _snapshotRepo.GetByBggIdAsync(game.BggId, ct);
            if (snapshot == null) return;

            bool isExpansion = game.Type == GameType.Expansion || BggRawSnapshotParser.IsExpansionTypeFromJson(snapshot.RawJson);

            if (isExpansion)
            {
                if (game.Type != GameType.Expansion)
                {
                    game.SetGameType(GameType.Expansion);
                }

                if (!game.BaseGameId.HasValue)
                {
                    int? inboundBaseBggId = BggRawSnapshotParser.ExtractInboundBaseGameBggIdFromJson(snapshot.RawJson);
                    if (inboundBaseBggId.HasValue && inboundBaseBggId.Value > 0)
                    {
                        var baseGame = await _gameRepo.GetByBggIdAsync(inboundBaseBggId.Value, ct);
                        if (baseGame != null)
                        {
                            game.SetBaseGameId(baseGame.Id);
                            _logger?.LogInformation(
                                "Auto-vinculada expansión '{ExpansionTitle}' (#{ExpansionBggId}) a juego base '{BaseTitle}' (#{BaseBggId}).",
                                game.SpanishTitle, game.BggId, baseGame.SpanishTitle, baseGame.BggId);
                        }
                    }
                }
            }
            else
            {
                // Es juego base: revisar si existen expansiones huérfanas en catálogo que dependan de este juego
                var outboundLinks = BggRawSnapshotParser.ExtractOutboundExpansionLinksFromJson(snapshot.RawJson);
                var outboundIds = outboundLinks.Select(l => l.BggId).Where(id => id > 0).Distinct().ToList();
                if (outboundIds.Count > 0)
                {
                    var existingChildren = await _gameRepo.GetByBggIdsAsync(outboundIds, ct);
                    foreach (var child in existingChildren)
                    {
                        if (child.BaseGameId == null)
                        {
                            child.SetBaseGameId(game.Id);
                            await _gameRepo.UpdateAsync(child, ct);
                            _logger?.LogInformation(
                                "Auto-vinculada expansión huérfana '{ChildTitle}' (#{ChildBggId}) al nuevo juego base '{BaseTitle}' (#{BaseBggId}).",
                                child.SpanishTitle, child.BggId, game.SpanishTitle, game.BggId);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error no bloqueante durante la auto-vinculación de BGG para #{BggId} ('{Title}').", game.BggId, game.SpanishTitle);
        }
    }
}
