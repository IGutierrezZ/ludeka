using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Enums;
using Ludeka.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Web;

public class MilestonesCardTests
{
    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task MilestonesCard_WhenProgressIsNull_RendersEmptyPlaceholder()
    {
        // Arrange
        var sp = CreateServiceProvider();
        var renderer = new HtmlRenderer(sp, NullLoggerFactory.Instance);

        // Act
        var rawHtml = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<MilestonesCard>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Progress"] = null
            }));
            return root.ToHtmlString();
        });
        var html = WebUtility.HtmlDecode(rawHtml);

        // Assert
        Assert.Contains("Cargando hitos", html);
    }

    [Fact]
    public async Task MilestonesCard_WhenProgressProvided_RendersCountAndPercentage()
    {
        // Arrange
        var sp = CreateServiceProvider();
        var renderer = new HtmlRenderer(sp, NullLoggerFactory.Instance);

        var unlockDate = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var milestones = new List<MilestoneDto>
        {
            new(MilestoneType.FirstGameInCollection, MilestoneCategory.Collection, "Primera Piedra", "Añadir el primer juego a tu colección.", "📦", 1, true, unlockDate),
            new(MilestoneType.TenGamesInCollection, MilestoneCategory.Collection, "Estantería Viva", "Alcanzar 10 títulos.", "📚", 2, false, null)
        };

        var progress = new UserMilestoneProgressDto("user-1", 1, 2, 50.0, milestones);

        // Act
        var rawHtml = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<MilestonesCard>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Progress"] = progress
            }));
            return root.ToHtmlString();
        });
        var html = WebUtility.HtmlDecode(rawHtml);

        // Assert
        Assert.Contains("1 de 2", html);
        Assert.Contains("50%", html);
        Assert.Contains("role=\"progressbar\"", html);
        Assert.Contains("aria-valuenow=\"50\"", html);
    }

    [Fact]
    public async Task MilestonesCard_WhenMilestoneIsUnlocked_RendersUnlockedBadgeAndTitle()
    {
        // Arrange
        var sp = CreateServiceProvider();
        var renderer = new HtmlRenderer(sp, NullLoggerFactory.Instance);

        var unlockDate = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var milestones = new List<MilestoneDto>
        {
            new(MilestoneType.FirstGameInCollection, MilestoneCategory.Collection, "Primera Piedra", "Añadir el primer juego a tu colección.", "📦", 1, true, unlockDate)
        };

        var progress = new UserMilestoneProgressDto("user-1", 1, 1, 100.0, milestones);

        // Act
        var rawHtml = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<MilestonesCard>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Progress"] = progress
            }));
            return root.ToHtmlString();
        });
        var html = WebUtility.HtmlDecode(rawHtml);

        // Assert
        Assert.Contains("Primera Piedra", html);
        Assert.Contains("📦", html);
        Assert.Contains("Desbloqueado", html);
    }

    [Fact]
    public async Task MilestonesCard_WhenMilestoneIsLocked_RendersLockedState()
    {
        // Arrange
        var sp = CreateServiceProvider();
        var renderer = new HtmlRenderer(sp, NullLoggerFactory.Instance);

        var milestones = new List<MilestoneDto>
        {
            new(MilestoneType.TenGamesInCollection, MilestoneCategory.Collection, "Estantería Viva", "Alcanzar 10 títulos.", "📚", 2, false, null)
        };

        var progress = new UserMilestoneProgressDto("user-1", 0, 1, 0.0, milestones);

        // Act
        var rawHtml = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<MilestonesCard>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Progress"] = progress
            }));
            return root.ToHtmlString();
        });
        var html = WebUtility.HtmlDecode(rawHtml);

        // Assert
        Assert.Contains("Estantería Viva", html);
        Assert.Contains("Por desbloquear", html);
    }
}
