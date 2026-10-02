using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Infrastructure.Bgg;

/// <summary>
/// Implementación simulada offline de IBggClient para desarrollo, pruebas automatizadas,
/// CI/CD y despliegues sin dependencia de la API externa de BoardGameGeek.
/// </summary>
public class SimulatedBggClient : IBggClient
{
    public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var game = BggSimulationDataset.CreateGameInstance(bggId);
        return Task.FromResult(game);
    }

    public Task<string?> FetchRawThingXmlAsync(int bggId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var xml = BggSimulationDataset.GetRawThingXml(bggId);
        return Task.FromResult<string?>(xml);
    }

    public Task<string?> FetchRawThingsXmlAsync(IEnumerable<int> bggIds, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (bggIds == null) return Task.FromResult<string?>(null);

        var validIds = bggIds.Where(id => id > 0).Distinct().Take(20).ToList();
        if (validIds.Count == 0) return Task.FromResult<string?>(null);

        var xml = BggSimulationDataset.GetRawThingsXml(validIds);
        return Task.FromResult<string?>(xml);
    }

    public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var results = BggSimulationDataset.Search(query);
        return Task.FromResult(results);
    }

    public Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(string username, CancellationToken ct = default)
    {
        return FetchUserCollectionAsync(username, null, ct);
    }

    public Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(
        string username,
        IProgress<BggImportProgressReport>? progress,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        progress?.Report(new BggImportProgressReport(BggImportPhase.Initializing, "Iniciando solicitud simulada a BGG..."));
        progress?.Report(new BggImportProgressReport(BggImportPhase.RequestingBgg, "Recuperando colección simulada de BGG..."));

        var collection = BggSimulationDataset.GetUserCollection(username);

        progress?.Report(new BggImportProgressReport(
            BggImportPhase.ProcessingItems,
            $"Colección simulada obtenida. Procesando {collection.Count} juegos...",
            ItemsFound: collection.Count));

        return Task.FromResult(collection);
    }

    public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var topGames = BggSimulationDataset.GetTopGames(limit);
        return Task.FromResult(topGames);
    }
}
