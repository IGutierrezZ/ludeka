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

        // Filtro por término de búsqueda (bilingüe: título español o título original)
        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            string term = criteria.SearchTerm.Trim();
            query = query.Where(g =>
                EF.Functions.Like(g.SpanishTitle, $"%{term}%") ||
                EF.Functions.Like(g.OriginalTitle, $"%{term}%") ||
                EF.Functions.Like(g.Designer, $"%{term}%") ||
                EF.Functions.Like(g.Publisher, $"%{term}%"));
        }

        // Filtro por estilo lúdico
        if (criteria.Style.HasValue)
        {
            query = query.Where(g => g.Style == criteria.Style.Value);
        }

        // Filtro por confrontación
        if (criteria.Confrontation.HasValue)
        {
            query = query.Where(g => g.Confrontation == criteria.Confrontation.Value);
        }

        // Filtro por duración máxima
        if (criteria.MaxDurationMinutes.HasValue)
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

        // Filtro por tipo de juego (BaseGame vs Expansion)
        if (criteria.TypeFilter.HasValue)
        {
            query = query.Where(g => g.Type == criteria.TypeFilter.Value);
        }

        // Para filtros que evalúan elementos de colecciones JSON complejas en SQLite
        var list = await query.ToListAsync(ct);

        if (criteria.EspecialParejas)
        {
            list = list.Where(g => g.Scalability.Any(s => s.PlayerCount == 2 && s.Status == ScalabilityStatus.MustPlay)).ToList();
        }

        if (criteria.PlayerCount.HasValue)
        {
            int p = criteria.PlayerCount.Value;
            list = list.Where(g => g.Scalability.Any(s =>
                (p >= 7 ? s.PlayerCount >= 7 : s.PlayerCount == p) &&
                s.Status != ScalabilityStatus.NotRecommended)).ToList();
        }

        int totalCount = list.Count;

        // Ordenar por ranking BGG (con los rankeados primero) y luego rating
        var paged = list
            .OrderBy(g => g.BggRank.HasValue ? 0 : 1)
            .ThenBy(g => g.BggRank ?? int.MaxValue)
            .ThenByDescending(g => g.BggRating)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (paged, totalCount);
    }

    public async Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.Games.AddRangeAsync(games, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Game game, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.Games.FirstOrDefaultAsync(g => g.Id == game.Id, ct);
        if (existing != null)
        {
            if (!ReferenceEquals(existing, game))
            {
                existing.UpdateLudistRating(game.LudistRating);
                if (game.AiSummary != null)
                {
                    existing.SetAiSummary(game.AiSummary);
                }

                int minPlayers = game.Scalability.Count > 0 ? game.Scalability.Min(s => s.PlayerCount) : 1;
                int maxPlayers = game.Scalability.Count > 0 ? game.Scalability.Max(s => s.PlayerCount) : 4;

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

                existing.UpdateImages(game.CoverImageUrl, game.ThumbnailUrl);
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
        if (string.IsNullOrWhiteSpace(publisherName)) return Array.Empty<Game>();

        var clean = publisherName.Trim();
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Games
            .AsNoTracking()
            .Where(g => EF.Functions.Like(g.Publisher, $"%{clean}%"))
            .OrderBy(g => g.SpanishTitle)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Game>> GetByDesignerAsync(string designerName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(designerName)) return Array.Empty<Game>();

        var clean = designerName.Trim();
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Games
            .AsNoTracking()
            .Where(g => EF.Functions.Like(g.Designer, $"%{clean}%"))
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
}
