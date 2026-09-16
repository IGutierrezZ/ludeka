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

namespace Ludeka.Application.Features.Directory;

public class PublisherService : IPublisherService
{
    private readonly IPublisherRepository _publisherRepository;
    private readonly IGameRepository _gameRepository;
    private readonly ICurrentUserService? _currentUserService;
    private readonly IAuditService? _auditService;

    public PublisherService(
        IPublisherRepository publisherRepository,
        IGameRepository gameRepository,
        ICurrentUserService? currentUserService = null,
        IAuditService? auditService = null)
    {
        _publisherRepository = publisherRepository ?? throw new ArgumentNullException(nameof(publisherRepository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    public async Task<IReadOnlyList<PublisherDto>> GetAllAsync(string? search = null, CancellationToken ct = default)
    {
        var publishers = await _publisherRepository.GetAllAsync(ct);
        var allGames = await _gameRepository.GetAllGamesAsync(ct);

        var query = publishers.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var cleanSearch = search.Trim();
            query = query.Where(p =>
                p.Name.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase) ||
                p.Country.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase) ||
                (p.City != null && p.City.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase)));
        }

        return query
            .OrderBy(p => p.Name)
            .Select(p =>
            {
                var gamesCount = allGames.Count(g => IsMatchPublisher(g.Publisher, p.Name));
                return MapToDto(p, gamesCount);
            })
            .ToList();
    }

    private static bool IsMatchPublisher(string gamePublisher, string publisherName)
    {
        if (string.IsNullOrWhiteSpace(gamePublisher) || string.IsNullOrWhiteSpace(publisherName))
            return false;

        if (string.Equals(gamePublisher, publisherName, StringComparison.OrdinalIgnoreCase) ||
            gamePublisher.Contains(publisherName, StringComparison.OrdinalIgnoreCase) ||
            publisherName.Contains(gamePublisher, StringComparison.OrdinalIgnoreCase))
            return true;

        var firstWord = publisherName.Split(' ', '-', '/', '&')[0];
        if (firstWord.Length >= 4 && gamePublisher.Contains(firstWord, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public async Task<PublisherDetailDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;

        var publisher = await _publisherRepository.GetBySlugAsync(slug.Trim().ToLowerInvariant(), ct);
        if (publisher == null) return null;

        var games = await _gameRepository.GetByPublisherAsync(publisher.Name, ct);
        var gameSummaries = games.Select(g => GameSummaryDto.FromEntity(g)).ToList();

        return MapToDetailDto(publisher, gameSummaries);
    }

    public async Task<PublisherDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var publisher = await _publisherRepository.GetByIdAsync(id, ct);
        if (publisher == null) return null;

        var games = await _gameRepository.GetByPublisherAsync(publisher.Name, ct);
        var gameSummaries = games.Select(g => GameSummaryDto.FromEntity(g)).ToList();

        return MapToDetailDto(publisher, gameSummaries);
    }

    public async Task<PublisherDto> CreateAsync(CreatePublisherDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsurePermission();

        var slug = !string.IsNullOrWhiteSpace(dto.Slug)
            ? Game.GenerateSlug(dto.Slug)
            : Game.GenerateSlug(dto.Name);

        var existing = await _publisherRepository.GetBySlugAsync(slug, ct);
        if (existing != null)
        {
            slug = $"{slug}-{Guid.NewGuid().ToString()[..4]}";
        }

        var socialLinks = dto.SocialLinks?
            .Where(l => !string.IsNullOrWhiteSpace(l.Url))
            .Select(l => new SocialNetworkLink(l.Platform, l.Url, l.Handle, l.Title))
            .ToList();

        var publisher = new Publisher(
            name: dto.Name,
            slug: slug,
            country: dto.Country,
            city: dto.City,
            description: dto.Description,
            logoUrl: dto.LogoUrl,
            websiteUrl: dto.WebsiteUrl,
            socialLinks: socialLinks
        );

        await _publisherRepository.AddAsync(publisher, ct);

        if (_auditService != null && _currentUserService != null)
        {
            await _auditService.RecordChangeAsync(new RecordAuditCommand(
                UserId: _currentUserService.UserId,
                UserName: _currentUserService.UserName,
                Action: AuditAction.Created,
                EntityType: AuditEntityType.Publisher,
                EntityId: publisher.Slug,
                EntityName: publisher.Name,
                Summary: $"Alta de editorial '{publisher.Name}' ({publisher.Country})"
            ), ct);
        }

        return MapToDto(publisher, 0);
    }

    public async Task<PublisherDto> UpdateAsync(Guid id, UpdatePublisherDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsurePermission();

        var publisher = await _publisherRepository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"No se encontró la editorial con ID {id}.");

        var changes = new List<FieldChangeDto>();
        if (!string.Equals(publisher.Name, dto.Name, StringComparison.Ordinal))
            changes.Add(new FieldChangeDto("Name", publisher.Name, dto.Name));
        if (!string.Equals(publisher.Country, dto.Country, StringComparison.Ordinal))
            changes.Add(new FieldChangeDto("Country", publisher.Country, dto.Country));

        publisher.UpdateDetails(
            name: dto.Name,
            country: dto.Country,
            city: dto.City,
            description: dto.Description,
            logoUrl: dto.LogoUrl,
            websiteUrl: dto.WebsiteUrl
        );

        if (dto.SocialLinks != null)
        {
            var mappedLinks = dto.SocialLinks
                .Where(l => !string.IsNullOrWhiteSpace(l.Url))
                .Select(l => new SocialNetworkLink(l.Platform, l.Url, l.Handle, l.Title))
                .ToList();
            publisher.SetSocialLinks(mappedLinks);
        }

        await _publisherRepository.UpdateAsync(publisher, ct);

        if (_auditService != null && _currentUserService != null)
        {
            await _auditService.RecordChangeAsync(new RecordAuditCommand(
                UserId: _currentUserService.UserId,
                UserName: _currentUserService.UserName,
                Action: AuditAction.Updated,
                EntityType: AuditEntityType.Publisher,
                EntityId: publisher.Slug,
                EntityName: publisher.Name,
                Summary: $"Modificación de editorial '{publisher.Name}'",
                Changes: changes
            ), ct);
        }

        var games = await _gameRepository.GetByPublisherAsync(publisher.Name, ct);
        return MapToDto(publisher, games.Count);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        EnsurePermission();

        var publisher = await _publisherRepository.GetByIdAsync(id, ct);
        if (publisher != null)
        {
            await _publisherRepository.DeleteAsync(id, ct);

            if (_auditService != null && _currentUserService != null)
            {
                await _auditService.RecordChangeAsync(new RecordAuditCommand(
                    UserId: _currentUserService.UserId,
                    UserName: _currentUserService.UserName,
                    Action: AuditAction.Deleted,
                    EntityType: AuditEntityType.Publisher,
                    EntityId: publisher.Slug,
                    EntityName: publisher.Name,
                    Summary: $"Eliminación de editorial '{publisher.Name}'"
                ), ct);
            }
        }
    }

    private void EnsurePermission()
    {
        if (_currentUserService == null) return;

        // Invariante de anonimia: sin sesión no hay permiso que valga ni escritura posible.
        SessionIdentity.Require(_currentUserService);

        if (!_currentUserService.IsFoundingTeam && !_currentUserService.HasPermission(ModeratorPermission.CanManagePublishers))
        {
            throw new UnauthorizedAccessException("Se requiere el permiso de moderación 'CanManagePublishers' para dar de alta o editar editoriales.");
        }
    }

    private static PublisherDto MapToDto(Publisher p, int gamesCount)
    {
        var socialDtos = p.SocialLinks.Select(l => new SocialNetworkLinkDto(
            l.Platform, l.Url, l.Handle, l.Title, l.PlatformIcon, l.PlatformName)).ToList();

        return new PublisherDto(
            p.Id,
            p.Name,
            p.Slug,
            p.Country,
            p.City,
            p.Description,
            p.LogoUrl,
            p.WebsiteUrl,
            gamesCount,
            socialDtos,
            p.CreatedAt
        );
    }

    private static PublisherDetailDto MapToDetailDto(Publisher p, IReadOnlyList<GameSummaryDto> games)
    {
        var socialDtos = p.SocialLinks.Select(l => new SocialNetworkLinkDto(
            l.Platform, l.Url, l.Handle, l.Title, l.PlatformIcon, l.PlatformName)).ToList();

        return new PublisherDetailDto(
            p.Id,
            p.Name,
            p.Slug,
            p.Country,
            p.City,
            p.Description,
            p.LogoUrl,
            p.WebsiteUrl,
            socialDtos,
            games,
            p.CreatedAt,
            p.UpdatedAt
        );
    }
}
