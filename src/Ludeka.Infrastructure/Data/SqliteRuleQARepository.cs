using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Data;

public class SqliteRuleQARepository : DbContextRepositoryBase, IRuleQARepository
{
    public SqliteRuleQARepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteRuleQARepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<RuleQuestion>> GetQuestionsByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var list = await scope.Context.RuleQuestions
            .AsNoTracking()
            .Include(q => q.Answers)
            .Where(q => q.GameId == gameId)
            .ToListAsync(ct);

        // Ordenar en memoria por votos descendente y luego por fecha
        return list
            .OrderByDescending(q => q.VotesCount)
            .ThenByDescending(q => q.CreatedAt)
            .ToList();
    }

    public async Task<RuleQuestion?> GetQuestionWithAnswersAsync(Guid questionId, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.RuleQuestions
            .AsNoTracking()
            .Include(q => q.Answers)
            .FirstOrDefaultAsync(q => q.Id == questionId, ct);
    }

    public async Task<RuleAnswer?> GetAnswerByIdAsync(Guid answerId, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.RuleAnswers
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == answerId, ct);
    }

    public async Task<RuleVote?> GetUserVoteAsync(string userId, Guid? questionId, Guid? answerId, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.RuleVotes
            .AsNoTracking()
            .FirstOrDefaultAsync(v =>
                v.UserId == userId &&
                v.QuestionId == questionId &&
                v.AnswerId == answerId, ct);
    }

    public async Task<HashSet<Guid>> GetUserVotedQuestionIdsAsync(string userId, IEnumerable<Guid> questionIds, CancellationToken ct = default)
    {
        var idList = questionIds.ToList();
        await using var scope = await CreateScopeAsync(ct);
        var votedIds = await scope.Context.RuleVotes
            .AsNoTracking()
            .Where(v => v.UserId == userId && v.QuestionId.HasValue && idList.Contains(v.QuestionId.Value))
            .Select(v => v.QuestionId!.Value)
            .ToListAsync(ct);

        return votedIds.ToHashSet();
    }

    public async Task<HashSet<Guid>> GetUserVotedAnswerIdsAsync(string userId, IEnumerable<Guid> answerIds, CancellationToken ct = default)
    {
        var idList = answerIds.ToList();
        await using var scope = await CreateScopeAsync(ct);
        var votedIds = await scope.Context.RuleVotes
            .AsNoTracking()
            .Where(v => v.UserId == userId && v.AnswerId.HasValue && idList.Contains(v.AnswerId.Value))
            .Select(v => v.AnswerId!.Value)
            .ToListAsync(ct);

        return votedIds.ToHashSet();
    }

    public async Task AddQuestionAsync(RuleQuestion question, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.RuleQuestions.AddAsync(question, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task AddAnswerAsync(RuleAnswer answer, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.RuleAnswers.AddAsync(answer, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task AddVoteAsync(RuleVote vote, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.RuleVotes.AddAsync(vote, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task RemoveVoteAsync(RuleVote vote, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        scope.Context.RuleVotes.Remove(vote);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task UpdateQuestionAsync(RuleQuestion question, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        scope.Context.RuleQuestions.Update(question);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task UpdateAnswerAsync(RuleAnswer answer, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        scope.Context.RuleAnswers.Update(answer);
        await scope.Context.SaveChangesAsync(ct);
    }
}
