using System;

namespace Ludeka.Infrastructure.Options;

/// <summary>
/// Opciones de configuración para la persistencia y base de datos de Ludeka.
/// </summary>
public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Nombre del proveedor de base de datos ("Sqlite" o "PostgreSql").
    /// Si no se especifica, se autodetecta según la cadena de conexión.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Indica si deben sembrarse datos demostrativos/ficticios (catálogo inicial de prueba, eventos mock).
    /// En entorno de producción debe permanecer en false para garantizar catálogo real.
    /// </summary>
    public bool SeedDemoData { get; set; } = false;

    /// <summary>
    /// Determina si la configuración efectiva corresponde a PostgreSQL (Supabase).
    /// </summary>
    public bool IsPostgreSql(string? connectionString)
    {
        if (string.Equals(Provider, "PostgreSql", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Provider, "Postgres", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Provider, "Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(Provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        // Detección automática por parámetros de conexión
        return connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
               connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
               connectionString.Contains("Port=5432", StringComparison.OrdinalIgnoreCase) ||
               connectionString.Contains("supabase.co", StringComparison.OrdinalIgnoreCase) ||
               connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
               connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);
    }
}
