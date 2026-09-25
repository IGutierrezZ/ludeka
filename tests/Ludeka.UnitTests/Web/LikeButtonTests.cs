using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Enums;
using Ludeka.UnitTests.Application;
using Ludeka.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Web;

public class LikeButtonTests
{
    private sealed class RecordingNavigationManager : NavigationManager
    {
        public RecordingNavigationManager(string uri = "https://ludeka.test/editoriales/devir")
        {
            Initialize("https://ludeka.test/", uri);
        }

        public string? LastTarget { get; private set; }
        public bool? LastForceLoad { get; private set; }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            LastTarget = uri;
            LastForceLoad = forceLoad;
        }
    }

    private sealed class FakeUserLikeService : IUserLikeService
    {
        public bool ToggleResult { get; set; } = true;
        public int ToggleCountResult { get; set; } = 1;
        public (Guid UserId, LikeTargetType TargetType, Guid TargetId)? LastToggle { get; private set; }

        public Task<LikeStatusDto> GetStatusAsync(Guid? userId, LikeTargetType targetType, Guid targetId, CancellationToken ct = default)
        {
            return Task.FromResult(new LikeStatusDto(false, 0));
        }

        public Task<Dictionary<Guid, LikeStatusDto>> GetStatusesAsync(Guid? userId, LikeTargetType targetType, IEnumerable<Guid> targetIds, CancellationToken ct = default)
        {
            return Task.FromResult(new Dictionary<Guid, LikeStatusDto>());
        }

        public Task<LikeStatusDto> ToggleLikeAsync(Guid userId, LikeTargetType targetType, Guid targetId, CancellationToken ct = default)
        {
            LastToggle = (userId, targetType, targetId);
            return Task.FromResult(new LikeStatusDto(ToggleResult, ToggleCountResult));
        }
    }

    private static IServiceProvider CreateServiceProvider(
        ICurrentUserService currentUserService,
        IUserLikeService likeService,
        NavigationManager navigationManager)
    {
        var services = new ServiceCollection();
        services.AddSingleton(currentUserService);
        services.AddSingleton(likeService);
        services.AddSingleton(navigationManager);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task RenderedLikeButton_WhenNotLiked_HasAppropriateAriaAndAttributes()
    {
        var targetId = Guid.NewGuid();
        var nav = new RecordingNavigationManager();
        var sp = CreateServiceProvider(StubCurrentUserService.Anonymous(), new FakeUserLikeService(), nav);
        var renderer = new HtmlRenderer(sp, NullLoggerFactory.Instance);

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<LikeButton>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["TargetType"] = LikeTargetType.Publisher,
                ["TargetId"] = targetId,
                ["InitialLikesCount"] = 5,
                ["InitialIsLiked"] = false
            }));
            return root.ToHtmlString();
        });

        Assert.Contains("aria-pressed=\"false\"", html);
        Assert.Contains("aria-label=\"Me gusta (5 me gusta)\"", html);
        Assert.Contains(">5<", html);
        Assert.Contains("fill=\"none\"", html);
    }

    [Fact]
    public async Task RenderedLikeButton_WhenLiked_HasAriaPressedTrueAndFilledHeart()
    {
        var targetId = Guid.NewGuid();
        var nav = new RecordingNavigationManager();
        var sp = CreateServiceProvider(StubCurrentUserService.Anonymous(), new FakeUserLikeService(), nav);
        var renderer = new HtmlRenderer(sp, NullLoggerFactory.Instance);

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<LikeButton>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["TargetType"] = LikeTargetType.Store,
                ["TargetId"] = targetId,
                ["InitialLikesCount"] = 8,
                ["InitialIsLiked"] = true
            }));
            return root.ToHtmlString();
        });

        Assert.Contains("aria-pressed=\"true\"", html);
        Assert.Contains("aria-label=\"Ya no me gusta (8 me gusta)\"", html);
        Assert.Contains(">8<", html);
        Assert.Contains("fill=\"currentColor\"", html);
    }

    [Fact]
    public async Task HandleClickAsync_WhenUnauthenticated_RedirectsToLoginWithReturnUrl()
    {
        var targetId = Guid.NewGuid();
        var nav = new RecordingNavigationManager("https://ludeka.test/tiendas/zacatrus");
        var likeService = new FakeUserLikeService();
        var anonUser = StubCurrentUserService.Anonymous();

        var component = new LikeButton();

        // Inyectar dependencias por reflexión
        SetInjectedProperty(component, "Navigation", nav);
        SetInjectedProperty(component, "UserLikeService", likeService);
        SetInjectedProperty(component, "CurrentUserService", anonUser);

#pragma warning disable BL0005
        component.TargetType = LikeTargetType.Store;
        component.TargetId = targetId;
        component.InitialLikesCount = 3;
#pragma warning restore BL0005

        await InvokeHandleClickAsync(component);

        // Debe haber redirigido a login preservando returnUrl
        Assert.NotNull(nav.LastTarget);
        Assert.StartsWith("/login?ReturnUrl=", nav.LastTarget);
        Assert.Contains("%2Ftiendas%2Fzacatrus", nav.LastTarget);
        Assert.True(nav.LastForceLoad);
        Assert.Null(likeService.LastToggle); // No debe haber llamado a toggle en el servicio
    }

    [Fact]
    public async Task HandleClickAsync_WhenAuthenticated_CallsServiceAndUpdatesLocalState()
    {
        var targetId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var nav = new RecordingNavigationManager();
        var likeService = new FakeUserLikeService
        {
            ToggleResult = true,
            ToggleCountResult = 4
        };
        var authedUser = StubCurrentUserService.WithSession(userId.ToString(), "TestUser");

        var component = new LikeButton();
        SetInjectedProperty(component, "Navigation", nav);
        SetInjectedProperty(component, "UserLikeService", likeService);
        SetInjectedProperty(component, "CurrentUserService", authedUser);

#pragma warning disable BL0005
        component.TargetType = LikeTargetType.Creator;
        component.TargetId = targetId;
        component.InitialLikesCount = 3;
        component.InitialIsLiked = false;

        int? callbackLikes = null;
        component.OnLikesCountChanged = EventCallback.Factory.Create<int>(this, cnt => callbackLikes = cnt);
#pragma warning restore BL0005

        await InvokeHandleClickAsync(component);

        // Debe haber ejecutado el toggle con los parámetros correctos
        Assert.NotNull(likeService.LastToggle);
        Assert.Equal(userId, likeService.LastToggle.Value.UserId);
        Assert.Equal(LikeTargetType.Creator, likeService.LastToggle.Value.TargetType);
        Assert.Equal(targetId, likeService.LastToggle.Value.TargetId);
        Assert.Null(nav.LastTarget); // No redirige porque está autenticado
        Assert.Equal(4, callbackLikes);
    }

    private static void SetInjectedProperty(object target, string propertyName, object value)
    {
        var prop = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Propiedad '{propertyName}' no encontrada en '{target.GetType().Name}'.");
        prop.SetValue(target, value);
    }

    private static Task InvokeHandleClickAsync(LikeButton button)
    {
        var method = typeof(LikeButton).GetMethod("HandleClickAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Método 'HandleClickAsync' no encontrado en LikeButton.");
        return (Task)method.Invoke(button, null)!;
    }
}
