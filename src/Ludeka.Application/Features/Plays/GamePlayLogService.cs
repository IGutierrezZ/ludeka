using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Features.Plays;

public class GamePlayLogService : IGamePlayLogService
{
    private readonly IGamePlayLogRepository _playRepo;
    private readonly IGameRepository _gameRepo;
    private readonly IUserCollectionRepository _collectionRepo;
    private readonly ICurrentUserService _currentUserService;

    public GamePlayLogService(
        IGamePlayLogRepository playRepo,
        IGameRepository gameRepo,
        IUserCollectionRepository collectionRepo,
        ICurrentUserService currentUserService)
    {
        _playRepo = playRepo;
        _gameRepo = gameRepo;
        _collectionRepo = collectionRepo;
        _currentUserService = currentUserService;
    }

    public async Task<GamePlayLogDto> RecordPlayAsync(RecordPlayRequest request, CancellationToken cancellationToken = default)
    {
        // Invariante de anonimia: registrar una partida exige sesión y escribe con la identidad real.
        string userId = SessionIdentity.Require(_currentUserService);
        var game = await _gameRepo.GetByIdAsync(request.GameId, cancellationToken);
        if (game == null)
        {
            throw new KeyNotFoundException($"No se encontró el juego con ID {request.GameId}.");
        }

        var play = new GamePlayLog(
            userId,
            request.GameId,
            request.PlayDate,
            request.Location,
            request.PlayerCount,
            request.DurationMinutes,
            request.Comment
        );

        await _playRepo.AddAsync(play, cancellationToken);

        // Regla de dominio: registrar una partida marca automáticamente el juego como Jugado (IsPlayed = true)
        var collectionItem = await _collectionRepo.GetByUserAndGameAsync(userId, request.GameId, cancellationToken);
        if (collectionItem == null)
        {
            var newItem = new UserCollectionItem(userId, request.GameId, status: null, isPlayed: true);
            await _collectionRepo.AddAsync(newItem, cancellationToken);
        }
        else if (!collectionItem.IsPlayed)
        {
            collectionItem.SetPlayed(true);
            await _collectionRepo.UpdateAsync(collectionItem, cancellationToken);
        }

        return MapPlay(play, game);
    }

    public async Task<List<GamePlayLogDto>> GetUserPlaysAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        string resolvedUserId = string.IsNullOrWhiteSpace(userId) ? _currentUserService.UserId : userId.Trim();
        var plays = await _playRepo.GetByUserIdAsync(resolvedUserId, cancellationToken);

        var result = new List<GamePlayLogDto>();
        foreach (var p in plays)
        {
            var game = p.Game ?? await _gameRepo.GetByIdAsync(p.GameId, cancellationToken);
            result.Add(MapPlay(p, game));
        }

        return result;
    }

    public async Task<List<GamePlayLogDto>> GetGamePlaysAsync(Guid gameId, string? userId = null, CancellationToken cancellationToken = default)
    {
        string resolvedUserId = string.IsNullOrWhiteSpace(userId) ? _currentUserService.UserId : userId.Trim();
        var plays = await _playRepo.GetByUserAndGameAsync(resolvedUserId, gameId, cancellationToken);

        var game = await _gameRepo.GetByIdAsync(gameId, cancellationToken);
        return plays.Select(p => MapPlay(p, game)).ToList();
    }

    public async Task<UserPlaysStatsDto> GetUserPlaysStatsAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        string resolvedUserId = string.IsNullOrWhiteSpace(userId) ? _currentUserService.UserId : userId.Trim();
        var plays = await _playRepo.GetByUserIdAsync(resolvedUserId, cancellationToken);

        if (plays.Count == 0)
        {
            return new UserPlaysStatsDto(0, null, 0, null, null, 0);
        }

        int totalPlays = plays.Count;

        // Título más jugado
        var mostPlayedGroup = plays
            .GroupBy(p => p.GameId)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        string? mostPlayedTitle = null;
        int mostPlayedCount = 0;
        if (mostPlayedGroup != null)
        {
            mostPlayedCount = mostPlayedGroup.Count();
            var firstPlay = mostPlayedGroup.First();
            var game = firstPlay.Game ?? await _gameRepo.GetByIdAsync(firstPlay.GameId, cancellationToken);
            mostPlayedTitle = game?.SpanishTitle ?? "Juego";
        }

        // Ubicación favorita
        var favLocation = plays
            .GroupBy(p => p.Location)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        // Número de jugadores más habitual
        int? mostCommonPlayerCount = plays
            .GroupBy(p => p.PlayerCount)
            .OrderByDescending(g => g.Count())
            .Select(g => (int?)g.Key)
            .FirstOrDefault();

        // Partidas este mes
        var now = DateTimeOffset.UtcNow;
        int playsThisMonth = plays.Count(p => p.PlayDate.Year == now.Year && p.PlayDate.Month == now.Month);

        return new UserPlaysStatsDto(
            totalPlays,
            mostPlayedTitle,
            mostPlayedCount,
            favLocation,
            mostCommonPlayerCount,
            playsThisMonth
        );
    }

    public async Task DeletePlayAsync(Guid playId, CancellationToken cancellationToken = default)
    {
        string userId = SessionIdentity.Require(_currentUserService);
        var play = await _playRepo.GetByIdAsync(playId, cancellationToken);
        if (play == null || !string.Equals(play.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _playRepo.DeleteAsync(playId, cancellationToken);
    }

    private static GamePlayLogDto MapPlay(GamePlayLog play, Game? game)
    {
        return new GamePlayLogDto(
            play.Id,
            play.GameId,
            game?.SpanishTitle ?? "Juego",
            game?.CoverImageUrl,
            game?.Slug ?? string.Empty,
            play.PlayDate,
            play.Location,
            play.PlayerCount,
            play.DurationMinutes,
            play.Comment,
            play.CreatedAt
        );
    }
}
