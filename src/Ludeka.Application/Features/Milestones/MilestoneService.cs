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

namespace Ludeka.Application.Features.Milestones;

public class MilestoneService : IMilestoneService
{
    private readonly IUserMilestoneRepository _milestoneRepo;
    private readonly IUserCollectionRepository _collectionRepo;
    private readonly IGamePlayLogRepository _playLogRepo;
    private readonly IGameLoanRepository _loanRepo;
    private readonly IUserReviewRepository _reviewRepo;
    private readonly IUserLikeRepository _likeRepo;

    public MilestoneService(
        IUserMilestoneRepository milestoneRepo,
        IUserCollectionRepository collectionRepo,
        IGamePlayLogRepository playLogRepo,
        IGameLoanRepository loanRepo,
        IUserReviewRepository reviewRepo,
        IUserLikeRepository likeRepo)
    {
        _milestoneRepo = milestoneRepo;
        _collectionRepo = collectionRepo;
        _playLogRepo = playLogRepo;
        _loanRepo = loanRepo;
        _reviewRepo = reviewRepo;
        _likeRepo = likeRepo;
    }

    public async Task<UserMilestoneProgressDto> GetUserMilestonesAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BuildEmptyProgress(string.Empty);

        string normalizedUserId = userId.Trim();
        var unlockedList = await _milestoneRepo.GetByUserIdAsync(normalizedUserId, ct);
        return BuildProgressDto(normalizedUserId, unlockedList);
    }

    public async Task<UserMilestoneProgressDto> EvaluateAndSyncMilestonesAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BuildEmptyProgress(string.Empty);

        string normalizedUserId = userId.Trim();
        var existingMilestones = await _milestoneRepo.GetByUserIdAsync(normalizedUserId, ct);
        var existingTypes = new HashSet<MilestoneType>(existingMilestones.Select(m => m.Type));

        var newlyUnlocked = new List<UserMilestone>();

        // 1. Colección
        var collectionItems = await _collectionRepo.GetByUserIdAsync(normalizedUserId, null, ct);
        var counts = await _collectionRepo.GetCountsByStatusAsync(normalizedUserId, ct);
        int totalInCollection = counts.GetValueOrDefault(CollectionStatus.InCollection, 0);
        int totalPlayed = collectionItems.Count(i => i.IsPlayed);

        if (totalInCollection >= 1 && !existingTypes.Contains(MilestoneType.FirstGameInCollection))
        {
            newlyUnlocked.Add(new UserMilestone(normalizedUserId, MilestoneType.FirstGameInCollection));
        }

        if (totalInCollection >= 10 && !existingTypes.Contains(MilestoneType.TenGamesInCollection))
        {
            newlyUnlocked.Add(new UserMilestone(normalizedUserId, MilestoneType.TenGamesInCollection));
        }

        if (totalPlayed >= 1 && !existingTypes.Contains(MilestoneType.FirstGamePlayed))
        {
            newlyUnlocked.Add(new UserMilestone(normalizedUserId, MilestoneType.FirstGamePlayed));
        }

        if (totalPlayed >= 10 && !existingTypes.Contains(MilestoneType.TenGamesPlayed))
        {
            newlyUnlocked.Add(new UserMilestone(normalizedUserId, MilestoneType.TenGamesPlayed));
        }

        // 2. Préstamos
        int activeLoans = await _loanRepo.GetActiveLoansCountAsync(normalizedUserId, ct);
        var loanHistory = await _loanRepo.GetLoanHistoryByUserIdAsync(normalizedUserId, ct);
        if ((activeLoans > 0 || loanHistory.Count > 0) && !existingTypes.Contains(MilestoneType.FirstGameLoaned))
        {
            newlyUnlocked.Add(new UserMilestone(normalizedUserId, MilestoneType.FirstGameLoaned));
        }

        // 3. Diario de Partidas
        int playsCount = await _playLogRepo.GetCountByUserAsync(normalizedUserId, ct);
        if (playsCount >= 1 && !existingTypes.Contains(MilestoneType.FirstPlayLogged))
        {
            newlyUnlocked.Add(new UserMilestone(normalizedUserId, MilestoneType.FirstPlayLogged));
        }

        if (playsCount >= 5 && !existingTypes.Contains(MilestoneType.FivePlaysLogged))
        {
            newlyUnlocked.Add(new UserMilestone(normalizedUserId, MilestoneType.FivePlaysLogged));
        }

        if (!existingTypes.Contains(MilestoneType.LargeGroupPlay))
        {
            var plays = await _playLogRepo.GetByUserIdAsync(normalizedUserId, ct);
            if (plays.Any(p => p.PlayerCount >= 5))
            {
                newlyUnlocked.Add(new UserMilestone(normalizedUserId, MilestoneType.LargeGroupPlay));
            }
        }

        // 4. Micro-reseñas
        int reviewsCount = await _reviewRepo.GetCountByUserIdAsync(normalizedUserId, ct);
        if (reviewsCount >= 1 && !existingTypes.Contains(MilestoneType.FirstReviewSubmitted))
        {
            newlyUnlocked.Add(new UserMilestone(normalizedUserId, MilestoneType.FirstReviewSubmitted));
        }

        // 5. Comunidad: Me gusta
        if (!existingTypes.Contains(MilestoneType.FirstLikeGiven))
        {
            if (Guid.TryParse(normalizedUserId, out var guidUserId))
            {
                int likesGiven = await _likeRepo.GetLikesGivenCountByUserAsync(guidUserId, ct);
                if (likesGiven >= 1)
                {
                    newlyUnlocked.Add(new UserMilestone(normalizedUserId, MilestoneType.FirstLikeGiven));
                }
            }
        }

        // Guardar nuevos hitos de manera idempotente
        if (newlyUnlocked.Count > 0)
        {
            await _milestoneRepo.UnlockMilestonesAsync(newlyUnlocked, ct);
        }

        // Reconsultar la lista actualizada de hitos
        var updatedList = await _milestoneRepo.GetByUserIdAsync(normalizedUserId, ct);
        return BuildProgressDto(normalizedUserId, updatedList);
    }

    private static UserMilestoneProgressDto BuildProgressDto(string userId, List<UserMilestone> unlockedMilestones)
    {
        var unlockedMap = unlockedMilestones.ToDictionary(m => m.Type, m => m.UnlockedAt);
        var allDefinitions = MilestoneCatalog.All;

        var milestoneDtos = allDefinitions.Select(def =>
        {
            bool isUnlocked = unlockedMap.TryGetValue(def.Type, out var unlockedAt);
            return new MilestoneDto(
                def.Type,
                def.Category,
                def.Title,
                def.Description,
                def.IconEmoji,
                def.SortOrder,
                isUnlocked,
                isUnlocked ? unlockedAt : null
            );
        }).OrderBy(m => m.SortOrder).ToList();

        int totalUnlocked = milestoneDtos.Count(m => m.IsUnlocked);
        int totalMilestones = milestoneDtos.Count;
        double percentage = totalMilestones > 0
            ? Math.Round((double)totalUnlocked / totalMilestones * 100.0, 1)
            : 0.0;

        return new UserMilestoneProgressDto(
            userId,
            totalUnlocked,
            totalMilestones,
            percentage,
            milestoneDtos
        );
    }

    private static UserMilestoneProgressDto BuildEmptyProgress(string userId)
    {
        var allDefinitions = MilestoneCatalog.All;
        var milestoneDtos = allDefinitions.Select(def => new MilestoneDto(
            def.Type,
            def.Category,
            def.Title,
            def.Description,
            def.IconEmoji,
            def.SortOrder,
            false,
            null
        )).OrderBy(m => m.SortOrder).ToList();

        return new UserMilestoneProgressDto(userId, 0, allDefinitions.Count, 0.0, milestoneDtos);
    }
}
