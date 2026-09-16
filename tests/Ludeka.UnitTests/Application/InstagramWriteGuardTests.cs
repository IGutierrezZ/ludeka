using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Admin;
using Ludeka.Application.Features.Community;
using Ludeka.Application.Features.Instagram;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>Cliente de Meta instrumentado: cuenta contenedores creados y devuelve identificadores estables.</summary>
internal sealed class RecordingInstagramApiClient : IInstagramApiClient
{
    public int ContainerCalls { get; private set; }

    public Task<string> CreateMediaContainerAsync(string imageUrl, string caption, CancellationToken ct = default)
    {
        ContainerCalls++;
        return Task.FromResult("creation_123");
    }

    public Task<string> PublishMediaAsync(string creationId, CancellationToken ct = default)
        => Task.FromResult("media_456");

    public Task<string?> GetPermalinkAsync(string mediaId, CancellationToken ct = default)
        => Task.FromResult<string?>($"https://www.instagram.com/p/{mediaId}/");
}

/// <summary>Publicación en Instagram: borradores y publicación desde el panel de moderación.</summary>
public class InstagramPublisherWriteGuardTests : AdministrativeWriteGuardTestBase
{
    private readonly RecordingInstagramApiClient _apiClient = new();

    private InstagramPublisherService CreateService(ICurrentUserService currentUser, ISessionPermissionGuard? guard)
        => new(
            new SqliteInstagramPostDraftRepository(Context),
            new SqliteGiveawayRepository(Context),
            new SqliteWeeklyReleaseRepository(Context),
            new SqliteGameRepository(Context),
            new InstagramComposerService(new SocialCardService()),
            _apiClient,
            new AuditService(new SqliteAuditLogRepository(Context), currentUser),
            guard);

    private async Task<InstagramPostDraft> SeedDraftAsync()
    {
        var draft = new InstagramPostDraft(
            title: "Borrador de prueba",
            caption: "Texto del borrador.",
            sourceType: InstagramPostSourceType.Giveaway,
            sourceId: Guid.NewGuid().ToString(),
            createdByUserId: ModeratorId,
            createdByUserName: ModeratorName);

        Context.InstagramPostDrafts.Add(draft);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return draft;
    }

    [Fact]
    public async Task PublishDraftAsync_WithoutSession_DeniesAndKeepsTheDraftUnpublished()
    {
        var draft = await SeedDraftAsync();
        var service = CreateService(Anonymous(), CreateGuard(Anonymous()));

        await AssertDenied(() => service.PublishDraftAsync(draft.Id, ModeratorId, ModeratorName));

        Assert.Equal(0, _apiClient.ContainerCalls);
        var stored = await Context.InstagramPostDrafts.AsNoTracking().SingleAsync(d => d.Id == draft.Id);
        Assert.Equal(InstagramPostDraftStatus.Draft, stored.Status);
        Assert.Empty(await Context.AuditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task PublishDraftAsync_WithSuspendedAccount_DeniesEvenWithLiveCookie()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanPublishInstagram));
        await SuspendAsync(ModeratorId);
        var draft = await SeedDraftAsync();
        var service = CreateService(LiveCookie(), CreateGuard(LiveCookie()));

        await AssertDenied(() => service.PublishDraftAsync(draft.Id, ModeratorId, ModeratorName));

        var stored = await Context.InstagramPostDrafts.AsNoTracking().SingleAsync(d => d.Id == draft.Id);
        Assert.Equal(InstagramPostDraftStatus.Draft, stored.Status);
    }

    [Fact]
    public async Task CreateDraftFromGiveawayAsync_WithoutThePermission_DeniesAndPersistsNothing()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanApproveMedia));
        var giveaway = new Giveaway(
            title: "Sorteo sin permiso de Instagram",
            organizer: "Editorial Local",
            url: "https://example.test/sorteo",
            platform: GiveawayPlatform.Instagram,
            deadlineAt: DateTimeOffset.UtcNow.AddDays(5));

        Context.Giveaways.Add(giveaway);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var service = CreateService(LiveCookie(), CreateGuard(LiveCookie()));

        await AssertDenied(() => service.CreateDraftFromGiveawayAsync(giveaway.Id, ModeratorId, ModeratorName));

        Assert.Empty(await Context.InstagramPostDrafts.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task PublishDraftAsync_WithThePermission_PublishesAndAuditsTheSession()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanPublishInstagram));
        var draft = await SeedDraftAsync();
        var service = CreateService(SessionWithPermission(ModeratorPermission.CanPublishInstagram), CreateGuard(LiveCookie()));

        var result = await service.PublishDraftAsync(draft.Id, ModeratorId, ModeratorName);

        Assert.True(result.Success);
        Assert.Equal(1, _apiClient.ContainerCalls);
        var stored = await Context.InstagramPostDrafts.AsNoTracking().SingleAsync(d => d.Id == draft.Id);
        Assert.Equal(InstagramPostDraftStatus.Published, stored.Status);
        var audit = await Context.AuditLogs.AsNoTracking().SingleAsync();
        Assert.Equal(ModeratorId, audit.UserId);
    }
}
