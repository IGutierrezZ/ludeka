using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Notifications;
using Ludeka.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

/// <summary>
/// Cableado real de <see cref="ICommunityNotificationQueue"/> sobre el outbox persistente
/// (INC-47, R4b, diseño §6.2, tasks.md 6.10). Cubre C2 (diseño §2): <c>EnqueueAsync</c> cambia
/// de perfil de fallo (de un <c>Channel&lt;T&gt;</c> en memoria a una escritura en base de
/// datos) y los dos productores lo silencian en un <c>catch</c> vacío
/// (<c>FoundingVerdictService.cs:219-222</c>, <c>RuleQAService.cs:201-204</c>, ninguno de los
/// dos se toca): esta clase deja el rastro en el log antes de relanzar.
/// </summary>
public class OutboxCommunityNotificationQueueTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public OutboxCommunityNotificationQueueTests()
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

    private sealed class ThrowingOutboxRepository : INotificationOutboxRepository
    {
        public Task EnqueueAsync(NotificationOutboxMessage message, CancellationToken ct = default) =>
            throw new InvalidOperationException("Fallo simulado de persistencia del outbox.");

        public Task<IReadOnlyList<OutboxClaim>> ClaimPendingAsync(int batchSize, string claimedBy, TimeSpan lease, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<IReadOnlyList<CommunityNotificationLog>> GetDeliveriesAsync(Guid messageId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<CommunityNotificationLog> EnsureDeliveryAsync(Guid messageId, NotificationChannel channel, OutboxClaim claim, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task CompleteMessageAsync(Guid messageId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ReleaseMessageAsync(Guid messageId, DateTimeOffset nextAttemptAt, string? lastError, CancellationToken ct = default) => throw new NotImplementedException();
        public Task MarkMessageDeadAsync(Guid messageId, string reason, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<OutboxHealthSnapshot> GetHealthSnapshotAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task EnqueueAsync_ConMensajeValido_PersisteUnMensajeDelOutboxConFieldsJsonMapeado()
    {
        using var context = CreateContext();
        var queue = new OutboxCommunityNotificationQueue(
            new NotificationOutboxRepository(context), NullLogger<OutboxCommunityNotificationQueue>.Instance);

        var message = new CommunityNotificationMessage(
            NotificationEventType.GiveawayExpiring,
            "Sorteo de Catán",
            "Quedan 24 horas para participar",
            TargetUrl: "https://ludeka.es/sorteos/catan",
            Fields: new Dictionary<string, string> { { "Plataforma", "Instagram" } });

        await queue.EnqueueAsync(message);

        using var readContext = CreateContext();
        var persisted = await readContext.NotificationOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal("Sorteo de Catán", persisted.Title);
        Assert.Contains("Instagram", persisted.FieldsJson);
    }

    [Fact]
    public async Task EnqueueAsync_ConFalloDePersistencia_RegistraElErrorEnElLogYRelanzaLaExcepcion()
    {
        var queue = new OutboxCommunityNotificationQueue(
            new ThrowingOutboxRepository(), NullLogger<OutboxCommunityNotificationQueue>.Instance);

        var message = new CommunityNotificationMessage(
            NotificationEventType.CustomTestPing, "Ping de prueba", "Resumen del ping");

        // A diferencia de FoundingVerdictService/RuleQAService (catch vacío), aquí la excepción
        // se relanza: el rastro queda en el log (NullLogger no lo verifica, pero el relanzamiento
        // sí es observable) en vez de perderse en silencio (resuelve C2).
        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.EnqueueAsync(message).AsTask());
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
