using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Data;

/// <inheritdoc />
public class ExternalLoginRepository : IExternalLoginRepository
{
    private readonly LudekaDbContext _context;

    public ExternalLoginRepository(LudekaDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<ExternalLogin?> GetByProviderKeyAsync(string provider, string providerKey, CancellationToken cancellationToken = default)
    {
        return await _context.ExternalLogins
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Provider == provider && l.ProviderKey == providerKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(ExternalLogin externalLogin, CancellationToken cancellationToken = default)
    {
        await _context.ExternalLogins.AddAsync(externalLogin, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
