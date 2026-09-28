using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Identity;
using Ludeka.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Data;

/// <inheritdoc />
public class ExternalLoginRepository : DbContextRepositoryBase, IExternalLoginRepository
{
    public ExternalLoginRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal ExternalLoginRepository(LudekaDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<ExternalLogin?> GetByProviderKeyAsync(string provider, string providerKey, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        return await scope.Context.ExternalLogins
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Provider == provider && l.ProviderKey == providerKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(ExternalLogin externalLogin, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        await scope.Context.ExternalLogins.AddAsync(externalLogin, cancellationToken);

        try
        {
            await scope.Context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Segunda barrera de LinkAsync (INC-49, diseño §3.1): traduce la excepción del ORM a
            // lenguaje de dominio aquí, en Infrastructure, para que Ludeka.Application no necesite
            // conocer EF Core (Clean Architecture: el ORM se queda en esta capa).
            throw new DuplicateExternalLoginException(
                $"El par (Provider, ProviderKey) = ({externalLogin.Provider}, {externalLogin.ProviderKey}) ya está tomado.",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExternalLogin>> ListByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        return await scope.Context.ExternalLogins
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(ExternalLogin externalLogin, CancellationToken cancellationToken = default)
    {
        // Corrección INC-49 (PR #2): si esta misma fila ya está bajo seguimiento por otra instancia
        // -por ejemplo, tras un AddAsync anterior en el mismo DbContext, el caso real de LinkAsync
        // seguido de UnlinkAsync sobre el mismo ámbito- se elimina esa instancia rastreada. Adjuntar
        // una segunda instancia con la misma clave lanzaría un conflicto de identidad en el ChangeTracker.
        await using var scope = await CreateScopeAsync(cancellationToken);
        var tracked = scope.Context.ChangeTracker.Entries<ExternalLogin>()
            .FirstOrDefault(e => e.Entity.Id == externalLogin.Id)?.Entity;

        scope.Context.ExternalLogins.Remove(tracked ?? externalLogin);
        await scope.Context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        await using var scope = await CreateScopeAsync(cancellationToken);
        var logins = await scope.Context.ExternalLogins
            .Where(l => l.UserId == userId)
            .ToListAsync(cancellationToken);

        if (logins.Count > 0)
        {
            scope.Context.ExternalLogins.RemoveRange(logins);
            await scope.Context.SaveChangesAsync(cancellationToken);
        }
    }
}

