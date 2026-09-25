using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ludeka.Application.Contracts;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Application.Features.Sleeves;

/// <summary>
/// Resolvedor contextual de tiendas asociadas y URLs de búsqueda quirúrgica para fundas de cartas.
/// </summary>
public class SleeveStoreUrlResolver : ISleeveStoreUrlResolver
{
    private const string DefaultAffiliateTag = "ludeka";
    private readonly IAffiliateUrlResolver? _affiliateResolver;

    public SleeveStoreUrlResolver(IAffiliateUrlResolver? affiliateResolver = null)
    {
        _affiliateResolver = affiliateResolver;
    }

    public string ResolveStoreUrl(string storeName, double widthMm, double heightMm, string? affiliateCode = null)
    {
        var tag = string.IsNullOrWhiteSpace(affiliateCode) ? DefaultAffiliateTag : affiliateCode.Trim();
        var widthStr = widthMm.ToString("0.#", CultureInfo.InvariantCulture);
        var heightStr = heightMm.ToString("0.#", CultureInfo.InvariantCulture);
        var dimensionQuery = $"{widthStr}x{heightStr}";

        var normalizedStore = storeName?.Trim().ToLowerInvariant() ?? string.Empty;
        string rawUrl;

        if (normalizedStore.Contains("zacatrus"))
        {
            rawUrl = $"https://zacatrus.es/catalogsearch/result/?q=fundas+{dimensionQuery}";
        }
        else if (normalizedStore.Contains("dungeon") || normalizedStore.Contains("marvels"))
        {
            rawUrl = $"https://dungeonmarvels.com/buscar?controller=search&s=fundas+{dimensionQuery}";
        }
        else if (normalizedStore.Contains("cuarto") || normalizedStore.Contains("juegos"))
        {
            rawUrl = $"https://cuartodejuegos.es/buscar?q=fundas+{dimensionQuery}";
        }
        else if (normalizedStore.Contains("tablerum"))
        {
            rawUrl = $"https://tablerum.es/buscar?q=fundas+{dimensionQuery}";
        }
        else if (normalizedStore.Contains("amazon"))
        {
            rawUrl = $"https://www.amazon.es/s?k=fundas+{dimensionQuery}";
        }
        else
        {
            // Fallback genérico a búsqueda
            rawUrl = $"https://zacatrus.es/catalogsearch/result/?q=fundas+{dimensionQuery}";
        }

        if (_affiliateResolver != null)
        {
            return _affiliateResolver.ResolveAffiliateUrl(rawUrl, storeName);
        }

        // Fallback heredado directo si no hay resolver configurado
        string paramName = "ref";
        string effectiveTag = tag;

        if (normalizedStore.Contains("tablerum"))
        {
            paramName = "partner";
        }
        else if (normalizedStore.Contains("amazon"))
        {
            paramName = "tag";
            if (string.Equals(tag, DefaultAffiliateTag, StringComparison.OrdinalIgnoreCase))
            {
                effectiveTag = "ludeka-21";
            }
        }

        var separator = rawUrl.Contains('?') ? "&" : "?";
        return $"{rawUrl}{separator}{paramName}={effectiveTag}";
    }

    public IReadOnlyList<SleevePurchaseOptionDto> ResolvePurchaseOptions(SleeveItem sleeve, string? userCountry = null)
    {
        ArgumentNullException.ThrowIfNull(sleeve);

        var options = new List<SleevePurchaseOptionDto>();

        // 1. Si la funda ya tiene una URL de afiliado directa personalizada en base de datos
        if (!string.IsNullOrWhiteSpace(sleeve.AffiliateUrl))
        {
            var directStore = string.IsNullOrWhiteSpace(sleeve.StoreName) ? "Tienda Oficial / Asociada" : sleeve.StoreName.Trim();
            var directCountry = string.IsNullOrWhiteSpace(sleeve.Country) ? "España" : CountryCatalog.Normalize(sleeve.Country);

            var resolvedDirectUrl = _affiliateResolver != null
                ? _affiliateResolver.ResolveAffiliateUrl(sleeve.AffiliateUrl, directStore)
                : sleeve.AffiliateUrl;

            options.Add(new SleevePurchaseOptionDto(
                StoreName: directStore,
                StoreLogoUrl: "/images/store-placeholder.svg",
                PurchaseUrl: resolvedDirectUrl,
                Country: directCountry,
                FormattedPrice: "Desde ~2,95 €",
                Badge: "Recomendado",
                IsDirectPartner: true,
                ShippingCountries: sleeve.ShippingCountries
            ));
        }

        // 2. Opciones de socios comerciales con enlace quirúrgico por medidas
        var partnerStores = new[]
        {
            new
            {
                Name = "Zacatrus",
                Logo = "/images/stores/zacatrus.png",
                Country = "España",
                ShippingCountries = new[] { "España", "Portugal" },
                Badge = "Envío 24h",
                Price = "~2,95 € (pack)"
            },
            new
            {
                Name = "Dungeon Marvels",
                Logo = "/images/stores/dungeon-marvels.png",
                Country = "España",
                ShippingCountries = new[] { "España", "Portugal" },
                Badge = "Gran Variedad",
                Price = "~2,80 € (pack)"
            },
            new
            {
                Name = "Cuarto de Juegos",
                Logo = "/images/stores/cuarto-de-juegos.png",
                Country = "España",
                ShippingCountries = new[] { "España", "Portugal" },
                Badge = "Especialistas",
                Price = "~2,90 € (pack)"
            },
            new
            {
                Name = "Tablerum",
                Logo = "/images/stores/tablerum.png",
                Country = "España",
                ShippingCountries = new[] { "España", "Portugal" },
                Badge = "Gran Catálogo",
                Price = "~2,85 € (pack)"
            },
            new
            {
                Name = "Amazon",
                Logo = "/images/stores/amazon.png",
                Country = "España",
                ShippingCountries = new[] { "España", "Portugal", "Internacional" },
                Badge = "Prime / Rápido",
                Price = "Ver opciones"
            }
        };

        foreach (var partner in partnerStores)
        {
            // Evitar duplicar si ya vino en la URL directa
            if (options.Any(o => string.Equals(o.StoreName, partner.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var url = ResolveStoreUrl(partner.Name, sleeve.WidthMm, sleeve.HeightMm);
            options.Add(new SleevePurchaseOptionDto(
                StoreName: partner.Name,
                StoreLogoUrl: partner.Logo,
                PurchaseUrl: url,
                Country: partner.Country,
                FormattedPrice: partner.Price,
                Badge: partner.Badge,
                IsDirectPartner: true,
                ShippingCountries: partner.ShippingCountries
            ));
        }

        // 3. Filtrado territorial según el país efectivo del usuario (INC-29)
        if (string.IsNullOrWhiteSpace(userCountry))
        {
            return options;
        }

        var normalizedUserCountry = CountryCatalog.Normalize(userCountry);

        return options.Where(opt =>
        {
            if (string.Equals(CountryCatalog.Normalize(opt.Country), normalizedUserCountry, StringComparison.OrdinalIgnoreCase))
                return true;

            if (CountryCatalog.IsInternational(opt.Country))
                return true;

            if (opt.ShippingCountries != null && opt.ShippingCountries.Any(c =>
                CountryCatalog.IsInternational(c) ||
                string.Equals(CountryCatalog.Normalize(c), normalizedUserCountry, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }).ToList();
    }

    public StandardSleeveFormat? MatchStandardFormat(double widthMm, double heightMm)
    {
        return StandardSleeveCatalog.Match(widthMm, heightMm);
    }
}
