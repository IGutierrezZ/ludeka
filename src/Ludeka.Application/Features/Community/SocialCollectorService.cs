using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Application.Features.Community;

public class SocialCollectorService : ISocialCollectorService
{
    private readonly IMonitoredAccountRepository _accountRepository;
    private readonly ISocialInboxRepository _inboxRepository;
    private readonly ISocialIngestionService _ingestionService;
    private readonly IEnumerable<ISocialChannelCollector> _collectors;
    private readonly IOptionsMonitor<SocialCollectorOptions> _optionsMonitor;
    private readonly ILogger<SocialCollectorService> _logger;

    public SocialCollectorService(
        IMonitoredAccountRepository accountRepository,
        ISocialInboxRepository inboxRepository,
        ISocialIngestionService ingestionService,
        IEnumerable<ISocialChannelCollector> collectors,
        IOptionsMonitor<SocialCollectorOptions> optionsMonitor,
        ILogger<SocialCollectorService> logger)
    {
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _inboxRepository = inboxRepository ?? throw new ArgumentNullException(nameof(inboxRepository));
        _ingestionService = ingestionService ?? throw new ArgumentNullException(nameof(ingestionService));
        _collectors = collectors ?? throw new ArgumentNullException(nameof(collectors));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SocialCollectorRunResultDto> CollectAllAccountsAsync(int maxItemsPerAccount = 5, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var options = _optionsMonitor.CurrentValue;
        var limit = maxItemsPerAccount > 0 ? maxItemsPerAccount : options.MaxItemsPerAccount;

        _logger.LogInformation("Iniciando escaneo masivo de canales sociales monitorizados (Límite por canal: {Limit}).", limit);

        var enabledAccounts = await _accountRepository.GetAllAsync(onlyEnabled: true, ct: ct);
        var summaries = new List<SocialCollectorAccountSummaryDto>();

        var totalDiscovered = 0;
        var totalImported = 0;
        var totalSkipped = 0;
        var totalErrors = 0;

        foreach (var account in enabledAccounts)
        {
            if (ct.IsCancellationRequested)
                break;

            var summary = await ProcessAccountAsync(account, limit, options, ct);
            summaries.Add(summary);

            totalDiscovered += summary.DiscoveredCount;
            totalImported += summary.ImportedCount;
            totalSkipped += summary.SkippedCount;
            if (summary.HasError)
            {
                totalErrors++;
            }
        }

        sw.Stop();

        var result = new SocialCollectorRunResultDto
        {
            AccountsScanned = enabledAccounts.Count,
            ItemsDiscovered = totalDiscovered,
            ItemsImported = totalImported,
            ItemsSkippedDuplicates = totalSkipped,
            ErrorsCount = totalErrors,
            Duration = sw.Elapsed,
            ExecutedAt = DateTimeOffset.UtcNow,
            AccountSummaries = summaries
        };

        _logger.LogInformation(
            "Ciclo de recolección social finalizado: {Scanned} cuentas, {Discovered} descubiertos, {Imported} importados, {Skipped} duplicados en {Duration:F1}s.",
            result.AccountsScanned,
            result.ItemsDiscovered,
            result.ItemsImported,
            result.ItemsSkippedDuplicates,
            result.Duration.TotalSeconds);

        return result;
    }

    public async Task<SocialCollectorRunResultDto> CollectAccountAsync(Guid accountId, int maxItems = 5, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var options = _optionsMonitor.CurrentValue;
        var limit = maxItems > 0 ? maxItems : options.MaxItemsPerAccount;

        var account = await _accountRepository.GetByIdAsync(accountId, ct)
            ?? throw new ArgumentException($"No se encontró la cuenta monitorizada con ID '{accountId}'.", nameof(accountId));

        _logger.LogInformation("Iniciando sondeo bajo demanda para cuenta '{Name}' ({Platform}).", account.Name, account.Platform);

        var summary = await ProcessAccountAsync(account, limit, options, ct);
        sw.Stop();

        return new SocialCollectorRunResultDto
        {
            AccountsScanned = 1,
            ItemsDiscovered = summary.DiscoveredCount,
            ItemsImported = summary.ImportedCount,
            ItemsSkippedDuplicates = summary.SkippedCount,
            ErrorsCount = summary.HasError ? 1 : 0,
            Duration = sw.Elapsed,
            ExecutedAt = DateTimeOffset.UtcNow,
            AccountSummaries = [summary]
        };
    }

    private async Task<SocialCollectorAccountSummaryDto> ProcessAccountAsync(
        MonitoredSocialAccount account,
        int maxItems,
        SocialCollectorOptions options,
        CancellationToken ct)
    {
        if (!IsPlatformEnabled(account.Platform, options))
        {
            _logger.LogDebug("Plataforma {Platform} desactivada en configuración. Saltando cuenta '{Name}'.", account.Platform, account.Name);
            return new SocialCollectorAccountSummaryDto
            {
                AccountId = account.Id,
                AccountName = account.Name,
                Platform = account.Platform,
                DiscoveredCount = 0,
                ImportedCount = 0,
                SkippedCount = 0,
                ErrorMessage = "Plataforma deshabilitada en configuración."
            };
        }

        var collector = _collectors.FirstOrDefault(c => c.CanHandle(account.Platform));
        if (collector == null)
        {
            _logger.LogWarning("No hay recolector registrado para la plataforma {Platform} (Cuenta: '{Name}').", account.Platform, account.Name);
            return new SocialCollectorAccountSummaryDto
            {
                AccountId = account.Id,
                AccountName = account.Name,
                Platform = account.Platform,
                DiscoveredCount = 0,
                ImportedCount = 0,
                SkippedCount = 0,
                ErrorMessage = $"No existe recolector registrado para {account.Platform}."
            };
        }

        var discoveredCount = 0;
        var importedCount = 0;
        var skippedCount = 0;
        string? errorMessage = null;

        try
        {
            var posts = await collector.CollectRecentPostsAsync(account, maxItems, ct);
            discoveredCount = posts.Count;

            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-Math.Abs(options.MaxPostAgeDays));

            foreach (var post in posts)
            {
                if (ct.IsCancellationRequested)
                    break;

                if (string.IsNullOrWhiteSpace(post.SourceUrl))
                    continue;

                // Filtrar por antigüedad si la publicación es excesivamente vieja
                if (post.PublishedAt < cutoffDate)
                {
                    skippedCount++;
                    continue;
                }

                // Verificar duplicados contra la bandeja de moderación
                var exists = await _inboxRepository.ExistsBySourceUrlAsync(post.SourceUrl, ct);
                if (exists)
                {
                    skippedCount++;
                    continue;
                }

                // Ingestar en la bandeja de moderación como borrador PendingReview
                try
                {
                    await _ingestionService.IngestFromUrlAsync(post.SourceUrl, post.Description, ct);
                    importedCount++;
                    _logger.LogInformation("Publicación autodescubierta e importada: {Url} ({Title})", post.SourceUrl, post.Title);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Error al ingestar publicación autodescubierta '{Url}': {Message}", post.SourceUrl, ex.Message);
                }
            }

            account.MarkChecked();
            await _accountRepository.UpdateAsync(account, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Error al recolectar cuenta '{Name}' ({Platform}): {Message}", account.Name, account.Platform, ex.Message);
            errorMessage = ex.Message;
        }

        return new SocialCollectorAccountSummaryDto
        {
            AccountId = account.Id,
            AccountName = account.Name,
            Platform = account.Platform,
            DiscoveredCount = discoveredCount,
            ImportedCount = importedCount,
            SkippedCount = skippedCount,
            ErrorMessage = errorMessage
        };
    }

    private static bool IsPlatformEnabled(SocialPlatform platform, SocialCollectorOptions options) =>
        platform switch
        {
            SocialPlatform.YouTube => options.YouTubeEnabled,
            SocialPlatform.Telegram => options.TelegramEnabled,
            SocialPlatform.RssFeed => options.RssBlogEnabled,
            SocialPlatform.Website => options.RssBlogEnabled,
            SocialPlatform.Instagram => options.InstagramEnabled,
            _ => true
        };
}
