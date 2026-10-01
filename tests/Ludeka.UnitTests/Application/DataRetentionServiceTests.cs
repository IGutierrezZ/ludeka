using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Maintenance;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class DataRetentionServiceTests
{
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeImageStorageService : IImageStorageService
    {
        public List<string> DeletedKeys { get; } = [];
        public bool ShouldThrowOnDelete { get; set; }
        public bool ReturnFalseOnDelete { get; set; }

        public Task<bool> DeleteImageAsync(string objectKey, CancellationToken ct = default)
        {
            if (ShouldThrowOnDelete)
                throw new InvalidOperationException("Fallo simulado al eliminar del storage");

            DeletedKeys.Add(objectKey);
            return Task.FromResult(!ReturnFalseOnDelete);
        }

        public Task<string> UploadOptimizedImageAsync(Stream inputStream, string objectKey, int maxWidth = 1000, int quality = 82, CancellationToken ct = default) => Task.FromResult($"/images/{objectKey}");
        public Task<Ludeka.Core.ValueObjects.ImageVariantUrls> UploadGameImageVariantsAsync(Stream rawImageStream, int bggId, string imageType, CancellationToken ct = default) => Task.FromResult(new Ludeka.Core.ValueObjects.ImageVariantUrls("", ""));
        public string GetPublicUrl(string objectKey) => $"/images/{objectKey}";
        public Task<GameImageUploadResult> SaveGameCoverAsync(string slug, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default) => Task.FromResult(new GameImageUploadResult(true, "", null));
        public Task<GameImageUploadResult> SaveGameImageAsync(string slug, string slot, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default) => Task.FromResult(new GameImageUploadResult(true, "", null));
        public Task<GameImageUploadResult> SaveEventPosterAsync(string eventSlugOrId, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default) => Task.FromResult(new GameImageUploadResult(true, "", null));
        public Task<GameImageUploadResult> SaveCommunityImageAsync(string subfolder, string identifier, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default) => Task.FromResult(new GameImageUploadResult(true, "", null));
        public Task<GameImageUploadResult> ValidateCoverUrlAsync(string imageUrl, CancellationToken ct = default) => Task.FromResult(new GameImageUploadResult(true, imageUrl, null));
    }

    private sealed class FakeGiveawayRepository : IGiveawayRepository
    {
        public List<Giveaway> Items { get; } = [];
        public List<Guid> DeletedIds { get; } = [];

        public Task<IReadOnlyList<Giveaway>> GetGiveawaysAsync(bool includeExpired = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Giveaway>>(Items.ToList());

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            DeletedIds.Add(id);
            Items.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
        }

        public Task<Giveaway?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<Giveaway?> FindDuplicateOrCollaborativeAsync(string title, string organizer, DateTimeOffset deadline, CancellationToken ct = default) => Task.FromResult<Giveaway?>(null);
        public Task AddAsync(Giveaway giveaway, CancellationToken ct = default) { Items.Add(giveaway); return Task.CompletedTask; }
        public Task UpdateAsync(Giveaway giveaway, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeEventRepository : IBoardGameEventRepository
    {
        public List<BoardGameEvent> Items { get; } = [];
        public List<Guid> DeletedIds { get; } = [];

        public Task<IReadOnlyList<BoardGameEvent>> GetAllEventsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BoardGameEvent>>(Items.ToList());

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            DeletedIds.Add(id);
            Items.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<BoardGameEvent>> GetUpcomingEventsAsync(int limit = 20, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BoardGameEvent>>(Items.ToList());
        public Task<BoardGameEvent?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task AddAsync(BoardGameEvent boardGameEvent, CancellationToken ct = default) { Items.Add(boardGameEvent); return Task.CompletedTask; }
        public Task UpdateAsync(BoardGameEvent boardGameEvent, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeReleaseRepository : IWeeklyReleaseRepository
    {
        public List<WeeklyRelease> Items { get; } = [];
        public List<Guid> DeletedIds { get; } = [];

        public Task<IReadOnlyList<WeeklyRelease>> GetReleasesAsync(DateOnly? fromDate = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WeeklyRelease>>(Items.ToList());

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            DeletedIds.Add(id);
            Items.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
        }

        public Task<WeeklyRelease?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task AddAsync(WeeklyRelease release, CancellationToken ct = default) { Items.Add(release); return Task.CompletedTask; }
        public Task UpdateAsync(WeeklyRelease release, CancellationToken ct = default) => Task.CompletedTask;
    }

    // --- Pruebas de ManagedImageKeyExtractor ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://cf.geekdo-images.com/item123.jpg")]
    [InlineData("https://instagram.com/p/abc123")]
    [InlineData("https://scontent.cdninstagram.com/v/t51/photo.jpg")]
    [InlineData("https://images.unsplash.com/photo-123")]
    public void ManagedImageKeyExtractor_UrlsNoGestionadasOInvalidas_DevuelveFalse(string? url)
    {
        var result = ManagedImageKeyExtractor.TryExtract(url, out var key);
        Assert.False(result);
        Assert.Null(key);
    }

    [Theory]
    [InlineData("/images/events/festival-cordoba-123.webp", "events/festival-cordoba-123.webp")]
    [InlineData("images/events/festival-cordoba-123.webp", "events/festival-cordoba-123.webp")]
    [InlineData("/images/community/sorteo-456.jpg", "community/sorteo-456.jpg")]
    [InlineData("https://media.ludeka.es/events/interocio-2026.webp", "events/interocio-2026.webp")]
    [InlineData("https://cdn.ludeka.es/images/community/giveaway.webp", "community/giveaway.webp")]
    public void ManagedImageKeyExtractor_UrlsGestionadas_DevuelveTrueYClaveRelativa(string url, string expectedKey)
    {
        var result = ManagedImageKeyExtractor.TryExtract(url, out var key);
        Assert.True(result);
        Assert.Equal(expectedKey, key);
    }

    // --- Pruebas de DataRetentionService ---

    [Fact]
    public async Task PurgeExpiredDataAsync_CuandoEstaDeshabilitado_NoPurgaNadaYDevuelveCeros()
    {
        var giveawayRepo = new FakeGiveawayRepository();
        var eventRepo = new FakeEventRepository();
        var releaseRepo = new FakeReleaseRepository();
        var storage = new FakeImageStorageService();
        var options = Options.Create(new DataRetentionOptions { Enabled = false });

        var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var service = new DataRetentionService(
            giveawayRepo, eventRepo, releaseRepo, storage, options,
            NullLogger<DataRetentionService>.Instance, new FixedTimeProvider(now));

        var result = await service.PurgeExpiredDataAsync();

        Assert.Equal(0, result.TotalPurgedEntities);
        Assert.Equal(0, result.PurgedGiveawaysCount);
        Assert.Equal(0, result.PurgedEventsCount);
        Assert.Equal(0, result.PurgedReleasesCount);
        Assert.Equal(0, result.DeletedImagesCount);
    }

    [Fact]
    public async Task PurgeExpiredDataAsync_Sorteos_PurgaSoloLosQueSuperanMargenDeGracia()
    {
        var giveawayRepo = new FakeGiveawayRepository();
        var eventRepo = new FakeEventRepository();
        var releaseRepo = new FakeReleaseRepository();
        var storage = new FakeImageStorageService();
        var options = Options.Create(new DataRetentionOptions { GiveawayGracePeriodDays = 7 });

        var now = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

        // 1. Sorteo vencido hace 8 días (Deadline: 7 de octubre) con imagen gestionada -> DEBE PURGARSE Y BORRAR IMAGEN
        var gVencidoAntiguo = new Giveaway(
            "Sorteo Antiguo", "Devir", "https://ludeka.es", GiveawayPlatform.Instagram,
            deadlineAt: now.AddDays(-8),
            thumbnailUrl: "/images/community/sorteo-antiguo.jpg");

        // 2. Sorteo vencido hace 4 días (Deadline: 11 de octubre) -> EN GRACIA, NO SE PURGA
        var gEnGracia = new Giveaway(
            "Sorteo Reciente", "Asmodee", "https://ludeka.es", GiveawayPlatform.Instagram,
            deadlineAt: now.AddDays(-4),
            thumbnailUrl: "/images/community/sorteo-reciente.jpg");

        // 3. Sorteo activo (Deadline: 20 de octubre) -> NO SE PURGA
        var gActivo = new Giveaway(
            "Sorteo Activo", "Tranjis", "https://ludeka.es", GiveawayPlatform.Instagram,
            deadlineAt: now.AddDays(5),
            thumbnailUrl: "/images/community/sorteo-activo.jpg");

        // 4. Sorteo vencido hace 10 días con imagen externa (Instagram) -> SE PURGA PERO NO LLAMA A DELETEIMAGE
        var gVencidoExterna = new Giveaway(
            "Sorteo Externo", "Maldito Games", "https://ludeka.es", GiveawayPlatform.Instagram,
            deadlineAt: now.AddDays(-10),
            thumbnailUrl: "https://scontent.cdninstagram.com/photo.jpg");

        giveawayRepo.Items.AddRange([gVencidoAntiguo, gEnGracia, gActivo, gVencidoExterna]);

        var service = new DataRetentionService(
            giveawayRepo, eventRepo, releaseRepo, storage, options,
            NullLogger<DataRetentionService>.Instance, new FixedTimeProvider(now));

        var result = await service.PurgeExpiredDataAsync();

        Assert.Equal(2, result.PurgedGiveawaysCount);
        Assert.Contains(gVencidoAntiguo.Id, giveawayRepo.DeletedIds);
        Assert.Contains(gVencidoExterna.Id, giveawayRepo.DeletedIds);
        Assert.DoesNotContain(gEnGracia.Id, giveawayRepo.DeletedIds);
        Assert.DoesNotContain(gActivo.Id, giveawayRepo.DeletedIds);

        // Solo se borró la imagen del sorteo gestionado internamente
        Assert.Single(storage.DeletedKeys);
        Assert.Equal("community/sorteo-antiguo.jpg", storage.DeletedKeys[0]);
        Assert.Equal(1, result.DeletedImagesCount);
    }

    [Fact]
    public async Task PurgeExpiredDataAsync_Eventos_PurgaSoloLosQueSuperanMargenDeGracia()
    {
        var giveawayRepo = new FakeGiveawayRepository();
        var eventRepo = new FakeEventRepository();
        var releaseRepo = new FakeReleaseRepository();
        var storage = new FakeImageStorageService();
        var options = Options.Create(new DataRetentionOptions { EventGracePeriodDays = 7 });

        var now = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        var today = DateOnly.FromDateTime(now.DateTime); // 2026-10-20

        // 1. Evento finalizado hace 9 días (EndDate: 11 de octubre) -> SE PURGA Y BORRA IMAGEN
        var evAntiguo = new BoardGameEvent(
            "Festival Pasado", "Desc", "/images/events/festival-pasado.webp",
            startDate: today.AddDays(-12), endDate: today.AddDays(-9), location: "Córdoba");

        // 2. Evento finalizado hace 3 días (EndDate: 17 de octubre) -> EN GRACIA, NO SE PURGA
        var evEnGracia = new BoardGameEvent(
            "Feria En Gracia", "Desc", "/images/events/feria-reciente.webp",
            startDate: today.AddDays(-5), endDate: today.AddDays(-3), location: "Madrid");

        // 3. Evento en curso (hoy es entre StartDate y EndDate) -> NO SE PURGA
        var evEnCurso = new BoardGameEvent(
            "Jornadas En Curso", "Desc", "/images/events/en-curso.webp",
            startDate: today.AddDays(-1), endDate: today.AddDays(2), location: "Barcelona");

        eventRepo.Items.AddRange([evAntiguo, evEnGracia, evEnCurso]);

        var service = new DataRetentionService(
            giveawayRepo, eventRepo, releaseRepo, storage, options,
            NullLogger<DataRetentionService>.Instance, new FixedTimeProvider(now));

        var result = await service.PurgeExpiredDataAsync();

        Assert.Equal(1, result.PurgedEventsCount);
        Assert.Contains(evAntiguo.Id, eventRepo.DeletedIds);
        Assert.DoesNotContain(evEnGracia.Id, eventRepo.DeletedIds);
        Assert.DoesNotContain(evEnCurso.Id, eventRepo.DeletedIds);

        Assert.Single(storage.DeletedKeys);
        Assert.Equal("events/festival-pasado.webp", storage.DeletedKeys[0]);
    }

    [Fact]
    public async Task PurgeExpiredDataAsync_Novedades_PurgaSoloLanzadasHaceMasDe60DiasConFechaCumplidaOSinFecha()
    {
        var giveawayRepo = new FakeGiveawayRepository();
        var eventRepo = new FakeEventRepository();
        var releaseRepo = new FakeReleaseRepository();
        var storage = new FakeImageStorageService();
        var options = Options.Create(new DataRetentionOptions { ReleaseRetentionDays = 60 });

        var now = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        var today = DateOnly.FromDateTime(now.DateTime); // 2026-10-15

        // Usamos reflexión para setear CreatedAt en el pasado para las pruebas
        var createdAtProp = typeof(WeeklyRelease).GetProperty(nameof(WeeklyRelease.CreatedAt))!;

        // 1. Novedad de hace 65 días SIN FECHA -> SE PURGA
        var rel65DiasSinFecha = new WeeklyRelease("Novedad Sin Fecha 65d", "Devir", releaseDate: null, coverImageUrl: "/images/games/sin-fecha.webp");
        createdAtProp.SetValue(rel65DiasSinFecha, now.AddDays(-65));

        // 2. Novedad de hace 65 días con FECHA PASADA (ej. 2026-09-01 < 2026-10-15) -> SE PURGA
        var rel65DiasFechaPasada = new WeeklyRelease("Novedad Fecha Pasada 65d", "Asmodee", releaseDate: today.AddDays(-20), coverImageUrl: "/images/games/pasada.webp");
        createdAtProp.SetValue(rel65DiasFechaPasada, now.AddDays(-65));

        // 3. Novedad de hace 65 días con FECHA FUTURA (ej. 2026-11-01 > 2026-10-15) -> AÚN NO HA SALIDO, NO SE PURGA
        var rel65DiasFechaFutura = new WeeklyRelease("Novedad Fecha Futura 65d", "Tranjis", releaseDate: today.AddDays(15), coverImageUrl: "/images/games/futura.webp");
        createdAtProp.SetValue(rel65DiasFechaFutura, now.AddDays(-65));

        // 4. Novedad de hace 20 días SIN FECHA -> LLEVA MENOS DE 60 DÍAS, NO SE PURGA
        var rel20DiasSinFecha = new WeeklyRelease("Novedad Sin Fecha 20d", "Maldito", releaseDate: null, coverImageUrl: "/images/games/reciente-sin-fecha.webp");
        createdAtProp.SetValue(rel20DiasSinFecha, now.AddDays(-20));

        // 5. Novedad de hace 20 días con FECHA PASADA -> LLEVA MENOS DE 60 DÍAS, NO SE PURGA
        var rel20DiasFechaPasada = new WeeklyRelease("Novedad Fecha Pasada 20d", "TCG Factory", releaseDate: today.AddDays(-5), coverImageUrl: "/images/games/reciente-pasada.webp");
        createdAtProp.SetValue(rel20DiasFechaPasada, now.AddDays(-20));

        releaseRepo.Items.AddRange([rel65DiasSinFecha, rel65DiasFechaPasada, rel65DiasFechaFutura, rel20DiasSinFecha, rel20DiasFechaPasada]);

        var service = new DataRetentionService(
            giveawayRepo, eventRepo, releaseRepo, storage, options,
            NullLogger<DataRetentionService>.Instance, new FixedTimeProvider(now));

        var result = await service.PurgeExpiredDataAsync();

        Assert.Equal(2, result.PurgedReleasesCount);
        Assert.Contains(rel65DiasSinFecha.Id, releaseRepo.DeletedIds);
        Assert.Contains(rel65DiasFechaPasada.Id, releaseRepo.DeletedIds);
        Assert.DoesNotContain(rel65DiasFechaFutura.Id, releaseRepo.DeletedIds);
        Assert.DoesNotContain(rel20DiasSinFecha.Id, releaseRepo.DeletedIds);
        Assert.DoesNotContain(rel20DiasFechaPasada.Id, releaseRepo.DeletedIds);

        Assert.Equal(2, result.DeletedImagesCount);
        Assert.Contains("games/sin-fecha.webp", storage.DeletedKeys);
        Assert.Contains("games/pasada.webp", storage.DeletedKeys);
    }

    [Fact]
    public async Task PurgeExpiredDataAsync_CuandoBorradoDeImagenFalla_AunEliminaEntidadYRegistraFallo()
    {
        var giveawayRepo = new FakeGiveawayRepository();
        var eventRepo = new FakeEventRepository();
        var releaseRepo = new FakeReleaseRepository();
        var storage = new FakeImageStorageService { ShouldThrowOnDelete = true };
        var options = Options.Create(new DataRetentionOptions { GiveawayGracePeriodDays = 0 });

        var now = DateTimeOffset.UtcNow;
        var gVencido = new Giveaway(
            "Sorteo Vencido", "Devir", "https://ludeka.es", GiveawayPlatform.Instagram,
            deadlineAt: now.AddDays(-1),
            thumbnailUrl: "/images/community/sorteo-error.jpg");

        giveawayRepo.Items.Add(gVencido);

        var service = new DataRetentionService(
            giveawayRepo, eventRepo, releaseRepo, storage, options,
            NullLogger<DataRetentionService>.Instance, new FixedTimeProvider(now));

        var result = await service.PurgeExpiredDataAsync();

        // Se eliminó la entidad de base de datos a pesar del fallo de storage
        Assert.Equal(1, result.PurgedGiveawaysCount);
        Assert.Contains(gVencido.Id, giveawayRepo.DeletedIds);
        Assert.Equal(0, result.DeletedImagesCount);
        Assert.Equal(1, result.FailedImagesCount);
    }
}
