using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Contracts;

public interface IGameRepository
{
    Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default);
    Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default);
    Task UpdateAsync(Game game, CancellationToken ct = default);
    Task<bool> HasAnyAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Game>> GetGamesWithoutAiSummaryAsync(int limit = 20, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Game>>([]);

    Task<IReadOnlyList<Game>> GetByPublisherAsync(string publisherName, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Game>>([]);

    Task<IReadOnlyList<Game>> GetByDesignerAsync(string designerName, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Game>>([]);

    Task<IReadOnlyList<Game>> GetAllGamesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Game>>([]);

    Task<IReadOnlyList<Game>> GetGamesPendingQualityBackfillAsync(int limit = 50, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Game>>([]);

    Task<IReadOnlyList<Game>> GetGamesPendingQualityBackfillAsync(int afterBggId, int limit, CancellationToken ct = default)
        => GetGamesPendingQualityBackfillAsync(limit, ct);

    Task<int> GetGamesPendingQualityBackfillCountAsync(CancellationToken ct = default)
        => Task.FromResult(0);

    Task<IReadOnlyList<Game>> GetGamesCursorPagedAsync(int afterBggId, int limit = 50, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Game>>([]);

    Task<int> GetTotalCatalogCountAsync(CancellationToken ct = default)
        => Task.FromResult(0);

    Task<IReadOnlyList<Game>> QuickSearchAsync(string term, int limit = 5, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Game>>([]);

    Task<IReadOnlyDictionary<string, int>> GetOfferCountsByStoreAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());

    Task<IReadOnlyDictionary<string, int>> GetGameCountsByPublisherAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());

    Task<IReadOnlyList<Game>> GetGamesWithStoreOffersAsync(string storeName, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Game>>([]);

    Task<IReadOnlyList<Game>> GetGamesWithPurchaseLinksAsync(int? limit = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Game>>([]);

    Task<IReadOnlyList<Game>> GetByBggIdsAsync(IEnumerable<int> bggIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Game>>([]);

    Task<IReadOnlyList<Game>> GetUnlinkedExpansionsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Game>>([]);
}

