using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.IntegrationTests;

/// <summary>
/// Verifica, contra un PostgreSQL 17 real y vacío (Testcontainers), que las migraciones de Entity
/// Framework Core existentes producen el esquema completo declarado por el modelo (INC-48,
/// diseño §D5, especificación <c>postgres-schema-verification</c>). Colección propia
/// (<see cref="PostgresSchemaCollection"/>): migra el historial COMPLETO desde cero y asevera
/// recuentos exactos, algo que ninguna de las otras colecciones de <see cref="PostgresFixture"/>
/// hace sobre "postgres-real" o sus derivadas — ver el resumen de esa colección para el motivo
/// completo del aislamiento.
///
/// Dos niveles de aserción (diseño §D5): un INVARIANTE que compara el modelo real contra el
/// esquema real, y detecta cualquier divergencia accidental entre ambos; y un CANARIO literal (34
/// tablas, 7 migraciones) que caduca deliberadamente con la siguiente migración que añada o quite
/// una tabla, para que ese cambio no pase inadvertido sin revisar y actualizar este número.
/// </summary>
[Collection("postgres-real-schema")]
public class PostgresSchemaVerificationTests
{
    /// <summary>Canario literal (diseño §D5): número de tablas declaradas por el modelo hoy.</summary>
    private const int TablasEsperadas = 40;

    /// <summary>Canario literal (diseño §D5): un fichero de migración real bajo Migrations/.</summary>
    private const int MigracionesEsperadas = 18;

    private readonly PostgresFixture _fixture;

    public PostgresSchemaVerificationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MigrateAsync_DesdeUnaBaseVacia_CreaExactamenteLasTablasDelModeloYRegistraUnaFilaPorMigracion()
    {
        _fixture.EnsureAvailable();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        // Act: migrar el historial completo desde un contenedor recién arrancado (sin estado
        // previo). Contexto propio para esta fase, igual que el patrón ya establecido en
        // NotificationOutboxMigrationTests/JobExecutionLeaseMigrationTests.
        await using (var migrateContext = new LudekaDbContext(options))
        {
            await migrateContext.Database.MigrateAsync();
        }

        await using var assertContext = new LudekaDbContext(options);

        // Invariante 1: ninguna migración queda pendiente tras aplicar el historial completo.
        var pending = (await assertContext.Database.GetPendingMigrationsAsync()).ToList();
        Assert.Empty(pending);

        // Invariante 2: una fila de "__EFMigrationsHistory" por cada fichero de migración del
        // proyecto, contada de forma independiente al recuento que EF cree haber aplicado.
        var migrationFileCount = assertContext.Database.GetMigrations().Count();
        var historyRowCount = await ContarFilasDeHistorialDeMigracionesAsync(assertContext);
        Assert.Equal(migrationFileCount, historyRowCount);

        // Invariante 3: las tablas base reales de PostgreSQL coinciden, nombre a nombre, con las
        // tablas declaradas por el modelo (excluyendo la propia tabla de historial de EF).
        var tablasReales = await ObtenerTablasBaseRealesAsync(assertContext);
        var tablasDelModelo = assertContext.Model.GetEntityTypes()
            .Select(entityType => entityType.GetTableName())
            .Where(nombre => nombre is not null)
            .Select(nombre => nombre!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(nombre => nombre, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(tablasDelModelo, tablasReales);

        // Canario literal: caduca deliberadamente con la siguiente migración que cambie el número
        // de tablas o de ficheros de migración.
        Assert.Equal(TablasEsperadas, tablasReales.Count);
        Assert.Equal(MigracionesEsperadas, historyRowCount);
    }

    private static async Task<int> ContarFilasDeHistorialDeMigracionesAsync(LudekaDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """SELECT COUNT(*) FROM "__EFMigrationsHistory";""";
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task<List<string>> ObtenerTablasBaseRealesAsync(LudekaDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT table_name
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_type = 'BASE TABLE'
                  AND table_name <> '__EFMigrationsHistory'
                ORDER BY table_name;
                """;
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult);
            var tablas = new List<string>();
            while (await reader.ReadAsync())
            {
                tablas.Add(reader.GetString(0));
            }
            return tablas;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
