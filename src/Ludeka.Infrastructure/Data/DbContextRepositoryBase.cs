using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ludeka.Infrastructure.Data;

/// <summary>
/// Clase base para repositorios que acceden a <see cref="LudekaDbContext"/>.
/// Admite inyección mediante <see cref="IDbContextFactory{LudekaDbContext}"/> para aislar cada
/// operación en su propio contexto efímero (patrón recomendado por Microsoft para Blazor Server),
/// o inyección directa de <see cref="LudekaDbContext"/> para pruebas unitarias e integración.
/// </summary>
public abstract class DbContextRepositoryBase
{
    private readonly IDbContextFactory<LudekaDbContext>? _factory;
    private readonly LudekaDbContext? _context;

    [ActivatorUtilitiesConstructor]
    protected DbContextRepositoryBase(IDbContextFactory<LudekaDbContext> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    protected internal DbContextRepositoryBase(LudekaDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Genera un ámbito de contexto para la operación actual. En tiempo de ejecución con factoría,
    /// cada ámbito contiene una instancia aislada y fresca que se desecha al finalizar el bloque <c>using</c>.
    /// </summary>
    protected async ValueTask<DbContextScope> CreateScopeAsync(CancellationToken cancellationToken = default)
    {
        if (_factory != null)
        {
            var db = await _factory.CreateDbContextAsync(cancellationToken);
            return new DbContextScope(db, shouldDispose: true);
        }

        return new DbContextScope(_context!, shouldDispose: false);
    }
}
