using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato para la Ludoteca Integral (7 pestañas, modales de gestión,
/// radar de compra y PWA offline) de INC-118.
/// </summary>
public class MyLibraryContractTests
{
    private const string MyLibraryPath = "src/Ludeka.Web/Components/Pages/MyLibrary.razor";
    private const string UserLibraryPath = "src/Ludeka.Web/Components/Pages/UserLibrary.razor";
    private const string RecordPlayModalPath = "src/Ludeka.Web/Components/Shared/RecordPlayModal.razor";
    private const string LoanModalPath = "src/Ludeka.Web/Components/Shared/LoanModal.razor";

    private static string ReadSource(string relativePath)
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            var parent = Directory.GetParent(dir);
            dir = parent?.FullName;
        }

        throw new FileNotFoundException($"No se localizó el archivo fuente en la ruta {relativePath}");
    }

    [Fact]
    public void MyLibrary_ShouldContainAllSevenCanonicalTabs()
    {
        var source = ReadSource(MyLibraryPath);

        // 7 pestañas canónicas con sus identificadores de accesibilidad
        Assert.Contains("id=\"tab-incollection\"", source, StringComparison.Ordinal);
        Assert.Contains("id=\"tab-played\"", source, StringComparison.Ordinal);
        Assert.Contains("id=\"tab-wanttobuy\"", source, StringComparison.Ordinal);
        Assert.Contains("id=\"tab-loans\"", source, StringComparison.Ordinal);
        Assert.Contains("id=\"tab-queue\"", source, StringComparison.Ordinal);
        Assert.Contains("id=\"tab-stats\"", source, StringComparison.Ordinal);
        Assert.Contains("id=\"tab-radar\"", source, StringComparison.Ordinal);

        // Etiquetas legibles de las pestañas
        Assert.Contains("Mi Colección", source, StringComparison.Ordinal);
        Assert.Contains("Jugados", source, StringComparison.Ordinal);
        Assert.Contains("Deseados", source, StringComparison.Ordinal);
        Assert.Contains("En Préstamo", source, StringComparison.Ordinal);
        Assert.Contains("Cola Comunitaria", source, StringComparison.Ordinal);
        Assert.Contains("ADN y Estadísticas", source, StringComparison.Ordinal);
        Assert.Contains("Radar de Compra", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MyLibrary_ShouldMountManagementAndBggModals()
    {
        var source = ReadSource(MyLibraryPath);

        // Modales interactivos de gestión y catálogo
        Assert.Contains("<RecordPlayModal", source, StringComparison.Ordinal);
        Assert.Contains("<LoanModal", source, StringComparison.Ordinal);
        Assert.Contains("<BggImportModal", source, StringComparison.Ordinal);
        Assert.Contains("<BggSearchModal", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MyLibrary_ShouldContainPwaOfflineContingencyElements()
    {
        var source = ReadSource(MyLibraryPath);

        // Contingencia PWA Offline
        Assert.Contains("Estás fuera de", source, StringComparison.Ordinal);
        Assert.Contains("cobertura", source, StringComparison.Ordinal);
        Assert.Contains("Modo sin conexión", source, StringComparison.Ordinal);
        Assert.Contains("Reintentar conexión", source, StringComparison.Ordinal);
        Assert.Contains("Abrir Mi Ludoteca offline", source, StringComparison.Ordinal);
        Assert.Contains("LudekaOffline.isOnline", source, StringComparison.Ordinal);
        Assert.Contains("initConnectivityListener", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MyLibrary_ShouldContainCollectionFiltersAndSearch()
    {
        var source = ReadSource(MyLibraryPath);

        // Buscador y filtros rápidos de colección
        Assert.Contains("Buscar por título en tu colección...", source, StringComparison.Ordinal);
        Assert.Contains("Todos", source, StringComparison.Ordinal);
        Assert.Contains("Juegos base", source, StringComparison.Ordinal);
        Assert.Contains("Expansiones", source, StringComparison.Ordinal);
        Assert.Contains("Prestados", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UserLibrary_ShouldDeclareRoutesAndDelegateToMyLibrary()
    {
        var source = ReadSource(UserLibraryPath);

        // Rutas y delegación a MyLibrary
        Assert.Contains("@page \"/cuenta/userlibrary\"", source, StringComparison.Ordinal);
        Assert.Contains("@page \"/cuenta/coleccion\"", source, StringComparison.Ordinal);
        Assert.Contains("<MyLibrary", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ManagementModals_ShouldSupportGenericSelectionWhenNoGameIsPreselected()
    {
        var recordPlaySource = ReadSource(RecordPlayModalPath);
        Assert.Contains("AvailableGames", recordPlaySource, StringComparison.Ordinal);
        Assert.Contains("select-play-game", recordPlaySource, StringComparison.Ordinal);

        var loanSource = ReadSource(LoanModalPath);
        Assert.Contains("AvailableGames", loanSource, StringComparison.Ordinal);
        Assert.Contains("loan-game-select", loanSource, StringComparison.Ordinal);
    }
}
