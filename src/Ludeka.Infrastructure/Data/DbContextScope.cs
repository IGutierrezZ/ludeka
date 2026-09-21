using System;
using System.Threading.Tasks;

namespace Ludeka.Infrastructure.Data;

/// <summary>
/// Proporciona un ámbito efímero o compartido de <see cref="LudekaDbContext"/>.
/// Si fue creado a partir de una factoría (<see cref="Microsoft.EntityFrameworkCore.IDbContextFactory{LudekaDbContext}"/>),
/// se desecha automáticamente al salir del bloque <c>using</c> o <c>await using</c>.
/// Si se originó de una instancia única inyectada (p. ej. en pruebas unitarias con SQLite en memoria),
/// no se desecha para preservar la conexión abierta de la prueba.
/// </summary>
public readonly struct DbContextScope : IAsyncDisposable, IDisposable
{
    private readonly bool _shouldDispose;
    public LudekaDbContext Context { get; }

    public DbContextScope(LudekaDbContext context, bool shouldDispose)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        _shouldDispose = shouldDispose;
    }

    public void Dispose()
    {
        if (_shouldDispose)
        {
            Context.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_shouldDispose)
        {
            await Context.DisposeAsync();
        }
    }
}
