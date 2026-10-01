using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace Ludeka.Application.Features.Home;

/// <summary>
/// Decorador de alto rendimiento para IHomeDashboardService con IMemoryCache.
/// Garantiza tiempos de respuesta SSR inferiores a 100ms para la portada de Ludeka.
/// </summary>
public class CachedHomeDashboardService : IHomeDashboardService
{
    private readonly IHomeDashboardService _inner;
    private readonly IMemoryCache _cache;
    private const string DashboardCacheKey = "home:dashboard:editorial";
    private static readonly TimeSpan DefaultSlidingExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultAbsoluteExpiration = TimeSpan.FromMinutes(15);

    public CachedHomeDashboardService(IHomeDashboardService inner, IMemoryCache cache)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<HomeDashboardDto> GetDashboardDataAsync(CancellationToken ct = default)
    {
        return await _cache.GetOrCreateAsync(DashboardCacheKey, async entry =>
        {
            entry.SlidingExpiration = DefaultSlidingExpiration;
            entry.AbsoluteExpirationRelativeToNow = DefaultAbsoluteExpiration;
            return await _inner.GetDashboardDataAsync(ct);
        }) ?? new HomeDashboardDto([], [], [], [], []);
    }

    public void Invalidate()
    {
        _cache.Remove(DashboardCacheKey);
    }
}
