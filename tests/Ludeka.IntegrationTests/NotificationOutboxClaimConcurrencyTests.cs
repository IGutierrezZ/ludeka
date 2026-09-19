using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.IntegrationTests;

/// <summary>
/// Reclamación exclusiva de <see cref="NotificationOutboxRepository.ClaimPendingAsync"/> contra
/// PostgreSQL real (INC-47, R4a, diseño §6.3). Especificación <c>notification-outbox</c>,
/// escenario "Dos despachadores reclaman lotes al mismo tiempo sin solaparse": es el único
/// requisito que exige <c>SELECT ... FOR UPDATE SKIP LOCKED</c>, primitiva que SQLite no
/// implementa y que Entity Framework Core no traduce desde LINQ (spec.md:48). La primitiva en
/// sí ya queda demostrada de forma determinista (transacciones explícitas que fuerzan el
/// solapamiento) por <see cref="PostgresSkipLockedSmokeTests"/> (R1); esta prueba comprueba que
/// el repositorio la conecta correctamente a la tabla del outbox: SQL, parámetros y mapeo del
/// resultado de vuelta a <c>OutboxClaim</c>, contra dos conexiones reales y concurrentes.
/// </summary>
[Collection("postgres-real-outbox-claim")]
public class NotificationOutboxClaimConcurrencyTests
{
    private readonly PostgresFixture _fixture;

    public NotificationOutboxClaimConcurrencyTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ClaimPendingAsync_ConDosDespachadoresReclamandoAlMismoTiempo_NoSolapaNiPierdeMensajes()
    {
        _fixture.EnsureAvailable();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        // Arrange: esquema al día y 20 mensajes pendientes, reclamables de inmediato.
        var messageIds = new List<Guid>();
        await using (var arrangeDb = new LudekaDbContext(options))
        {
            await arrangeDb.Database.MigrateAsync();
            var arrangeRepository = new NotificationOutboxRepository(arrangeDb);
            for (var i = 0; i < 20; i++)
            {
                var message = new NotificationOutboxMessage(
                    NotificationEventType.CustomTestPing,
                    $"Ping de concurrencia {i}",
                    "Resumen del ping de concurrencia");
                messageIds.Add(message.Id);
                await arrangeRepository.EnqueueAsync(message);
            }
        }

        // Act: dos despachadores reales, cada uno con su propia conexión, reclaman al mismo
        // tiempo lotes de 10 (justo la mitad cada uno de los 20 mensajes disponibles).
        await using var dbOne = new LudekaDbContext(options);
        await using var dbTwo = new LudekaDbContext(options);
        var repositoryOne = new NotificationOutboxRepository(dbOne);
        var repositoryTwo = new NotificationOutboxRepository(dbTwo);

        var claimTaskOne = repositoryOne.ClaimPendingAsync(
            batchSize: 10, claimedBy: "despachador-1", lease: TimeSpan.FromMinutes(5));
        var claimTaskTwo = repositoryTwo.ClaimPendingAsync(
            batchSize: 10, claimedBy: "despachador-2", lease: TimeSpan.FromMinutes(5));
        var results = await Task.WhenAll(claimTaskOne, claimTaskTwo);

        var claimedByOne = results[0].Select(c => c.Id).ToList();
        var claimedByTwo = results[1].Select(c => c.Id).ToList();

        // Assert: cada mensaje reclamado por exactamente uno de los dos; ninguno se pierde ni
        // se solapa entre los dos lotes devueltos.
        Assert.Equal(10, claimedByOne.Count);
        Assert.Equal(10, claimedByTwo.Count);
        Assert.Empty(claimedByOne.Intersect(claimedByTwo));
        Assert.Equal(
            messageIds.OrderBy(id => id),
            claimedByOne.Concat(claimedByTwo).OrderBy(id => id));

        // Comprobación adicional a nivel de fila: si una reclamación hubiera actualizado dos
        // veces el mismo mensaje (el propio bug que SKIP LOCKED existe para evitar), su
        // contador de intentos sería 2, no 1, aunque el resultado devuelto no mostrara
        // intersección.
        await using var assertDb = new LudekaDbContext(options);
        var attemptsByMessage = await assertDb.NotificationOutboxMessages
            .AsNoTracking()
            .Where(m => messageIds.Contains(m.Id))
            .Select(m => m.Attempts)
            .ToListAsync();
        Assert.All(attemptsByMessage, attempts => Assert.Equal(1, attempts));
    }
}
