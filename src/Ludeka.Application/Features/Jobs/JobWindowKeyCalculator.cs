using System;
using System.Globalization;

namespace Ludeka.Application.Features.Jobs;

/// <summary>
/// Fronteras de ventana temporal ancladas al epoch Unix en UTC, estables entre ejecuciones y
/// entre instancias (INC-47, R5, diseño §7.2, decisión D4). Ubicación y forma decididas por
/// <c>sdd-apply</c> (tasks.md 9.6): el diseño da la fórmula pero no fija dónde vive; se centraliza
/// aquí porque la tarea 9.6 exige probar también la granularidad del despachador de outbox
/// (<c>notification-outbox</c>, por segundo), que ningún <c>BackgroundService</c> de esta fase
/// calcula todavía — su único consumidor llega con los <em>runners</em> de <c>Ludeka.Jobs</c>
/// (R6). Cada trabajo llama al método que corresponde a su propia granularidad; el coordinador
/// (<see cref="JobExecutionCoordinator"/>) es agnóstico a cómo se calculó la clave.
/// </summary>
public static class JobWindowKeyCalculator
{
    /// <summary>Ventana diaria del lote nocturno (<c>nightly-cataloging</c>): la fecha de
    /// calendario UTC ya es estable por sí misma entre instancias, sin necesitar anclaje al
    /// epoch (diseño §7.2, tabla).</summary>
    public static string DailyUtc(DateTimeOffset nowUtc) =>
        nowUtc.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Bloque de <paramref name="blockHours"/> horas del radar de precios
    /// (<c>price-radar</c>), anclado al epoch Unix: <c>inicioBloque = epoch +
    /// floor((ahora-epoch)/N) × N</c> (diseño §7.2).</summary>
    public static string HourlyBlock(DateTimeOffset nowUtc, int blockHours)
    {
        var totalHours = (nowUtc - DateTimeOffset.UnixEpoch).TotalHours;
        var blockStartHours = Math.Floor(totalHours / blockHours) * blockHours;
        var blockStart = DateTimeOffset.UnixEpoch.AddHours(blockStartHours);
        return blockStart.ToString("yyyy-MM-ddTHH", CultureInfo.InvariantCulture);
    }

    /// <summary>Bloque de <paramref name="blockMinutes"/> minutos del recolector social
    /// (<c>social-collector</c>), mismo anclaje al epoch Unix que <see cref="HourlyBlock"/>
    /// (diseño §7.2).</summary>
    public static string MinuteBlock(DateTimeOffset nowUtc, int blockMinutes)
    {
        var totalMinutes = (nowUtc - DateTimeOffset.UnixEpoch).TotalMinutes;
        var blockStartMinutes = Math.Floor(totalMinutes / blockMinutes) * blockMinutes;
        var blockStart = DateTimeOffset.UnixEpoch.AddMinutes(blockStartMinutes);
        return blockStart.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>Ventana de un segundo del despachador de outbox (<c>notification-outbox</c>,
    /// diseño §7.2: "no es un trabajo con ventana, es un drenaje" — la granularidad mínima hace
    /// que solo un disparo genuinamente duplicado en el mismo segundo choque contra
    /// <c>UNIQUE</c>). Su único consumidor llega en R6 (<c>Ludeka.Jobs</c>); esta fase solo fija
    /// el cálculo, cubierto por la tabla de casos de la tarea 9.6.</summary>
    public static string PerSecond(DateTimeOffset nowUtc) =>
        nowUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>Semana ISO-8601 en UTC del boletín semanal de novedades
    /// (<c>community-weekly-bulletin</c>): granularidad decidida por <c>sdd-apply</c> (hueco G3
    /// de <c>tasks.md</c>, tarea 9.16 — el diseño no la fija). Usa <see cref="ISOWeek"/> en vez
    /// de <see cref="Calendar.GetWeekOfYear(DateTime, CalendarWeekRule, DayOfWeek)"/> porque el
    /// calendario gregoriano por defecto no es ISO-8601 en las fronteras de año (p. ej. el 1 de
    /// enero puede pertenecer a la semana 52/53 del año anterior).</summary>
    public static string IsoWeek(DateTimeOffset nowUtc)
    {
        var utcDateTime = nowUtc.UtcDateTime;
        var isoYear = ISOWeek.GetYear(utcDateTime);
        var isoWeekNumber = ISOWeek.GetWeekOfYear(utcDateTime);
        return $"{isoYear}-W{isoWeekNumber:D2}";
    }
}
