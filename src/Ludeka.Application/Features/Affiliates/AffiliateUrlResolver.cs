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
}
