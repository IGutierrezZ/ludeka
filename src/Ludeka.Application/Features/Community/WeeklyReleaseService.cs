using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Features.Community;

public class WeeklyReleaseService : IWeeklyReleaseService
{
    private const string DenialMessage =
        "Se requiere el permiso de moderación 'CanApproveMedia' para gestionar novedades editoriales.";

    private readonly IWeeklyReleaseRepository _repository;
    private readonly ISessionPermissionGuard? _permissionGuard;
    private readonly IGameRepository? _gameRepository;
    private readonly IBggClient? _bggClient;

    public WeeklyReleaseService(
        IWeeklyReleaseRepository repository,
        ISessionPermissionGuard? permissionGuard = null,
        IGameRepository? gameRepository = null,
        IBggClient? bggClient = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _permissionGuard = permissionGuard;
        _gameRepository = gameRepository;
        _bggClient = bggClient;
    }

    /// <summary>
    /// Revalida sesión y permiso releyendo el <c>AppUser</c> actual (INC-46, W1). Las novedades se
    /// crean desde una página pública, así que la única puerta es esta comprobación de servicio.
    /// </summary>
    private Task RequirePermissionAsync(CancellationToken ct)
        => _permissionGuard is null
            ? Task.CompletedTask
            : _permissionGuard.RequireAsync(ModeratorPermission.CanApproveMedia, DenialMessage, ct);

    public async Task<IReadOnlyList<WeeklyReleaseDto>> GetReleasesAsync(DateOnly? fromDate = null, CancellationToken ct = default)
    {
        var releases = await _repository.GetReleasesAsync(fromDate, ct);
        return releases
            .Where(r => r.Status == WeeklyReleaseStatus.Published)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<IReadOnlyList<WeeklyReleaseDto>> GetPendingModerationReleasesAsync(CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        var releases = await _repository.GetReleasesAsync(null, ct);
        return releases
            .Where(r => r.Status == WeeklyReleaseStatus.PendingModeration)
            .OrderByDescending(r => r.CreatedAt)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<WeeklyReleaseDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var release = await _repository.GetByIdAsync(id, ct);
        return release is null ? null : MapToDto(release);
    }

    public async Task<WeeklyReleaseDto> CreateReleaseAsync(CreateWeeklyReleaseRequest request, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        ArgumentNullException.ThrowIfNull(request);

        var release = new WeeklyRelease(
            title: request.Title,
            publisher: request.Publisher,
            releaseDate: request.ReleaseDate,
            gameId: request.GameId,
            coverImageUrl: request.CoverImageUrl,
            estimatedPvp: request.EstimatedPvp,
            isReprint: request.IsReprint,
            notes: request.Notes,
            sourceUrl: request.SourceUrl,
            isMonthOnly: request.IsMonthOnly);

        await _repository.AddAsync(release, ct);
        return MapToDto(release);
    }

    public async Task<WeeklyReleaseDto> UpdateReleaseAsync(Guid id, UpdateWeeklyReleaseRequest request, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        ArgumentNullException.ThrowIfNull(request);

        var existing = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"No se encontró el lanzamiento con ID {id}.");

        existing.Update(
            title: request.Title,
            publisher: request.Publisher,
            releaseDate: request.ReleaseDate,
            gameId: request.GameId,
            coverImageUrl: request.CoverImageUrl,
            estimatedPvp: request.EstimatedPvp,
            isReprint: request.IsReprint,
            notes: request.Notes,
            sourceUrl: request.SourceUrl,
            isMonthOnly: request.IsMonthOnly);

        await _repository.UpdateAsync(existing, ct);
        return MapToDto(existing);
    }

    public async Task<WeeklyReleaseDto> ApproveReleaseAsync(
        Guid id,
        Guid? linkedGameId = null,
        bool useAiSuggestionIfAvailable = true,
        CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        var existing = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"No se encontró el lanzamiento con ID {id}.");

        Guid? finalGameId = linkedGameId;

        if (!finalGameId.HasValue && useAiSuggestionIfAvailable && existing.AiSuggestedBggId.HasValue)
        {
            var bggId = existing.AiSuggestedBggId.Value;
            if (_gameRepository != null)
            {
                var localGame = await _gameRepository.GetByBggIdAsync(bggId, ct);
                if (localGame != null)
                {
                    finalGameId = localGame.Id;
                }
                else if (_bggClient != null)
                {
                    try
                    {
                        var fetched = await _bggClient.FetchGameByBggIdAsync(bggId, ct);
                        if (fetched != null)
                        {
                            await _gameRepository.AddRangeAsync([fetched], ct);
                            finalGameId = fetched.Id;
                        }
                    }
                    catch
                    {
                        // Fallback seguro si la llamada de red a BGG falla
                    }
                }
            }
        }

        existing.Approve(finalGameId);
        await _repository.UpdateAsync(existing, ct);
        return MapToDto(existing);
    }

    public async Task RejectReleaseAsync(Guid id, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        var existing = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"No se encontró el lanzamiento con ID {id}.");

        existing.Reject();
        await _repository.UpdateAsync(existing, ct);
    }

    public async Task DeleteReleaseAsync(Guid id, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        await _repository.DeleteAsync(id, ct);
    }

    private static WeeklyReleaseDto MapToDto(WeeklyRelease r)
    {
        return new WeeklyReleaseDto(
            r.Id,
            r.Title,
            r.Publisher,
            r.ReleaseDate,
            r.GameId,
            r.CoverImageUrl,
            r.EstimatedPvp,
            r.IsReprint,
            r.Notes,
            r.InstagramPermalink,
            r.IsPublishedOnInstagram,
            r.SourceUrl,
            r.CreatedAt,
            r.IsMonthOnly,
            r.Status,
            r.AiSuggestedBggId,
            r.AiSuggestedTitle,
            r.AiMatchReasoning);
    }
}
