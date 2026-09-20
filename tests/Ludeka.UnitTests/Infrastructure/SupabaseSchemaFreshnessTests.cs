using System;
using System.IO;
using System.Linq;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

/// <summary>
/// Verifica, sin PostgreSQL real (offline, sobre el modelo compilado), que
/// <c>docs/database/supabase_schema.sql</c> no diverge del modelo real de
/// <see cref="LudekaDbContext"/> (INC-48, diseño §D5, especificación
/// <c>postgres-schema-verification</c>). Misma técnica de búsqueda de texto sobre el árbol real de
/// ficheros que <c>WebHostHostedServiceCompositionTests.cs</c> (escenario 2, offline): el fichero
/// es DERIVADO de las migraciones, nunca fuente de verdad editable a mano, y esta prueba es la
/// guarda automática que evita que vuelva a mentir sobre qué tablas existen (diseño §D5, "Cómo se
/// evita que vuelva a mentir").
/// </summary>
public class SupabaseSchemaFreshnessTests
{
    [Fact]
    public void SupabaseSchemaSql_ContieneUnCreateTablePorCadaTablaDeclaradaPorElModelo()
    {
        using var context = new LudekaDbContext(
            new DbContextOptionsBuilder<LudekaDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options);

        var tablasDelModelo = context.Model.GetEntityTypes()
            .Select(entityType => entityType.GetTableName())
            .Where(nombre => nombre is not null)
            .Select(nombre => nombre!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var schemaSqlPath = Path.Combine(GetRepoRoot(), "docs", "database", "supabase_schema.sql");
        var schemaSql = File.ReadAllText(schemaSqlPath);

        var tablasSinCreateTable = tablasDelModelo
            .Where(nombre => !schemaSql.Contains($"CREATE TABLE \"{nombre}\"", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(tablasSinCreateTable);
    }

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Ludeka.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
