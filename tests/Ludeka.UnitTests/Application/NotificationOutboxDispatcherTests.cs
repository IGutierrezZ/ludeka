using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Community;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// Despachador real del outbox (INC-47, R4b, diseño §6.5, tasks.md 7.1-7.5). Especificación
/// <c>notification-outbox</c>: "Reintento con contador de intentos y estado terminal al
/// agotarse" (3 escenarios) y "Supervivencia de notificaciones pendientes ante apagado de la
/// instancia". <see cref="ICommunityNotificationService"/> se sustituye aquí por un doble de
/// prueba (<see cref="FakeChannelDeliveryService"/>) que aplica los mismos métodos de dominio
/// que la implementación real de <c>DeliverAsync</c> (probada por separado en
/// <c>CommunityNotificationServiceTests</c>), para aislar la orquestación del despachador de la
/// mecánica de envío HTTP. El repositorio SÍ es real (SQLite en memoria), mismo patrón que
/// <c>NotificationOutboxRepositoryTests</c>.
/// </summary>
public class NotificationOutboxDispatcherTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public NotificationOutboxDispatcherTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var setupContext = CreateContext();
        setupContext.Database.EnsureCreated();
    }

    private LudekaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new LudekaDbContext(options);
    }

    private static NotificationOutboxDispatcher NewDispatcher(
        LudekaDbContext context,
        FakeChannelDeliveryService notificationService,
        CommunityNotificationOptions? channelOptions = null,
        OutboxOptions? outboxOptions = null) =>
        new(
            new NotificationOutboxRepository(context),
            notificationService,
            Options.Create(channelOptions ?? new CommunityNotificationOptions
            {
                Enabled = true,
                DiscordEnabled = true,
                TelegramEnabled = true
            }),
            Options.Create(outboxOptions ?? new OutboxOptions()),
            NullLogger<NotificationOutboxDispatcher>.Instance);

    private static NotificationOutboxMessage NewMessage(NotificationChannel? targetChannel = null) =>
        new(NotificationEventType.CustomTestPing, "Ping de prueba", "Resumen del ping", targetChannel: targetChannel);

    /// <summary>Doble de <see cref="ICommunityNotificationService"/> que aplica los mismos
    /// métodos de dominio que usará <c>CommunityNotificationService.DeliverAsync</c>
    /// (<c>MarkAsSent</c>/<c>RegisterFailedAttempt</c>/<c>MarkAsPermanentlyFailed</c>) según un
    /// resultado configurado por el test, sin tocar HTTP. Su <c>maxDeliveryAttempts</c> es
    /// propio del doble, independiente de <see cref="OutboxOptions.MaxDeliveryAttempts"/>: aquí
    /// solo se aísla la orquestación del despachador, no la política real de reintento por
    /// canal (esa la cubre <c>CommunityNotificationServiceTests</c>).</summary>
    private sealed class FakeChannelDeliveryService : ICommunityNotificationService
    {
        private readonly Func<CommunityNotificationLog, bool> _shouldSucceed;
        private readonly int _maxDeliveryAttempts;

        public int DeliverCallCount { get; private set; }

        public FakeChannelDeliveryService(Func<CommunityNotificationLog, bool> shouldSucceed, int maxDeliveryAttempts = 5)
        {
            _shouldSucceed = shouldSucceed;
            _maxDeliveryAttempts = maxDeliveryAttempts;
        }

        public Task<NotificationDispatchResult> DeliverAsync(
            CommunityNotificationLog delivery, CommunityNotificationMessage message, CancellationToken ct = default)
        {
            DeliverCallCount++;

            if (_shouldSucceed(delivery))
            {
                delivery.MarkAsSent();
                return Task.FromResult(new NotificationDispatchResult(true, delivery.Channel, NotificationStatus.Sent));
            }

            const string error = "Fallo simulado de envío";
            delivery.RegisterFailedAttempt(error, DateTimeOffset.UtcNow.AddSeconds(30));
            if (delivery.Attempts >= _maxDeliveryAttempts)
            {
                delivery.MarkAsPermanentlyFailed(error);
            }

            return Task.FromResult(new NotificationDispatchResult(false, delivery.Channel, delivery.Status, error));
        }

        public Task<IReadOnlyList<NotificationDispatchResult>> BroadcastAsync(CommunityNotificationMessage message, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<NotificationDispatchResult> SendToDiscordAsync(CommunityNotificationMessage message, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<NotificationDispatchResult> SendToTelegramAsync(CommunityNotificationMessage message, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<CommunityNotificationLogDto>> GetHistoryAsync(int limit = 50, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<NotificationChannelStatusDto>> GetChannelStatusesAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<NotificationDispatchResult> RetryFailedNotificationAsync(Guid logId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task TriggerExpiringGiveawaysScanAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task RunExpiringGiveawaysScanAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task TriggerFridayReleasesBulletinAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task RunFridayReleasesBulletinAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task SendTestPingAsync(NotificationChannel? channel = null, CancellationToken ct = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task DispatchPendingAsync_ConIntentoFallido_IncrementaElContadorYProgramaUnReintento()
    {
        using var context = CreateContext();
        var repository = new NotificationOutboxRepository(context);
        var message = NewMessage(NotificationChannel.Discord);
        await repository.EnqueueAsync(message);

        var fakeService = new FakeChannelDeliveryService(_ => false, maxDeliveryAttempts: 5);
        var dispatcher = NewDispatcher(context, fakeService);

        var result = await dispatcher.DispatchPendingAsync();

        Assert.Equal(1, result.RetriedCount);
        using var readContext = CreateContext();
        var readRepository = new NotificationOutboxRepository(readContext);
        var delivery = Assert.Single(await readRepository.GetDeliveriesAsync(message.Id));

        Assert.Equal(NotificationStatus.Queued, delivery.Status);
        Assert.Equal(1, delivery.Attempts);
        Assert.NotNull(delivery.NextAttemptAt);
    }

    [Fact]
    public async Task DispatchPendingAsync_AgotandoMaxDeliveryAttempts_MarcaLaSubentregaFailedYNoVuelveAReclamarse()
    {
        using var context = CreateContext();
        var repository = new NotificationOutboxRepository(context);
        var message = NewMessage(NotificationChannel.Discord);
        await repository.EnqueueAsync(message);

        // MaxDeliveryAttempts = 1: el primer fallo ya agota los intentos configurados del doble.
        var fakeService = new FakeChannelDeliveryService(_ => false, maxDeliveryAttempts: 1);
        var dispatcher = NewDispatcher(context, fakeService);

        var firstResult = await dispatcher.DispatchPendingAsync();

        Assert.Equal(1, firstResult.CompletedCount);
        using var readContext = CreateContext();
        var readRepository = new NotificationOutboxRepository(readContext);
        var delivery = Assert.Single(await readRepository.GetDeliveriesAsync(message.Id));
        Assert.Equal(NotificationStatus.Failed, delivery.Status);

        // El mensaje quedó Completed (nada pendiente): una ejecución futura no vuelve a reclamarlo.
        var secondResult = await dispatcher.DispatchPendingAsync();
        Assert.Equal(0, secondResult.ClaimedCount);
        Assert.Equal(1, fakeService.DeliverCallCount);
    }

    [Fact]
    public async Task DispatchPendingAsync_ConEntregaExitosaEnElPrimerIntento_MarcaLaSubentregaSentYCompletaElMensaje()
    {
        using var context = CreateContext();
        var repository = new NotificationOutboxRepository(context);
        var message = NewMessage(NotificationChannel.Discord);
        await repository.EnqueueAsync(message);

        var fakeService = new FakeChannelDeliveryService(_ => true);
        var dispatcher = NewDispatcher(context, fakeService);

        var result = await dispatcher.DispatchPendingAsync();

        Assert.Equal(1, result.CompletedCount);
        using var readContext = CreateContext();
        var readRepository = new NotificationOutboxRepository(readContext);
        var delivery = Assert.Single(await readRepository.GetDeliveriesAsync(message.Id));
        Assert.Equal(NotificationStatus.Sent, delivery.Status);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
