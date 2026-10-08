using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Contracts;

public interface IBggClient
{
    Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default);

    Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(string username, CancellationToken ct = default)
        => FetchUserCollectionAsync(username, null, ct);

    Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(
        string username,
        IProgress<BggImportProgressReport>? progress,
        CancellationToken ct = default)
        => FetchUserCollectionAsync(username, ct);

    Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default);

    Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default);

    Task<string?> FetchRawThingXmlAsync(int bggId, CancellationToken ct = default)
        => FetchRawThingXmlAsync(bggId, false, ct);

    Task<string?> FetchRawThingXmlAsync(int bggId, bool includeVersions, CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    Task<string?> FetchRawThingJsonAsync(int bggId, bool includeVersions = true, CancellationToken ct = default)
        => Task.FromResult<string?>(null);


    Task<string?> FetchRawThingsXmlAsync(IEnumerable<int> bggIds, CancellationToken ct = default)
        => FetchRawThingsXmlAsync(bggIds, false, ct);

    Task<string?> FetchRawThingsXmlAsync(IEnumerable<int> bggIds, bool includeVersions, CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    /// <summary>
    /// Obtiene una lista de entidades Game a partir de una colección de identificadores BGG en una o varias llamadas agrupadas.
    /// </summary>
    Task<IReadOnlyList<Game>> FetchGamesByBggIdsAsync(IEnumerable<int> bggIds, CancellationToken ct = default)
        => FetchGamesByBggIdsAsync(bggIds, true, ct);

    /// <summary>
    /// Obtiene una lista de entidades Game a partir de una colección de identificadores BGG con control de subárbol de versiones.
    /// </summary>
    async Task<IReadOnlyList<Game>> FetchGamesByBggIdsAsync(IEnumerable<int> bggIds, bool includeVersions, CancellationToken ct = default)
    {
        var list = new List<Game>();
        if (bggIds == null) return list;
        foreach (var id in bggIds)
        {
            var game = await FetchGameByBggIdAsync(id, ct);
            if (game != null) list.Add(game);
        }
        return list;
    }

    /// <summary>
    /// Parsea una entidad Game completa desde el payload JSON de un snapshot satélite de BGG en memoria (sin llamadas HTTP).
    /// </summary>
    Game? ParseGameFromRawJson(string rawJson)
        => null;
}
