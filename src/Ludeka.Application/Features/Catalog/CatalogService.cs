using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Features.Catalog;

public class CatalogService : ICatalogService
{
    private readonly IGameRepository _repository;

    public CatalogService(IGameRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<CatalogResult> GetCatalogAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (items, total) = await _repository.SearchAsync(criteria, page, pageSize, ct);
        var dtos = items.Select(GameSummaryDto.FromEntity).ToList();
        return new CatalogResult(dtos, total, page, pageSize);
    }

    public async Task<GameDetailDto?> GetGameBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;

        var game = await _repository.GetBySlugAsync(slug.Trim(), ct);
        return game is null ? null : GameDetailDto.FromEntity(game);
    }

    public async Task<IReadOnlyList<GameSummaryDto>> GetQuickSearchAsync(string term, int limit = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term)) return Array.Empty<GameSummaryDto>();

        var quickItems = await _repository.QuickSearchAsync(term.Trim(), limit, ct);
        if (quickItems.Count > 0)
        {
            return quickItems.Select(GameSummaryDto.FromEntity).ToList();
        }

        var criteria = new GameFilterCriteria(SearchTerm: term.Trim());
        var (items, _) = await _repository.SearchAsync(criteria, page: 1, pageSize: limit, ct);
        return items.Select(GameSummaryDto.FromEntity).ToList();
    }
}
