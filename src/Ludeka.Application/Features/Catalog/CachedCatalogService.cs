using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace Ludeka.Application.Features.Catalog;

/// <summary>
/// Decorador de alto rendimiento para ICatalogService utilizando IMemoryCache (Nivel 1 de Caché).
/// Reduce sustancialmente los accesos a SQLite para rutas de catálogo, autocompletado y fichas de detalle.
/// </summary>
public class CachedCatalogService : ICatalogService
{
    private readonly ICatalogService _inner;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan DefaultSlidingExpiration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DefaultAbsoluteExpiration = TimeSpan.FromMinutes(30);

    public CachedCatalogService(ICatalogService inner, IMemoryCache cache)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<CatalogResult> GetCatalogAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var criteriaHash = ComputeCriteriaHash(criteria);
        var cacheKey = $"catalog:p{page}:s{pageSize}:crit_{criteriaHash}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SlidingExpiration = DefaultSlidingExpiration;
            entry.AbsoluteExpirationRelativeToNow = DefaultAbsoluteExpiration;
            return await _inner.GetCatalogAsync(criteria, page, pageSize, ct);
        }) ?? new CatalogResult([], 0, page, pageSize);
    }

    public async Task<GameDetailDto?> GetGameBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;

        var cacheKey = $"game:slug:{slug.Trim().ToLowerInvariant()}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(15);
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await _inner.GetGameBySlugAsync(slug, ct);
        });
    }

    public async Task<IReadOnlyList<GameSummaryDto>> GetQuickSearchAsync(string term, int limit = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term)) return [];

        var cacheKey = $"quicksearch:{term.Trim().ToLowerInvariant()}:l{limit}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(5);
            return await _inner.GetQuickSearchAsync(term, limit, ct);
        }) ?? [];
    }

    public void Invalidate(string? slug = null)
    {
        if (!string.IsNullOrWhiteSpace(slug))
        {
            _cache.Remove($"game:slug:{slug.Trim().ToLowerInvariant()}");
        }
    }

    private static string ComputeCriteriaHash(GameFilterCriteria c)
    {
        var sb = new StringBuilder();
        sb.Append(c.SearchTerm?.Trim().ToLowerInvariant()).Append('|');
        sb.Append(c.PlayerCount).Append('|');
        if (c.PlayerCounts != null && c.PlayerCounts.Count > 0)
            sb.Append(string.Join(',', c.PlayerCounts.OrderBy(x => x)));
        sb.Append('|').Append(c.PlayerCountsMatchAll).Append('|');
        sb.Append(c.Style).Append('|');
        if (c.Styles != null && c.Styles.Count > 0)
            sb.Append(string.Join(',', c.Styles.OrderBy(x => (int)x)));
        sb.Append('|');
        sb.Append(c.Confrontation).Append('|');
        if (c.Confrontations != null && c.Confrontations.Count > 0)
            sb.Append(string.Join(',', c.Confrontations.OrderBy(x => (int)x)));
        sb.Append('|');
        sb.Append(c.MaxDurationMinutes).Append('|');
        if (c.MaxDurations != null && c.MaxDurations.Count > 0)
            sb.Append(string.Join(',', c.MaxDurations.OrderBy(x => x)));
        sb.Append('|');
        sb.Append(c.EspecialParejas).Append('|');
        sb.Append(c.MesaFamiliar).Append('|');
        sb.Append(c.SoloTop).Append('|');
        sb.Append(c.TypeFilter).Append('|');
        if (c.Types != null && c.Types.Count > 0)
            sb.Append(string.Join(',', c.Types.OrderBy(x => (int)x)));
        sb.Append('|');
        sb.Append(c.Footprint).Append('|');
        if (c.Footprints != null && c.Footprints.Count > 0)
            sb.Append(string.Join(',', c.Footprints.OrderBy(x => (int)x)));
        sb.Append('|');
        if (c.Complexities != null && c.Complexities.Count > 0)
            sb.Append(string.Join(',', c.Complexities.OrderBy(x => (int)x)));
        sb.Append('|').Append(c.SortBy);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..16];
    }
}
