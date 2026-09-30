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

public class StoreService : IStoreService
{
    private readonly IStoreRepository _storeRepository;
    private readonly IGameRepository _gameRepository;
    private readonly ICurrentUserService? _currentUserService;
    private readonly IAuditService? _auditService;
    private readonly IUserLikeRepository? _userLikeRepository;

    public StoreService(
        IStoreRepository storeRepository,
        IGameRepository gameRepository,
        ICurrentUserService? currentUserService = null,
        IAuditService? auditService = null,
        IUserLikeRepository? userLikeRepository = null)
    {
        _storeRepository = storeRepository ?? throw new ArgumentNullException(nameof(storeRepository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _currentUserService = currentUserService;
        _auditService = auditService;
        _userLikeRepository = userLikeRepository;
    }

    public async Task<IReadOnlyList<StoreDto>> GetAllAsync(string? search = null, StoreType? type = null, string? country = null, CancellationToken ct = default)
    {
        var stores = await _storeRepository.GetAllAsync(ct);
        var offerCountsByStore = await _gameRepository.GetOfferCountsByStoreAsync(ct);
        var likesCounts = _userLikeRepository != null
            ? await _userLikeRepository.GetLikesCountsAsync(LikeTargetType.Store, stores.Select(s => s.Id), ct)
            : new Dictionary<Guid, int>();

        var query = stores.AsEnumerable();

        if (type.HasValue)
        {
            query = query.Where(s => s.Type == type.Value);
        }

        if (!string.IsNullOrWhiteSpace(country))
        {
            query = query.Where(s => s.ShipsTo(country));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var cleanSearch = search.Trim();
            query = query.Where(s =>
                s.Name.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase) ||
                s.Country.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase) ||
                (s.City != null && s.City.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase)) ||
                (s.Address != null && s.Address.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase)));
        }

        return query
            .Select(s =>
            {
                var activeOffersCount = offerCountsByStore
                    .Where(kvp => IsMatchStore(kvp.Key, s.Name))
                    .Sum(kvp => kvp.Value);
                var likesCount = likesCounts.GetValueOrDefault(s.Id, 0);

                return MapToDto(s, activeOffersCount, likesCount);
            })
            .OrderByDescending(s => s.LikesCount)
            .ThenBy(s => s.Name)
            .ToList();
    }

    public async Task<StoreDetailDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;

        var store = await _storeRepository.GetBySlugAsync(slug.Trim().ToLowerInvariant(), ct);
        if (store == null) return null;

        var gamesWithOffers = await _gameRepository.GetGamesWithStoreOffersAsync(store.Name, ct);
        var offers = ExtractOffersForStore(store, gamesWithOffers);
        var likesCount = _userLikeRepository != null
            ? await _userLikeRepository.GetLikesCountAsync(LikeTargetType.Store, store.Id, ct)
            : 0;

        return MapToDetailDto(store, offers, likesCount);
    }

    public async Task<StoreDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var store = await _storeRepository.GetByIdAsync(id, ct);
        if (store == null) return null;

        var gamesWithOffers = await _gameRepository.GetGamesWithStoreOffersAsync(store.Name, ct);
        var offers = ExtractOffersForStore(store, gamesWithOffers);
        var likesCount = _userLikeRepository != null
            ? await _userLikeRepository.GetLikesCountAsync(LikeTargetType.Store, store.Id, ct)
            : 0;

        return MapToDetailDto(store, offers, likesCount);
    }

    public async Task<StoreDto> CreateAsync(CreateStoreDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsurePermission();

        var slug = !string.IsNullOrWhiteSpace(dto.Slug)
            ? Game.GenerateSlug(dto.Slug)
            : Game.GenerateSlug(dto.Name);

        var existing = await _storeRepository.GetBySlugAsync(slug, ct);
        if (existing != null)
        {
            slug = $"{slug}-{Guid.NewGuid().ToString()[..4]}";
        }

        var socialLinks = dto.SocialLinks?
            .Where(l => !string.IsNullOrWhiteSpace(l.Url))
            .Select(l => new SocialNetworkLink(l.Platform, l.Url, l.Handle, l.Title))
            .ToList();

        var store = new Store(
            name: dto.Name,
            slug: slug,
            type: dto.Type,
            country: dto.Country,
            city: dto.City,
            address: dto.Address,
            description: dto.Description,
            logoUrl: dto.LogoUrl,
            websiteUrl: dto.WebsiteUrl,
            affiliateCode: dto.AffiliateCode,
            hasLoyaltyProgram: dto.HasLoyaltyProgram,
            socialLinks: socialLinks,
            shippingCountries: dto.ShippingCountries
        );

        await _storeRepository.AddAsync(store, ct);

        if (_auditService != null && _currentUserService != null)
        {
            await _auditService.RecordChangeAsync(new RecordAuditCommand(
                UserId: _currentUserService.UserId,
                UserName: _currentUserService.UserName,
                Action: AuditAction.Created,
                EntityType: AuditEntityType.Store,
                EntityId: store.Slug,
                EntityName: store.Name,
                Summary: $"Alta de tienda '{store.Name}' ({store.Country})"
            ), ct);
        }

        return MapToDto(store, 0);
    }

    public async Task<StoreDto> UpdateAsync(Guid id, UpdateStoreDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsurePermission();

        var store = await _storeRepository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"No se encontró la tienda con ID {id}.");

        var changes = new List<FieldChangeDto>();
        if (!string.Equals(store.Name, dto.Name, StringComparison.Ordinal))
            changes.Add(new FieldChangeDto("Name", store.Name, dto.Name));

        if (!string.Equals(store.Country, dto.Country, StringComparison.Ordinal))
            changes.Add(new FieldChangeDto("Country", store.Country, dto.Country));

        store.UpdateDetails(
            name: dto.Name,
            type: dto.Type,
            country: dto.Country,
            city: dto.City,
            address: dto.Address,
            description: dto.Description,
            logoUrl: dto.LogoUrl,
            websiteUrl: dto.WebsiteUrl,
            affiliateCode: dto.AffiliateCode,
            hasLoyaltyProgram: dto.HasLoyaltyProgram,
            shippingCountries: dto.ShippingCountries
        );

        if (dto.SocialLinks != null)
        {
            var mappedLinks = dto.SocialLinks
                .Where(l => !string.IsNullOrWhiteSpace(l.Url))
                .Select(l => new SocialNetworkLink(l.Platform, l.Url, l.Handle, l.Title))
                .ToList();
            store.SetSocialLinks(mappedLinks);
        }

        await _storeRepository.UpdateAsync(store, ct);

        if (_auditService != null && _currentUserService != null)
        {
            await _auditService.RecordChangeAsync(new RecordAuditCommand(
                UserId: _currentUserService.UserId,
                UserName: _currentUserService.UserName,
                Action: AuditAction.Updated,
                EntityType: AuditEntityType.Store,
                EntityId: store.Slug,
                EntityName: store.Name,
                Summary: $"Modificación de tienda '{store.Name}'",
                Changes: changes
            ), ct);
        }

        var gamesWithOffers = await _gameRepository.GetGamesWithStoreOffersAsync(store.Name, ct);
        return MapToDto(store, gamesWithOffers.Count);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        EnsurePermission();

        var store = await _storeRepository.GetByIdAsync(id, ct);
        if (store != null)
        {
            await _storeRepository.DeleteAsync(id, ct);

            if (_auditService != null && _currentUserService != null)
            {
                await _auditService.RecordChangeAsync(new RecordAuditCommand(
                    UserId: _currentUserService.UserId,
                    UserName: _currentUserService.UserName,
                    Action: AuditAction.Deleted,
                    EntityType: AuditEntityType.Store,
                    EntityId: store.Slug,
                    EntityName: store.Name,
                    Summary: $"Eliminación de tienda '{store.Name}'"
                ), ct);
            }
        }
    }

    private void EnsurePermission()
    {
        if (_currentUserService == null) return;

        // Invariante de anonimia: sin sesión no hay permiso que valga ni escritura posible.
        SessionIdentity.Require(_currentUserService);

        if (!_currentUserService.IsFoundingTeam && !_currentUserService.HasPermission(ModeratorPermission.CanManageStoreLinks))
        {
            throw new UnauthorizedAccessException("Se requiere el permiso de moderación 'CanManageStoreLinks' para dar de alta o editar tiendas.");
        }
    }

    private static bool IsMatchStore(string offerStoreName, string storeName)
    {
        if (string.IsNullOrWhiteSpace(offerStoreName) || string.IsNullOrWhiteSpace(storeName))
            return false;

        return string.Equals(offerStoreName, storeName, StringComparison.OrdinalIgnoreCase) ||
               offerStoreName.Contains(storeName, StringComparison.OrdinalIgnoreCase) ||
               storeName.Contains(offerStoreName, StringComparison.OrdinalIgnoreCase);
    }

    private static List<StoreGameOfferDto> ExtractOffersForStore(Store store, IReadOnlyList<Game> allGames)
    {
        var result = new List<StoreGameOfferDto>();

        foreach (var g in allGames)
        {
            foreach (var link in g.PurchaseLinks)
            {
                if (IsMatchStore(link.StoreName, store.Name))
                {
                    result.Add(new StoreGameOfferDto(
                        GameId: g.Id,
                        GameTitle: g.SpanishTitle,
                        GameSlug: g.Slug,
                        CoverImageUrl: g.CoverImageUrl,
                        Price: link.Price,
                        Currency: link.Currency,
                        InStock: link.InStock,
                        Badge: link.Badge,
                        AffiliateUrl: link.AffiliateUrl
                    ));
                }
            }
        }

        return result.OrderBy(o => o.Price).ToList();
    }

    private static StoreDto MapToDto(Store s, int activeOffersCount, int likesCount = 0)
    {
        var socialDtos = s.SocialLinks.Select(l => new SocialNetworkLinkDto(
            l.Platform, l.Url, l.Handle, l.Title, l.PlatformIcon, l.PlatformName)).ToList();

        return new StoreDto(
            s.Id,
            s.Name,
            s.Slug,
            s.Type,
            s.Country,
            s.City,
            s.Address,
            s.Description,
            s.LogoUrl,
            s.WebsiteUrl,
            s.AffiliateCode,
            s.HasLoyaltyProgram,
            activeOffersCount,
            socialDtos,
            s.CreatedAt,
            s.ShippingCountries,
            likesCount
        );
    }

    private static StoreDetailDto MapToDetailDto(Store s, IReadOnlyList<StoreGameOfferDto> offers, int likesCount = 0)
    {
        var socialDtos = s.SocialLinks.Select(l => new SocialNetworkLinkDto(
            l.Platform, l.Url, l.Handle, l.Title, l.PlatformIcon, l.PlatformName)).ToList();

        return new StoreDetailDto(
            s.Id,
            s.Name,
            s.Slug,
            s.Type,
            s.Country,
            s.City,
            s.Address,
            s.Description,
            s.LogoUrl,
            s.WebsiteUrl,
            s.AffiliateCode,
            s.HasLoyaltyProgram,
            socialDtos,
            offers,
            s.CreatedAt,
            s.UpdatedAt,
            s.ShippingCountries,
            likesCount
        );
    }
}
