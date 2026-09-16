using System;
using System.IO;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Reports;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ludeka.UnitTests.Web;

public class GameReportsWebIntegrationTests
{
    [Fact]
    public void DependencyInjection_RegistersGameIssueReportServicesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        services.AddDbContext<LudekaDbContext>(opts => opts.UseSqlite(connection));
        services.AddScoped<IGameIssueReportRepository, SqliteGameIssueReportRepository>();
        services.AddScoped<IGameIssueReportService, GameIssueReportService>();

        var provider = services.BuildServiceProvider();

        // Act
        var repo = provider.GetService<IGameIssueReportRepository>();
        var service = provider.GetService<IGameIssueReportService>();

        // Assert
        Assert.NotNull(repo);
        Assert.NotNull(service);
        Assert.IsType<SqliteGameIssueReportRepository>(repo);
        Assert.IsType<GameIssueReportService>(service);
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
    public void GameReportModal_RazorFile_HasAccessibleAttributesAndAllIssueTypes()
    {
        // Assert file exists and contains WCAG 2.2 AA accessibility attributes
        var path = Path.Combine(GetRepoRoot(), "src", "Ludeka.Web", "Components", "Shared", "GameReportModal.razor");
        Assert.True(File.Exists(path), $"GameReportModal.razor no encontrado en {path}");

        var content = File.ReadAllText(path);
        Assert.Contains("role=\"dialog\"", content);
        Assert.Contains("aria-modal=\"true\"", content);
        Assert.Contains("aria-labelledby=\"report-modal-title\"", content);
        Assert.Contains("WrongImage", content);
        Assert.Contains("BrokenImage", content);
        Assert.Contains("BrokenPurchaseLink", content);
    }

    [Fact]
    public void GameReportsModeration_RazorFile_HasExpectedRoutesAndAuthorizationCheck()
    {
        var path = Path.Combine(GetRepoRoot(), "src", "Ludeka.Web", "Components", "Pages", "GameReportsModeration.razor");
        Assert.True(File.Exists(path), $"GameReportsModeration.razor no encontrado en {path}");

        var content = File.ReadAllText(path);
        Assert.Contains("@page \"/moderacion/reportes\"", content);
        Assert.Contains("@page \"/admin/reportes\"", content);
        // INC-46 F3: el marcado refleja el permiso de la política, ya sin la identidad simulada.
        Assert.Contains("CurrentUserService.HasPermission(ModeratorPermission.CanResolveReports)", content);
        Assert.Contains("Pendientes", content);
        Assert.Contains("En Revisión", content);
        Assert.Contains("Resueltos", content);
    }
}
