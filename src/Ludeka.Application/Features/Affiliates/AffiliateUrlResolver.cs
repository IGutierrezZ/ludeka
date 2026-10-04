using System;
using System.Linq;
using System.Text.RegularExpressions;
using Ludeka.Application.Contracts;
using Ludeka.Application.Options;
using Microsoft.Extensions.Options;

namespace Ludeka.Application.Features.Affiliates;

/// <summary>
/// Motor centralizado para la resolución e inyección privada de parámetros de afiliado en URLs de tiendas.
/// </summary>
public class AffiliateUrlResolver : IAffiliateUrlResolver
{
    private readonly AffiliateOptions _options;

    public AffiliateUrlResolver(IOptions<AffiliateOptions>? options = null)
    {
        _options = options?.Value ?? new AffiliateOptions();
    }

    public string ResolveAffiliateUrl(string rawUrl, string? storeName = null)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(rawUrl))
            return rawUrl;

        var trimmedUrl = rawUrl.Trim();

        // Si no es una URL web válida http/https, no alterar
        if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return trimmedUrl;
        }

        var rule = FindMatchingRule(trimmedUrl, storeName, uri.Host);
        if (rule == null || !rule.Enabled || string.IsNullOrWhiteSpace(rule.AffiliateTag))
        {
            return trimmedUrl;
        }

        return InjectAffiliateParameter(trimmedUrl, rule.ParamName, rule.AffiliateTag);
    }

    private StoreAffiliateRule? FindMatchingRule(string url, string? storeName, string host)
    {
        // 1. Coincidencia por nombre de tienda si se proporciona
        if (!string.IsNullOrWhiteSpace(storeName))
        {
            var normalizedStoreName = storeName.Trim();
            if (_options.Stores.TryGetValue(normalizedStoreName, out var directRule))
            {
                return directRule;
            }

            // Búsqueda aproximada por coincidencia en clave
            var partialRule = _options.Stores.FirstOrDefault(kvp =>
                normalizedStoreName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains(normalizedStoreName, StringComparison.OrdinalIgnoreCase)).Value;

            if (partialRule != null)
                return partialRule;
        }

        // 2. Coincidencia heurística por host/dominio
        var cleanHost = host.ToLowerInvariant();
        foreach (var rule in _options.Stores.Values)
        {
            if (!string.IsNullOrWhiteSpace(rule.DomainMatch) &&
                cleanHost.Contains(rule.DomainMatch.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
            {
                return rule;
            }
        }

        return null;
    }

    private static string InjectAffiliateParameter(string url, string paramName, string affiliateTag)
    {
        var cleanParamName = Uri.EscapeDataString(paramName.Trim());
        var cleanTag = Uri.EscapeDataString(affiliateTag.Trim());

        // Separar fragmento hash si existe (#section)
        string baseAndQuery = url;
        string fragment = string.Empty;
        var hashIndex = url.IndexOf('#');
        if (hashIndex >= 0)
        {
            baseAndQuery = url.Substring(0, hashIndex);
            fragment = url.Substring(hashIndex);
        }

        // Comprobar si el parámetro ya existe en la query string
        var pattern = $@"(?<=[?&]){Regex.Escape(cleanParamName)}=([^&#]*)";
        if (Regex.IsMatch(baseAndQuery, pattern, RegexOptions.IgnoreCase))
        {
            // Reemplazar valor existente
            baseAndQuery = Regex.Replace(
                baseAndQuery,
                pattern,
                $"{cleanParamName}={cleanTag}",
                RegexOptions.IgnoreCase);
        }
        else
        {
            // Añadir nuevo parámetro (? o &)
            var separator = baseAndQuery.Contains('?') ? "&" : "?";
            baseAndQuery = $"{baseAndQuery}{separator}{cleanParamName}={cleanTag}";
        }

        return $"{baseAndQuery}{fragment}";
    }

    public bool IsAllowedStoreUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        var cleanHost = uri.Host.ToLowerInvariant();
        return _options.Stores.Values.Any(rule =>
            !string.IsNullOrWhiteSpace(rule.DomainMatch) &&
            cleanHost.Contains(rule.DomainMatch.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));
    }

    public string BuildSearchUrl(string storeName, string searchQuery)
    {
        var cleanQuery = Uri.EscapeDataString(searchQuery.Trim());
        var normStore = storeName?.Trim().ToLowerInvariant() ?? string.Empty;

        string rawUrl = normStore switch
        {
            "amazon" or "amazon.es" => $"https://www.amazon.es/s?k={cleanQuery}",
            "zacatrus" or "zacatrus.es" => $"https://zacatrus.es/catalogsearch/result/?q={cleanQuery}",
            "cuarto de juegos" or "cuartodejuegos" or "cuartodejuegos.es" => $"https://cuartodejuegos.es/buscar?controller=search&s={cleanQuery}",
            "dungeon marvels" or "dungeonmarvels" or "dungeonmarvels.com" => $"https://dungeonmarvels.com/buscar?controller=search&s={cleanQuery}",
            "tablerum" or "tablerum.es" => $"https://tablerum.es/buscar?controller=search&s={cleanQuery}",
            "mathom" or "mathom.es" => $"https://mathom.es/es/buscar?controller=search&s={cleanQuery}",
            "dracotienda" => $"https://www.dracotienda.com/buscar?controller=search&s={cleanQuery}",
            "jugamos otra" or "jugamosotra" => $"https://jugamosotra.com/buscar?controller=search&s={cleanQuery}",
            _ => $"https://www.google.com/search?q={Uri.EscapeDataString($"{storeName} {searchQuery}")}"
        };

        return ResolveAffiliateUrl(rawUrl, storeName);
    }
}
