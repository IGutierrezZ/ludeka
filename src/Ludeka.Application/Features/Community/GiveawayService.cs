using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Caching.Memory;

namespace Ludeka.Application.Features.Community;

public class GiveawayService : IGiveawayService
{
    private const string DenialMessage =
        "Se requiere el permiso de moderación 'CanApproveMedia' para crear o promover sorteos.";

    private readonly IGiveawayRepository _repository;
    private readonly ISessionPermissionGuard? _permissionGuard;
    private readonly IMemoryCache? _cache;
    private static int _cacheVersion = 0;

    public GiveawayService(
        IGiveawayRepository repository,
        ISessionPermissionGuard? permissionGuard = null,
        IMemoryCache? cache = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _permissionGuard = permissionGuard;
        _cache = cache;
    }

    private static void InvalidateCache() => Interlocked.Increment(ref _cacheVersion);

    /// <summary>
    /// Revalida sesión y permiso releyendo el <c>AppUser</c> actual (INC-46, W1). Los sorteos se
    /// crean y promueven desde una página pública, así que la única puerta es esta comprobación.
    /// </summary>
    private Task RequirePermissionAsync(CancellationToken ct)
        => _permissionGuard is null
            ? Task.CompletedTask
            : _permissionGuard.RequireAsync(ModeratorPermission.CanApproveMedia, DenialMessage, ct);

    public async Task<IReadOnlyList<GiveawayDto>> GetGiveawaysAsync(bool includeExpired = false, string? country = null, CancellationToken ct = default)
    {
        string cacheKey = $"giveaways:v{_cacheVersion}:{includeExpired}:{country?.ToLowerInvariant() ?? "all"}";
        if (_cache != null && _cache.TryGetValue(cacheKey, out IReadOnlyList<GiveawayDto>? cached) && cached != null)
        {
            return cached;
        }

        var giveaways = await _repository.GetGiveawaysAsync(includeExpired, ct);

        if (!string.IsNullOrWhiteSpace(country))
        {
            giveaways = giveaways.Where(g => g.IsAvailableInCountry(country)).ToList();
        }

        var result = giveaways
            .OrderByDescending(g => g.IsPromoted)
            .ThenBy(g => g.DeadlineAt)
            .Select(MapToDto)
            .ToList();

        _cache?.Set(cacheKey, (IReadOnlyList<GiveawayDto>)result, TimeSpan.FromMinutes(5));
        return result;
    }

    public async Task<GiveawayDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var giveaway = await _repository.GetByIdAsync(id, ct);
        return giveaway != null ? MapToDto(giveaway) : null;
    }

    public async Task SetPromotedAsync(Guid id, bool isPromoted, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        var giveaway = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"No se encontró ningún sorteo con el identificador '{id}'.");

        giveaway.SetPromoted(isPromoted);
        await _repository.UpdateAsync(giveaway, ct);
        InvalidateCache();
    }

    public async Task<GiveawayDto> UpdateGiveawayAsync(UpdateGiveawayRequest request, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        ArgumentNullException.ThrowIfNull(request);

        var giveaway = await _repository.GetByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException($"No se encontró ningún sorteo con el identificador '{request.Id}'.");

        giveaway.Update(
            title: request.Title,
            organizer: request.Organizer,
            collaborator: request.Collaborator,
            url: request.Url,
            platform: request.Platform,
            deadlineAt: request.DeadlineAt,
            country: request.Country,
            gameId: request.GameId,
            gameTitle: request.GameTitle,
            thumbnailUrl: request.ThumbnailUrl,
            isCommunityExclusive: request.IsCommunityExclusive,
            isPromoted: request.IsPromoted);

        await _repository.UpdateAsync(giveaway, ct);
        InvalidateCache();
        return MapToDto(giveaway);
    }

    public async Task DeleteGiveawayAsync(Guid id, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        var giveaway = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"No se encontró ningún sorteo con el identificador '{id}'.");

        await _repository.DeleteAsync(id, ct);
        InvalidateCache();
    }

    public async Task<GiveawayDto> CreateOrMergeGiveawayAsync(CreateGiveawayRequest request, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        ArgumentNullException.ThrowIfNull(request);

        // Buscar posible colaboración o duplicado
        var existing = await _repository.FindDuplicateOrCollaborativeAsync(
            request.Title,
            request.Organizer,
            request.DeadlineAt,
            ct);

        if (existing != null)
        {
            // Fusión de colaboraciones (ej. editorial + influencer)
            var collaboratorToMerge = !string.IsNullOrWhiteSpace(request.Collaborator)
                ? request.Collaborator
                : request.Organizer;

            existing.MergeCollaborator(collaboratorToMerge);
            await _repository.UpdateAsync(existing, ct);
            InvalidateCache();
            return MapToDto(existing);
        }

        var newGiveaway = new Giveaway(
            title: request.Title,
            organizer: request.Organizer,
            url: request.Url,
            platform: request.Platform,
            deadlineAt: request.DeadlineAt,
            country: request.Country,
            gameId: request.GameId,
            gameTitle: request.GameTitle,
            collaborator: request.Collaborator,
            thumbnailUrl: request.ThumbnailUrl,
            isCommunityExclusive: request.IsCommunityExclusive,
            isPromoted: request.IsPromoted);

        await _repository.AddAsync(newGiveaway, ct);
        InvalidateCache();
        return MapToDto(newGiveaway);
    }

    private static GiveawayDto MapToDto(Giveaway g)
    {
        var remainingText = CalculateRemainingTime(g.DeadlineAt, g.IsExpired);

        return new GiveawayDto(
            g.Id,
            g.Title,
            g.Organizer,
            g.Collaborator,
            g.FormattedOrganizer,
            g.Url,
            g.Platform,
            g.DeadlineAt,
            remainingText,
            g.IsExpired,
            g.GameId,
            g.GameTitle,
            g.ThumbnailUrl,
            g.IsCommunityExclusive,
            g.CreatedAt,
            g.IsPromoted,
            g.Country,
            Ludeka.Core.ValueObjects.CountryCatalog.GetFlag(g.Country),
            g.IsInternational,
            g.InstagramPermalink,
            g.IsPublishedOnInstagram);
    }

    private static string CalculateRemainingTime(DateTimeOffset deadline, bool isExpired)
    {
        if (isExpired)
            return "Finalizado";

        var diff = deadline - DateTimeOffset.UtcNow;
        if (diff.TotalHours < 24)
            return "Finaliza hoy";

        var days = (int)Math.Floor(diff.TotalDays);
        return days == 1 ? "1 día" : $"{days} días";
    }
}
