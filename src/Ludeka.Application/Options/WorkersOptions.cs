namespace Ludeka.Application.Options;

/// <summary>
/// Opciones de configuración de los cuatro trabajos externalizados (INC-47, R7, diseño §7.5).
/// El registro en el contenedor de dependencias vive en R7 (tarea 11.4): antes de esta fase,
/// <see cref="StaleLeaseMinutes"/> vivía como constante en
/// <c>Ludeka.Infrastructure.Repositories.JobExecutionLeaseRepository</c> (deuda declarada en
/// tasks.md 9b.3, cerrada aquí).
/// </summary>
public class WorkersOptions
{
    public const string SectionName = "Workers";

    /// <summary>Parada de emergencia del lado del código: con <c>false</c>, todos los
    /// <c>IJobRunner</c> de <c>Ludeka.Jobs</c> salen con código 0 sin reclamar ninguna ventana
    /// (diseño §7.5), complementaria al primer paso del plan de reversión (pausar los cuatro
    /// Cloud Scheduler). No sustituye el interruptor <c>Enabled</c> propio de cada trabajo
    /// (<c>NightlyCataloging:Enabled</c>, <c>PriceRadar:Enabled</c>,
    /// <c>SocialCollector:Enabled</c>, <c>CommunityNotifications:Enabled</c>), que conserva su
    /// semántica intacta.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Tercera prioridad de selección del trabajo a ejecutar, tras el argumento
    /// posicional y <c>--job=&lt;nombre&gt;</c> (diseño §8.4); honra la variable de entorno
    /// <c>Workers__JobName</c>.</summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>Presupuesto máximo de una ejecución antes de cancelarla (diseño §8.3/§8.5).</summary>
    public int JobTimeoutMinutes { get; set; } = 30;

    /// <summary>Umbral de latido caducado para tomar el control de una concesión huérfana
    /// (<c>Running</c> con <c>HeartbeatAt</c> anterior a <c>ahora − StaleLeaseMinutes</c>, diseño
    /// §7.3).</summary>
    public int StaleLeaseMinutes { get; set; } = 60;

    /// <summary>Guarda de arranque de <c>Ludeka.Jobs</c> (diseño §8.6): en producción, exige una
    /// cadena de conexión PostgreSQL y falla nombrando <c>SUPABASE_DB_CONNECTION</c> si detecta
    /// SQLite.</summary>
    public bool RequirePostgreSqlInProduction { get; set; } = true;
}
