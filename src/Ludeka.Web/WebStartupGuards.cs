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

    /// <summary>Evalúa la guarda de identidad del Administrador Fundador (INC-52, PR5, diseño D8) y
    /// devuelve el mensaje de fallo si no pasa, o <see langword="null"/> si pasa. Función pura,
    /// hermana de <see cref="Evaluate"/> y evaluada de forma independiente de ella.</summary>
    /// <remarks>
    /// Lee <c>configuration["AdminUser:Email"]</c> DIRECTAMENTE, nunca a través de
    /// <see cref="AdminUserOptions"/> enlazado. <see cref="AdminUserOptions.Email"/> tiene un valor
    /// por defecto en C# (<c>"admin@ludeka.es"</c>): si esta guarda leyera de las opciones enlazadas,
    /// borrar la clave del <c>appsettings.json</c> en vez de vaciarla resucitaría ese valor por
    /// defecto y la guarda fallaría EN ABIERTO — exactamente el defecto que existe para cerrar.
    /// Leyendo la configuración directamente, tanto la clave ausente como el valor vacío se
    /// traducen en «no informado» y la guarda falla en cerrado.
    /// </remarks>
    public static string? EvaluateAdminUserIdentity(IConfiguration configuration, string? environmentName)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var isProduction = string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
        var adminUserEmail = configuration["AdminUser:Email"];

        if (isProduction && string.IsNullOrWhiteSpace(adminUserEmail))
        {
            return "Guarda de arranque (identidad del Administrador Fundador): en Production se exige " +
                   "un valor explícito y no vacío para AdminUser:Email (variable de entorno " +
                   "AdminUser__Email, secreto ADMIN_USER_EMAIL) y no está informado. El sembrado del " +
                   "Administrador Fundador ocurre una sola vez, contra base vacía, y es IRREVERSIBLE " +
                   "por la vía de la aplicación: si se elude esta guarda con un valor cualquiera o " +
                   "equivocado, ese correo queda fijado para siempre y el maintainer real pierde el " +
                   "acceso a su propio panel de administración, sin otra vía de recuperación que " +
                   "editar la fila a mano en la base de datos. Corrige AdminUser__Email con el correo " +
                   "real y verificable del proveedor social elegido antes de volver a desplegar.";
        }

        return null;
    }
}
