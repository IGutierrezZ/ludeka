using System.Collections.Generic;

namespace Ludeka.Jobs;

/// <summary>
/// Nombres de los cuatro trabajos de fondo externalizados (INC-47, R6, diseño §8.1/§8.4): única
/// fuente de verdad, consumida tanto por la selección de trabajo de <c>Program.cs</c> como por el
/// registro de <see cref="IJobRunner"/> en <c>JobRunnerServiceCollectionExtensions</c>.
/// </summary>
public static class JobNames
{
    public const string NightlyCataloging = "nightly-cataloging";
    public const string PriceRadar = "price-radar";
    public const string SocialCollector = "social-collector";
    public const string NotificationOutbox = "notification-outbox";

    /// <summary>Los cuatro nombres válidos, en el orden en que aparecen en la tabla del diseño
    /// §7.2. Comparación sensible a mayúsculas y exacta (diseño §8.4): aceptar variantes
    /// ortográficas del nombre que decide qué se ejecuta en producción no compensa el riesgo.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        NightlyCataloging,
        PriceRadar,
        SocialCollector,
        NotificationOutbox
    ];
}
