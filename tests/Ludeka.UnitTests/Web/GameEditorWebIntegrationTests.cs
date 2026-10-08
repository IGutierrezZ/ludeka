using System;
using System.IO;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Catalog;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Repositories;
using Ludeka.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Ludeka.UnitTests.Web;

public class GameEditorWebIntegrationTests
{
    [Fact]
    public void DependencyInjection_RegistersGameEditorServicesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        services.AddMemoryCache();
        services.AddDbContext<LudekaDbContext>(opts => opts.UseSqlite(connection));
        services.AddDbContextFactory<LudekaDbContext>(opts => opts.UseSqlite(connection));
        services.AddScoped<IGameRepository, SqliteGameRepository>();
        services.AddScoped<CatalogService>();
        services.AddScoped<ICatalogService, CatalogService>();

        var user = new FakeCurrentUser();
        services.AddSingleton<ICurrentUserService>(user);

        // Registro de servicios de INC-18
        services.AddScoped<IImageStorageService, PhysicalFileImageStorageService>();
        services.AddScoped<IGameEditLogRepository, SqliteGameEditLogRepository>();
        services.AddScoped<IGameEditorService, GameEditorService>();

        var provider = services.BuildServiceProvider();

        // Act & Assert
        var imgStorage = provider.GetService<IImageStorageService>();
        var editLogRepo = provider.GetService<IGameEditLogRepository>();
        var editorService = provider.GetService<IGameEditorService>();

        Assert.NotNull(imgStorage);
        Assert.NotNull(editLogRepo);
        Assert.NotNull(editorService);

        Assert.IsType<PhysicalFileImageStorageService>(imgStorage);
        Assert.IsType<SqliteGameEditLogRepository>(editLogRepo);
        Assert.IsType<GameEditorService>(editorService);
    }

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Ludeka.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    [Fact]
    public void GameEditorModal_RazorFile_HasAccessibleAttributesAndEditorialTabs()
    {
        var path = Path.Combine(GetRepoRoot(), "src", "Ludeka.Web", "Components", "Shared", "GameEditorModal.razor");
        Assert.True(File.Exists(path), $"GameEditorModal.razor no encontrado en {path}");

        var content = File.ReadAllText(path);

        // WCAG 2.2 AA Accessibility Checks
        Assert.Contains("role=\"dialog\"", content);
        Assert.Contains("aria-modal=\"true\"", content);
        Assert.Contains("aria-labelledby=\"game-editor-modal-title\"", content);
        Assert.Contains("aria-label=\"Cerrar modal de edición\"", content);

        // Pestañas editoriales
        Assert.Contains("Metadatos", content);
        Assert.Contains("Mesa y ADN", content);
        Assert.Contains("Sinopsis", content);
        Assert.Contains("Carátula", content);
        Assert.Contains("Fundas", content);

        // Soporte InputFile y URL
        Assert.Contains("<InputFile", content);
        Assert.Contains("SaveGameCoverAsync", content);

        // Galería complementaria dinámica (INC-139)
        Assert.Contains("4. Galería Complementaria", content);
        Assert.Contains("AddGalleryImageFromUrl", content);
        Assert.Contains("RemoveAdditionalImage", content);

        // Resolución en cascada de reportes
        Assert.Contains("AssociatedReportId", content);
        Assert.Contains("Guardar Ficha y Resolver Reporte", content);
    }

    [Fact]
    public void SleeveGuideCard_RazorFile_HasEditorialAndPurchaseCapabilities()
    {
        var path = Path.Combine(GetRepoRoot(), "src", "Ludeka.Web", "Components", "Shared", "SleeveGuideCard.razor");
        Assert.True(File.Exists(path), $"SleeveGuideCard.razor no encontrado en {path}");

        var content = File.ReadAllText(path);

        // Cabecera e identidad editorial
        Assert.Contains("Protege tu juego: Guía de Fundas", content);
        Assert.Contains("¿Standard o Premium?", content);
        Assert.Contains("Guía rápida de grosor", content);

        // Silueta proporcional y métricas
        Assert.Contains("ISleeveStoreUrlResolver", content);
        Assert.Contains("PacksNeeded50", content);
        Assert.Contains("PacksNeeded100", content);
        Assert.Contains("DimensionText", content);

        // Compra contextual en tiendas colaboradoras
        Assert.Contains("ResolvePurchaseOptions", content);
        Assert.Contains("Comprar en @opt.StoreName", content);
    }

    [Fact]
    public void GameDetail_RazorFile_HasModeratorEditButtonAndEditorModal()
    {
        var path = Path.Combine(GetRepoRoot(), "src", "Ludeka.Web", "Components", "Pages", "GameDetail.razor");
        Assert.True(File.Exists(path), $"GameDetail.razor no encontrado en {path}");

        var content = File.ReadAllText(path);

        // Botón de edición para moderadores
        Assert.Contains("Editar Ficha", content);
        Assert.Contains("_isEditorModalOpen = true", content);
        Assert.Contains("<GameEditorModal", content);
    }

    [Fact]
    public void GameReportsModeration_RazorFile_HasCrossActionCorrectAndResolve()
    {
        var path = Path.Combine(GetRepoRoot(), "src", "Ludeka.Web", "Components", "Pages", "GameReportsModeration.razor");
        Assert.True(File.Exists(path), $"GameReportsModeration.razor no encontrado en {path}");

        var content = File.ReadAllText(path);

        // Acción cruzada directa desde la bandeja de INC-17
        Assert.Contains("Corregir Ficha y Resolver", content);
        Assert.Contains("OpenEditorForReport", content);
        Assert.Contains("<GameEditorModal", content);
    }

    private class FakeCurrentUser : ICurrentUserService
    {
        public string UserId => "mod-1";
        public string UserName => "Moderador";
        public System.Collections.Generic.IReadOnlyList<string> Roles => ["Moderator"];
        public bool IsFoundingTeam => true;
        public bool IsInRole(string role) => true;
        public bool HasPermission(ModeratorPermission permission) => true;
    }
}
