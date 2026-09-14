using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Community;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class MonitoredAccountServiceTests
{
    private class FakeMonitoredAccountRepository : IMonitoredAccountRepository
    {
        public List<MonitoredSocialAccount> Items { get; } = new();

        public Task<IReadOnlyList<MonitoredSocialAccount>> GetAllAsync(SocialPlatform? platform = null, MonitoredAccountType? type = null, bool? onlyEnabled = null, CancellationToken ct = default)
        {
            var query = Items.AsEnumerable();
            if (platform.HasValue) query = query.Where(a => a.Platform == platform.Value);
            if (type.HasValue) query = query.Where(a => a.AccountType == type.Value);
            if (onlyEnabled.HasValue) query = query.Where(a => a.IsEnabled == onlyEnabled.Value);
            return Task.FromResult<IReadOnlyList<MonitoredSocialAccount>>(query.ToList());
        }

        public Task<MonitoredSocialAccount?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(Items.Find(a => a.Id == id));
        }

        public Task<MonitoredSocialAccount> AddAsync(MonitoredSocialAccount account, CancellationToken ct = default)
        {
            Items.Add(account);
            return Task.FromResult(account);
        }

        public Task UpdateAsync(MonitoredSocialAccount account, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Items.RemoveAll(a => a.Id == id);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(SocialPlatform platform, string handleOrChannelId, CancellationToken ct = default)
        {
            return Task.FromResult(Items.Any(a => a.Platform == platform && a.HandleOrChannelId.Equals(handleOrChannelId, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private class FakePublisherRepository : IPublisherRepository
    {
        public List<Publisher> Items { get; } = new();
        public Task<IReadOnlyList<Publisher>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Publisher>>(Items);
        public Task<Publisher?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.Find(p => p.Id == id));
        public Task<Publisher?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Items.Find(p => p.Slug == slug));
        public Task<Publisher?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(Items.Find(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        public Task AddAsync(Publisher publisher, CancellationToken ct = default) { Items.Add(publisher); return Task.CompletedTask; }
        public Task UpdateAsync(Publisher publisher, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(p => p.Id == id); return Task.CompletedTask; }
    }

    private class FakeCreatorRepository : ICreatorRepository
    {
        public List<Creator> Items { get; } = new();
        public Task<IReadOnlyList<Creator>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Creator>>(Items);
        public Task<Creator?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.Find(c => c.Id == id));
        public Task<Creator?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Items.Find(c => c.Slug == slug));
        public Task<Creator?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(Items.Find(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        public Task AddAsync(Creator creator, CancellationToken ct = default) { Items.Add(creator); return Task.CompletedTask; }
        public Task UpdateAsync(Creator creator, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(c => c.Id == id); return Task.CompletedTask; }
    }

    private class FakeStoreRepository : IStoreRepository
    {
        public List<Store> Items { get; } = new();
        public Task<IReadOnlyList<Store>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Store>>(Items);
        public Task<Store?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.Find(s => s.Id == id));
        public Task<Store?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Items.Find(s => s.Slug == slug));
        public Task<Store?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(Items.Find(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        public Task AddAsync(Store store, CancellationToken ct = default) { Items.Add(store); return Task.CompletedTask; }
        public Task UpdateAsync(Store store, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(s => s.Id == id); return Task.CompletedTask; }
    }

    private readonly FakeMonitoredAccountRepository _accountRepo = new();
    private readonly FakePublisherRepository _pubRepo = new();
    private readonly FakeCreatorRepository _creatorRepo = new();
    private readonly FakeStoreRepository _storeRepo = new();

    private MonitoredAccountService CreateService()
    {
        return new MonitoredAccountService(
            _accountRepo,
            _pubRepo,
            _creatorRepo,
            _storeRepo,
            NullLogger<MonitoredAccountService>.Instance);
    }

    [Fact]
    public async Task CreateAccountAsync_ValidAccount_CreatesAndReturnsDto()
    {
        // Arrange
        var service = CreateService();
        var dto = new MonitoredAccountDto(
            Id: Guid.NewGuid(),
            Name: "Maldito Games",
            Platform: SocialPlatform.Instagram,
            HandleOrChannelId: "@malditogames",
            AccountType: MonitoredAccountType.Publisher,
            ProfileUrl: "https://instagram.com/malditogames",
            IsEnabled: true,
            LastCheckedAt: null,
            Notes: "Editorial española",
            CreatedAt: DateTimeOffset.UtcNow);

        // Act
        var created = await service.CreateAccountAsync(dto);

        // Assert
        Assert.Equal("Maldito Games", created.Name);
        Assert.Equal("@malditogames", created.HandleOrChannelId);
        Assert.Single(_accountRepo.Items);
    }

    [Fact]
    public async Task CreateAccountAsync_DuplicateAccount_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        var account = new MonitoredSocialAccount(
            name: "Devir",
            platform: SocialPlatform.Instagram,
            handleOrChannelId: "@deviriberia",
            accountType: MonitoredAccountType.Publisher,
            profileUrl: "https://instagram.com/deviriberia");
        _accountRepo.Items.Add(account);

        var duplicateDto = new MonitoredAccountDto(
            Id: Guid.NewGuid(),
            Name: "Devir Otro",
            Platform: SocialPlatform.Instagram,
            HandleOrChannelId: "@deviriberia",
            AccountType: MonitoredAccountType.Publisher,
            ProfileUrl: "https://instagram.com/deviriberia",
            IsEnabled: true,
            LastCheckedAt: null,
            Notes: null,
            CreatedAt: DateTimeOffset.UtcNow);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAccountAsync(duplicateDto));
    }

    [Fact]
    public async Task ToggleAccountStatusAsync_ChangesStatus()
    {
        // Arrange
        var service = CreateService();
        var account = new MonitoredSocialAccount(
            name: "Canal A",
            platform: SocialPlatform.YouTube,
            handleOrChannelId: "UC123",
            accountType: MonitoredAccountType.Creator,
            profileUrl: "https://youtube.com/canalA",
            isEnabled: true);
        _accountRepo.Items.Add(account);

        // Act
        await service.ToggleAccountStatusAsync(account.Id, false);

        // Assert
        Assert.False(account.IsEnabled);
    }

    [Fact]
    public async Task SyncFromDirectoryAsync_WhenPublishersAndCreatorsExist_ImportsAccounts()
    {
        // Arrange
        var service = CreateService();

        var pub = new Publisher(
            name: "Asmodee España",
            slug: "asmodee-es",
            country: "España",
            socialLinks: new[]
            {
                new SocialNetworkLink(SocialPlatform.Instagram, "https://instagram.com/asmodee_es", "@asmodee_es")
            });
        _pubRepo.Items.Add(pub);

        var creator = new Creator(
            name: "Análisis Parálisis",
            slug: "analisis-paralisis",
            socialLinks: new[]
            {
                new SocialNetworkLink(SocialPlatform.YouTube, "https://youtube.com/@analisisparalisis", "@analisisparalisis")
            });
        _creatorRepo.Items.Add(creator);

        // Act
        var count = await service.SyncFromDirectoryAsync();

        // Assert
        Assert.Equal(2, count);
        Assert.Equal(2, _accountRepo.Items.Count);
        Assert.Contains(_accountRepo.Items, a => a.Name == "Asmodee España" && a.Platform == SocialPlatform.Instagram);
        Assert.Contains(_accountRepo.Items, a => a.Name == "Análisis Parálisis" && a.Platform == SocialPlatform.YouTube);
    }
}
