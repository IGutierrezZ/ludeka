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
/// Persistencia del outbox de notificaciones (INC-47, R4a, diseño §6). Especificación
/// <c>notification-outbox</c>: "El registro persiste inmediatamente al encolar" y la
/// supervivencia de <c>FieldsJson</c>/<c>TargetChannel</c> al ciclo de encolado (C1).
/// Los casos que parten de un mensaje ya reclamado —idempotencia de
/// <c>EnsureDeliveryAsync</c>, las tres transiciones de estado y la inyección SQL en la
/// reclamación— se añaden a esta misma clase en el PR 6c, junto con el método
/// <c>ClaimPendingAsync</c> del que dependen.
/// La conexión SQLite en memoria se mantiene abierta durante todo el test porque ":memory:"
/// crea una base nueva y vacía por cada conexión distinta: reutilizarla es lo que simula
/// instancias/DbContext independientes compartiendo el mismo almacén persistido.
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
