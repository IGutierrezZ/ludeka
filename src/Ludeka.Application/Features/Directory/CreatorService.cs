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

public class CreatorService : ICreatorService
{
    private readonly ICreatorRepository _creatorRepository;
    private readonly ICurrentUserService? _currentUserService;
    private readonly IAuditService? _auditService;

    public CreatorService(
        ICreatorRepository creatorRepository,
        ICurrentUserService? currentUserService = null,
        IAuditService? auditService = null)
    {
        _creatorRepository = creatorRepository ?? throw new ArgumentNullException(nameof(creatorRepository));
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    public async Task<IReadOnlyList<CreatorDto>> GetAllAsync(string? search = null, CancellationToken ct = default)
    {
        var creators = await _creatorRepository.GetAllAsync(ct);

        var query = creators.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var cleanSearch = search.Trim();
            query = query.Where(c =>
                c.Name.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase) ||
                (c.Nationality != null && c.Nationality.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase)));
        }

        return query
            .OrderBy(c => c.Name)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<CreatorDetailDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;

        var creator = await _creatorRepository.GetBySlugAsync(slug.Trim().ToLowerInvariant(), ct);
        if (creator == null) return null;

        return MapToDetailDto(creator);
    }

    public async Task<CreatorDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var creator = await _creatorRepository.GetByIdAsync(id, ct);
        if (creator == null) return null;

        return MapToDetailDto(creator);
    }

    public async Task<CreatorDto> CreateAsync(CreateCreatorDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsurePermission();

        var slug = !string.IsNullOrWhiteSpace(dto.Slug)
            ? Game.GenerateSlug(dto.Slug)
            : Game.GenerateSlug(dto.Name);

        var existing = await _creatorRepository.GetBySlugAsync(slug, ct);
        if (existing != null)
        {
            slug = $"{slug}-{Guid.NewGuid().ToString()[..4]}";
        }

        var socialLinks = dto.SocialLinks?
            .Where(l => !string.IsNullOrWhiteSpace(l.Url))
            .Select(l => new SocialNetworkLink(l.Platform, l.Url, l.Handle, l.Title))
            .ToList();

        var creator = new Creator(
            name: dto.Name,
            slug: slug,
            nationality: dto.Nationality,
            bio: dto.Bio,
            avatarUrl: dto.AvatarUrl,
            bggPersonId: dto.BggPersonId,
            websiteUrl: dto.WebsiteUrl,
            socialLinks: socialLinks
        );

        await _creatorRepository.AddAsync(creator, ct);

        if (_auditService != null && _currentUserService != null)
        {
            await _auditService.RecordChangeAsync(new RecordAuditCommand(
                UserId: _currentUserService.UserId,
                UserName: _currentUserService.UserName,
                Action: AuditAction.Created,
                EntityType: AuditEntityType.Creator,
                EntityId: creator.Slug,
                EntityName: creator.Name,
                Summary: $"Alta de creador de contenido '{creator.Name}'"
            ), ct);
        }

        return MapToDto(creator);
    }

    public async Task<CreatorDto> UpdateAsync(Guid id, UpdateCreatorDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsurePermission();

        var creator = await _creatorRepository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"No se encontró el creador de contenido con ID {id}.");

        var changes = new List<FieldChangeDto>();
        if (!string.Equals(creator.Name, dto.Name, StringComparison.Ordinal))
            changes.Add(new FieldChangeDto("Name", creator.Name, dto.Name));

        creator.UpdateDetails(
            name: dto.Name,
            nationality: dto.Nationality,
            bio: dto.Bio,
            avatarUrl: dto.AvatarUrl,
            bggPersonId: dto.BggPersonId,
            websiteUrl: dto.WebsiteUrl
        );

        if (dto.SocialLinks != null)
        {
            var mappedLinks = dto.SocialLinks
                .Where(l => !string.IsNullOrWhiteSpace(l.Url))
                .Select(l => new SocialNetworkLink(l.Platform, l.Url, l.Handle, l.Title))
                .ToList();
            creator.SetSocialLinks(mappedLinks);
        }

        await _creatorRepository.UpdateAsync(creator, ct);

        if (_auditService != null && _currentUserService != null)
        {
            await _auditService.RecordChangeAsync(new RecordAuditCommand(
                UserId: _currentUserService.UserId,
                UserName: _currentUserService.UserName,
                Action: AuditAction.Updated,
                EntityType: AuditEntityType.Creator,
                EntityId: creator.Slug,
                EntityName: creator.Name,
                Summary: $"Modificación de creador de contenido '{creator.Name}'",
                Changes: changes
            ), ct);
        }

        return MapToDto(creator);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        EnsurePermission();

        var creator = await _creatorRepository.GetByIdAsync(id, ct);
        if (creator != null)
        {
            await _creatorRepository.DeleteAsync(id, ct);

            if (_auditService != null && _currentUserService != null)
            {
                await _auditService.RecordChangeAsync(new RecordAuditCommand(
                    UserId: _currentUserService.UserId,
                    UserName: _currentUserService.UserName,
                    Action: AuditAction.Deleted,
                    EntityType: AuditEntityType.Creator,
                    EntityId: creator.Slug,
                    EntityName: creator.Name,
                    Summary: $"Eliminación de creador de contenido '{creator.Name}'"
                ), ct);
            }
        }
    }

    private void EnsurePermission()
    {
        if (_currentUserService == null) return;

        // Invariante de anonimia: sin sesión no hay permiso que valga ni escritura posible.
        SessionIdentity.Require(_currentUserService);

        if (!_currentUserService.IsFoundingTeam && !_currentUserService.HasPermission(ModeratorPermission.CanManageCreators))
        {
            throw new UnauthorizedAccessException("Se requiere el permiso de moderación 'CanManageCreators' para dar de alta o editar creadores de contenido.");
        }
    }

    private static CreatorDto MapToDto(Creator c)
    {
        var socialDtos = c.SocialLinks.Select(l => new SocialNetworkLinkDto(
            l.Platform, l.Url, l.Handle, l.Title, l.PlatformIcon, l.PlatformName)).ToList();

        return new CreatorDto(
            c.Id,
            c.Name,
            c.Slug,
            c.Nationality,
            c.Bio,
            c.AvatarUrl,
            c.BggPersonId,
            c.WebsiteUrl,
            socialDtos,
            c.CreatedAt
        );
    }

    private static CreatorDetailDto MapToDetailDto(Creator c)
    {
        var socialDtos = c.SocialLinks.Select(l => new SocialNetworkLinkDto(
            l.Platform, l.Url, l.Handle, l.Title, l.PlatformIcon, l.PlatformName)).ToList();

        return new CreatorDetailDto(
            c.Id,
            c.Name,
            c.Slug,
            c.Nationality,
            c.Bio,
            c.AvatarUrl,
            c.BggPersonId,
            c.WebsiteUrl,
            socialDtos,
            c.CreatedAt,
            c.UpdatedAt
        );
    }
}
