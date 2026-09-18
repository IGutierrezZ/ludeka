using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;

namespace Ludeka.IntegrationTests;

/// <summary>
/// Prueba de humo que justifica todo el habilitador: ejercita <c>SELECT ... FOR UPDATE SKIP
/// LOCKED</c> contra PostgreSQL real con dos conexiones que reclaman lotes disjuntos sobre una
/// tabla desechable propia (especificación <c>postgres-integration-testing</c>, "La suite levanta
/// PostgreSQL real y ejercita FOR UPDATE SKIP LOCKED"). El outbox real (R4) y la idempotencia por
/// ventana (R5/R9) reutilizan esta misma primitiva sobre sus propias tablas.
/// </summary>
[Collection("postgres-real")]
public class PostgresSkipLockedSmokeTests
{
    private readonly PostgresFixture _fixture;

    public PostgresSkipLockedSmokeTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ForUpdateSkipLocked_ConDosConexionesReales_DebeReclamarFilasDisjuntas()
    {
        _fixture.EnsureAvailable();

        // Arrange: tabla desechable propia de esta prueba, con 10 filas reclamables.
        await using var setupConnection = new NpgsqlConnection(_fixture.ConnectionString);
        await setupConnection.OpenAsync();

        await using (var setupCommand = setupConnection.CreateCommand())
        {
            setupCommand.CommandText =
                "CREATE TABLE IF NOT EXISTS skip_locked_smoke_test (id serial PRIMARY KEY, claimed_by text NULL); " +
                "TRUNCATE TABLE skip_locked_smoke_test; " +
                "INSERT INTO skip_locked_smoke_test (claimed_by) SELECT NULL FROM generate_series(1, 10);";
            await setupCommand.ExecuteNonQueryAsync();
        }

        // Act: dos conexiones reales reclaman lotes de 5 filas cada una. La segunda reclama con
        // la primera transacción todavía abierta, así que FOR UPDATE SKIP LOCKED debe saltarse
        // las 5 filas que la primera ya tiene bloqueadas.
        await using var connectionOne = new NpgsqlConnection(_fixture.ConnectionString);
        await connectionOne.OpenAsync();
        await using var transactionOne = await connectionOne.BeginTransactionAsync();
        var claimedByOne = await ClaimRowsAsync(connectionOne, transactionOne, batchSize: 5);

        await using var connectionTwo = new NpgsqlConnection(_fixture.ConnectionString);
        await connectionTwo.OpenAsync();
        await using var transactionTwo = await connectionTwo.BeginTransactionAsync();
        var claimedByTwo = await ClaimRowsAsync(connectionTwo, transactionTwo, batchSize: 5);

        await transactionOne.CommitAsync();
        await transactionTwo.CommitAsync();

        // Assert: exactamente 5 filas cada una, y ninguna fila aparece en ambos lotes.
        Assert.Equal(5, claimedByOne.Count);
        Assert.Equal(5, claimedByTwo.Count);
        Assert.Empty(claimedByOne.Intersect(claimedByTwo));
    }

    private static async Task<List<int>> ClaimRowsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, int batchSize)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT id FROM skip_locked_smoke_test WHERE claimed_by IS NULL " +
            "ORDER BY id LIMIT @batchSize FOR UPDATE SKIP LOCKED;";
        command.Parameters.AddWithValue("batchSize", batchSize);

        var claimedIds = new List<int>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            claimedIds.Add(reader.GetInt32(0));
        }

        return claimedIds;
    }
}
