using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory? _scopeFactory;

    public SimulatedBggClient(IServiceScopeFactory? scopeFactory = null)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var game = BggSimulationDataset.CreateGameInstance(bggId);
        if (game != null && _scopeFactory != null)
        {
            var xml = BggSimulationDataset.GetRawThingXml(bggId);
            await TryPersistSimulatedSnapshotsAsync([bggId], xml, ct);
        }
        return game;
    }

    /// <inheritdoc />
    public Game? ParseGameFromRawJson(string rawJson)
    {
        var item = BggJsonToXmlConverter.ConvertToItemElement(rawJson);
        if (item == null) return null;
        return BggXmlParser.ParseItem(item);
    }

    public Task<string?> FetchRawThingXmlAsync(int bggId, CancellationToken ct = default)
    {
        if (bggId <= 0) return Task.FromResult<string?>(null);
        return FetchRawThingsXmlAsync([bggId], ct);
    }

    public async Task<string?> FetchRawThingsXmlAsync(IEnumerable<int> bggIds, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (bggIds == null) return null;

        var validIds = bggIds.Where(id => id > 0).Distinct().Take(20).ToList();
        if (validIds.Count == 0) return null;

        var xml = BggSimulationDataset.GetRawThingsXml(validIds);
        await TryPersistSimulatedSnapshotsAsync(validIds, xml, ct);
        return xml;
    }

    private async Task TryPersistSimulatedSnapshotsAsync(IEnumerable<int> validIds, string? xml, CancellationToken ct)
    {
        if (_scopeFactory == null || string.IsNullOrWhiteSpace(xml)) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetService<IBggRawSnapshotRepository>();
            if (repo == null) return;

            var doc = XDocument.Parse(xml);
            var items = doc.Root?.Elements("item")?.ToList();
            if (items == null) return;

            foreach (var item in items)
            {
                if (int.TryParse(item.Attribute("id")?.Value, out int bggId) && bggId > 0)
                {
                    string rawJson = BggXmlToJsonConverter.ConvertToJson(item);
                    var snapshot = new BggRawSnapshot(bggId, rawJson, apiVersion: 2, fetchedAt: DateTimeOffset.UtcNow);
                    await repo.UpsertAsync(snapshot, ct);
                }
            }
        }
        catch
        {
            // Silencioso en simulación
        }
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
