using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Seeding;

/// <summary>
/// Resultado del proceso de siembra y reconciliación del directorio lúdico.
/// </summary>
public record DirectorySeedResult(
    int PublishersAdded,
    int PublishersUpdated,
    int StoresAdded,
    int StoresUpdated,
    int CreatorsAdded,
    int CreatorsUpdated,
    int TotalPublishers,
    int TotalStores,
    int TotalCreators);

/// <summary>
/// Sembrador y reconciliador de Editoriales, Tiendas y Creadores de contenido del ecosistema hispanohablante (INC-54).
/// Lee los datos desde el recurso canónico embebido <c>seed-directory.json</c> (con fallback en disco).
/// Purga los diseñadores de juegos retirados (INC-31) y preserva registros creados manualmente en runtime.
/// </summary>
public static class DirectorySeeder
{
    private class SeedDirectoryModel
    {
        public List<SeedPublisherModel>? Publishers { get; set; }
        public List<SeedStoreModel>? Stores { get; set; }
        public List<SeedCreatorModel>? Creators { get; set; }
    }

    private class SeedPublisherModel
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Country { get; set; } = "España";
        public string? City { get; set; }
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? WebsiteUrl { get; set; }
        public List<SeedSocialLinkModel>? SocialLinks { get; set; }
    }

    private class SeedStoreModel
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Type { get; set; } = "Hybrid";
        public string Country { get; set; } = "España";
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? AffiliateCode { get; set; }
        public bool HasLoyaltyProgram { get; set; }
        public List<SeedSocialLinkModel>? SocialLinks { get; set; }
        public List<string>? ShippingCountries { get; set; }
    }

    private class SeedCreatorModel
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Nationality { get; set; }
        public string? Bio { get; set; }
        public string? AvatarUrl { get; set; }
        public int? BggPersonId { get; set; }
        public string? WebsiteUrl { get; set; }
        public List<SeedSocialLinkModel>? SocialLinks { get; set; }
    }

    private class SeedSocialLinkModel
    {
        public string Platform { get; set; } = "Website";
        public string Url { get; set; } = string.Empty;
        public string? Handle { get; set; }
        public string? Title { get; set; }
    }

    /// <summary>
    /// Slugs de los diseñadores de juegos sembrados por el seed histórico del directorio (INC-31).
    /// Lista cerrada: la purga solo elimina estas filas de semilla, jamás creadores manuales.
    /// </summary>
    private static readonly string[] RetiredSeedCreatorSlugs =
    [
        "elizabeth-hargrave", "klaus-teuber", "uwe-rosenberg",
        "bruno-cathala", "jacob-fryxelius", "jamey-stegmaier"
    ];

    public static async Task<DirectorySeedResult> SeedDirectoryAsync(LudekaDbContext db, CancellationToken ct = default)
    {
        var model = LoadSeedDirectoryModel();

        var (pubsAdded, pubsUpdated) = await SeedPublishersAsync(db, model?.Publishers, ct);
        var (creatorsAdded, creatorsUpdated) = await SeedCreatorsAsync(db, model?.Creators, ct);
        var (storesAdded, storesUpdated) = await SeedStoresAsync(db, model?.Stores, ct);

        var totalPublishers = await db.Publishers.CountAsync(ct);
        var totalStores = await db.Stores.CountAsync(ct);
        var totalCreators = await db.Creators.CountAsync(ct);

        return new DirectorySeedResult(
            pubsAdded, pubsUpdated,
            storesAdded, storesUpdated,
            creatorsAdded, creatorsUpdated,
            totalPublishers, totalStores, totalCreators);
    }

    private static async Task<(int Added, int Updated)> SeedPublishersAsync(LudekaDbContext db, List<SeedPublisherModel>? items, CancellationToken ct)
    {
        if (items == null || items.Count == 0)
        {
            return (0, 0);
        }

        var existing = await db.Publishers.ToListAsync(ct);
        int added = 0;
        int updated = 0;

        foreach (var m in items)
        {
            var match = existing.FirstOrDefault(p => string.Equals(p.Slug, m.Slug, StringComparison.OrdinalIgnoreCase));
            var links = (m.SocialLinks ?? []).Select(MapSocialLink).ToList();

            if (match == null)
            {
                var pub = new Publisher(
                    name: m.Name,
                    slug: m.Slug,
                    country: m.Country,
                    city: m.City,
                    description: m.Description,
                    logoUrl: m.LogoUrl,
                    websiteUrl: m.WebsiteUrl,
                    socialLinks: links
                );

                await db.Publishers.AddAsync(pub, ct);
                existing.Add(pub);
                added++;
            }
            else
            {
                bool changed = false;

                if (string.IsNullOrWhiteSpace(match.LogoUrl) && !string.IsNullOrWhiteSpace(m.LogoUrl))
                {
                    match.UpdateDetails(match.Name, match.Country, match.City ?? m.City, match.Description ?? m.Description, m.LogoUrl, match.WebsiteUrl ?? m.WebsiteUrl);
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(match.WebsiteUrl) && !string.IsNullOrWhiteSpace(m.WebsiteUrl))
                {
                    match.UpdateDetails(match.Name, match.Country, match.City ?? m.City, match.Description ?? m.Description, match.LogoUrl, m.WebsiteUrl);
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(match.City) && !string.IsNullOrWhiteSpace(m.City))
                {
                    match.UpdateDetails(match.Name, match.Country, m.City, match.Description ?? m.Description, match.LogoUrl, match.WebsiteUrl);
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(match.Description) && !string.IsNullOrWhiteSpace(m.Description))
                {
                    match.UpdateDetails(match.Name, match.Country, match.City, m.Description, match.LogoUrl, match.WebsiteUrl);
                    changed = true;
                }

                foreach (var link in links)
                {
                    if (!match.SocialLinks.Any(sl => sl.Platform == link.Platform && sl.Url.Equals(link.Url, StringComparison.OrdinalIgnoreCase)))
                    {
                        match.AddOrUpdateSocialLink(link);
                        changed = true;
                    }
                }

                if (changed)
                {
                    updated++;
                }
            }
        }

        if (added > 0 || updated > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return (added, updated);
    }

    private static async Task<(int Added, int Updated)> SeedCreatorsAsync(LudekaDbContext db, List<SeedCreatorModel>? items, CancellationToken ct)
    {
        // 1. PURGA quirúrgica: elimina solo los diseñadores de la lista cerrada de semillas retiradas (INC-31).
        var retired = await db.Creators
            .Where(c => RetiredSeedCreatorSlugs.Contains(c.Slug))
            .ToListAsync(ct);
        if (retired.Count > 0)
        {
            db.Creators.RemoveRange(retired);
            await db.SaveChangesAsync(ct);
        }

        if (items == null || items.Count == 0)
        {
            return (0, 0);
        }

        var existing = await db.Creators.ToListAsync(ct);
        int added = 0;
        int updated = 0;

        foreach (var m in items)
        {
            var match = existing.FirstOrDefault(c => string.Equals(c.Slug, m.Slug, StringComparison.OrdinalIgnoreCase));
            var links = (m.SocialLinks ?? []).Select(MapSocialLink).ToList();

            if (match == null)
            {
                var creator = new Creator(
                    name: m.Name,
                    slug: m.Slug,
                    nationality: m.Nationality,
                    bio: m.Bio,
                    avatarUrl: m.AvatarUrl,
                    bggPersonId: m.BggPersonId,
                    websiteUrl: m.WebsiteUrl,
                    socialLinks: links
                );

                await db.Creators.AddAsync(creator, ct);
                existing.Add(creator);
                added++;
            }
            else
            {
                bool changed = false;

                if (string.IsNullOrWhiteSpace(match.AvatarUrl) && !string.IsNullOrWhiteSpace(m.AvatarUrl))
                {
                    match.UpdateDetails(match.Name, match.Nationality ?? m.Nationality, match.Bio ?? m.Bio, m.AvatarUrl, match.BggPersonId ?? m.BggPersonId, match.WebsiteUrl ?? m.WebsiteUrl);
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(match.Bio) && !string.IsNullOrWhiteSpace(m.Bio))
                {
                    match.UpdateDetails(match.Name, match.Nationality ?? m.Nationality, m.Bio, match.AvatarUrl, match.BggPersonId ?? m.BggPersonId, match.WebsiteUrl ?? m.WebsiteUrl);
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(match.WebsiteUrl) && !string.IsNullOrWhiteSpace(m.WebsiteUrl))
                {
                    match.UpdateDetails(match.Name, match.Nationality ?? m.Nationality, match.Bio, match.AvatarUrl, match.BggPersonId ?? m.BggPersonId, m.WebsiteUrl);
                    changed = true;
                }

                foreach (var link in links)
                {
                    if (!match.SocialLinks.Any(sl => sl.Platform == link.Platform && sl.Url.Equals(link.Url, StringComparison.OrdinalIgnoreCase)))
                    {
                        match.AddOrUpdateSocialLink(link);
                        changed = true;
                    }
                }

                if (changed)
                {
                    updated++;
                }
            }
        }

        if (added > 0 || updated > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return (added, updated);
    }

    private static async Task<(int Added, int Updated)> SeedStoresAsync(LudekaDbContext db, List<SeedStoreModel>? items, CancellationToken ct)
    {
        if (items == null || items.Count == 0)
        {
            return (0, 0);
        }

        var existing = await db.Stores.ToListAsync(ct);
        int added = 0;
        int updated = 0;

        foreach (var m in items)
        {
            var match = existing.FirstOrDefault(s => string.Equals(s.Slug, m.Slug, StringComparison.OrdinalIgnoreCase));
            var links = (m.SocialLinks ?? []).Select(MapSocialLink).ToList();
            var storeType = Enum.TryParse<StoreType>(m.Type, true, out var st) ? st : StoreType.Hybrid;

            if (match == null)
            {
                var store = new Store(
                    name: m.Name,
                    slug: m.Slug,
                    type: storeType,
                    city: m.City,
                    address: m.Address,
                    description: m.Description,
                    logoUrl: m.LogoUrl,
                    websiteUrl: m.WebsiteUrl,
                    affiliateCode: m.AffiliateCode,
                    hasLoyaltyProgram: m.HasLoyaltyProgram,
                    socialLinks: links,
                    country: m.Country,
                    shippingCountries: m.ShippingCountries
                );

                await db.Stores.AddAsync(store, ct);
                existing.Add(store);
                added++;
            }
            else
            {
                bool changed = false;

                if (string.IsNullOrWhiteSpace(match.LogoUrl) && !string.IsNullOrWhiteSpace(m.LogoUrl))
                {
                    match.UpdateDetails(match.Name, match.Type, match.City ?? m.City, match.Address ?? m.Address, match.Description ?? m.Description, m.LogoUrl, match.WebsiteUrl ?? m.WebsiteUrl, match.AffiliateCode ?? m.AffiliateCode, match.HasLoyaltyProgram, match.Country, match.ShippingCountries);
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(match.WebsiteUrl) && !string.IsNullOrWhiteSpace(m.WebsiteUrl))
                {
                    match.UpdateDetails(match.Name, match.Type, match.City ?? m.City, match.Address ?? m.Address, match.Description ?? m.Description, match.LogoUrl, m.WebsiteUrl, match.AffiliateCode ?? m.AffiliateCode, match.HasLoyaltyProgram, match.Country, match.ShippingCountries);
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(match.Address) && !string.IsNullOrWhiteSpace(m.Address))
                {
                    match.UpdateDetails(match.Name, match.Type, match.City ?? m.City, m.Address, match.Description ?? m.Description, match.LogoUrl, match.WebsiteUrl, match.AffiliateCode ?? m.AffiliateCode, match.HasLoyaltyProgram, match.Country, match.ShippingCountries);
                    changed = true;
                }

                foreach (var link in links)
                {
                    if (!match.SocialLinks.Any(sl => sl.Platform == link.Platform && sl.Url.Equals(link.Url, StringComparison.OrdinalIgnoreCase)))
                    {
                        match.AddOrUpdateSocialLink(link);
                        changed = true;
                    }
                }

                if (changed)
                {
                    updated++;
                }
            }
        }

        if (added > 0 || updated > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return (added, updated);
    }

    private static SocialNetworkLink MapSocialLink(SeedSocialLinkModel m)
    {
        var platform = Enum.TryParse<SocialPlatform>(m.Platform, true, out var p) ? p : SocialPlatform.Website;
        return new SocialNetworkLink(platform, m.Url, m.Handle, m.Title);
    }

    private static SeedDirectoryModel? LoadSeedDirectoryModel()
    {
        string json = ReadDirectorySeedJson();
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<SeedDirectoryModel>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private static string ReadDirectorySeedJson()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("seed-directory.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName != null)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
        }

        // Fallback al sistema de archivos local
        string localPath = Path.Combine(AppContext.BaseDirectory, "Seeding", "seed-directory.json");
        if (File.Exists(localPath))
        {
            return File.ReadAllText(localPath);
        }

        string directPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "Ludeka.Infrastructure", "Seeding", "seed-directory.json");
        if (File.Exists(directPath))
        {
            return File.ReadAllText(directPath);
        }

        return string.Empty;
    }
}
