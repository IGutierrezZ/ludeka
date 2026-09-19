using System;
using System.Linq;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

/// <summary>
/// Persistencia y reclamación del outbox de notificaciones (INC-47, R4a, diseño §6).
/// Especificación <c>notification-outbox</c>: "El registro persiste inmediatamente al
/// encolar", "El registro sobrevive aunque el despachador nunca llegue a ejecutarse",
/// idempotencia de <c>EnsureDeliveryAsync</c> vía <c>UNIQUE(MessageId, Channel)</c>, y matriz
/// de amenazas §14 ("Inyección SQL en la reclamación"). La conexión SQLite en memoria se
/// mantiene abierta durante todo el test porque ":memory:" crea una base nueva y vacía por
/// cada conexión distinta: reutilizarla es lo que simula instancias/DbContext independientes
/// compartiendo el mismo almacén persistido.
/// </summary>
public class NotificationOutboxRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public NotificationOutboxRepositoryTests()
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

    private static NotificationOutboxMessage NewMessage(
        NotificationChannel? targetChannel = null,
        string fieldsJson = "{}") =>
        new(
            NotificationEventType.CustomTestPing,
            "Ping de prueba",
            "Resumen del ping de prueba",
            targetUrl: "https://ludeka.es/ping",
            imageUrl: null,
            fieldsJson: fieldsJson,
            targetChannel: targetChannel);

    [Fact]
    public async Task EnqueueAsync_AlEncolar_PersisteElRegistroInmediatamenteSinInvocarUnDespachador()
    {
        using var context = CreateContext();
        var repository = new NotificationOutboxRepository(context);
        var message = NewMessage();

        await repository.EnqueueAsync(message);

        // Lectura directa del registro persistido desde un DbContext nuevo, sin invocar
        // ClaimPendingAsync ni ningún despachador.
        using var readContext = CreateContext();
        var persisted = await readContext.NotificationOutboxMessages
            .AsNoTracking()
            .SingleAsync(m => m.Id == message.Id);

        Assert.Equal(OutboxMessageStatus.Pending, persisted.Status);
        Assert.Equal("Ping de prueba", persisted.Title);
    }

    [Fact]
    public async Task ClaimPendingAsync_ConMensajeDeUnaInstanciaYaApagada_LoReclamaUnDespachadorPosterior()
    {
        // Arrange: "la instancia que encoló" se apaga inmediatamente después (se descarta su
        // contexto y su repositorio).
        Guid messageId;
        using (var writerContext = CreateContext())
        {
            var writerRepository = new NotificationOutboxRepository(writerContext);
            var message = NewMessage();
            messageId = message.Id;
            await writerRepository.EnqueueAsync(message);
        }

        // Act: "un despachador posterior" -> contexto y repositorio nuevos, sin relación con
        // el anterior, contra el mismo almacén persistido.
        using var dispatcherContext = CreateContext();
        var dispatcherRepository = new NotificationOutboxRepository(dispatcherContext);
        var claimed = await dispatcherRepository.ClaimPendingAsync(
            batchSize: 10, claimedBy: "despachador-posterior", lease: TimeSpan.FromMinutes(5));

        Assert.Contains(claimed, c => c.Id == messageId);
    }

    [Fact]
    public async Task EnqueueAsync_ConFieldsJsonYTargetChannel_SobrevivenAlCicloDeEncoladoSinInvocarElDespachador()
    {
        using var context = CreateContext();
        var repository = new NotificationOutboxRepository(context);
        var message = NewMessage(targetChannel: NotificationChannel.Telegram, fieldsJson: "{\"jugadores\":\"2-4\"}");

        await repository.EnqueueAsync(message);

        using var readContext = CreateContext();
        var persisted = await readContext.NotificationOutboxMessages
            .AsNoTracking()
            .SingleAsync(m => m.Id == message.Id);

        Assert.Equal("{\"jugadores\":\"2-4\"}", persisted.FieldsJson);
        Assert.Equal(NotificationChannel.Telegram, persisted.TargetChannel);
    }

    [Fact]
    public async Task EnsureDeliveryAsync_ReclamadaDosVeces_NoDuplicaLaSubentregaPorCanalGraciasAlIndiceUnico()
    {
        using var context = CreateContext();
        var repository = new NotificationOutboxRepository(context);
        var message = NewMessage();
        await repository.EnqueueAsync(message);

        var claim = (await repository.ClaimPendingAsync(
            batchSize: 10, claimedBy: "despachador-1", lease: TimeSpan.FromMinutes(5))).Single();

        var firstDelivery = await repository.EnsureDeliveryAsync(message.Id, NotificationChannel.Discord, claim);
        var secondDelivery = await repository.EnsureDeliveryAsync(message.Id, NotificationChannel.Discord, claim);

        Assert.Equal(firstDelivery.Id, secondDelivery.Id);
        var deliveries = await repository.GetDeliveriesAsync(message.Id);
        Assert.Single(deliveries);
    }

    [Fact]
    public async Task ClaimPendingAsync_ConClaimedByConteniendoComillas_NoAlteraLaSentenciaSql()
    {
        using var context = CreateContext();
        var repository = new NotificationOutboxRepository(context);
        var message = NewMessage();
        await repository.EnqueueAsync(message);

        const string maliciousClaimedBy = "o'brien'; DROP TABLE NotificationOutboxMessages; --";

        var claimed = await repository.ClaimPendingAsync(
            batchSize: 10, claimedBy: maliciousClaimedBy, lease: TimeSpan.FromMinutes(5));

        // Si "claimedBy" se hubiera interpolado en la sentencia en vez de parametrizarse, el
        // DROP TABLE se habría ejecutado de verdad: la tabla ya no existiría y esta lectura
        // fallaría. Además, el valor persistido debe conservar las comillas literales: si el
        // driver las hubiera necesitado escapar a mano, el valor leído no coincidiría.
        Assert.Single(claimed);

        using var readContext = CreateContext();
        var persisted = await readContext.NotificationOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == message.Id);
        Assert.Equal(maliciousClaimedBy, persisted.ClaimedBy);
    }

    [Fact]
    public async Task GetDeliveriesAsync_SinSubentregasCreadas_DevuelveListaVacia()
    {
        using var context = CreateContext();
        var repository = new NotificationOutboxRepository(context);
        var message = NewMessage();
        await repository.EnqueueAsync(message);

        var deliveries = await repository.GetDeliveriesAsync(message.Id);

        Assert.Empty(deliveries);
    }

    [Fact]
    public async Task CompleteMessageAsync_MarcaElMensajeComoCompletadoYYaNoEsReclamable()
    {
        using var context = CreateContext();
        var repository = new NotificationOutboxRepository(context);
        var message = NewMessage();
        await repository.EnqueueAsync(message);

        await repository.CompleteMessageAsync(message.Id);

        var claimed = await repository.ClaimPendingAsync(
            batchSize: 10, claimedBy: "despachador-1", lease: TimeSpan.FromMinutes(5));
        Assert.DoesNotContain(claimed, c => c.Id == message.Id);
    }

    [Fact]
    public async Task ReleaseMessageAsync_TrasUnFallo_LiberaElMensajeParaUnReintentoInmediato()
    {
        using var context = CreateContext();
        var repository = new NotificationOutboxRepository(context);
        var message = NewMessage();
        await repository.EnqueueAsync(message);

        // Reclamar primero desplaza NextAttemptAt al futuro (concesión); sin liberar, una
        // segunda reclamación inmediata no debería volver a encontrar el mensaje.
        await repository.ClaimPendingAsync(batchSize: 10, claimedBy: "despachador-1", lease: TimeSpan.FromMinutes(5));

        await repository.ReleaseMessageAsync(message.Id, DateTimeOffset.UtcNow.AddSeconds(-1), "Discord no respondió");

        var claimed = await repository.ClaimPendingAsync(
            batchSize: 10, claimedBy: "despachador-2", lease: TimeSpan.FromMinutes(5));
        Assert.Contains(claimed, c => c.Id == message.Id);
    }

    [Fact]
    public async Task MarkMessageDeadAsync_AgotaLosIntentosYElMensajeYaNoEsReclamable()
    {
        using var context = CreateContext();
        var repository = new NotificationOutboxRepository(context);
        var message = NewMessage();
        await repository.EnqueueAsync(message);

        await repository.MarkMessageDeadAsync(message.Id, "Se agotó MaxClaimAttempts");

        var claimed = await repository.ClaimPendingAsync(
            batchSize: 10, claimedBy: "despachador-1", lease: TimeSpan.FromMinutes(5));
        Assert.DoesNotContain(claimed, c => c.Id == message.Id);
    }

    [Fact]
    public async Task GetHealthSnapshotAsync_ConMensajesPendientesYMuertos_CuentaCadaEstadoPorSeparado()
    {
        using var context = CreateContext();
        var repository = new NotificationOutboxRepository(context);

        var pending = NewMessage();
        var dead = NewMessage();
        await repository.EnqueueAsync(pending);
        await repository.EnqueueAsync(dead);
        await repository.MarkMessageDeadAsync(dead.Id, "Se agotó MaxClaimAttempts");

        var snapshot = await repository.GetHealthSnapshotAsync();

        Assert.Equal(1, snapshot.PendingCount);
        Assert.Equal(1, snapshot.DeadCount);
        Assert.NotNull(snapshot.OldestPendingAt);
        Assert.Equal("SQLite", snapshot.Provider);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
