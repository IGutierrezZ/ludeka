using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Application.Features.Founding;

public class FoundingVerdictService : IFoundingVerdictService
{
    private readonly IFoundingVerdictRepository _verdictRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IUserReviewRepository _reviewRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICommunityNotificationQueue? _notificationQueue;
    private readonly IAiGameSummaryService? _aiSummaryService;

    public const int FoundingVoteWeight = 3;

    public FoundingVerdictService(
        IFoundingVerdictRepository verdictRepository,
        IGameRepository gameRepository,
        IUserReviewRepository reviewRepository,
        ICurrentUserService currentUserService,
        ICommunityNotificationQueue? notificationQueue = null,
        IAiGameSummaryService? aiSummaryService = null)
    {
        _verdictRepository = verdictRepository;
        _gameRepository = gameRepository;
        _reviewRepository = reviewRepository;
        _currentUserService = currentUserService;
        _notificationQueue = notificationQueue;
        _aiSummaryService = aiSummaryService;
    }

    public async Task<FoundingVerdictDto?> GetVerdictByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        var verdict = await _verdictRepository.GetByGameIdAsync(gameId, ct);
        return verdict != null ? FoundingVerdictDto.FromEntity(verdict) : null;
    }

    public async Task<AiGameSummaryDto?> GetAiSummaryForGameAsync(Guid gameId, CancellationToken ct = default)
    {
        var game = await _gameRepository.GetByIdAsync(gameId, ct);
        if (game == null) return null;

        if (game.AiSummary != null)
        {
            return new AiGameSummaryDto(
                game.Id,
                game.SpanishTitle,
                game.AiSummary.ScalabilitySummary,
                game.AiSummary.AgeSummary,
                game.AiSummary.FootprintSummary,
                game.AiSummary.GeneralVerdict,
                game.AiSummary.Model,
                game.AiSummary.GeneratedAt
            );
        }

        return null;
    }

    public async Task<AiGameSummaryDto> RequestAiSummaryGenerationAsync(Guid gameId, CancellationToken ct = default)
    {
        // Invariante de anonimia: la generación bajo demanda exige sesión real.
        SessionIdentity.Require(_currentUserService);

        if (!_currentUserService.IsInRole("FoundingTeam") && !_currentUserService.IsInRole("Moderator"))
        {
            throw new UnauthorizedAccessException("Solo miembros de la Mesa Fundadora o moderadores pueden solicitar la generación bajo demanda de síntesis con IA.");
        }

        var game = await _gameRepository.GetByIdAsync(gameId, ct)
            ?? throw new InvalidOperationException($"No se encontró el juego con ID {gameId}");

        AiGameSummaryDto generated;
        if (_aiSummaryService != null)
        {
            generated = await _aiSummaryService.GenerateSummaryAsync(game, ct);
        }
        else
        {
            generated = GenerateHeuristicSummary(game);
        }

        var aiSummaryVo = new AiGameSummary(
            generated.GeneralVerdict,
            generated.ScalabilitySummary,
            generated.AgeSummary,
            generated.FootprintSummary,
            generated.Model,
            generated.GeneratedAt ?? DateTime.UtcNow
        );

        game.SetAiSummary(aiSummaryVo);
        await _gameRepository.UpdateAsync(game, ct);

        return generated;
    }

    private static AiGameSummaryDto GenerateHeuristicSummary(Game game)
    {
        string scalabilitySummary = game.Scalability.Count > 0
            ? $"{game.IdealPlayerCountText}. El consenso internacional destaca que la dinámica fluye con menor entreturno y mayor tensión estratégica en este rango."
            : "Escalabilidad según especificación oficial del fabricante.";

        string ageSummary = game.Age.IsAccessibleEarlier
            ? $"Edad de caja {game.Age.BoxAge}+ (normativa de componentes), pero la comunidad lo juega fluidamente desde los {game.Age.CommunityAge}+ años en entorno guiado."
            : $"Edad recomendada comunitaria: {game.Age.CommunityAge}+ años acorde al peso y complejidad de reglas.";

        string footprintName = game.Footprint switch
        {
            TableFootprint.SmallTable => "Mesa pequeña / cafetería (despliegue compacto sin tableros masivos)",
            TableFootprint.StandardTable => "Mesa de comedor estándar (espacio para tablero central y tableros personales)",
            TableFootprint.TableMonster => "Monstruo de mesa (requiere mesa amplia para reservas, mercados y áreas individuales)",
            _ => "Mesa de comedor estándar"
        };

        string footprintSummary = $"{footprintName}. Duración estimada de ~{game.Duration.EstimatedPerPlayerMinutes} min por comensal (partidas de {game.Duration.MinMinutes} a {game.Duration.MaxMinutes} minutos).";

        string confrontationDesc = game.Confrontation switch
        {
            ConfrontationType.Cooperative => "experiencia colaborativa donde todos ganan o pierden juntos",
            ConfrontationType.Competitive => "duelo competitivo con interacción directa o de tablero",
            ConfrontationType.HiddenRolesOrTeams => "dinámica de identidades secretas, faroleo y deducción social",
            ConfrontationType.SemiCooperative => "tensión semi-cooperativa con agenda individual oculta",
            _ => "dinámica estratégica"
        };

        string generalVerdict = $"{game.SpanishTitle} es un referente del estilo {game.Style} enfocado en una {confrontationDesc}. Cuenta con un rating internacional de {game.BggRating:0.0}/10 en BGG. (Síntesis objetiva generada automáticamente por IA para evitar fichas vacías hasta la publicación del veredicto oficial de la mesa fundadora).";

        return new AiGameSummaryDto(
            game.Id,
            game.SpanishTitle,
            scalabilitySummary,
            ageSummary,
            footprintSummary,
            generalVerdict,
            "Heurística Editorial",
            DateTime.UtcNow
        );
    }

    public async Task<FoundingVerdictDto> SaveVerdictAsync(SaveFoundingVerdictRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Invariante de anonimia: el veredicto se firma siempre con la identidad real de la sesión.
        string authorUserId = SessionIdentity.Require(_currentUserService);

        if (!_currentUserService.IsInRole("FoundingTeam") && !_currentUserService.IsInRole("Moderator"))
        {
            throw new UnauthorizedAccessException("Acceso denegado: solo miembros del equipo fundador o moderadores pueden emitir o editar veredictos.");
        }

        var game = await _gameRepository.GetByIdAsync(request.GameId, ct)
            ?? throw new InvalidOperationException($"No se encontró el juego con ID {request.GameId}");

        var photos = request.Photos?.Select(p => new FoundingPhoto(p.PhotoUrl, p.Caption)).ToList() ?? [];

        var existing = await _verdictRepository.GetByGameIdAsync(request.GameId, ct);
        if (existing == null)
        {
            existing = new FoundingVerdict(
                request.GameId,
                authorUserId,
                _currentUserService.UserName,
                request.Recommendation,
                request.OverallVerdict,
                request.TwoPlayerVerdict,
                request.FamilyVerdict,
                photos
            );
            await _verdictRepository.AddAsync(existing, ct);
        }
        else
        {
            existing.Update(
                request.Recommendation,
                request.OverallVerdict,
                request.TwoPlayerVerdict,
                request.FamilyVerdict,
                photos
            );
            await _verdictRepository.UpdateAsync(existing, ct);
        }

        // Recalcular rating ponderado Ludist incorporando el veredicto fundador
        await RecalculateLudistRatingWithFoundingWeightAsync(game, existing, ct);

        if (_notificationQueue != null)
        {
            try
            {
                var notificationMessage = new CommunityNotificationMessage(
                    NotificationEventType.FoundingVerdictPublished,
                    $"🛡️ NUEVO VEREDICTO FUNDADOR: {game.SpanishTitle}",
                    $"La Mesa Fundadora ha emitido su análisis oficial para {game.SpanishTitle}: \"{existing.OverallVerdict}\"",
                    TargetUrl: $"https://ludeka.es/juegos/{game.Slug}",
                    ImageUrl: game.CoverImageUrl,
                    Fields: new Dictionary<string, string>
                    {
                        { "Sello", existing.Recommendation switch {
                            FoundingRecommendation.MustPlay => "🟢 Imprescindible",
                            FoundingRecommendation.RecommendedWithAdaptations => "🟡 Recomendado con adaptaciones",
                            _ => "🔴 Prescindible"
                        }},
                        { "Autor", existing.AuthorName }
                    });

                await _notificationQueue.EnqueueAsync(notificationMessage, ct);
            }
            catch
            {
                // La notificación no debe impedir guardar el veredicto
            }
        }

        return FoundingVerdictDto.FromEntity(existing);
    }

    public async Task DeleteVerdictAsync(Guid gameId, CancellationToken ct = default)
    {
        SessionIdentity.Require(_currentUserService);

        if (!_currentUserService.IsInRole("FoundingTeam") && !_currentUserService.IsInRole("Moderator"))
        {
            throw new UnauthorizedAccessException("Acceso denegado: solo miembros del equipo fundador o moderadores pueden eliminar veredictos.");
        }

        var verdict = await _verdictRepository.GetByGameIdAsync(gameId, ct);
        if (verdict != null)
        {
            await _verdictRepository.DeleteAsync(verdict.Id, ct);

            var game = await _gameRepository.GetByIdAsync(gameId, ct);
            if (game != null)
            {
                await RecalculateLudistRatingWithFoundingWeightAsync(game, null, ct);
            }
        }
    }

    private async Task RecalculateLudistRatingWithFoundingWeightAsync(Game game, FoundingVerdict? verdict, CancellationToken ct)
    {
        var reviews = await _reviewRepository.GetByGameIdAsync(game.Id, ct);

        double totalScore = reviews.Sum(r => r.Score);
        int totalVotes = reviews.Count;

        if (verdict != null)
        {
            // El veredicto fundador aporta FoundingVoteWeight votos con su nota equivalente
            double foundingScore = verdict.GetEquivalentRating();
            totalScore += (foundingScore * FoundingVoteWeight);
            totalVotes += FoundingVoteWeight;
        }

        if (totalVotes > 0)
        {
            double weightedAverage = totalScore / totalVotes;
            game.UpdateLudistRating(weightedAverage);
        }
        else
        {
            // Si no hay votos ni veredicto, conservar nota base BGG como fallback
            game.UpdateLudistRating(game.BggRating);
        }

        await _gameRepository.UpdateAsync(game, ct);
    }
}
