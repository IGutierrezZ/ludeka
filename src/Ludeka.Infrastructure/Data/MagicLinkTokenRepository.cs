using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Data;

/// <inheritdoc />
public class MagicLinkTokenRepository : DbContextRepositoryBase, IMagicLinkTokenRepository
{
    public MagicLinkTokenRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal MagicLinkTokenRepository(LudekaDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task AddAsync(MagicLinkToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        await using var scope = await CreateScopeAsync(cancellationToken);
        await scope.Context.MagicLinkTokens.AddAsync(token, cancellationToken);
        await scope.Context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MagicLinkToken?> GetValidByTokenHashAsync(string tokenHash, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            return null;

        await using var scope = await CreateScopeAsync(cancellationToken);
        var token = await scope.Context.MagicLinkTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (token is null || !token.IsValid(nowUtc))
        {
            return null;
        }

        return token;
    }

    /// <inheritdoc />
    public async Task<MagicLinkToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        return await scope.Context.MagicLinkTokens
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(MagicLinkToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        await using var scope = await CreateScopeAsync(cancellationToken);
        scope.Context.MagicLinkTokens.Update(token);
        await scope.Context.SaveChangesAsync(cancellationToken);
    }
}
