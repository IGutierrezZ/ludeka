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

namespace Ludeka.Application.Features.Community;

public class RuleQAService : IRuleQAService
{
    private readonly IRuleQARepository _repository;
    private readonly ICommunityNotificationQueue? _notificationQueue;

    public RuleQAService(IRuleQARepository repository, ICommunityNotificationQueue? notificationQueue = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _notificationQueue = notificationQueue;
    }

    public async Task<IReadOnlyList<RuleQuestionDto>> GetQuestionsByGameIdAsync(Guid gameId, string? currentUserId = null, CancellationToken ct = default)
    {
        var questions = await _repository.GetQuestionsByGameIdAsync(gameId, ct);
        if (questions.Count == 0)
            return Array.Empty<RuleQuestionDto>();

        var questionIds = questions.Select(q => q.Id).ToList();
        var answerIds = questions.SelectMany(q => q.Answers).Select(a => a.Id).ToList();

        HashSet<Guid> votedQuestionIds = new();
        HashSet<Guid> votedAnswerIds = new();

        if (!string.IsNullOrWhiteSpace(currentUserId))
        {
            votedQuestionIds = await _repository.GetUserVotedQuestionIdsAsync(currentUserId, questionIds, ct);
            votedAnswerIds = await _repository.GetUserVotedAnswerIdsAsync(currentUserId, answerIds, ct);
        }

        return questions.Select(q => MapQuestionToDto(q, votedQuestionIds, votedAnswerIds)).ToList();
    }

    public async Task<RuleQuestionDto?> GetQuestionByIdAsync(Guid questionId, string? currentUserId = null, CancellationToken ct = default)
    {
        var question = await _repository.GetQuestionWithAnswersAsync(questionId, ct);
        if (question == null)
            return null;

        HashSet<Guid> votedQuestionIds = new();
        HashSet<Guid> votedAnswerIds = new();

        if (!string.IsNullOrWhiteSpace(currentUserId))
        {
            votedQuestionIds = await _repository.GetUserVotedQuestionIdsAsync(currentUserId, new[] { question.Id }, ct);
            votedAnswerIds = await _repository.GetUserVotedAnswerIdsAsync(currentUserId, question.Answers.Select(a => a.Id), ct);
        }

        return MapQuestionToDto(question, votedQuestionIds, votedAnswerIds);
    }

    public async Task<RuleQuestionDto> CreateQuestionAsync(CreateRuleQuestionRequest request, string userId, string userName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Invariante de anonimia: publicar una duda de reglas exige sesión.
        string authorUserId = SessionIdentity.Require(userId);

        var question = new RuleQuestion(
            gameId: request.GameId,
            userId: authorUserId,
            userName: userName,
            title: request.Title,
            body: request.Body);

        await _repository.AddQuestionAsync(question, ct);
        return MapQuestionToDto(question, new HashSet<Guid>(), new HashSet<Guid>());
    }

    public async Task<RuleAnswerDto> AddAnswerAsync(CreateRuleAnswerRequest request, string userId, string userName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Invariante de anonimia: responder a una duda exige sesión.
        string authorUserId = SessionIdentity.Require(userId);

        var question = await _repository.GetQuestionWithAnswersAsync(request.QuestionId, ct)
            ?? throw new InvalidOperationException($"No se encontró la pregunta con ID {request.QuestionId}.");

        var answer = new RuleAnswer(
            questionId: request.QuestionId,
            userId: authorUserId,
            userName: userName,
            body: request.Body,
            officialRuleReference: request.OfficialRuleReference);

        question.AddAnswer(answer);
        await _repository.AddAnswerAsync(answer, ct);

        return new RuleAnswerDto(
            answer.Id,
            answer.QuestionId,
            answer.UserId,
            answer.UserName,
            answer.Body,
            answer.OfficialRuleReference,
            answer.VotesCount,
            answer.IsAccepted,
            HasUserVoted: false,
            answer.CreatedAt);
    }

    public async Task<bool> ToggleVoteQuestionAsync(Guid questionId, string userId, CancellationToken ct = default)
    {
        // Invariante de anonimia: votar exige sesión.
        string voterUserId = SessionIdentity.Require(userId);

        var question = await _repository.GetQuestionWithAnswersAsync(questionId, ct)
            ?? throw new InvalidOperationException($"No se encontró la pregunta con ID {questionId}.");

        var existingVote = await _repository.GetUserVoteAsync(voterUserId, questionId: questionId, answerId: null, ct);

        if (existingVote != null)
        {
            await _repository.RemoveVoteAsync(existingVote, ct);
            question.Downvote();
            await _repository.UpdateQuestionAsync(question, ct);
            return false;
        }
        else
        {
            var vote = new RuleVote(voterUserId, questionId: questionId, answerId: null);
            await _repository.AddVoteAsync(vote, ct);
            question.Upvote();
            await _repository.UpdateQuestionAsync(question, ct);
            return true;
        }
    }

