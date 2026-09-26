using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ludeka.Infrastructure.Data;

public class SqliteGameLoanRepository : DbContextRepositoryBase, IGameLoanRepository
{
    public SqliteGameLoanRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteGameLoanRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<GameLoan?> GetByIdAsync(Guid loanId, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        return await scope.Context.Loans
            .Include(l => l.Game)
            .FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);
    }

    public async Task<GameLoan?> GetActiveLoanByUserAndGameAsync(string userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        return await scope.Context.Loans
            .Include(l => l.Game)
            .FirstOrDefaultAsync(l => l.UserId == userId && l.GameId == gameId && !l.IsReturned, cancellationToken);
    }

    public async Task<List<GameLoan>> GetActiveLoansByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        var list = await scope.Context.Loans
            .Include(l => l.Game)
            .Where(l => l.UserId == userId && !l.IsReturned)
            .ToListAsync(cancellationToken);

        return list.OrderByDescending(l => l.LoanDate).ToList();
    }

    public async Task<List<GameLoan>> GetLoanHistoryByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        var list = await scope.Context.Loans
            .Include(l => l.Game)
            .Where(l => l.UserId == userId)
            .ToListAsync(cancellationToken);

        return list.OrderByDescending(l => l.LoanDate).ToList();
    }

    public async Task<int> GetActiveLoansCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        return await scope.Context.Loans
            .CountAsync(l => l.UserId == userId && !l.IsReturned, cancellationToken);
    }

    public async Task AddAsync(GameLoan loan, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        await scope.Context.Loans.AddAsync(loan, cancellationToken);
        await scope.Context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(GameLoan loan, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        var existing = await scope.Context.Loans.FirstOrDefaultAsync(l => l.Id == loan.Id, cancellationToken);
        if (existing != null)
        {
            if (loan.IsReturned && !existing.IsReturned)
            {
                existing.MarkAsReturned();
            }
            await scope.Context.SaveChangesAsync(cancellationToken);
        }
    }
}
