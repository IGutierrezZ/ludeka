using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Community;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class SocialCollectorServiceTests
{
    private class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public T CurrentValue { get; set; }
        public TestOptionsMonitor(T value) => CurrentValue = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private class TestAccountRepository : IMonitoredAccountRepository
    {
        public List<MonitoredSocialAccount> Accounts = new();

        public Task<MonitoredSocialAccount> AddAsync(MonitoredSocialAccount account, CancellationToken ct = default)
        {
            Accounts.Add(account);
            return Task.FromResult(account);
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Accounts.RemoveAll(a => a.Id == id);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(SocialPlatform platform, string handleOrChannelId, CancellationToken ct = default)
        {
            return Task.FromResult(Accounts.Any(a => a.Platform == platform && a.HandleOrChannelId == handleOrChannelId));
        }

        public Task<IReadOnlyList<MonitoredSocialAccount>> GetAllAsync(
            SocialPlatform? platform = null,
            MonitoredAccountType? type = null,
            bool? onlyEnabled = null,
            CancellationToken ct = default)
        {
            var q = Accounts.AsEnumerable();
            if (platform.HasValue) q = q.Where(a => a.Platform == platform.Value);
            if (type.HasValue) q = q.Where(a => a.AccountType == type.Value);
            if (onlyEnabled.HasValue && onlyEnabled.Value) q = q.Where(a => a.IsEnabled);
            return Task.FromResult<IReadOnlyList<MonitoredSocialAccount>>(q.ToList());
        }

        public Task<MonitoredSocialAccount?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(Accounts.FirstOrDefault(a => a.Id == id));
        }

        public Task UpdateAsync(MonitoredSocialAccount account, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }

    private class TestInboxRepository : ISocialInboxRepository
    {
        public HashSet<string> ExistingUrls = new();
        public List<SocialInboxItem> SavedItems = new();

        public Task<SocialInboxItem> AddAsync(SocialInboxItem item, CancellationToken ct = default)
        {
            SavedItems.Add(item);
            ExistingUrls.Add(item.SourceUrl);
            return Task.FromResult(item);
        }

        public Task<bool> ExistsBySourceUrlAsync(string sourceUrl, CancellationToken ct = default)
        {
            return Task.FromResult(ExistingUrls.Contains(sourceUrl));
        }

        public Task<IReadOnlyList<SocialInboxItem>> GetAllAsync(SocialInboxStatus? statusFilter = null, SocialSubmissionType? typeFilter = null, int page = 1, int pageSize = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SocialInboxItem>>([]);

        public Task<SocialInboxItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<SocialInboxItem?>(null);

        public Task<IReadOnlyList<SocialInboxItem>> GetPendingAsync(SocialSubmissionType? typeFilter = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SocialInboxItem>>([]);

        public Task<int> GetPendingCountAsync(CancellationToken ct = default)
            => Task.FromResult(0);

        public Task UpdateAsync(SocialInboxItem item, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private class TestIngestionService : ISocialIngestionService
    {
        public List<string> IngestedUrls = new();

        public Task<SocialInboxItemDto> IngestFromUrlAsync(string url, string? manualCaption = null, CancellationToken ct = default)
        {
            IngestedUrls.Add(url);
            var item = new SocialInboxItem(
                sourceUrl: url,
                platform: SocialPlatform.YouTube,
                detectedType: SocialSubmissionType.MediaItem,
                title: "Test Ingested",
                organizerOrAuthor: "Test Author");
            return Task.FromResult(SocialInboxItemDto.FromEntity(item));
        }

        public Task<SocialInboxItemDto> IngestManualAdvancedAsync(SocialInboxManualInputDto input, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<SocialInboxItemDto> UpdateItemAsync(SocialInboxUpdateDto dto, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Guid> ApproveAndPublishAsync(Guid inboxItemId, string reviewerUserId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task RejectItemAsync(Guid inboxItemId, string reason, string reviewerUserId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SocialInboxItemDto>> GetPendingItemsAsync(SocialSubmissionType? typeFilter = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SocialInboxItemDto>> GetAllItemsAsync(SocialInboxStatus? statusFilter = null, SocialSubmissionType? typeFilter = null, int page = 1, int pageSize = 50, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<SocialInboxItemDto?> GetItemByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> GetPendingCountAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class TestChannelCollector : ISocialChannelCollector
    {
        public SocialPlatform SupportedPlatform { get; set; } = SocialPlatform.YouTube;
        public List<DiscoveredSocialPostDto> PostsToReturn = new();

        public bool CanHandle(SocialPlatform platform) => platform == SupportedPlatform;

        public Task<IReadOnlyList<DiscoveredSocialPostDto>> CollectRecentPostsAsync(MonitoredSocialAccount account, int maxItems = 5, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<DiscoveredSocialPostDto>>(PostsToReturn);
        }
    }

    [Fact]
    public async Task CollectAllAccountsAsync_ScansEnabledAccounts_DeduplicatesAndIngests()
    {
        var accountRepo = new TestAccountRepository();
        var inboxRepo = new TestInboxRepository();
        var ingestionService = new TestIngestionService();
        var collector = new TestChannelCollector();
        var optionsMonitor = new TestOptionsMonitor<SocialCollectorOptions>(new SocialCollectorOptions());

        var enabledAccount = new MonitoredSocialAccount("Canal Activo", SocialPlatform.YouTube, "UC1234", MonitoredAccountType.Creator, "https://youtube.com/canal", isEnabled: true);
        var disabledAccount = new MonitoredSocialAccount("Canal Pausado", SocialPlatform.YouTube, "UC5678", MonitoredAccountType.Creator, "https://youtube.com/pausado", isEnabled: false);
        accountRepo.Accounts.Add(enabledAccount);
        accountRepo.Accounts.Add(disabledAccount);

        var existingUrl = "https://www.youtube.com/watch?v=already_imported";
        var newUrl = "https://www.youtube.com/watch?v=fresh_video";

        inboxRepo.ExistingUrls.Add(existingUrl);

        collector.PostsToReturn =
        [
            new DiscoveredSocialPostDto(existingUrl, "Vídeo Ya Existente", "Canal Activo", "", null, DateTimeOffset.UtcNow, true, SocialPlatform.YouTube),
            new DiscoveredSocialPostDto(newUrl, "Vídeo Nuevo", "Canal Activo", "Descripción", null, DateTimeOffset.UtcNow, true, SocialPlatform.YouTube)
        ];

        var service = new SocialCollectorService(
            accountRepo,
            inboxRepo,
            ingestionService,
            [collector],
            optionsMonitor,
            NullLogger<SocialCollectorService>.Instance);

        var result = await service.CollectAllAccountsAsync();

        Assert.Equal(1, result.AccountsScanned);
        Assert.Equal(2, result.ItemsDiscovered);
        Assert.Equal(1, result.ItemsSkippedDuplicates);
        Assert.Equal(1, result.ItemsImported);
        Assert.Equal(0, result.ErrorsCount);

        Assert.Single(ingestionService.IngestedUrls);
        Assert.Equal(newUrl, ingestionService.IngestedUrls[0]);
        Assert.NotNull(enabledAccount.LastCheckedAt);
    }

    [Fact]
    public async Task CollectAccountAsync_SingleAccount_UpdatesLastCheckedAt()
    {
        var accountRepo = new TestAccountRepository();
        var inboxRepo = new TestInboxRepository();
        var ingestionService = new TestIngestionService();
        var collector = new TestChannelCollector { SupportedPlatform = SocialPlatform.Telegram };
        var optionsMonitor = new TestOptionsMonitor<SocialCollectorOptions>(new SocialCollectorOptions());

        var account = new MonitoredSocialAccount("Devir Telegram", SocialPlatform.Telegram, "deviriberia", MonitoredAccountType.Publisher, "https://t.me/deviriberia");
        accountRepo.Accounts.Add(account);

        collector.PostsToReturn =
        [
            new DiscoveredSocialPostDto("https://t.me/deviriberia/999", "Preventa", "Devir", "Texto", null, DateTimeOffset.UtcNow, false, SocialPlatform.Telegram)
        ];

        var service = new SocialCollectorService(
            accountRepo,
            inboxRepo,
            ingestionService,
            [collector],
            optionsMonitor,
            NullLogger<SocialCollectorService>.Instance);

        var result = await service.CollectAccountAsync(account.Id);

        Assert.Equal(1, result.AccountsScanned);
        Assert.Equal(1, result.ItemsImported);
        Assert.NotNull(account.LastCheckedAt);
    }
}
