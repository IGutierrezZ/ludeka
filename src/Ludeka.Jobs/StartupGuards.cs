using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ludeka.Jobs;

/// <summary>
/// Guardas de arranque de <c>Ludeka.Jobs</c> (INC-47, R6, diseño §8.6): un trabajo contra la base
/// de datos equivocada es peor que un trabajo caído. Ambas se evalúan antes de que
/// <c>Program.cs</c> seleccione y ejecute ningún <see cref="IJobRunner"/>. Nombre de fichero fijado
/// por la propia tarea 10.11.
/// </summary>
public static class StartupGuards
{
    /// <summary>Evalúa las dos guardas y devuelve el mensaje de fallo de la primera que no pase,
    /// o <c>null</c> si ambas pasan. <paramref name="environmentName"/> se pasa explícitamente en
    /// vez de leerse aquí de <c>Environment.GetEnvironmentVariable</c>, para que la guarda sea una
    /// función pura y comprobable sin mutar variables de entorno globales.</summary>
    public static async Task<string?> EvaluateAsync(
        IServiceProvider services,
        IConfiguration configuration,
        string? environmentName,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration.GetConnectionString("PostgreSqlConnection")
            ?? "Data Source=ludeka.db";
        var isPostgreSql = dbOptions.IsPostgreSql(connectionString);

        // Guarda 1 — Coherencia de proveedor (diseño §8.6, guarda 1). Solo se exige en Production
        // cuando Workers:RequirePostgreSqlInProduction está activo (true por defecto, diseño §7.5).
        var requirePostgreSql = configuration.GetValue("Workers:RequirePostgreSqlInProduction", defaultValue: true);
        var isProduction = string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);

        if (requirePostgreSql && isProduction && !isPostgreSql)
        {
            return "Guarda de arranque (coherencia de proveedor): en Production se exige PostgreSQL " +
                   "(Workers:RequirePostgreSqlInProduction) y la cadena resuelta es SQLite. Falta el secreto " +
                   "SUPABASE_DB_CONNECTION (variable ConnectionStrings__DefaultConnection).";
        }

        // Guarda 2 — Esquema al día (diseño §8.6, guarda 2; §4.5). El trabajo NUNCA migra: si hay
        // migraciones pendientes en PostgreSQL, sale antes de ejecutar cualquier unidad de trabajo.
        if (isPostgreSql)
        {
            var db = sp.GetRequiredService<LudekaDbContext>();
            var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
            if (pending.Count > 0)
            {
                return $"Guarda de arranque (esquema al día): el esquema de PostgreSQL tiene {pending.Count} " +
                       "migración(es) pendiente(s). El trabajo no migra nunca el esquema.";
            }
        }
        else
        {
            // En desarrollo local (SQLite), garantizar que el esquema esté inicializado y actualizado
            var db = sp.GetRequiredService<LudekaDbContext>();
            await db.Database.EnsureCreatedAsync(ct);
            if (db.Database.IsSqlite())
            {
                await SqliteSchemaMigrator.EnsureSchemaUpToDateAsync(db, ct);
            }
        }

        return null;
    }
}
