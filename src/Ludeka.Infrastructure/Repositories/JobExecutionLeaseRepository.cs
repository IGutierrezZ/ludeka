using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ludeka.Infrastructure.Repositories;

/// <summary>
/// Adquisición y transición de <see cref="JobExecutionLease"/> (INC-47, R5, diseño §7.3/§7.4).
/// <see cref="TryAcquireAsync"/> usa un comando ADO.NET crudo y parametrizado sobre
/// <c>Database.GetDbConnection()</c> — mismo patrón que
/// <see cref="NotificationOutboxRepository.ClaimPendingAsync"/> — porque el intercambio
/// condicional (<c>UPDATE ... RETURNING</c>) no se compone desde LINQ. El resto de métodos usa
/// EF Core normal sobre la entidad ya cargada, igual que
/// <see cref="NotificationOutboxRepository.CompleteMessageAsync"/> y análogos.
/// </summary>
public class JobExecutionLeaseRepository : IJobExecutionLeaseRepository
{
    // INC-47 (R7, diseño §7.5, tasks.md 11.4): StaleLeaseMinutes se lee ahora de
    // Workers:StaleLeaseMinutes — cierra la deuda declarada en tasks.md 9b.3, cuando esta
    // constante nació porque la sección "Workers" de appsettings.json todavía no existía.
    // IOptions<WorkersOptions> es opcional para no romper los constructores directos que ya
    // usaban las pruebas (JobExecutionLeaseRepositoryTests, JobExecutionCoordinatorTests,
    // JobExecutionCoordinatorConcurrencyTests): sin contenedor de dependencias de por medio, se
    // conserva el mismo valor por defecto que el diseño documenta.
    private readonly int _staleLeaseMinutes;

    private readonly LudekaDbContext _db;

    public JobExecutionLeaseRepository(LudekaDbContext db, IOptions<WorkersOptions>? workersOptions = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _staleLeaseMinutes = workersOptions?.Value.StaleLeaseMinutes ?? new WorkersOptions().StaleLeaseMinutes;
    }

