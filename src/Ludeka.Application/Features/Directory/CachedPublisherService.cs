using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace Ludeka.Application.Features.Directory;

/// <summary>
/// Decorador de alto rendimiento para IPublisherService utilizando IMemoryCache (Nivel 1 de Caché).
/// Evita accesos repetidos a la base de datos para listados de editoriales y fichas individuales.
/// </summary>
public class CachedPublisherService : IPublisherService
{
    private readonly IPublisherService _inner;
    private readonly IMemoryCache _cache;
    private static int _cacheVersion = 0;

    private static readonly TimeSpan DefaultSlidingExpiration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DefaultAbsoluteExpiration = TimeSpan.FromMinutes(30);

    public CachedPublisherService(IPublisherService inner, IMemoryCache cache)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<IReadOnlyList<PublisherDto>> GetAllAsync(string? search = null, CancellationToken ct = default)
    {
        var cacheKey = $"publishers:v{_cacheVersion}:all:{search?.Trim().ToLowerInvariant() ?? ""}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SlidingExpiration = DefaultSlidingExpiration;
            entry.AbsoluteExpirationRelativeToNow = DefaultAbsoluteExpiration;
            return await _inner.GetAllAsync(search, ct);
        }) ?? [];
    }

    public async Task<PublisherDetailDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;

        var cacheKey = $"publishers:v{_cacheVersion}:slug:{slug.Trim().ToLowerInvariant()}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(15);
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await _inner.GetBySlugAsync(slug, ct);
        });
    }

    public async Task<PublisherDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var cacheKey = $"publishers:v{_cacheVersion}:id:{id}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(15);
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await _inner.GetByIdAsync(id, ct);
        });
    }

    public async Task<PublisherDto> CreateAsync(CreatePublisherDto dto, CancellationToken ct = default)
    {
        var result = await _inner.CreateAsync(dto, ct);
        Invalidate();
        return result;
    }

    public async Task<PublisherDto> UpdateAsync(Guid id, UpdatePublisherDto dto, CancellationToken ct = default)
    {
        var result = await _inner.UpdateAsync(id, dto, ct);
        Invalidate();
        return result;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _inner.DeleteAsync(id, ct);
        Invalidate();
    }

    public void Invalidate()
    {
        Interlocked.Increment(ref _cacheVersion);
    }
}
