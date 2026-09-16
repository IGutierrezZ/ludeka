using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Features.Events;

public class BoardGameEventService : IBoardGameEventService
{
    private const string DenialMessage =
        "Se requiere el permiso de moderación 'CanManageEvents' para gestionar eventos lúdicos.";

    private readonly IBoardGameEventRepository _repository;
    private readonly ISessionPermissionGuard? _permissionGuard;

    public BoardGameEventService(
        IBoardGameEventRepository repository,
        ISessionPermissionGuard? permissionGuard = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _permissionGuard = permissionGuard;
    }

    /// <summary>
    /// Revalida sesión y permiso releyendo el <c>AppUser</c> actual (INC-46, W1); sin contexto web
    /// (pruebas y ejecuciones sin consola de sesión) la dependencia es nula y no hay nada que exigir.
    /// </summary>
    private Task RequirePermissionAsync(CancellationToken ct)
        => _permissionGuard is null
            ? Task.CompletedTask
            : _permissionGuard.RequireAsync(ModeratorPermission.CanManageEvents, DenialMessage, ct);

    public async Task<IReadOnlyList<BoardGameEventDto>> GetUpcomingEventsAsync(int limit = 50, string? country = null, CancellationToken ct = default)
    {
        if (limit < 1) limit = 50;

        var allEvents = await _repository.GetAllEventsAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = allEvents.Where(e => e.EndDate >= today);

        if (!string.IsNullOrWhiteSpace(country))
        {
            query = query.Where(e => e.IsCelebratedInCountry(country));
        }

        return query
            .OrderBy(e => e.StartDate)
            .Take(limit)
            .Select(e => MapToDto(e, today))
            .ToList();
    }

    public async Task<IReadOnlyList<BoardGameEventDto>> GetPastEventsAsync(int limit = 50, string? country = null, CancellationToken ct = default)
    {
        if (limit < 1) limit = 50;

        var allEvents = await _repository.GetAllEventsAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = allEvents.Where(e => e.EndDate < today);

        if (!string.IsNullOrWhiteSpace(country))
        {
            query = query.Where(e => e.IsCelebratedInCountry(country));
        }

        return query
            .OrderByDescending(e => e.EndDate)
            .Take(limit)
            .Select(e => MapToDto(e, today))
            .ToList();
    }

    public async Task<BoardGameEventDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var evt = await _repository.GetByIdAsync(id, ct);
        if (evt == null) return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return MapToDto(evt, today);
    }

    public async Task<BoardGameEventDto> CreateEventAsync(CreateBoardGameEventRequest request, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        ArgumentNullException.ThrowIfNull(request);

        var evt = new BoardGameEvent(
            title: request.Title,
            description: request.Description,
            imageUrl: request.ImageUrl,
            startDate: request.StartDate,
            endDate: request.EndDate,
            location: request.Location,
            country: request.Country,
            websiteUrl: request.WebsiteUrl,
            organizer: request.Organizer,
            isOfficial: request.IsOfficial);

        await _repository.AddAsync(evt, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return MapToDto(evt, today);
    }

    public async Task<BoardGameEventDto> UpdateEventAsync(Guid id, UpdateBoardGameEventRequest request, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        ArgumentNullException.ThrowIfNull(request);

        var evt = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"No se encontró ningún evento lúdico con el identificador '{id}'.");

        evt.Update(
            title: request.Title,
            description: request.Description,
            imageUrl: request.ImageUrl,
            startDate: request.StartDate,
            endDate: request.EndDate,
            location: request.Location,
            country: request.Country,
            websiteUrl: request.WebsiteUrl,
            organizer: request.Organizer,
            isOfficial: request.IsOfficial);

        await _repository.UpdateAsync(evt, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return MapToDto(evt, today);
    }

    public async Task DeleteEventAsync(Guid id, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        await _repository.DeleteAsync(id, ct);
    }

    public static BoardGameEventDto MapToDto(BoardGameEvent e, DateOnly today)
    {
        string remainingDaysText;
        if (e.IsOngoing(today))
        {
            remainingDaysText = "¡En curso!";
        }
        else
        {
            int days = e.DaysUntilStart(today);
            remainingDaysText = days switch
            {
                0 => "Empieza hoy",
                1 => "Empieza mañana",
                > 1 => $"En {days} días",
                _ => "Finalizado"
            };
        }

        return new BoardGameEventDto(
            e.Id,
            e.Title,
            e.Description,
            e.ImageUrl,
            e.StartDate,
            e.EndDate,
            e.Location,
            e.WebsiteUrl,
            e.Organizer,
            e.IsOfficial,
            e.GetFormattedDates(),
            remainingDaysText,
            e.Country,
            Ludeka.Core.ValueObjects.CountryCatalog.GetFlag(e.Country),
            e.IsInternational);
    }
}
