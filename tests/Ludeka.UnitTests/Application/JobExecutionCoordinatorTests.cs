using System;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Jobs;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Notifications;
using Ludeka.Infrastructure.Background;
using Ludeka.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// Coordinador de idempotencia por ventana (INC-47, R5, diseño §7.4, tasks.md 9.3/9.4/9.7-9.9).
/// El repositorio es real (SQLite en memoria), mismo patrón que
/// <see cref="NotificationOutboxDispatcherTests"/>: solo se aísla la infraestructura de arranque
/// del host (bucles, <c>IOptionsMonitor</c>), no la persistencia.
/// </summary>
public class JobExecutionCoordinatorTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public JobExecutionCoordinatorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setupContext = CreateContext();
        setupContext.Database.EnsureCreated();
    }

    private LudekaDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<LudekaDbContext>().UseSqlite(_connection).Options);

    private static JobExecutionCoordinator NewCoordinator(LudekaDbContext context) =>
        new(new JobExecutionLeaseRepository(context));

    [Fact]
    public async Task ExecuteWithWindowLeaseAsync_ConFallosParciales_PersisteBitacoraCompletaConMetricas()
    {
        using var context = CreateContext();

        var before = DateTimeOffset.UtcNow;
        var outcome = await NewCoordinator(context).ExecuteWithWindowLeaseAsync(
            "nightly-cataloging", "2026-09-19",
            (_, _) => Task.FromResult(new JobWorkResult(Processed: 8, Failed: 2, Message: "2 fallos parciales")));
        var after = DateTimeOffset.UtcNow;

        // Un fallo PARCIAL (algunos elementos fallan, pero la unidad de trabajo completa sin
        // lanzar) es un desenlace terminal — Completed — no Failed: el reintento del contrato de
        // código de salida (Fase 10) solo se dispara ante una excepción no controlada.
        Assert.Equal(JobLeaseOutcome.Completed, outcome);

        var lease = await context.JobExecutionLeases.AsNoTracking().SingleAsync(
            l => l.JobName == "nightly-cataloging" && l.WindowKey == "2026-09-19");
        Assert.Equal("Completed", lease.Status);
        Assert.InRange(lease.StartedAt, before, after);
        Assert.NotNull(lease.CompletedAt);
        Assert.InRange(lease.CompletedAt!.Value, before, after);
        Assert.Equal(8, lease.ProcessedCount);
        Assert.Equal(2, lease.FailedCount);
        Assert.NotNull(lease.DurationMs);
    }

    [Fact]
    public async Task ExecuteWithWindowLeaseAsync_ConVentanaYaCompletada_NoRepiteLaFaseDeCatalogacion()
    {
        using (var seedContext = CreateContext())
        {
            await NewCoordinator(seedContext).ExecuteWithWindowLeaseAsync(
                "nightly-cataloging", "2026-09-17",
                (_, _) => Task.FromResult(new JobWorkResult(3, 0, "ok")));
        }

        var catalogingPhaseInvoked = false;
        using var context = CreateContext();
        var outcome = await NewCoordinator(context).ExecuteWithWindowLeaseAsync(
            "nightly-cataloging", "2026-09-17",
            (_, _) => { catalogingPhaseInvoked = true; return Task.FromResult(new JobWorkResult(3, 0, "ok")); });

        Assert.Equal(JobLeaseOutcome.SkippedAlreadyCompleted, outcome);
        Assert.False(catalogingPhaseInvoked);
    }

    [Fact]
    public async Task ExecuteWithWindowLeaseAsync_ConVentanaPendiente_ReservaEnBaseDeDatosAntesDeEjecutarYPersisteMetricasAlFinalizar()
    {
        using var context = CreateContext();

        string? statusDuranteElTrabajo = null;
        var outcome = await NewCoordinator(context).ExecuteWithWindowLeaseAsync(
            "nightly-cataloging", "2026-09-19",
            async (_, _) =>
            {
                // La reserva debe ser visible en base de datos ANTES de ejecutar el trabajo
                // (diseño §7.3, "INSERT primero"): TryAcquireAsync usa ADO.NET crudo, así que
                // esta lectura por LINQ no encuentra nada ya rastreado en el contexto y consulta
                // de verdad la fila recién insertada.
                var duringWork = await context.JobExecutionLeases.AsNoTracking().SingleAsync(
                    l => l.JobName == "nightly-cataloging" && l.WindowKey == "2026-09-19");
                statusDuranteElTrabajo = duringWork.Status;

                return new JobWorkResult(12, 0, "ok");
            });

        Assert.Equal("Running", statusDuranteElTrabajo);
        Assert.Equal(JobLeaseOutcome.Completed, outcome);

        var leaseAlFinalizar = await context.JobExecutionLeases.AsNoTracking().SingleAsync(
            l => l.JobName == "nightly-cataloging" && l.WindowKey == "2026-09-19");
        Assert.Equal("Completed", leaseAlFinalizar.Status);
        Assert.Equal(12, leaseAlFinalizar.ProcessedCount);
        Assert.Equal(0, leaseAlFinalizar.FailedCount);
        Assert.NotNull(leaseAlFinalizar.DurationMs);
    }

    public void Dispose() => _connection.Dispose();
}
