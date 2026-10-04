using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ludeka.Infrastructure.Data;

public class SqliteGameRepository : DbContextRepositoryBase, IGameRepository
{
    public SqliteGameRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteGameRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Games
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id, ct);
    }

    public async Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        string normalized = slug.Trim().ToLowerInvariant();

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Games
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Slug == normalized, ct);
    }

    public async Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Games
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.BggId == bggId, ct);
    }

    public async Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(
        GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        await using var scope = await CreateScopeAsync(ct);
        var query = scope.Context.Games.AsNoTracking().AsQueryable();



        // Filtro por estilo lúdico (multiselección o individual)
        if (criteria.Styles != null && criteria.Styles.Count > 0)
        {
            query = query.Where(g => criteria.Styles.Contains(g.Style));
        }
        else if (criteria.Style.HasValue)
        {
            query = query.Where(g => g.Style == criteria.Style.Value);
        }

        // Filtro por confrontación (multiselección o individual)
        if (criteria.Confrontations != null && criteria.Confrontations.Count > 0)
        {
            query = query.Where(g => criteria.Confrontations.Contains(g.Confrontation));
        }
        else if (criteria.Confrontation.HasValue)
        {
            query = query.Where(g => g.Confrontation == criteria.Confrontation.Value);
        }

        // Filtro por duración máxima
        if (criteria.MaxDurations != null && criteria.MaxDurations.Count > 0)
        {
            int maxD = criteria.MaxDurations.Max();
            query = query.Where(g => g.Duration.MaxMinutes <= maxD);
        }
        else if (criteria.MaxDurationMinutes.HasValue)
        {
            query = query.Where(g => g.Duration.MaxMinutes <= criteria.MaxDurationMinutes.Value);
        }

        // Filtro "Mesa Familiar" (edad comunitaria <= 10 y dependencia de idioma nula o baja)
        if (criteria.MesaFamiliar)
        {
            query = query.Where(g =>
                g.Age.CommunityAge <= 10 &&
                g.Language != LanguageDependence.High);
        }

        // Filtro "Solo Top" (modo solitario oficial)
        if (criteria.SoloTop)
        {
            query = query.Where(g => g.IsOfficialSolo);
        }

        // Filtro por tipo de juego (BaseGame vs Expansion vs StandaloneExpansion)
        if (criteria.Types != null && criteria.Types.Count > 0)
        {
            query = query.Where(g => criteria.Types.Contains(g.Type));
        }
        else if (criteria.TypeFilter.HasValue)
        {
            query = query.Where(g => g.Type == criteria.TypeFilter.Value);
        }

        // Filtro por espacio / tamaño en mesa (INC-59 / INC-72)
        if (criteria.Footprints != null && criteria.Footprints.Count > 0)
        {
            query = query.Where(g => criteria.Footprints.Contains(g.Footprint));
        }
        else if (criteria.Footprint.HasValue)
        {
            query = query.Where(g => g.Footprint == criteria.Footprint.Value);
        }

        // Filtro por término de búsqueda (bilingüe y multipaís en SQL: títulos, diseñador, editoriales)
        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            string term = criteria.SearchTerm.Trim();
            string pattern = $"%{term}%";
            if (scope.Context.Database.IsNpgsql())
            {
                query = query.Where(g =>
                    EF.Functions.ILike(g.SpanishTitle, pattern) ||
                    EF.Functions.ILike(g.OriginalTitle, pattern) ||
                    (g.SpanishPublisher != null && EF.Functions.ILike(g.SpanishPublisher, pattern)) ||
                    (g.Publisher != null && EF.Functions.ILike(g.Publisher, pattern)) ||
                    (g.Designer != null && EF.Functions.ILike(g.Designer, pattern)));
            }
            else
            {
                query = query.Where(g =>
                    EF.Functions.Like(g.SpanishTitle, pattern) ||
                    EF.Functions.Like(g.OriginalTitle, pattern) ||
                    (g.SpanishPublisher != null && EF.Functions.Like(g.SpanishPublisher, pattern)) ||
                    (g.Publisher != null && EF.Functions.Like(g.Publisher, pattern)) ||
                    (g.Designer != null && EF.Functions.Like(g.Designer, pattern)));
            }
        }

        // Determinar si hay filtros que requieran deserialización de colecciones complejas en memoria (Scalability, Complexities)
        bool hasInMemoryFilters = criteria.EspecialParejas
            || (criteria.PlayerCounts != null && criteria.PlayerCounts.Count > 0)
            || criteria.PlayerCount.HasValue
            || (criteria.Complexities != null && criteria.Complexities.Count > 0)
            || criteria.SortBy == GameSortOrder.ComplexityAsc
            || criteria.SortBy == GameSortOrder.ComplexityDesc;

        if (!hasInMemoryFilters)
        {
            int total = await query.CountAsync(ct);
            if (criteria.SortBy == GameSortOrder.Trending)
            {
                var latestDate = await scope.Context.DailyTrendingGames.Select(t => (DateOnly?)t.DateUtc).MaxAsync(ct);
                if (latestDate.HasValue)
                {
                    query = from g in query
                            join dt in scope.Context.DailyTrendingGames.Where(t => t.DateUtc == latestDate.Value)
                            on g.Id equals dt.GameId into dtg
                            from t in dtg.DefaultIfEmpty()
                            orderby t != null ? 0 : 1,
                                    t != null ? t.Rank : int.MaxValue,
                                    g.BggRank.HasValue ? 0 : 1,
                                    g.BggRank ?? int.MaxValue,
                                    g.BggRating descending
                            select g;
                }
                else
                {
                    query = ApplyQuerySorting(query, criteria.SortBy);
                }
            }
            else
            {
                query = ApplyQuerySorting(query, criteria.SortBy);
            }

            var pagedItems = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (pagedItems, total);
        }

        // Paginación en dos fases (INC-87): Proyectar índice ligero para evitar egress masivo
        var indexList = await query
            .Select(g => new GameFilterIndexItem(
                g.Id,
                g.Scalability,
                g.Duration,
                g.Age,
                g.Style,
                g.BggRank,
                g.BggRating,
                g.YearPublished,
                g.SpanishTitle
            ))
            .ToListAsync(ct);

        if (criteria.EspecialParejas)
        {
            indexList = indexList.Where(g => g.Scalability.Any(s => s.PlayerCount == 2 && s.Status == ScalabilityStatus.MustPlay)).ToList();
        }

        // Filtro por número de comensales (multiselección o individual)
        if (criteria.PlayerCounts != null && criteria.PlayerCounts.Count > 0)
        {
            if (criteria.PlayerCountsMatchAll)
            {
                indexList = indexList.Where(g => criteria.PlayerCounts.All(p => g.Scalability.Any(s =>
                    (p >= 7 ? s.PlayerCount >= 7 : s.PlayerCount == p) &&
                    s.Status != ScalabilityStatus.NotRecommended))).ToList();
            }
            else
            {
                indexList = indexList.Where(g => criteria.PlayerCounts.Any(p => g.Scalability.Any(s =>
                    (p >= 7 ? s.PlayerCount >= 7 : s.PlayerCount == p) &&
                    s.Status != ScalabilityStatus.NotRecommended))).ToList();
            }
        }
        else if (criteria.PlayerCount.HasValue)
        {
            int p = criteria.PlayerCount.Value;
            indexList = indexList.Where(g => g.Scalability.Any(s =>
                (p >= 7 ? s.PlayerCount >= 7 : s.PlayerCount == p) &&
                s.Status != ScalabilityStatus.NotRecommended)).ToList();
        }

        // Filtro por dureza / complejidad cognitiva
        if (criteria.Complexities != null && criteria.Complexities.Count > 0)
        {
            indexList = indexList.Where(g => criteria.Complexities.Contains(CalculateComplexity(g.Style, g.Duration.MaxMinutes, g.Age.CommunityAge))).ToList();
        }

        int totalCount = indexList.Count;

        // Cargar mapa de tendencias si el orden solicitado es Trending
        Dictionary<Guid, int>? trendingRanks = null;
        if (criteria.SortBy == GameSortOrder.Trending)
        {
            var latestDate = await scope.Context.DailyTrendingGames.Select(t => (DateOnly?)t.DateUtc).MaxAsync(ct);
            if (latestDate.HasValue)
            {
                trendingRanks = await scope.Context.DailyTrendingGames
                    .Where(t => t.DateUtc == latestDate.Value && t.GameId.HasValue)
                    .ToDictionaryAsync(t => t.GameId!.Value, t => t.Rank, ct);
            }
        }

        // Ordenar según el criterio solicitado (ranking BGG por defecto, rating, dureza, duración, año, título, tendencias)
        var pagedIds = ApplyIndexSorting(indexList, criteria.SortBy, trendingRanks)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => g.Id)
            .ToList();

        if (pagedIds.Count == 0)
        {
            return ([], totalCount);
        }

        // Fase 2: Hidratación exclusiva de los elementos de la página seleccionada
        var finalItems = await scope.Context.Games
            .AsNoTracking()
            .Where(g => pagedIds.Contains(g.Id))
            .ToListAsync(ct);

        // Preservar el orden exacto de los IDs paginados
        var itemsById = finalItems.ToDictionary(g => g.Id);
        var orderedItems = pagedIds
            .Where(id => itemsById.ContainsKey(id))
            .Select(id => itemsById[id])
            .ToList();

        return (orderedItems, totalCount);
    }

    public async Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var gamesList = games.ToList();
        var usedSlugsInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var game in gamesList)
        {
            await EnsureUniqueSlugAsync(scope.Context, game, usedSlugsInBatch, ct);
            usedSlugsInBatch.Add(game.Slug);
        }

        await scope.Context.Games.AddRangeAsync(gamesList, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    private static async Task EnsureUniqueSlugAsync(
        LudekaDbContext context,
        Game game,
        HashSet<string> usedSlugsInBatch,
        CancellationToken ct)
    {
        string baseSlug = string.IsNullOrWhiteSpace(game.Slug)
            ? Game.GenerateSlug(game.SpanishTitle)
            : game.Slug;

        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = $"juego-{game.BggId}";
        }

        string currentSlug = baseSlug;

        // Si el slug actual no está en la base de datos ni en el lote en curso, lo conservamos
        bool existsInDb = await context.Games.AnyAsync(g => g.Slug == currentSlug && g.Id != game.Id, ct);
        if (!existsInDb && !usedSlugsInBatch.Contains(currentSlug))
        {
            if (game.Slug != currentSlug)
            {
                game.SetSlug(currentSlug);
            }
            return;
        }

        // Colisión detectada: intentamos desambiguar primero por año de publicación
        if (game.YearPublished > 0)
        {
            string yearSlug = $"{baseSlug}-{game.YearPublished}";
            bool yearExists = await context.Games.AnyAsync(g => g.Slug == yearSlug && g.Id != game.Id, ct);
            if (!yearExists && !usedSlugsInBatch.Contains(yearSlug))
            {
                game.SetSlug(yearSlug);
                return;
            }
        }

        // Si también colisiona con el año (o no tiene año), probamos con el BggId
        if (game.BggId > 0)
        {
            string bggSlug = $"{baseSlug}-{game.BggId}";
            bool bggExists = await context.Games.AnyAsync(g => g.Slug == bggSlug && g.Id != game.Id, ct);
            if (!bggExists && !usedSlugsInBatch.Contains(bggSlug))
            {
                game.SetSlug(bggSlug);
                return;
            }
        }

        // Si todavía colisiona, usamos un contador incremental -2, -3, etc.
        int suffix = 2;
        while (true)
        {
            string suffixSlug = $"{baseSlug}-{suffix}";
            bool suffixExists = await context.Games.AnyAsync(g => g.Slug == suffixSlug && g.Id != game.Id, ct);
            if (!suffixExists && !usedSlugsInBatch.Contains(suffixSlug))
            {
                game.SetSlug(suffixSlug);
                return;
            }
            suffix++;
        }
    }

    public async Task UpdateAsync(Game game, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.Games.FirstOrDefaultAsync(g => g.Id == game.Id, ct);
        if (existing == null && game.BggId > 0)
        {
            existing = await scope.Context.Games.FirstOrDefaultAsync(g => g.BggId == game.BggId, ct);
        }

        if (existing != null)
        {
            if (!ReferenceEquals(existing, game))
            {
                existing.UpdateLudistRating(game.LudistRating);
                if (game.AiSummary != null)
                {
                    existing.SetAiSummary(game.AiSummary);
                }

                var validPlayerCounts = game.Scalability.Where(s => s.PlayerCount > 0).Select(s => s.PlayerCount).ToList();
                int minPlayers = validPlayerCounts.Count > 0 ? Math.Max(1, validPlayerCounts.Min()) : 1;
                int maxPlayers = validPlayerCounts.Count > 0 ? Math.Max(minPlayers, validPlayerCounts.Max()) : Math.Max(minPlayers, 4);

                existing.UpdateCatalogInformation(
                    game.SpanishTitle,
                    game.OriginalTitle,
                    game.Designer,
                    game.Publisher,
                    game.YearPublished,
                    game.Description,
                    game.Confrontation,
                    game.Style,
                    game.IsOfficialSolo,
                    game.Age,
                    game.Language,
                    game.Footprint,
                    game.Duration,
                    minPlayers,
                    maxPlayers
                );

                // INC-77 & INC-101: Sincronización incondicional de colecciones complejas y metadatos enriquecidos
                existing.UpdateScalability(game.Scalability.Where(s => s.PlayerCount > 0));
                existing.UpdateSleeves(game.Sleeves);
                if (!string.IsNullOrWhiteSpace(game.SpanishPublisher))
                {
                    existing.UpdateSpanishPublisher(game.SpanishPublisher);
                }
                if (game.RegionalPublishers != null && game.RegionalPublishers.Count > 0)
                {
                    existing.UpdateRegionalPublishers(game.RegionalPublishers);
                }

                existing.UpdateImages(game.CoverImageUrl, game.ThumbnailUrl);
                existing.UpdateMediaUrls(
                    game.CoverImageUrl,
                    game.ThumbnailUrl,
                    game.BackCoverImageUrl,
                    game.TableImageUrl
                );

                if (game.BaseGameId.HasValue && game.BaseGameId.Value != Guid.Empty)
                {
                    existing.SetBaseGameId(game.BaseGameId.Value);
                }
                existing.SetGameType(game.Type);
                existing.UpdateEan(game.Ean);
                existing.UpdateAdditionalBarcodes(game.AdditionalBarcodes);
                existing.UpdatePurchaseLinks(game.PurchaseLinks);
            }

            await scope.Context.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> HasAnyAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Games.AnyAsync(ct);
    }

    public async Task<IReadOnlyList<Game>> GetGamesWithoutAiSummaryAsync(int limit = 20, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Games
            .Where(g => g.AiSummary == null)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Game>> GetByPublisherAsync(string publisherName, CancellationToken ct = default)
    {
        var clean = publisherName.Trim();
        var pattern = $"%{clean}%";
        await using var scope = await CreateScopeAsync(ct);
        var isNpgsql = scope.Context.Database.IsNpgsql();

        // 1. Filtrar en SQL directamente los juegos cuya editorial principal o española coincide
        var matchedGames = await scope.Context.Games
            .AsNoTracking()
            .Where(g =>
                isNpgsql
                    ? ((g.Publisher != null && EF.Functions.ILike(g.Publisher, pattern)) ||
                       (g.SpanishPublisher != null && EF.Functions.ILike(g.SpanishPublisher, pattern)))
                    : ((g.Publisher != null && EF.Functions.Like(g.Publisher, pattern)) ||
                       (g.SpanishPublisher != null && EF.Functions.Like(g.SpanishPublisher, pattern))))
            .ToListAsync(ct);

        var matchedIds = new HashSet<Guid>(matchedGames.Select(g => g.Id));

        // 2. Comprobar juegos restantes proyectando exclusivamente Id y RegionalPublishers
        var regionalCandidates = await scope.Context.Games
            .AsNoTracking()
            .Where(g => !matchedIds.Contains(g.Id))
            .Select(g => new { g.Id, g.RegionalPublishers })
            .ToListAsync(ct);

        var regionalMatchedIds = regionalCandidates
            .Where(c => c.RegionalPublishers != null && c.RegionalPublishers.Any(r =>
                !string.IsNullOrWhiteSpace(r.PublisherName) &&
                r.PublisherName.Contains(clean, StringComparison.OrdinalIgnoreCase)))
            .Select(c => c.Id)
            .ToList();

        if (regionalMatchedIds.Count > 0)
        {
            var additionalGames = await scope.Context.Games
                .AsNoTracking()
                .Where(g => regionalMatchedIds.Contains(g.Id))
                .ToListAsync(ct);

            return matchedGames.Concat(additionalGames)
                .OrderBy(g => g.SpanishTitle)
                .ToList();
        }

        return matchedGames
            .OrderBy(g => g.SpanishTitle)
            .ToList();
    }

    public Task<IReadOnlyList<Game>> GetGamesPendingQualityBackfillAsync(int limit = 50, CancellationToken ct = default)
        => GetGamesPendingQualityBackfillAsync(0, limit, ct);

    public async Task<IReadOnlyList<Game>> GetGamesPendingQualityBackfillAsync(int afterBggId, int limit, CancellationToken ct = default)
    {
        if (limit <= 0) limit = 50;

        await using var scope = await CreateScopeAsync(ct);
        var candidates = await scope.Context.Games
            .AsNoTracking()
            .Where(g => g.BggId > afterBggId)
            .OrderBy(g => g.BggId)
            .Select(g => new { g.Id, g.BggId, g.Scalability })
            .ToListAsync(ct);

        var matchingIds = candidates
            .Where(g => g.Scalability.Count == 0 || g.Scalability.All(s => s.BestVotes == 0 && s.RecommendedVotes == 0))
            .Take(limit)
            .Select(g => g.Id)
            .ToList();

        if (matchingIds.Count == 0) return [];

        var pagedItems = await scope.Context.Games
            .AsNoTracking()
            .Where(g => matchingIds.Contains(g.Id))
            .ToListAsync(ct);

        var itemsById = pagedItems.ToDictionary(g => g.Id);
        return matchingIds
            .Where(id => itemsById.ContainsKey(id))
            .Select(id => itemsById[id])
            .ToList();
    }

    public async Task<int> GetGamesPendingQualityBackfillCountAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var scalabilities = await scope.Context.Games
            .AsNoTracking()
            .Select(g => g.Scalability)
            .ToListAsync(ct);

        return scalabilities.Count(s => s.Count == 0 || s.All(e => e.BestVotes == 0 && e.RecommendedVotes == 0));
    }

    public async Task<IReadOnlyList<Game>> GetGamesCursorPagedAsync(int afterBggId, int limit = 50, CancellationToken ct = default)
    {
        if (limit <= 0) limit = 50;

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Games
            .AsNoTracking()
            .Where(g => g.BggId > afterBggId)
            .OrderBy(g => g.BggId)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<int> GetTotalCatalogCountAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Games.CountAsync(ct);
    }

    public async Task<IReadOnlyList<Game>> GetByDesignerAsync(string designerName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(designerName)) return Array.Empty<Game>();

        var clean = designerName.Trim();
        var pattern = $"%{clean}%";
        await using var scope = await CreateScopeAsync(ct);
        var isNpgsql = scope.Context.Database.IsNpgsql();
        return await scope.Context.Games
            .AsNoTracking()
            .Where(g => isNpgsql ? EF.Functions.ILike(g.Designer, pattern) : EF.Functions.Like(g.Designer, pattern))
            .OrderBy(g => g.SpanishTitle)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Game>> GetAllGamesAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Games
            .AsNoTracking()
            .OrderBy(g => g.SpanishTitle)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Game>> QuickSearchAsync(string term, int limit = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term)) return [];
        if (limit < 1) limit = 5;

        string pattern = $"%{term.Trim()}%";

        await using var scope = await CreateScopeAsync(ct);
        var isNpgsql = scope.Context.Database.IsNpgsql();
        return await scope.Context.Games
            .AsNoTracking()
            .Where(g =>
                isNpgsql
                    ? (EF.Functions.ILike(g.SpanishTitle, pattern) ||
                       EF.Functions.ILike(g.OriginalTitle, pattern) ||
                       (g.SpanishPublisher != null && EF.Functions.ILike(g.SpanishPublisher, pattern)) ||
                       (g.Publisher != null && EF.Functions.ILike(g.Publisher, pattern)) ||
                       (g.Designer != null && EF.Functions.ILike(g.Designer, pattern)))
                    : (EF.Functions.Like(g.SpanishTitle, pattern) ||
                       EF.Functions.Like(g.OriginalTitle, pattern) ||
                       (g.SpanishPublisher != null && EF.Functions.Like(g.SpanishPublisher, pattern)) ||
                       (g.Publisher != null && EF.Functions.Like(g.Publisher, pattern)) ||
                       (g.Designer != null && EF.Functions.Like(g.Designer, pattern))))
            .OrderBy(g => g.BggRank.HasValue ? 0 : 1)
            .ThenBy(g => g.BggRank ?? int.MaxValue)
            .ThenByDescending(g => g.BggRating)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, int>> GetOfferCountsByStoreAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var gamesWithLinks = await scope.Context.Games
            .AsNoTracking()
            .Select(g => g.PurchaseLinks)
            .ToListAsync(ct);

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var links in gamesWithLinks)
        {
            if (links == null || links.Count == 0) continue;
            var distinctStoresInGame = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var link in links)
            {
                if (!string.IsNullOrWhiteSpace(link.StoreName))
                {
                    distinctStoresInGame.Add(link.StoreName.Trim());
                }
            }

            foreach (var storeName in distinctStoresInGame)
            {
                counts[storeName] = counts.GetValueOrDefault(storeName, 0) + 1;
            }
        }

        return counts;
    }

    public async Task<IReadOnlyDictionary<string, int>> GetGameCountsByPublisherAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var publishersList = await scope.Context.Games
            .AsNoTracking()
            .Select(g => new
            {
                g.Publisher,
                g.SpanishPublisher,
                g.RegionalPublishers
            })
            .ToListAsync(ct);

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in publishersList)
        {
            var distinctInGame = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(p.Publisher))
                distinctInGame.Add(p.Publisher.Trim());

            if (!string.IsNullOrWhiteSpace(p.SpanishPublisher))
                distinctInGame.Add(p.SpanishPublisher.Trim());

            if (p.RegionalPublishers != null)
            {
                foreach (var reg in p.RegionalPublishers)
                {
                    if (!string.IsNullOrWhiteSpace(reg.PublisherName))
                        distinctInGame.Add(reg.PublisherName.Trim());
                }
            }

            foreach (var pub in distinctInGame)
            {
                counts[pub] = counts.GetValueOrDefault(pub, 0) + 1;
            }
        }

        return counts;
    }

    public async Task<IReadOnlyList<Game>> GetGamesWithStoreOffersAsync(string storeName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storeName)) return [];

        var clean = storeName.Trim();
        await using var scope = await CreateScopeAsync(ct);

        var candidates = await scope.Context.Games
            .AsNoTracking()
            .Select(g => new { g.Id, Links = g.PurchaseLinks })
            .ToListAsync(ct);

        var matchingIds = candidates
            .Where(c => c.Links != null && c.Links.Any(l =>
                !string.IsNullOrWhiteSpace(l.StoreName) && (
                    l.StoreName.Equals(clean, StringComparison.OrdinalIgnoreCase) ||
                    l.StoreName.Contains(clean, StringComparison.OrdinalIgnoreCase) ||
                    clean.Contains(l.StoreName, StringComparison.OrdinalIgnoreCase))))
            .Select(c => c.Id)
            .ToList();

        if (matchingIds.Count == 0) return [];

        return await scope.Context.Games
            .AsNoTracking()
            .Where(g => matchingIds.Contains(g.Id))
            .OrderBy(g => g.SpanishTitle)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Game>> GetGamesWithPurchaseLinksAsync(int? limit = null, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);

        var candidates = await scope.Context.Games
            .AsNoTracking()
            .Select(g => new { g.Id, HasLinks = g.PurchaseLinks.Any() })
            .ToListAsync(ct);

        var matchingIds = candidates
            .Where(c => c.HasLinks)
            .Select(c => c.Id);

        if (limit.HasValue && limit.Value > 0)
        {
            matchingIds = matchingIds.Take(limit.Value);
        }

        var idList = matchingIds.ToList();
        if (idList.Count == 0) return [];

        return await scope.Context.Games
            .AsNoTracking()
            .Where(g => idList.Contains(g.Id))
            .OrderBy(g => g.SpanishTitle)
            .ToListAsync(ct);
    }

    public static GameComplexity CalculateComplexity(Game g)
    {
        return CalculateComplexity(g.Style, g.Duration.MaxMinutes, g.Age.CommunityAge);
    }

    public static GameComplexity CalculateComplexity(GameStyle style, int maxMinutes, int communityAge)
    {
        if (style == GameStyle.PartyGame || style == GameStyle.FillerAbstract || (maxMinutes <= 30 && communityAge <= 10))
            return GameComplexity.Light;
        if (maxMinutes >= 120 || communityAge >= 14 || (maxMinutes >= 90 && style == GameStyle.Eurogame))
            return GameComplexity.Heavy;
        return GameComplexity.Medium;
    }

    public static IQueryable<Game> ApplyQuerySorting(IQueryable<Game> query, GameSortOrder sortBy)
    {
        return sortBy switch
        {
            GameSortOrder.RatingDesc => query
                .OrderByDescending(g => g.BggRating)
                .ThenBy(g => g.BggRank.HasValue ? 0 : 1)
                .ThenBy(g => g.BggRank ?? int.MaxValue),
            GameSortOrder.DurationAsc => query
                .OrderBy(g => g.Duration.MaxMinutes)
                .ThenBy(g => g.BggRank.HasValue ? 0 : 1)
                .ThenBy(g => g.BggRank ?? int.MaxValue),
            GameSortOrder.DurationDesc => query
                .OrderByDescending(g => g.Duration.MaxMinutes)
                .ThenBy(g => g.BggRank.HasValue ? 0 : 1)
                .ThenBy(g => g.BggRank ?? int.MaxValue),
            GameSortOrder.YearDesc => query
                .OrderByDescending(g => g.YearPublished)
                .ThenBy(g => g.BggRank.HasValue ? 0 : 1)
                .ThenBy(g => g.BggRank ?? int.MaxValue),
            GameSortOrder.TitleAsc => query
                .OrderBy(g => g.SpanishTitle)
                .ThenBy(g => g.OriginalTitle),
            _ => query
                .OrderBy(g => g.BggRank.HasValue ? 0 : 1)
                .ThenBy(g => g.BggRank ?? int.MaxValue)
                .ThenByDescending(g => g.BggRating)
        };
    }

    public static IEnumerable<GameFilterIndexItem> ApplyIndexSorting(
        IEnumerable<GameFilterIndexItem> items,
        GameSortOrder sortBy,
        IReadOnlyDictionary<Guid, int>? trendingRanks = null)
    {
        return sortBy switch
        {
            GameSortOrder.Trending => items
                .OrderBy(g => (trendingRanks != null && trendingRanks.ContainsKey(g.Id)) ? 0 : 1)
                .ThenBy(g => (trendingRanks != null && trendingRanks.TryGetValue(g.Id, out var r)) ? r : int.MaxValue)
                .ThenBy(g => g.BggRank.HasValue ? 0 : 1)
                .ThenBy(g => g.BggRank ?? int.MaxValue)
                .ThenByDescending(g => g.BggRating),
            GameSortOrder.RatingDesc => items
                .OrderByDescending(g => g.BggRating)
                .ThenBy(g => g.BggRank.HasValue ? 0 : 1)
                .ThenBy(g => g.BggRank ?? int.MaxValue),
            GameSortOrder.ComplexityAsc => items
                .OrderBy(g => CalculateComplexity(g.Style, g.Duration.MaxMinutes, g.Age.CommunityAge))
                .ThenBy(g => g.BggRank.HasValue ? 0 : 1)
                .ThenBy(g => g.BggRank ?? int.MaxValue)
                .ThenByDescending(g => g.BggRating),
            GameSortOrder.ComplexityDesc => items
                .OrderByDescending(g => CalculateComplexity(g.Style, g.Duration.MaxMinutes, g.Age.CommunityAge))
                .ThenBy(g => g.BggRank.HasValue ? 0 : 1)
                .ThenBy(g => g.BggRank ?? int.MaxValue)
                .ThenByDescending(g => g.BggRating),
            GameSortOrder.DurationAsc => items
                .OrderBy(g => g.Duration.MaxMinutes)
                .ThenBy(g => g.BggRank.HasValue ? 0 : 1)
                .ThenBy(g => g.BggRank ?? int.MaxValue),
            GameSortOrder.DurationDesc => items
                .OrderByDescending(g => g.Duration.MaxMinutes)
                .ThenBy(g => g.BggRank.HasValue ? 0 : 1)
                .ThenBy(g => g.BggRank ?? int.MaxValue),
            GameSortOrder.YearDesc => items
                .OrderByDescending(g => g.YearPublished)
                .ThenBy(g => g.BggRank.HasValue ? 0 : 1)
                .ThenBy(g => g.BggRank ?? int.MaxValue),
            GameSortOrder.TitleAsc => items
                .OrderBy(g => g.SpanishTitle, StringComparer.CurrentCultureIgnoreCase),
            _ => items
                .OrderBy(g => g.BggRank.HasValue ? 0 : 1)
                .ThenBy(g => g.BggRank ?? int.MaxValue)
                .ThenByDescending(g => g.BggRating)
        };
    }

    public async Task<IReadOnlyList<Game>> GetByBggIdsAsync(IEnumerable<int> bggIds, CancellationToken ct = default)
    {
        var idList = bggIds.Where(id => id > 0).Distinct().ToList();
        if (idList.Count == 0) return [];

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Games
            .Where(g => idList.Contains(g.BggId))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Game>> GetUnlinkedExpansionsAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Games
            .Where(g => g.Type == GameType.Expansion && g.BaseGameId == null && g.BggId > 0)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Game>> GetTopRankedGamesWithoutVideosAsync(
        int maxRank = 4000,
        int limit = 60,
        int afterRank = 0,
        CancellationToken ct = default)
    {
        if (limit <= 0) limit = 60;
        await using var scope = await CreateScopeAsync(ct);

        var gamesWithVideos = scope.Context.MediaItems
            .Where(m => m.GameId.HasValue && m.Platform == MediaPlatform.YouTube)
            .Select(m => m.GameId!.Value)
            .Distinct();

        var query = scope.Context.Games
            .AsNoTracking()
            .Where(g => g.BggRank.HasValue && g.BggRank.Value > afterRank && g.BggRank.Value <= maxRank)
            .Where(g => !gamesWithVideos.Contains(g.Id))
            .OrderBy(g => g.BggRank!.Value)
            .Take(limit);

        return await query.ToListAsync(ct);
    }

    public async Task<int> GetTopRankedGamesWithoutVideosCountAsync(
        int maxRank = 4000,
        CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);

        var gamesWithVideos = scope.Context.MediaItems
            .Where(m => m.GameId.HasValue && m.Platform == MediaPlatform.YouTube)
            .Select(m => m.GameId!.Value)
            .Distinct();

        return await scope.Context.Games
            .AsNoTracking()
            .Where(g => g.BggRank.HasValue && g.BggRank.Value > 0 && g.BggRank.Value <= maxRank)
            .Where(g => !gamesWithVideos.Contains(g.Id))
            .CountAsync(ct);
    }
}

