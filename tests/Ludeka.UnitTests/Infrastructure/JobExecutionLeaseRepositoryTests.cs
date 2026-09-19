using System;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

/// <summary>
/// Adquisición de <see cref="JobExecutionLeaseRepository.TryAcquireAsync"/> contra SQLite
/// (INC-47, R5, diseño §7.3, tasks.md 9.5): concesión huérfana (latido caducado) tomada por
/// intercambio condicional, y concesión viva respetada sin tocar. Mismo patrón de conexión
/// compartida en memoria que <c>NotificationOutboxRepositoryTests</c>.
/// </summary>
public class JobExecutionLeaseRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public JobExecutionLeaseRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setupContext = CreateContext();
        setupContext.Database.EnsureCreated();
    }

    private LudekaDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<LudekaDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task TryAcquireAsync_ConConcesionHuerfanaDeLatidoCaducado_TomaElControlPorIntercambioCondicional()
    {
        Guid orphanId;
        using (var arrangeContext = CreateContext())
        {
            var orphan = new JobExecutionLease("nightly-cataloging", "2026-09-19", "host-caido:111");
            arrangeContext.JobExecutionLeases.Add(orphan);
            await arrangeContext.SaveChangesAsync();
            orphanId = orphan.Id;

            // Retrocede el latido más allá de StaleLeaseMinutes (60 por defecto, diseño §7.5): el
            // setter es privado; mismo recurso que HealthChecksTests (Fase 8) ya usó para
            // retroceder CreatedAt.
            arrangeContext.Entry(orphan).Property(l => l.HeartbeatAt).CurrentValue =
                DateTimeOffset.UtcNow.AddMinutes(-61);
            await arrangeContext.SaveChangesAsync();
        }

        using var actContext = CreateContext();
        var repository = new JobExecutionLeaseRepository(actContext);

        var acquisition = await repository.TryAcquireAsync("nightly-cataloging", "2026-09-19", "host-nuevo:222");

        Assert.Equal(LeaseAcquisitionOutcome.Acquired, acquisition.Outcome);
        Assert.Equal(orphanId, acquisition.LeaseId);

        using var assertContext = CreateContext();
        var lease = await assertContext.JobExecutionLeases.AsNoTracking().SingleAsync(l => l.Id == orphanId);
        Assert.Equal("Running", lease.Status);
        Assert.Equal("host-nuevo:222", lease.HostIdentifier);
    }

    [Fact]
    public async Task TryAcquireAsync_ConConcesionVivaDeLatidoReciente_NoLaTocaYReportaQueOtroLaTiene()
    {
        Guid liveId;
        using (var arrangeContext = CreateContext())
        {
            var live = new JobExecutionLease("nightly-cataloging", "2026-09-19", "host-vivo:333");
            arrangeContext.JobExecutionLeases.Add(live);
            await arrangeContext.SaveChangesAsync();
            liveId = live.Id;
        }

        using var actContext = CreateContext();
        var repository = new JobExecutionLeaseRepository(actContext);

        var acquisition = await repository.TryAcquireAsync("nightly-cataloging", "2026-09-19", "host-nuevo:222");

        Assert.Equal(LeaseAcquisitionOutcome.HeldByOther, acquisition.Outcome);
        Assert.Equal(liveId, acquisition.LeaseId);

        using var assertContext = CreateContext();
        var lease = await assertContext.JobExecutionLeases.AsNoTracking().SingleAsync(l => l.Id == liveId);
        Assert.Equal("host-vivo:333", lease.HostIdentifier);
        Assert.Equal("Running", lease.Status);
    }

    public void Dispose() => _connection.Dispose();
}
