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
    public const string SeedStaging = "seed-staging";
    public const string DrainStaging = "drain-staging";
    public const string SeedDirectory = "seed-directory";
    public const string BackfillQuality = "backfill-quality";
    public const string BggRawBackfill = "bgg-raw-backfill";
    public const string DataRetention = "data-retention";
    public const string BggReconcileExpansions = "bgg-reconcile-expansions";
    public const string BggVersionsSweep = "bgg-versions-sweep";
    public const string FeedSync = "feed-sync";
    public const string YouTubeAutoIngest = "youtube-auto-ingest";
    public const string BggImagesTop3000 = "bgg-images-top3000";
    public const string EditorialReleasesSync = "editorial-releases-sync";
    public const string DevirImagesBackfill = "devir-images-backfill";

    /// <summary>Los nombres válidos, en el orden en que aparecen en la tabla de configuración.
    /// Comparación sensible a mayúsculas y exacta.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        NightlyCataloging,
        PriceRadar,
        SocialCollector,
        NotificationOutbox,
        SeedStaging,
        DrainStaging,
        SeedDirectory,
        BackfillQuality,
        BggRawBackfill,
        DataRetention,
        BggReconcileExpansions,
        BggVersionsSweep,
        FeedSync,
        YouTubeAutoIngest,
        BggImagesTop3000,
        EditorialReleasesSync,
        DevirImagesBackfill
    ];
}
