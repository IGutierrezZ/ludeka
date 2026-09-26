using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Features.Library;

public class UserLibraryService : IUserLibraryService
{
    private readonly IUserCollectionRepository _collectionRepo;
    private readonly IGameLoanRepository _loanRepo;
    private readonly IUserReviewRepository _reviewRepo;
    private readonly IGameRepository _gameRepo;
    private readonly ICurrentUserService _currentUserService;

    public UserLibraryService(
        IUserCollectionRepository collectionRepo,
        IGameLoanRepository loanRepo,
        IUserReviewRepository reviewRepo,
        IGameRepository gameRepo,
        ICurrentUserService currentUserService)
    {
        _collectionRepo = collectionRepo;
        _loanRepo = loanRepo;
        _reviewRepo = reviewRepo;
        _gameRepo = gameRepo;
        _currentUserService = currentUserService;
    }

    public async Task<UserCollectionItemDto?> GetCollectionStateAsync(Guid gameId, CancellationToken ct = default)
    {
        string userId = _currentUserService.UserId;
        var item = await _collectionRepo.GetByUserAndGameAsync(userId, gameId, ct);
        if (item == null) return null;

        return await MapCollectionItemAsync(item, ct);
    }

    public async Task<UserCollectionItemDto?> SetCollectionStateAsync(Guid gameId, CollectionStatus? status, CancellationToken ct = default)
    {
        // Invariante de anonimia: escribir en la colección exige sesión.
        string userId = SessionIdentity.Require(_currentUserService);
        var existing = await _collectionRepo.GetByUserAndGameAsync(userId, gameId, ct);

        // Si el estado es Played, se redirige al flujo de jugado independiente
        if (status == CollectionStatus.Played)
        {
            return await TogglePlayedStateAsync(gameId, ct);
        }

        if (!status.HasValue)
        {
            if (existing != null)
            {
                if (existing.IsPlayed)
                {
                    existing.ChangeStatus(null);
                    await _collectionRepo.UpdateAsync(existing, ct);
                    return await MapCollectionItemAsync(existing, ct);
                }

                await _collectionRepo.RemoveAsync(existing, ct);
            }
            return null;
        }

        if (existing != null)
        {
            // Toggle: si se vuelve a pulsar el mismo estado, se desmarca
            if (existing.Status == status.Value)
            {
                if (existing.IsPlayed)
                {
                    existing.ChangeStatus(null);
                    await _collectionRepo.UpdateAsync(existing, ct);
                    return await MapCollectionItemAsync(existing, ct);
                }

                await _collectionRepo.RemoveAsync(existing, ct);
                return null;
            }

            existing.ChangeStatus(status.Value);
            await _collectionRepo.UpdateAsync(existing, ct);
            return await MapCollectionItemAsync(existing, ct);
        }

        var game = await _gameRepo.GetByIdAsync(gameId, ct);
        if (game == null)
        {
            throw new KeyNotFoundException($"No se encontró el juego con ID {gameId}.");
        }

        var newItem = new UserCollectionItem(userId, gameId, status.Value, isPlayed: false);
        await _collectionRepo.AddAsync(newItem, ct);

        return await MapCollectionItemAsync(newItem, ct);
    }

    public async Task<UserCollectionItemDto?> TogglePlayedStateAsync(Guid gameId, CancellationToken ct = default)
    {
        string userId = SessionIdentity.Require(_currentUserService);
        var existing = await _collectionRepo.GetByUserAndGameAsync(userId, gameId, ct);

        if (existing == null)
        {
            var game = await _gameRepo.GetByIdAsync(gameId, ct);
            if (game == null)
            {
                throw new KeyNotFoundException($"No se encontró el juego con ID {gameId}.");
            }

            var newItem = new UserCollectionItem(userId, gameId, status: null, isPlayed: true);
            await _collectionRepo.AddAsync(newItem, ct);
            return await MapCollectionItemAsync(newItem, ct);
        }

        bool newPlayed = !existing.IsPlayed;
        if (!newPlayed && !existing.Status.HasValue)
        {
            await _collectionRepo.RemoveAsync(existing, ct);
            return null;
        }

        existing.SetPlayed(newPlayed);
        await _collectionRepo.UpdateAsync(existing, ct);
        return await MapCollectionItemAsync(existing, ct);
    }