    public async Task<bool> ToggleVoteAnswerAsync(Guid answerId, string userId, CancellationToken ct = default)
    {
        string voterUserId = SessionIdentity.Require(userId);

        var answer = await _repository.GetAnswerByIdAsync(answerId, ct)
            ?? throw new InvalidOperationException($"No se encontró la respuesta con ID {answerId}.");

        var existingVote = await _repository.GetUserVoteAsync(voterUserId, questionId: null, answerId: answerId, ct);

        if (existingVote != null)
        {
            await _repository.RemoveVoteAsync(existingVote, ct);
            answer.Downvote();
            await _repository.UpdateAnswerAsync(answer, ct);
            return false;
        }
        else
        {
            var vote = new RuleVote(voterUserId, questionId: null, answerId: answerId);
            await _repository.AddVoteAsync(vote, ct);
            answer.Upvote();
            await _repository.UpdateAnswerAsync(answer, ct);
            return true;
        }
    }

    public async Task<RuleQuestionDto> MarkAcceptedAnswerAsync(Guid questionId, Guid answerId, string requestingUserId, bool isModerator, CancellationToken ct = default)
    {
        string requesterUserId = SessionIdentity.Require(requestingUserId);

        var question = await _repository.GetQuestionWithAnswersAsync(questionId, ct)
            ?? throw new InvalidOperationException($"No se encontró la pregunta con ID {questionId}.");

        question.MarkAcceptedAnswer(answerId, requesterUserId, isModerator);

        await _repository.UpdateQuestionAsync(question, ct);
        foreach (var ans in question.Answers)
        {
            await _repository.UpdateAnswerAsync(ans, ct);
        }

        if (_notificationQueue != null)
        {
            try
            {
                var acceptedAnswer = question.Answers.FirstOrDefault(a => a.Id == answerId);
                var message = new CommunityNotificationMessage(
                    NotificationEventType.RuleQuestionAnswered,
                    $"💡 DUDA DE REGLAS RESUELTA: {question.Title}",
                    $"Se ha aceptado una respuesta definitiva en Ludeka:\n\n\"{acceptedAnswer?.Body ?? "Respuesta de reglas"}\"",
                    TargetUrl: "https://ludeka.es",
                    Fields: new Dictionary<string, string>
                    {
                        { "Pregunta", question.Title },
                        { "Resuelto por", acceptedAnswer?.UserName ?? "Comunidad" }
                    });

                await _notificationQueue.EnqueueAsync(message, ct);
            }
            catch
            {
                // No interrumpir la transacción principal
            }
        }

        return MapQuestionToDto(question, new HashSet<Guid>(), new HashSet<Guid>());
    }

    public async Task<RuleQuestionDto> UnmarkAcceptedAnswerAsync(Guid questionId, string requestingUserId, bool isModerator, CancellationToken ct = default)
    {
        string requesterUserId = SessionIdentity.Require(requestingUserId);

        var question = await _repository.GetQuestionWithAnswersAsync(questionId, ct)
            ?? throw new InvalidOperationException($"No se encontró la pregunta con ID {questionId}.");

        question.UnmarkAcceptedAnswer(requesterUserId, isModerator);

        await _repository.UpdateQuestionAsync(question, ct);
        foreach (var ans in question.Answers)
        {
            await _repository.UpdateAnswerAsync(ans, ct);
        }

        return MapQuestionToDto(question, new HashSet<Guid>(), new HashSet<Guid>());
    }

    private static RuleQuestionDto MapQuestionToDto(
        RuleQuestion q,
        HashSet<Guid> votedQuestionIds,
        HashSet<Guid> votedAnswerIds)
    {
        var sortedAnswers = q.Answers
            .OrderByDescending(a => a.IsAccepted) // Solución aceptada primero
            .ThenByDescending(a => a.VotesCount)
            .ThenBy(a => a.CreatedAt)
            .Select(a => new RuleAnswerDto(
                a.Id,
                a.QuestionId,
                a.UserId,
                a.UserName,
                a.Body,
                a.OfficialRuleReference,
                a.VotesCount,
                a.IsAccepted,
                votedAnswerIds.Contains(a.Id),
                a.CreatedAt))
            .ToList();

        return new RuleQuestionDto(
            q.Id,
            q.GameId,
            q.UserId,
            q.UserName,
            q.Title,
            q.Body,
            q.VotesCount,
            q.AcceptedAnswerId,
            votedQuestionIds.Contains(q.Id),
            q.Answers.Count,
            q.CreatedAt,
            sortedAnswers);
    }
}
