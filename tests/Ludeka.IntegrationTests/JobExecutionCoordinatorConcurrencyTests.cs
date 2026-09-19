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

    /// <summary>Instancias que no consiguen la ventana: las cinco del escenario menos la ganadora.</summary>
    private const int ExpectedLosers = 4;

    /// <summary>
    /// Tope de seguridad para que la prueba no se cuelgue si un defecto real impide que las cuatro
    /// perdedoras lleguen a intentarlo. No participa en el camino normal, donde la espera se
    /// resuelve en milisegundos.
    /// </summary>
    private static readonly TimeSpan ContentionTimeout = TimeSpan.FromSeconds(30);

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

        // La ganadora retiene la ventana hasta que las otras cuatro hayan intentado adquirirla.
        // Sin esta sincronización el resultado depende del planificador: JobExecutionCoordinator
        // confirma la concesión ANTES de ejecutar el trabajo y llama a MarkCompletedAsync DESPUÉS,
        // así que una instancia que llegue tarde lee AlreadyCompleted en lugar de HeldByOther y la
        // aserción de contención falla de forma intermitente. El escenario AlreadyCompleted ya lo
        // cubre la otra prueba de esta clase; esta existe para ejercitar la contención real.
        var losersAttempted = 0;
        var allLosersAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act: cinco instancias del proceso (cada una con su propia conexión real), simulando el
        // escalado hasta --max-instances=5, disputan la misma ventana a la vez.
        var instanceTasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            await using var db = new LudekaDbContext(options);
            var coordinator = NewCoordinator(db);
            var outcome = await coordinator.ExecuteWithWindowLeaseAsync(jobName, windowKey, async (_, ct) =>
            {
                Interlocked.Increment(ref processedCount);
                // Retener la ventana tomada mientras las perdedoras la disputan. El coordinador no
                // mantiene ninguna transacción abierta durante el trabajo y la adquisición usa
                // SKIP LOCKED, así que ninguna instancia se bloquea esperando a esta.
                await Task.WhenAny(allLosersAttempted.Task, Task.Delay(ContentionTimeout, ct));
                return new JobWorkResult(20, 0, "ok");
            });

            // Solo las perdedoras cuentan. Si un defecto real dejara que dos instancias adquirieran
            // la ventana, el contador nunca llegaría a cuatro y la espera vencería por tiempo: la
            // prueba seguiría adelante y las aserciones reportarían el reparto real de resultados.
            if (outcome != JobLeaseOutcome.Completed &&
                Interlocked.Increment(ref losersAttempted) == ExpectedLosers)
            {
                allLosersAttempted.TrySetResult();
            }

            return outcome;
        });

        var outcomes = await Task.WhenAll(instanceTasks);

        // Assert: como máximo una completa el procesamiento; las demás detectan que la ventana
        // ya fue reclamada y terminan sin procesar ningún trabajo duplicado.
        Assert.Equal(1, outcomes.Count(o => o == JobLeaseOutcome.Completed));
        Assert.Equal(ExpectedLosers, outcomes.Count(o => o == JobLeaseOutcome.SkippedHeldByOther));
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