    public async Task<UserCollectionItemDto?> SetPlayedStateAsync(Guid gameId, bool isPlayed, CancellationToken ct = default)
    {
        string userId = SessionIdentity.Require(_currentUserService);
        var existing = await _collectionRepo.GetByUserAndGameAsync(userId, gameId, ct);

        if (existing == null)
        {
            if (!isPlayed) return null;

            var game = await _gameRepo.GetByIdAsync(gameId, ct);
            if (game == null)
            {
                throw new KeyNotFoundException($"No se encontró el juego con ID {gameId}.");
            }

            var newItem = new UserCollectionItem(userId, gameId, status: null, isPlayed: true);
            await _collectionRepo.AddAsync(newItem, ct);
            return await MapCollectionItemAsync(newItem, ct);
        }

        if (existing.IsPlayed == isPlayed)
        {
            return await MapCollectionItemAsync(existing, ct);
        }

        if (!isPlayed && !existing.Status.HasValue)
        {
            await _collectionRepo.RemoveAsync(existing, ct);
            return null;
        }

        existing.SetPlayed(isPlayed);
        await _collectionRepo.UpdateAsync(existing, ct);
        return await MapCollectionItemAsync(existing, ct);
    }

    public async Task<GameLoanDto?> GetActiveLoanAsync(Guid gameId, CancellationToken ct = default)
    {
        string userId = _currentUserService.UserId;
        var loan = await _loanRepo.GetActiveLoanByUserAndGameAsync(userId, gameId, ct);
        if (loan == null) return null;

        var game = loan.Game ?? await _gameRepo.GetByIdAsync(gameId, ct);

        return new GameLoanDto(
            loan.Id,
            loan.GameId,
            game?.SpanishTitle ?? "Juego",
            game?.CoverImageUrl,
            game?.Slug ?? string.Empty,
            loan.BorrowerName,
            loan.LoanDate,
            loan.Notes,
            loan.IsReturned,
            loan.ReturnedDate
        );
    }

    public async Task<GameLoanDto> CreateLoanAsync(CreateLoanRequest request, CancellationToken ct = default)
    {
        // Invariante de anonimia: registrar un préstamo exige sesión.
        string userId = SessionIdentity.Require(_currentUserService);

        // Invariante de negocio: solo se pueden prestar juegos que estén en la ludoteca propia (InCollection)
        var collectionItem = await _collectionRepo.GetByUserAndGameAsync(userId, request.GameId, ct);
        if (collectionItem == null || collectionItem.Status != CollectionStatus.InCollection)
        {
            throw new InvalidOperationException("Solo puedes registrar préstamos de juegos que estén en tu ludoteca física propia («En mi ludoteca»).");
        }

        var activeLoan = await _loanRepo.GetActiveLoanByUserAndGameAsync(userId, request.GameId, ct);
        if (activeLoan != null)
        {
            throw new InvalidOperationException("Este juego ya se encuentra registrado como prestado actualmente.");
        }

        var loan = new GameLoan(userId, request.GameId, request.BorrowerName, request.LoanDate, request.Notes);
        await _loanRepo.AddAsync(loan, ct);

        var game = await _gameRepo.GetByIdAsync(request.GameId, ct);

        return new GameLoanDto(
            loan.Id,
            loan.GameId,
            game?.SpanishTitle ?? "Juego",
            game?.CoverImageUrl,
            game?.Slug ?? string.Empty,
            loan.BorrowerName,
            loan.LoanDate,
            loan.Notes,
            loan.IsReturned,
            loan.ReturnedDate
        );
    }

