using System;
using System.Collections.Generic;
using Ludeka.Core.Enums;

namespace Ludeka.Application.DTOs;

public record SocialNetworkLinkDto(
    SocialPlatform Platform,
    string Url,
    string? Handle = null,
    string? Title = null,
    string? PlatformIcon = null,
    string? PlatformName = null
);

public record DirectorySeedResultDto(
    int PublishersAdded,
    int PublishersUpdated,
    int StoresAdded,
    int StoresUpdated,
    int CreatorsAdded,
    int CreatorsUpdated,
    int TotalPublishers,
    int TotalStores,
    int TotalCreators
);

// --- 1. Editoriales (Publishers) ---
public record PublisherDto(
    Guid Id,
    string Name,
    string Slug,
    string Country,
    string? City,
    string? Description,
    string? LogoUrl,
    string? WebsiteUrl,
    int GamesCount,
    IReadOnlyList<SocialNetworkLinkDto> SocialLinks,
    DateTimeOffset CreatedAt
);

public record PublisherDetailDto(
    Guid Id,
    string Name,
    string Slug,
    string Country,
    string? City,
    string? Description,
    string? LogoUrl,
    string? WebsiteUrl,
    IReadOnlyList<SocialNetworkLinkDto> SocialLinks,
    IReadOnlyList<GameSummaryDto> Games,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

public record CreatePublisherDto(
    string Name,
    string? Slug,
    string Country,
    string? City,
    string? Description,
    string? LogoUrl,
    string? WebsiteUrl,
    List<SocialNetworkLinkDto>? SocialLinks
);

public record UpdatePublisherDto(
    string Name,
    string Country,
    string? City,
    string? Description,
    string? LogoUrl,
    string? WebsiteUrl,
    List<SocialNetworkLinkDto>? SocialLinks
);

// --- 2. Creadores (Creators) ---
public record CreatorDto(
    Guid Id,
    string Name,
    string Slug,
    string? Nationality,
    string? Bio,
    string? AvatarUrl,
    int? BggPersonId,
    string? WebsiteUrl,
    IReadOnlyList<SocialNetworkLinkDto> SocialLinks,
    DateTimeOffset CreatedAt
);

public record CreatorDetailDto(
    Guid Id,
    string Name,
    string Slug,
    string? Nationality,
    string? Bio,
    string? AvatarUrl,
    int? BggPersonId,
    string? WebsiteUrl,
    IReadOnlyList<SocialNetworkLinkDto> SocialLinks,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

public record CreateCreatorDto(
    string Name,
    string? Slug,
    string? Nationality,
    string? Bio,
    string? AvatarUrl,
    int? BggPersonId,
    string? WebsiteUrl,
    List<SocialNetworkLinkDto>? SocialLinks
);

public record UpdateCreatorDto(
    string Name,
    string? Nationality,
    string? Bio,
    string? AvatarUrl,
    int? BggPersonId,
    string? WebsiteUrl,
    List<SocialNetworkLinkDto>? SocialLinks
);

// --- 3. Tiendas (Stores) ---
public record StoreDto(
    Guid Id,
    string Name,
    string Slug,
    StoreType Type,
    string Country,
    string? City,
    string? Address,
    string? Description,
    string? LogoUrl,
    string? WebsiteUrl,
    string? AffiliateCode,
    bool HasLoyaltyProgram,
    int ActiveOffersCount,
    IReadOnlyList<SocialNetworkLinkDto> SocialLinks,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string>? ShippingCountries = null
);

public record StoreDetailDto(
    Guid Id,
    string Name,
    string Slug,
    StoreType Type,
    string Country,
    string? City,
    string? Address,
    string? Description,
    string? LogoUrl,
    string? WebsiteUrl,
    string? AffiliateCode,
    bool HasLoyaltyProgram,
    IReadOnlyList<SocialNetworkLinkDto> SocialLinks,
    IReadOnlyList<StoreGameOfferDto> Offers,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<string>? ShippingCountries = null
);

public record StoreGameOfferDto(
    Guid GameId,
    string GameTitle,
    string GameSlug,
    string? CoverImageUrl,
    decimal? Price,
    string Currency,
    bool InStock,
    string? Badge,
    string AffiliateUrl,
    string? Country = null
);

public record CreateStoreDto(
    string Name,
    string? Slug,
    StoreType Type,
    string? City = null,
    string? Address = null,
    string? Description = null,
    string? LogoUrl = null,
    string? WebsiteUrl = null,
    string? AffiliateCode = null,
    bool HasLoyaltyProgram = false,
    List<SocialNetworkLinkDto>? SocialLinks = null,
    string Country = "España",
    List<string>? ShippingCountries = null
);

public record UpdateStoreDto(
    string Name,
    StoreType Type,
    string? City = null,
    string? Address = null,
    string? Description = null,
    string? LogoUrl = null,
    string? WebsiteUrl = null,
    string? AffiliateCode = null,
    bool HasLoyaltyProgram = false,
    List<SocialNetworkLinkDto>? SocialLinks = null,
    string Country = "España",
    List<string>? ShippingCountries = null
);