    public async Task<LeaseAcquisition> TryAcquireAsync(
        string jobName, string windowKey, string? hostIdentifier, CancellationToken ct = default)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = false;

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
            shouldClose = true;
        }

        try
        {
            var isSqlite = _db.Database.IsSqlite();
            var now = DateTimeOffset.UtcNow;
            var leaseId = Guid.NewGuid();

            try
            {
                using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = InsertLeaseSql;
                // SQLite: EF Core convierte Guid a TEXT en MAYÚSCULAS (verificado empíricamente
                // contra un INSERT normal por EF Core); el INSERT crudo debe igualar ese formato
                // byte a byte, o FindAsync (usado por TouchAsync/MarkCompletedAsync/
                // MarkFailedAsync) nunca encuentra la fila que esta misma sentencia acaba de
                // crear — SQLite compara TEXT con distinción de mayúsculas por defecto.
                AddParameter(insertCommand, "@id", isSqlite ? leaseId.ToString().ToUpperInvariant() : leaseId);
                AddParameter(insertCommand, "@job", jobName);
                AddParameter(insertCommand, "@ventana", windowKey);
                AddParameter(insertCommand, "@ahora", now);
                AddParameter(insertCommand, "@host", (object?)hostIdentifier ?? DBNull.Value);
                await insertCommand.ExecuteNonQueryAsync(ct);

                return new LeaseAcquisition(LeaseAcquisitionOutcome.Acquired, leaseId);
            }
            catch (Exception ex) when (IsUniqueViolation(ex))
            {
                return await TakeOverOrClassifyAsync(connection, isSqlite, jobName, windowKey, now, hostIdentifier, ct);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task TouchAsync(Guid leaseId, CancellationToken ct = default)
    {
        var lease = await GetTrackedLeaseAsync(leaseId, ct);
        lease.Touch();
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkCompletedAsync(
        Guid leaseId, int processedCount, int failedCount, long durationMs, CancellationToken ct = default)
    {
        var lease = await GetTrackedLeaseAsync(leaseId, ct);
        lease.MarkCompleted(processedCount, failedCount, durationMs);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(
        Guid leaseId, string? errorMessage, int processedCount, int failedCount, long durationMs,
        CancellationToken ct = default)
    {
        var lease = await GetTrackedLeaseAsync(leaseId, ct);
        lease.MarkFailed(errorMessage, processedCount, failedCount, durationMs);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Único intercambio condicional para ambos casos retomables (diseño §7.3):
    /// concesión huérfana (<c>Running</c> con latido caducado) y reintento tras fallo
    /// (<c>Failed</c>) — el propio diseño los describe como "el mismo intercambio condicional",
    /// solo cambia el predicado de estado; se unifican en un único <c>UPDATE</c>. Si ninguno de
    /// los dos predicados aplica (completada, o viva con latido reciente), clasifica cuál de las
    /// dos es para que el coordinador pueda distinguir <c>SkippedAlreadyCompleted</c> de
    /// <c>SkippedHeldByOther</c>.</summary>
    private async Task<LeaseAcquisition> TakeOverOrClassifyAsync(
        DbConnection connection, bool isSqlite, string jobName, string windowKey,
        DateTimeOffset now, string? hostIdentifier, CancellationToken ct)
    {
        var staleThreshold = now.AddMinutes(-_staleLeaseMinutes);

        using (var updateCommand = connection.CreateCommand())
        {
            updateCommand.CommandText = TakeOverSql;
            AddParameter(updateCommand, "@job", jobName);
            AddParameter(updateCommand, "@ventana", windowKey);
            AddParameter(updateCommand, "@ahora", now);
            AddParameter(updateCommand, "@umbral", staleThreshold);
            AddParameter(updateCommand, "@host", (object?)hostIdentifier ?? DBNull.Value);
            AddParameter(updateCommand, "@nota", "Concesión recuperada: latido caducado o ejecución anterior fallida.");

            using var updateReader = await updateCommand.ExecuteReaderAsync(ct);
            if (await updateReader.ReadAsync(ct))
            {
                return new LeaseAcquisition(LeaseAcquisitionOutcome.Acquired, ReadId(updateReader, 0, isSqlite));
            }
        }

        using var selectCommand = connection.CreateCommand();
        selectCommand.CommandText = SelectExistingSql;
        AddParameter(selectCommand, "@job", jobName);
        AddParameter(selectCommand, "@ventana", windowKey);

        using var selectReader = await selectCommand.ExecuteReaderAsync(ct);
        if (!await selectReader.ReadAsync(ct))
        {
            // La fila desapareció entre el INSERT fallido y esta lectura: no hay una concesión
            // propia que devolver, pero tampoco es seguro ejecutar aquí mismo.
            return new LeaseAcquisition(LeaseAcquisitionOutcome.HeldByOther, null);
        }

        var existingId = ReadId(selectReader, 0, isSqlite);
        var status = selectReader.GetString(1);
        return status == "Completed"
            ? new LeaseAcquisition(LeaseAcquisitionOutcome.AlreadyCompleted, existingId)
            : new LeaseAcquisition(LeaseAcquisitionOutcome.HeldByOther, existingId);
    }

    private async Task<JobExecutionLease> GetTrackedLeaseAsync(Guid leaseId, CancellationToken ct)
    {
        var lease = await _db.JobExecutionLeases.FindAsync(new object[] { leaseId }, ct);
        if (lease is null)
        {
            throw new InvalidOperationException($"No existe ninguna concesión de ejecución con Id '{leaseId}'.");
        }

        return lease;
    }

    private static bool IsUniqueViolation(Exception ex) => ex switch
    {
        Npgsql.PostgresException postgres => postgres.SqlState == "23505",
        Microsoft.Data.Sqlite.SqliteException sqlite => sqlite.SqliteErrorCode == 19,
        _ => false
    };

    private static Guid ReadId(DbDataReader reader, int ordinal, bool isSqlite) =>
        isSqlite ? Guid.Parse(reader.GetString(ordinal)) : reader.GetGuid(ordinal);

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    // ProcessedCount/FailedCount viajan explícitos a 0: la tabla los declara NOT NULL sin
    // DEFAULT a nivel de base de datos (migración AddJobExecutionLeases), así que omitirlos
    // provoca una violación de NOT NULL (23502 en PostgreSQL) en vez de la violación de UNIQUE
    // (23505) que esta sentencia existe para provocar.
    private const string InsertLeaseSql = """
        INSERT INTO "JobExecutionLeases"
            ("Id", "JobName", "WindowKey", "Status", "StartedAt", "HeartbeatAt", "HostIdentifier",
             "ProcessedCount", "FailedCount")
        VALUES (@id, @job, @ventana, 'Running', @ahora, @ahora, @host, 0, 0);
        """;

    private const string TakeOverSql = """
        UPDATE "JobExecutionLeases"
           SET "Status" = 'Running', "StartedAt" = @ahora, "HeartbeatAt" = @ahora,
               "HostIdentifier" = @host, "ErrorMessage" = @nota
         WHERE "JobName" = @job AND "WindowKey" = @ventana
           AND (("Status" = 'Running' AND "HeartbeatAt" < @umbral) OR "Status" = 'Failed')
        RETURNING "Id";
        """;

    private const string SelectExistingSql = """
        SELECT "Id", "Status" FROM "JobExecutionLeases"
         WHERE "JobName" = @job AND "WindowKey" = @ventana;
        """;
}
