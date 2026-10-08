using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Features.Expansions;

public class ExpansionService : IExpansionService
{
    private readonly IExpansionRepository _expansionRepository;
    private readonly IGameRepository _gameRepository;

    public ExpansionService(IExpansionRepository expansionRepository, IGameRepository gameRepository)
    {
        _expansionRepository = expansionRepository ?? throw new ArgumentNullException(nameof(expansionRepository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
    }

    public async Task<IReadOnlyList<ExpansionSummaryDto>> GetExpansionsForBaseGameAsync(Guid baseGameId, CancellationToken ct = default)
    {
        var expansions = await _expansionRepository.GetExpansionsByBaseGameIdAsync(baseGameId, ct);
        return expansions
            .Select(ExpansionSummaryDto.FromEntity)
            .OrderByDescending(e => e.LudistRating > 0 ? e.LudistRating : e.BggRating)
            .ThenByDescending(e => e.BggRating)
            .ThenBy(e => e.SpanishTitle)
            .ToList();
    }

    public async Task<ParentGameSummaryDto?> GetParentGameForExpansionAsync(Guid expansionId, CancellationToken ct = default)
    {
        var expansion = await _expansionRepository.GetExpansionWithBaseGameAsync(expansionId, ct);
        if (expansion?.BaseGame == null) return null;

        var baseGame = expansion.BaseGame;
        return new ParentGameSummaryDto(
            baseGame.Id,
            baseGame.BggId,
            baseGame.Slug,
            baseGame.SpanishTitle,
            baseGame.OriginalTitle,
            baseGame.CoverImageUrl,
            baseGame.LudistRating,
            baseGame.BggRating,
            baseGame.YearPublished,
            baseGame.Designer
        );
    }

    public async Task<IReadOnlyList<ExpansionSynergyDto>> GetSynergiesForBaseGameAsync(Guid baseGameId, CancellationToken ct = default)
    {
        var expansions = await _expansionRepository.GetExpansionsByBaseGameIdAsync(baseGameId, ct);
        var titleMap = expansions.ToDictionary(e => e.Id, e => e.SpanishTitle);

        var synergies = await _expansionRepository.GetSynergiesByBaseGameIdAsync(baseGameId, ct);
        return synergies.Select(s =>
        {
            string titleA = titleMap.GetValueOrDefault(s.ExpansionAId, "Expansión A");
            string titleB = titleMap.GetValueOrDefault(s.ExpansionBId, "Expansión B");
            return ExpansionSynergyDto.FromEntity(s, titleA, titleB);
        }).ToList();
    }

    public async Task<IReadOnlyList<ExpansionSynergyDto>> GetSynergiesForExpansionAsync(Guid expansionId, CancellationToken ct = default)
    {
        var expansion = await _gameRepository.GetByIdAsync(expansionId, ct);
        if (expansion?.BaseGameId == null) return [];

        var baseGameId = expansion.BaseGameId.Value;
        var allSynergies = await GetSynergiesForBaseGameAsync(baseGameId, ct);

        return allSynergies
            .Where(s => s.ExpansionAId == expansionId || s.ExpansionBId == expansionId)
            .ToList();
    }

    public async Task<ExpansionMixerEvaluationDto> EvaluateMixerCombinationAsync(
        Guid baseGameId,
        IEnumerable<Guid> selectedExpansionIds,
        CancellationToken ct = default)
    {
        var baseGame = await _gameRepository.GetByIdAsync(baseGameId, ct);
        if (baseGame == null)
        {
            return new ExpansionMixerEvaluationDto(
                "Balanced",
                "🟢 Juego Base",
                "Juego no encontrado.",
                1, 4, 1, 4, 45, 45,
                [], [], [], []
            );
        }

        int baseMinPlayers = Math.Max(1, baseGame.Scalability.Where(s => s.PlayerCount > 0).Select(s => s.PlayerCount).DefaultIfEmpty(1).Min());
        int baseMaxPlayers = Math.Max(baseMinPlayers, baseGame.Scalability.Where(s => s.PlayerCount > 0).Select(s => s.PlayerCount).DefaultIfEmpty(4).Max());
        int baseEstimatedMinutes = baseGame.Duration.MaxMinutes > 0 ? baseGame.Duration.MaxMinutes : 60;

        var selectedIds = selectedExpansionIds?.Distinct().ToHashSet() ?? [];
        if (selectedIds.Count == 0)
        {
            return new ExpansionMixerEvaluationDto(
                "Balanced",
                "🟢 Mesa Equilibrada",
                "Configuración con el juego base únicamente.",
                baseMinPlayers,
                baseMaxPlayers,
                baseMinPlayers,
                baseMaxPlayers,
                baseEstimatedMinutes,
                baseEstimatedMinutes,
                [], [], [], []
            );
        }

        var allExpansions = await _expansionRepository.GetExpansionsByBaseGameIdAsync(baseGameId, ct);
        var chosenExpansions = allExpansions.Where(e => selectedIds.Contains(e.Id)).ToList();

        // Modificadores de métricas
        int extraPlayers = chosenExpansions.Select(e => e.ExtraPlayerCount ?? 0).DefaultIfEmpty(0).Max();
        int extraMinutes = chosenExpansions.Sum(e => e.ExtraDurationMinutes ?? 0);

        bool addsSolo = chosenExpansions.Any(e => e.ImpactTags.Contains(ExpansionImpactTag.AddsSoloMode));
        int resultingMinPlayers = addsSolo ? 1 : baseMinPlayers;
        int resultingMaxPlayers = Math.Max(baseMaxPlayers, baseMaxPlayers + extraPlayers);
        int resultingEstimatedMinutes = Math.Max(20, baseEstimatedMinutes + extraMinutes);

        // Evaluar sinergias par-a-par
        var allSynergies = await _expansionRepository.GetSynergiesByBaseGameIdAsync(baseGameId, ct);
        var titleMap = allExpansions.ToDictionary(e => e.Id, e => e.SpanishTitle);

        var pairwiseDtos = new List<ExpansionSynergyDto>();
        var conflictWarnings = new List<string>();
        var cautionWarnings = new List<string>();
        var synergyHighlights = new List<string>();

        bool hasConflict = false;
        bool hasCaution = false;

        var chosenList = chosenExpansions.ToList();
        for (int i = 0; i < chosenList.Count; i++)
        {
            for (int j = i + 1; j < chosenList.Count; j++)
            {
                var expA = chosenList[i];
                var expB = chosenList[j];

                var synergy = allSynergies.FirstOrDefault(s => s.MatchesPair(expA.Id, expB.Id));
                if (synergy != null)
                {
                    var dto = ExpansionSynergyDto.FromEntity(synergy, expA.SpanishTitle, expB.SpanishTitle);
                    pairwiseDtos.Add(dto);

                    if (synergy.Level == ExpansionSynergyLevel.Incompatible)
                    {
                        hasConflict = true;
                        conflictWarnings.Add($"Conflicto entre '{expA.SpanishTitle}' y '{expB.SpanishTitle}': {synergy.Reason}");
                    }
                    else if (synergy.Level == ExpansionSynergyLevel.CompatibleWithCaution)
                    {
                        hasCaution = true;
                        cautionWarnings.Add($"Atención con '{expA.SpanishTitle}' y '{expB.SpanishTitle}': {synergy.Reason}");
                    }
                    else if (synergy.Level == ExpansionSynergyLevel.PerfectCombo)
                    {
                        synergyHighlights.Add($"Combo excelente ('{expA.SpanishTitle}' + '{expB.SpanishTitle}'): {synergy.Reason}");
                    }
                }
            }
        }

        // Advertencia por exceso de duración o saturación de expansiones
        if (resultingEstimatedMinutes > 120 && !hasCaution && !hasConflict)
        {
            hasCaution = true;
            cautionWarnings.Add($"La duración estimada ({resultingEstimatedMinutes} min) excede las 2 horas de mesa. Recomendado para grupos con experiencia.");
        }

        if (chosenExpansions.Count >= 3 && !hasCaution && !hasConflict)
        {
            cautionWarnings.Add("Estás combinando 3 o más expansiones simultáneas. Asegúrate de disponer de una mesa espaciosa.");
        }

        string globalStatus;
        string globalStatusBadge;
        string globalStatusMessage;

        if (hasConflict)
        {
            globalStatus = "Conflict";
            globalStatusBadge = "🔴 Conflicto de Componentes / Reglas";
            globalStatusMessage = "Se han seleccionado expansiones que colisionan directamente en componentes o mecánicas. No se recomienda jugarlas juntas.";
        }
        else if (hasCaution)
        {
            globalStatus = "Caution";
            globalStatusBadge = "🟡 Mesa Exigente / Sobrecarga";
            globalStatusMessage = "Esta combinación es compatible pero incrementa notablemente la complejidad, el espacio o el tiempo de partida.";
        }
        else
        {
            globalStatus = "Balanced";
            globalStatusBadge = "🟢 Mesa Equilibrada y Óptima";
            globalStatusMessage = "¡Excelente combinación! Las expansiones seleccionadas se complementan de forma fluida y enriquecen la experiencia.";
        }

        return new ExpansionMixerEvaluationDto(
            globalStatus,
            globalStatusBadge,
            globalStatusMessage,
            baseMinPlayers,
            baseMaxPlayers,
            resultingMinPlayers,
            resultingMaxPlayers,
            baseEstimatedMinutes,
            resultingEstimatedMinutes,
            pairwiseDtos.AsReadOnly(),
            conflictWarnings.AsReadOnly(),
            cautionWarnings.AsReadOnly(),
            synergyHighlights.AsReadOnly()
        );
    }

    public async Task<IReadOnlyList<ExpansionRecipeDto>> GetRecipesForBaseGameAsync(Guid baseGameId, CancellationToken ct = default)
    {
        var recipes = await _expansionRepository.GetRecipesByBaseGameIdAsync(baseGameId, ct);
        var expansions = await _expansionRepository.GetExpansionsByBaseGameIdAsync(baseGameId, ct);
        var expansionMap = expansions.ToDictionary(e => e.Id, ExpansionSummaryDto.FromEntity);

        return recipes.Select(r =>
        {
            var includedDtos = r.IncludedExpansionIds
                .Where(id => expansionMap.ContainsKey(id))
                .Select(id => expansionMap[id])
                .ToList();

            return new ExpansionRecipeDto(
                r.Id,
                r.BaseGameId,
                r.Name,
                r.Description,
                r.IdealFor,
                r.IncludedExpansionIds.AsReadOnly(),
                includedDtos.AsReadOnly()
            );
        }).ToList();
    }
}
