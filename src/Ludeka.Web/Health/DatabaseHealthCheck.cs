using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ludeka.Web.Health;

/// <summary>
/// Salud observable del proveedor de base de datos efectivamente configurado (INC-48, PR3,
/// diseño D4). Sustituye a <c>SqliteDatabaseHealthCheck</c>, que fijaba el metadato
/// <c>provider</c> al literal <c>"Microsoft.EntityFrameworkCore.Sqlite"</c> con independencia
/// del proveedor real. El metadato se lee ahora de <c>_dbContext.Database.ProviderName</c>, así
/// que en Production con PostgreSQL reporta el proveedor Npgsql real, nunca Sqlite.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly LudekaDbContext _dbContext;

    public DatabaseHealthCheck(LudekaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // INC-48 (PR3), diseño D4: proveedor real, no el literal fijo que reportaba
            // SqliteDatabaseHealthCheck con independencia de la base de datos configurada.
            // Se lee ANTES de intentar conectar y se reporta también cuando la conexión falla:
            // saber contra qué proveedor se intentó conectar es justo lo que hace falta cuando la
            // base no responde, y es lo que permite verificar este requisito sin un PostgreSQL
            // real, como exige la especificación (ProviderName no abre ninguna conexión).
            var provider = _dbContext.Database.ProviderName ?? "desconocido";

            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy(
                    "No se puede establecer conexión con la base de datos.",
                    data: new Dictionary<string, object> { { "can_connect", false }, { "provider", provider } });
            }

            // Comprobación de consulta básica
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1;", cancellationToken);

            var gameCount = await _dbContext.Games.CountAsync(cancellationToken);
            var data = new Dictionary<string, object>
            {
                { "can_connect", true },
                { "game_count", gameCount },
                { "provider", provider }
            };

            return HealthCheckResult.Healthy("Base de datos operativa y respondiendo.", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Fallo al verificar el estado de la base de datos.",
                ex,
                TryReadProviderData());
        }
    }

    /// <summary>
    /// Metadato de proveedor para la rama de excepción. Se lee dentro de su propio
    /// <c>try</c> porque en esa rama el contexto puede estar ya inutilizable —desechado, por
    /// ejemplo—, y leerlo sin protección sustituiría el fallo real por otro distinto. Si no se
    /// puede leer, se devuelve <see langword="null"/>: el resultado se queda sin metadato, pero
    /// conserva la excepción original, que es la información que de verdad importa ahí.
    /// </summary>
    private IReadOnlyDictionary<string, object>? TryReadProviderData()
    {
        try
        {
            return new Dictionary<string, object>
            {
                { "can_connect", false },
                { "provider", _dbContext.Database.ProviderName ?? "desconocido" }
            };
        }
        catch
        {
            return null;
        }
    }
}
