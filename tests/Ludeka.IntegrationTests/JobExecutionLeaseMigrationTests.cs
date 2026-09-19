using System;
using System.Threading.Tasks;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Ludeka.IntegrationTests;

/// <summary>
/// Verifica que la migración de Entity Framework Core que introduce <c>JobExecutionLeases</c>
/// (INC-47, R3b, diseño §7.1) no pierde ninguna fila ya existente en
/// "NightlyCatalogingExecutionLogs" al aplicarse sobre PostgreSQL real (especificación
/// <c>background-jobs-scheduling</c>, escenario "Migración Npgsql sobre datos de producción
/// existentes"). Colección propia: ver <see cref="PostgresJobLeasesCollection"/> para el motivo.
/// </summary>
[Collection("postgres-real-job-leases")]
public class JobExecutionLeaseMigrationTests
{
    /// <summary>Última migración real anterior a esta (INC-47, R3a completo).</summary>
    private const string PreviousMigration = "20260919010426_AddNotificationOutboxMessages";

    private static readonly Guid LegacyLogId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly PostgresFixture _fixture;

    public JobExecutionLeaseMigrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MigrateAsync_ConNightlyCatalogingExecutionLogDeProduccionPreexistente_NoPierdeLaFilaYCreaJobExecutionLeases()
    {
        _fixture.EnsureAvailable();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        // Arrange: aplicar el historial real hasta la última migración anterior a este incremento
        // y sembrar una fila de producción de la bitácora nocturna, ajena a JobExecutionLeases.
        await using (var arrangeDb = new LudekaDbContext(options))
        {
            await arrangeDb.GetService<IMigrator>().MigrateAsync(PreviousMigration);

            var connection = arrangeDb.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var seedCommand = connection.CreateCommand();
            seedCommand.CommandText = """
                INSERT INTO "NightlyCatalogingExecutionLogs"
                    ("Id", "StartedAt", "CompletedAt", "QueueProcessedCount", "NewsDiscoveryCount",
                     "BggDiscoveryCount", "TopBackfillCount", "TotalCatalogedCount", "FailedCount",
                     "CatalogedTitlesJson", "Status", "ErrorMessage")
                VALUES
                    (@id, '2026-09-01T02:00:00+00:00', '2026-09-01T02:04:00+00:00', 12, 3, 1, 2, 5, 0,
                     '["Preexistente"]', 'Completed', NULL);
                """;
            var idParam = seedCommand.CreateParameter();
            idParam.ParameterName = "id";
            idParam.Value = LegacyLogId;
            seedCommand.Parameters.Add(idParam);
            await seedCommand.ExecuteNonQueryAsync();
            await connection.CloseAsync();
        }

        // Act: aplicar el resto del historial, incluida la migración nueva de concesiones de ventana.
        await using (var migrateDb = new LudekaDbContext(options))
        {
            await migrateDb.GetService<IMigrator>().MigrateAsync();
        }

        // Assert: la fila preexistente de la bitácora nocturna conserva sus valores originales;
        // la tabla nueva de concesiones existe y aparece vacía.
        await using var assertDb = new LudekaDbContext(options);
        var legacyLog = await assertDb.NightlyCatalogingExecutionLogs.AsNoTracking().SingleAsync(l => l.Id == LegacyLogId);
        Assert.Equal(12, legacyLog.QueueProcessedCount);
        Assert.Equal("Completed", legacyLog.Status);
        Assert.Equal("[\"Preexistente\"]", legacyLog.CatalogedTitlesJson);

        var leases = await assertDb.JobExecutionLeases.AsNoTracking().ToListAsync();
        Assert.Empty(leases);
    }
}
