using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Seeding;

/// <summary>
/// Implementación de infraestructura para la siembra y sincronización del directorio (INC-54).
/// Ejecuta la operación contra un DbContext efímero para evitar colisiones de concurrencia en Blazor Server.
/// </summary>
public class DirectorySeederService : IDirectorySeederService
{
    private readonly IDbContextFactory<LudekaDbContext> _dbContextFactory;

    public DirectorySeederService(IDbContextFactory<LudekaDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public async Task<DirectorySeedResultDto> SeedDirectoryAsync(CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var result = await DirectorySeeder.SeedDirectoryAsync(db, ct);

        return new DirectorySeedResultDto(
            result.PublishersAdded,
            result.PublishersUpdated,
            result.StoresAdded,
            result.StoresUpdated,
            result.CreatorsAdded,
            result.CreatorsUpdated,
            result.TotalPublishers,
            result.TotalStores,
            result.TotalCreators);
    }
}