    public async Task<GameLoanDto> ReturnLoanAsync(Guid loanId, CancellationToken ct = default)
    {
        string userId = SessionIdentity.Require(_currentUserService);
        var loan = await _loanRepo.GetByIdAsync(loanId, ct);
        if (loan == null || !string.Equals(loan.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            throw new KeyNotFoundException("Préstamo no encontrado.");
        }

        loan.MarkAsReturned();
        await _loanRepo.UpdateAsync(loan, ct);

        var game = loan.Game ?? await _gameRepo.GetByIdAsync(loan.GameId, ct);

        return new GameLoanDto(
            loan.Id,
            loan.GameId,
            game?.SpanishTitle ?? "Juego",
            game?.CoverImageUrl,
            game?.Slug ?? string.Empty,
            loan.BorrowerName,
            loan.LoanDate,
            loan.Notes,
            loan.IsReturned,
            loan.ReturnedDate
        );
    }

    public async Task<UserReviewDto?> GetUserReviewAsync(Guid gameId, CancellationToken ct = default)
    {
        string userId = _currentUserService.UserId;
        var review = await _reviewRepo.GetByUserAndGameAsync(userId, gameId, ct);
        if (review == null) return null;

        return MapReview(review);
    }

    public async Task<UserReviewDto> SubmitReviewAsync(SubmitReviewRequest request, CancellationToken ct = default)
    {
        // Invariante de anonimia: valorar exige sesión.
        string userId = SessionIdentity.Require(_currentUserService);

        var review = await _reviewRepo.GetByUserAndGameAsync(userId, request.GameId, ct);

        // Regla de negocio de integridad: Solo se puede valorar si se ha jugado o se tiene en ludoteca propia
        var collectionItem = await _collectionRepo.GetByUserAndGameAsync(userId, request.GameId, ct);
        if (review == null && collectionItem != null && collectionItem.Status == CollectionStatus.WantToBuy && !collectionItem.IsPlayed)
        {
            throw new InvalidOperationException("No puedes valorar un juego en tu lista de compra sin haberlo jugado («Jugado»).");
        }

        // Si el juego no estaba marcado como jugado, al emitir la valoración se asegura IsPlayed = true
        if (collectionItem == null)
        {
            collectionItem = new UserCollectionItem(userId, request.GameId, status: null, isPlayed: true);
            await _collectionRepo.AddAsync(collectionItem, ct);
        }
        else if (!collectionItem.IsPlayed)
        {
            collectionItem.SetPlayed(true);
            await _collectionRepo.UpdateAsync(collectionItem, ct);
        }

        if (review != null)
        {
            review.Update(
                request.Score,
                request.MicroReview,
                request.PlayerCountVotes,
                request.FamilyExperience,
                request.PlayContext
            );
            await _reviewRepo.UpdateAsync(review, ct);
        }
        else
        {
            review = new UserGameReview(
                userId,
                request.GameId,
                request.Score,
                request.MicroReview,
                request.PlayerCountVotes,
                request.FamilyExperience,
                request.PlayContext
            );
            await _reviewRepo.AddAsync(review, ct);
        }

        // Recálculo dinámico del consenso local LudistRating
        var avgScore = await _reviewRepo.GetAverageScoreByGameIdAsync(request.GameId, ct);
        if (avgScore.HasValue)
        {
            var game = await _gameRepo.GetByIdAsync(request.GameId, ct);
            if (game != null)
            {
                game.UpdateLudistRating(avgScore.Value);
                await _gameRepo.UpdateAsync(game, ct);
            }
        }

        return MapReview(review);
    }

    public async Task<UserLibrarySummaryDto> GetLibrarySummaryAsync(CancellationToken ct = default)
    {
        string userId = _currentUserService.UserId;

        var counts = await _collectionRepo.GetCountsByStatusAsync(userId, ct);
        var activeLoansCount = await _loanRepo.GetActiveLoansCountAsync(userId, ct);

        var collectionItems = await _collectionRepo.GetByUserIdAsync(userId, null, ct);
        var activeLoans = await _loanRepo.GetActiveLoansByUserIdAsync(userId, ct);

        var activeLoanGameIds = activeLoans.Select(l => l.GameId).ToHashSet();

        var itemDtos = new List<UserCollectionItemDto>();
        foreach (var item in collectionItems)
        {
            if (item.IsPendingCataloging)
            {
                itemDtos.Add(new UserCollectionItemDto(
                    item.Id,
                    null,
                    item.PendingTitle ?? "Juego en cola",
                    item.PendingThumbnailUrl,
                    string.Empty,
                    item.Status,
                    item.IsPlayed,
                    item.AddedAt,
                    false,
                    item.BggId,
                    true
                ));
            }
            else
            {
                var game = item.Game ?? (item.GameId.HasValue ? await _gameRepo.GetByIdAsync(item.GameId.Value, ct) : null);
                bool isLoaned = item.GameId.HasValue && activeLoanGameIds.Contains(item.GameId.Value);
                itemDtos.Add(new UserCollectionItemDto(
                    item.Id,
                    item.GameId,
                    game?.SpanishTitle ?? item.PendingTitle ?? "Juego",
                    game?.CoverImageUrl ?? item.PendingThumbnailUrl,
                    game?.Slug ?? string.Empty,
                    item.Status,
                    item.IsPlayed,
                    item.AddedAt,
                    isLoaned,
                    item.BggId ?? game?.BggId,
                    false,
                    game?.IsExpansion ?? false
                ));
            }
        }

        var loanDtos = new List<GameLoanDto>();
        foreach (var loan in activeLoans)
        {
            var game = loan.Game ?? await _gameRepo.GetByIdAsync(loan.GameId, ct);
            loanDtos.Add(new GameLoanDto(
                loan.Id,
                loan.GameId,
                game?.SpanishTitle ?? "Juego",
                game?.CoverImageUrl,
                game?.Slug ?? string.Empty,
                loan.BorrowerName,
                loan.LoanDate,
                loan.Notes,
                loan.IsReturned,
                loan.ReturnedDate
            ));
        }

        int totalPlayed = collectionItems.Count(i => i.IsPlayed);

        return new UserLibrarySummaryDto(
            counts.GetValueOrDefault(CollectionStatus.InCollection, 0),
            totalPlayed,
            counts.GetValueOrDefault(CollectionStatus.Wishlist, 0),
            counts.GetValueOrDefault(CollectionStatus.WantToBuy, 0),
            activeLoansCount,
            itemDtos,
            loanDtos
        );
    }

    private async Task<UserCollectionItemDto> MapCollectionItemAsync(UserCollectionItem item, CancellationToken ct)
    {
        if (item.IsPendingCataloging)
        {
            return new UserCollectionItemDto(
                item.Id,
                null,
                item.PendingTitle ?? "Juego en cola",
                item.PendingThumbnailUrl,
                string.Empty,
                item.Status,
                item.IsPlayed,
                item.AddedAt,
                false,
                item.BggId,
                true
            );
        }

        var game = item.Game ?? (item.GameId.HasValue ? await _gameRepo.GetByIdAsync(item.GameId.Value, ct) : null);
        var activeLoan = item.GameId.HasValue ? await _loanRepo.GetActiveLoanByUserAndGameAsync(item.UserId, item.GameId.Value, ct) : null;

        return new UserCollectionItemDto(
            item.Id,
            item.GameId,
            game?.SpanishTitle ?? item.PendingTitle ?? "Juego",
            game?.CoverImageUrl ?? item.PendingThumbnailUrl,
            game?.Slug ?? string.Empty,
            item.Status,
            item.IsPlayed,
            item.AddedAt,
            activeLoan != null,
            item.BggId ?? game?.BggId,
            false,
            game?.IsExpansion ?? false
        );
    }

    private static UserReviewDto MapReview(UserGameReview review)
    {
        return new UserReviewDto(
            review.Id,
            review.GameId,
            review.Score,
            review.MicroReview,
            review.PlayerCountRatings.ToList(),
            review.FamilyExperience,
            review.PlayContext,
            review.CreatedAt,
            review.UpdatedAt
        );
    }
}
