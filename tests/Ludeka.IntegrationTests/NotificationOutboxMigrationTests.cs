using System;
using System.Threading.Tasks;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Ludeka.IntegrationTests;

/// <summary>
/// Verifica que la migración de Entity Framework Core que introduce el esquema del outbox de
/// notificaciones (INC-47, R3a, diseño §5) no pierde ningún registro ya existente en
/// "NotificationLogs" al aplicarse sobre PostgreSQL real (especificación
/// <c>notification-outbox</c>, escenario "Migración Npgsql sobre notificaciones de producción
/// existentes").
/// </summary>
[Collection("postgres-real")]
public class NotificationOutboxMigrationTests
{
    /// <summary>Última migración real anterior a este incremento, confirmada en disco (INC-49).</summary>
    private const string PreviousMigration = "20260917112154_AddProviderEmailVerifiedAtToExternalLogins";

    private static readonly Guid LegacyLogId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly PostgresFixture _fixture;

    public NotificationOutboxMigrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MigrateAsync_ConNotificationLogDeProduccionPreexistente_NoPierdeLaFilaYCreaElEsquemaDelOutbox()
    {
        _fixture.EnsureAvailable();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        // Arrange: aplicar el historial real hasta la última migración anterior a este incremento
        // y sembrar una fila de producción con el esquema todavía sin las columnas del outbox.
        await using (var arrangeDb = new LudekaDbContext(options))
        {
            await arrangeDb.GetService<IMigrator>().MigrateAsync(PreviousMigration);

            var connection = arrangeDb.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var seedCommand = connection.CreateCommand();
            seedCommand.CommandText = """
                INSERT INTO "NotificationLogs"
                    ("Id", "EventType", "Channel", "Title", "Summary", "TargetUrl", "ImageUrl",
                     "Status", "ErrorDetails", "CreatedAt", "SentAt")
                VALUES
                    (@id, 0, 0, 'Sorteo de mesa activo', 'Resumen del sorteo', NULL, NULL,
                     1, NULL, '2026-09-01T00:00:00+00:00', '2026-09-01T00:00:05+00:00');
                """;
            var idParam = seedCommand.CreateParameter();
            idParam.ParameterName = "id";
            idParam.Value = LegacyLogId;
            seedCommand.Parameters.Add(idParam);
            await seedCommand.ExecuteNonQueryAsync();
            await connection.CloseAsync();
        }

        // Act: aplicar el resto del historial, incluida la migración nueva del outbox.
        await using (var migrateDb = new LudekaDbContext(options))
        {
            await migrateDb.GetService<IMigrator>().MigrateAsync();
        }

        // Assert: la fila preexistente conserva su título y recibe valores por defecto en las
        // columnas nuevas; la tabla nueva de mensajes existe y aparece vacía.
        await using var assertDb = new LudekaDbContext(options);
        var legacyLog = await assertDb.NotificationLogs.AsNoTracking().SingleAsync(l => l.Id == LegacyLogId);
        Assert.Equal("Sorteo de mesa activo", legacyLog.Title);
        Assert.Null(legacyLog.MessageId);
        Assert.Equal(0, legacyLog.Attempts);
        Assert.Null(legacyLog.NextAttemptAt);

        var outboxMessages = await assertDb.NotificationOutboxMessages.AsNoTracking().ToListAsync();
        Assert.Empty(outboxMessages);
    }
}
