using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato para el sistema de avisos, campana de notificaciones, Web Push
/// y contador administrativo real (INC-149).
/// </summary>
public class UserNotificationsUiContractTests
{
    private const string AccountMenuPath = "src/Ludeka.Web/Components/Shared/AccountMenu.razor";
    private const string AccountNotificationsPath = "src/Ludeka.Web/Components/Pages/AccountNotifications.razor";
    private const string AccountPath = "src/Ludeka.Web/Components/Pages/Account.razor";
    private const string GameDetailPath = "src/Ludeka.Web/Components/Pages/GameDetail.razor";
    private const string StoreOffersCardPath = "src/Ludeka.Web/Components/Shared/StoreOffersCard.razor";
    private const string ServiceWorkerPath = "src/Ludeka.Web/wwwroot/service-worker.js";
    private const string LudekaPushJsPath = "src/Ludeka.Web/wwwroot/js/ludeka-push.js";

    [Fact]
    public void AccountMenu_ShouldRenderRedUnreadBadgeBellItemAndRealAdminCount()
    {
        var source = ReadSource(AccountMenuPath);

        // Globo circular rojo cuando el menú no está desplegado
        Assert.Contains("bg-rose-600 text-white", source, StringComparison.Ordinal);
        Assert.Contains("HeaderBadgeCount", source, StringComparison.Ordinal);

        // Enlace a /cuenta/notificaciones con icono de campana
        Assert.Contains("href=\"/cuenta/notificaciones\"", source, StringComparison.Ordinal);
        Assert.Contains("Icon Name=\"bell\"", source, StringComparison.Ordinal);
        Assert.Contains("Notificaciones", source, StringComparison.Ordinal);

        // El panel de gestión utiliza IAdminPendingSummaryService sin el número fijo "21"
        Assert.Contains("AdminPendingService.GetPendingSummaryAsync()", source, StringComparison.Ordinal);
        Assert.DoesNotContain(": \"21\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountNotificationsPage_ShouldRequireExplicitClickToMarkReadAndSupportWebPushAndGiveaways()
    {
        var source = ReadSource(AccountNotificationsPath);

        Assert.Contains("@page \"/cuenta/notificaciones\"", source, StringComparison.Ordinal);
        Assert.Contains("@attribute [Authorize]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("<AccountSectionNav", source, StringComparison.Ordinal);

        // Cada notificación requiere clic explícito para marcarse como leída
        Assert.Contains("MarkNotificationAsReadAsync(item)", source, StringComparison.Ordinal);
        Assert.Contains("NotificationService.MarkAsReadAsync", source, StringComparison.Ordinal);

        // Soporte de activación Web Push en móvil (PWA) y navegador
        Assert.Contains("ludekaPush.subscribePush", source, StringComparison.Ordinal);
        Assert.Contains("ludekaPush.unsubscribePush", source, StringComparison.Ordinal);

        // Switch para avisos de nuevos sorteos
        Assert.Contains("Avisarme cuando se publique un nuevo sorteo", source, StringComparison.Ordinal);
        Assert.Contains("SetNotifyNewGiveawaysAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GameDetail_ShouldExposeThreeSeparateAlertButtonsAndWantToBuyPriceDropPrompt()
    {
        var source = ReadSource(GameDetailPath);

        // Tres botones separados en la ficha del juego
        Assert.Contains("ToggleGameAlertAsync(GameAlertType.PriceDrop)", source, StringComparison.Ordinal);
        Assert.Contains("ToggleGameAlertAsync(GameAlertType.Reprint)", source, StringComparison.Ordinal);
        Assert.Contains("ToggleGameAlertAsync(GameAlertType.Restock)", source, StringComparison.Ordinal);

        Assert.Contains("Avísame de oferta (-15% PVP)", source, StringComparison.Ordinal);
        Assert.Contains("Avísame de reimpresión", source, StringComparison.Ordinal);
        Assert.Contains("Avísame de reestock", source, StringComparison.Ordinal);

        // Modal que pregunta al marcar «Lo quiero» si desea activar el aviso de bajada de precio (-15%)
        Assert.Contains("_isPriceDropPromptModalOpen", source, StringComparison.Ordinal);
        Assert.Contains("HandlePriceDropPromptYes", source, StringComparison.Ordinal);
        Assert.Contains("HandlePriceDropPromptNo", source, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreOffersCard_And_ServiceWorker_ShouldSupportRestockAlertAndWebPushEvents()
    {
        var storeOffersSource = ReadSource(StoreOffersCardPath);
        Assert.Contains("ToggleRestockAlertAsync", storeOffersSource, StringComparison.Ordinal);
        Assert.Contains("Avísame de reestock", storeOffersSource, StringComparison.Ordinal);

        var swSource = ReadSource(ServiceWorkerPath);
        Assert.Contains("self.addEventListener('push'", swSource, StringComparison.Ordinal);
        Assert.Contains("self.addEventListener('notificationclick'", swSource, StringComparison.Ordinal);

        var pushJsSource = ReadSource(LudekaPushJsPath);
        Assert.Contains("window.ludekaPush", pushJsSource, StringComparison.Ordinal);
        Assert.Contains("pushManager.subscribe", pushJsSource, StringComparison.Ordinal);

        var accountSource = ReadSource(AccountPath);
        Assert.Contains("ToggleNotifyGiveawaysAsync", accountSource, StringComparison.Ordinal);
    }

    private static string ReadSource(string relativePath)
    {
        var path = Path.Combine(GetRepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"No se encontró el archivo fuente: {relativePath}");
        return File.ReadAllText(path);
    }

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Ludeka.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se pudo localizar la raíz de la solución.");
    }
}
