using Ludeka.Jobs.Runners;
using Microsoft.Extensions.DependencyInjection;

namespace Ludeka.Jobs;

/// <summary>
/// Registro de los cuatro <see cref="IJobRunner"/> (INC-47, R6, diseño §8.1/§8.2).
/// <c>AddScoped</c>: cada uno depende de servicios con ámbito (el coordinador, los servicios de
/// dominio), y la selección por nombre se resuelve dentro del ámbito único por intento que
/// <c>Program.cs</c> crea (mismo patrón que <c>IEnumerable&lt;ISocialChannelCollector&gt;</c>,
/// diseño §8.2).
/// </summary>
public static class JobRunnerServiceCollectionExtensions
{
    public static IServiceCollection AddLudekaJobRunners(this IServiceCollection services)
    {
        services.AddScoped<IJobRunner, NightlyCatalogingJobRunner>();
        services.AddScoped<IJobRunner, PriceRadarJobRunner>();
        services.AddScoped<IJobRunner, SocialCollectorJobRunner>();
        services.AddScoped<IJobRunner, NotificationOutboxJobRunner>();
        services.AddScoped<IJobRunner, SeedStagingJobRunner>();
        services.AddScoped<IJobRunner, DrainStagingJobRunner>();
        services.AddScoped<IJobRunner, SeedDirectoryJobRunner>();
        services.AddScoped<IJobRunner, BackfillQualityJobRunner>();
        services.AddScoped<IJobRunner, BggRawBackfillJobRunner>();
        services.AddScoped<IJobRunner, DataRetentionJobRunner>();
        services.AddScoped<IJobRunner, BggReconcileExpansionsJobRunner>();
        services.AddScoped<IJobRunner, BggVersionsSweepJobRunner>();
        services.AddScoped<IJobRunner, CatalogFeedSyncJobRunner>();
        services.AddScoped<IJobRunner, YouTubeAutoIngestJobRunner>();
        services.AddScoped<IJobRunner, BggImagesTop3000JobRunner>();
        services.AddScoped<IJobRunner, EditorialReleasesSyncJobRunner>();
        services.AddScoped<IJobRunner, DevirImagesBackfillJobRunner>();
        return services;
    }
}
