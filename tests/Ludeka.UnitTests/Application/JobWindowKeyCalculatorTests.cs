using System;
using System.Globalization;
using Ludeka.Application.Features.Jobs;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// Fronteras de ventana ancladas al epoch Unix en UTC (INC-47, R5, diseño §7.2, tasks.md 9.6).
/// Tabla de casos para los cuatro trabajos: nightly-cataloging (diaria), price-radar (bloque de
/// horas), social-collector (bloque de minutos), notification-outbox (por segundo); más el
/// boletín semanal, cuya granularidad ISO-8601 decide esta fase (hueco G3, tasks.md 9.16). Los
/// valores esperados de <see cref="IsoWeek"/> se verificaron contra el calendario real antes de
/// escribir la aserción (2026-09-18 es viernes de la semana ISO 38; 2026-12-31 cae en la
/// semana ISO 53 de 2026, sin desbordar a 2027).
/// </summary>
public class JobWindowKeyCalculatorTests
{
    [Fact]
    public void DailyUtc_ConUnaFechaConcreta_DevuelveLaClaveDeCalendarioUtc()
    {
        var nowUtc = new DateTimeOffset(2026, 9, 18, 23, 59, 0, TimeSpan.Zero);

        Assert.Equal("2026-09-18", JobWindowKeyCalculator.DailyUtc(nowUtc));
    }

    [Theory]
    [InlineData("2026-09-18T11:59:00Z", 6, "2026-09-18T06")]
    [InlineData("2026-09-18T12:00:00Z", 6, "2026-09-18T12")]
    [InlineData("2026-09-18T17:59:00Z", 6, "2026-09-18T12")]
    public void HourlyBlock_AnclaAlEpochUnix_DevuelveElInicioDelBloqueEstableEntreInstancias(
        string nowUtcText, int blockHours, string expected)
    {
        var nowUtc = DateTimeOffset.Parse(nowUtcText, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

        Assert.Equal(expected, JobWindowKeyCalculator.HourlyBlock(nowUtc, blockHours));
    }

    [Theory]
    [InlineData("2026-09-18T14:01:00Z", 120, "2026-09-18T14:00")]
    [InlineData("2026-09-18T15:59:00Z", 120, "2026-09-18T14:00")]
    [InlineData("2026-09-18T16:00:00Z", 120, "2026-09-18T16:00")]
    public void MinuteBlock_AnclaAlEpochUnix_DevuelveElInicioDelBloqueEstableEntreInstancias(
        string nowUtcText, int blockMinutes, string expected)
    {
        var nowUtc = DateTimeOffset.Parse(nowUtcText, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

        Assert.Equal(expected, JobWindowKeyCalculator.MinuteBlock(nowUtc, blockMinutes));
    }

    [Fact]
    public void PerSecond_ConUnInstanteConcreto_DevuelveLaClaveConGranularidadDeUnSegundo()
    {
        var nowUtc = new DateTimeOffset(2026, 9, 18, 14, 3, 7, TimeSpan.Zero);

        Assert.Equal("2026-09-18T14:03:07", JobWindowKeyCalculator.PerSecond(nowUtc));
    }

    [Theory]
    [InlineData("2026-09-18T00:00:00Z", "2026-W38")]
    [InlineData("2026-12-31T00:00:00Z", "2026-W53")]
    public void IsoWeek_ConFechasEnFronteraDeAno_DevuelveLaSemanaIso8601(string nowUtcText, string expected)
    {
        var nowUtc = DateTimeOffset.Parse(nowUtcText, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

        Assert.Equal(expected, JobWindowKeyCalculator.IsoWeek(nowUtc));
    }
}
