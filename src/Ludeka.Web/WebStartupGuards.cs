using System;
using Ludeka.Infrastructure.Options;
using Microsoft.Extensions.Configuration;

namespace Ludeka.Web;

/// <summary>
/// Guarda de arranque de <c>Ludeka.Web</c> (INC-48, PR2, diseño D3): espejo de la Guarda 1 de
/// <see cref="Ludeka.Jobs.StartupGuards"/> (coherencia de proveedor, solo lectura) — en
/// <c>Production</c> se exige que la conexión efectiva resuelva a PostgreSQL, para eliminar el
/// fallback silencioso a una base SQLite efímera. La Guarda 2 de <c>Ludeka.Jobs</c> (migraciones
/// pendientes) NO se porta a este host: <c>Program.cs</c> sí migra el esquema al arrancar.
/// </summary>
public static class WebStartupGuards
{
    /// <summary>Evalúa la guarda de coherencia de proveedor y devuelve el mensaje de fallo si no
    /// pasa, o <see langword="null"/> si pasa. Función pura: no compone servicios ni lee variables
    /// de entorno globales; <paramref name="environmentName"/> se recibe explícitamente para que
    /// sea comprobable sin mutar estado del proceso.</summary>
    public static string? Evaluate(IConfiguration configuration, DatabaseOptions databaseOptions, string? environmentName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(databaseOptions);

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration.GetConnectionString("PostgreSqlConnection")
            ?? "Data Source=ludeka.db";
        var isPostgreSql = databaseOptions.IsPostgreSql(connectionString);

        // Coherencia de proveedor (diseño D3, espejo de Ludeka.Jobs.StartupGuards.cs:44-54). Solo
        // se exige en Production cuando Database:RequirePostgreSqlInProduction está activo (true
        // por defecto). Clave propia del host web: Ludeka.Jobs conserva su Workers:* homónima
        // (deuda declarada en diseño D3 — ambos hosts comparten un único appsettings.json vía
        // enlace de proyecto, pero cada uno lee su propia clave).
        var requirePostgreSql = configuration.GetValue("Database:RequirePostgreSqlInProduction", defaultValue: true);
        var isProduction = string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);

        if (requirePostgreSql && isProduction && !isPostgreSql)
        {
            return "Guarda de arranque (coherencia de proveedor): en Production se exige PostgreSQL " +
                   "(Database:RequirePostgreSqlInProduction) y la cadena resuelta es SQLite. Falta el secreto " +
                   "SUPABASE_DB_CONNECTION (variable ConnectionStrings__DefaultConnection).";
        }

        return null;
    }
}
