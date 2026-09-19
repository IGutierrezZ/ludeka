using System;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

    [Fact]
    public async Task TryAcquireAsync_ConStaleLeaseMinutesConfigurado_UsaElUmbralDeConfiguracionEnLugarDelValorPorDefecto()
    {
        // INC-47 (R7, diseño §7.5, tasks.md 11.4): cierra la deuda de tasks.md 9b.3 —
        // StaleLeaseMinutes deja de ser una constante fija y pasa a leerse de
        // Workers:StaleLeaseMinutes. Este latido tiene 10 minutos, insuficiente para el valor por
        // defecto (60, probado en TryAcquireAsync_ConConcesionVivaDeLatidoReciente_...) pero
        // suficiente para un umbral configurado a 5.
        Guid orphanId;
        using (var arrangeContext = CreateContext())
        {
            var orphan = new JobExecutionLease("price-radar", "2026-09-19T10", "host-caido:444");
            arrangeContext.JobExecutionLeases.Add(orphan);
            await arrangeContext.SaveChangesAsync();
            orphanId = orphan.Id;

            arrangeContext.Entry(orphan).Property(l => l.HeartbeatAt).CurrentValue =
                DateTimeOffset.UtcNow.AddMinutes(-10);
            await arrangeContext.SaveChangesAsync();
        }

        using var actContext = CreateContext();
        var workersOptions = Microsoft.Extensions.Options.Options.Create(new WorkersOptions { StaleLeaseMinutes = 5 });
        var repository = new JobExecutionLeaseRepository(actContext, workersOptions);

        var acquisition = await repository.TryAcquireAsync("price-radar", "2026-09-19T10", "host-nuevo:555");

        Assert.Equal(LeaseAcquisitionOutcome.Acquired, acquisition.Outcome);
        Assert.Equal(orphanId, acquisition.LeaseId);
    }

    public void Dispose() => _connection.Dispose();
}
