using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Jobs;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.IntegrationTests;

/// <summary>
/// Idempotencia por ventana bajo concurrencia real contra PostgreSQL (INC-47, R5, diseño §7,
/// tasks.md 9.1/9.2). Especificación <c>background-jobs-scheduling</c>, requisito "Ejecución
/// única por ventana temporal bajo escalado y reintentos del planificador": es el único
/// requisito que exige demostrar la restricción <c>UNIQUE</c> real bajo instancias de proceso
/// genuinamente concurrentes, primitiva que SQLite no puede ejercitar de forma equivalente bajo
/// concurrencia real (spec.md:67).
/// </summary>
[Collection("postgres-real-job-coordinator")]
public class JobExecutionCoordinatorConcurrencyTests
{
    private readonly PostgresFixture _fixture;

    public JobExecutionCoordinatorConcurrencyTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static JobExecutionCoordinator NewCoordinator(LudekaDbContext context) =>
        new(new JobExecutionLeaseRepository(context));

    [Fact]
    public async Task ExecuteWithWindowLeaseAsync_ConCincoInstanciasConcurrentes_SoloUnaCompletaLaVentana()
    {
        _fixture.EnsureAvailable();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        await using (var migrateDb = new LudekaDbContext(options))
        {
            await migrateDb.Database.MigrateAsync();
        }

        const string jobName = "nightly-cataloging";
        const string windowKey = "2026-09-19";
        var processedCount = 0;

        // Act: cinco instancias del proceso (cada una con su propia conexión real), simulando el
        // escalado hasta --max-instances=5, disputan la misma ventana a la vez.
        var instanceTasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            await using var db = new LudekaDbContext(options);
            var coordinator = NewCoordinator(db);
            return await coordinator.ExecuteWithWindowLeaseAsync(jobName, windowKey, async (_, ct) =>
            {
                Interlocked.Increment(ref processedCount);
                await Task.Delay(100, ct); // ensancha la ventana de carrera real
                return new JobWorkResult(20, 0, "ok");
            });
        });

        var outcomes = await Task.WhenAll(instanceTasks);

        // Assert: como máximo una completa el procesamiento; las demás detectan que la ventana
        // ya fue reclamada y terminan sin procesar ningún trabajo duplicado.
        Assert.Equal(1, outcomes.Count(o => o == JobLeaseOutcome.Completed));
        Assert.Equal(4, outcomes.Count(o => o == JobLeaseOutcome.SkippedHeldByOther));
        Assert.Equal(1, processedCount);

        await using var assertDb = new LudekaDbContext(options);
        var leaseCount = await assertDb.JobExecutionLeases
            .CountAsync(l => l.JobName == jobName && l.WindowKey == windowKey);
        Assert.Equal(1, leaseCount);
    }

    [Fact]
    public async Task ExecuteWithWindowLeaseAsync_ConVentanaYaCompletada_ElReintentoDelPlanificadorChocaContraUniqueYNoRepiteElTrabajo()
    {
        _fixture.EnsureAvailable();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        await using (var migrateDb = new LudekaDbContext(options))
        {
            await migrateDb.Database.MigrateAsync();
        }

        const string jobName = "nightly-cataloging";
        const string windowKey = "2026-09-18";

        await using (var firstDb = new LudekaDbContext(options))
        {
            var firstOutcome = await NewCoordinator(firstDb).ExecuteWithWindowLeaseAsync(
                jobName, windowKey, (_, _) => Task.FromResult(new JobWorkResult(9, 0, "ok")));
            Assert.Equal(JobLeaseOutcome.Completed, firstOutcome);
        }

        // El planificador externo reintenta el disparo de la misma ventana (p.ej. tras un
        // timeout de red), desde una instancia nueva sin ningún estado compartido en memoria con
        // la anterior: debe chocar contra la restricción UNIQUE, nunca contra un `if`.
        var retryWorkInvoked = false;
        await using var retryDb = new LudekaDbContext(options);
        var retryOutcome = await NewCoordinator(retryDb).ExecuteWithWindowLeaseAsync(
            jobName, windowKey,
            (_, _) => { retryWorkInvoked = true; return Task.FromResult(new JobWorkResult(9, 0, "ok")); });

        Assert.Equal(JobLeaseOutcome.SkippedAlreadyCompleted, retryOutcome);
        Assert.False(retryWorkInvoked);
    }
}
