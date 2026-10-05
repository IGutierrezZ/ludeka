using System;
using System.IO;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato y arquitectura para el rediseño editorial de la ficha de juego (INC-85).
/// Valida la distribución de dos columnas asimétricas, la presencia permanente del carrusel fotográfico,
/// la visibilidad fija de la sección de tiendas con fallback nacional y la agrupación en pestañas secundarias.
/// </summary>
public class GameDetailEditorialContractTests
{
    private const string GameDetailPath = "src/Ludeka.Web/Components/Pages/GameDetail.razor";
    private const string GameImageCarouselPath = "src/Ludeka.Web/Components/Shared/GameImageCarousel.razor";
    private const string StoreOffersCardPath = "src/Ludeka.Web/Components/Shared/StoreOffersCard.razor";

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
    public void GameDetail_ShouldImplementEditorialTwoColumnLayout()
    {
        var source = ReadSource(GameDetailPath);

        // Estructura de rejilla editorial asimétrica de 12 columnas (8 para contenido principal, 4 fija lateral)
        Assert.Contains("grid grid-cols-1 lg:grid-cols-12", source, StringComparison.Ordinal);
        Assert.Contains("lg:col-span-8", source, StringComparison.Ordinal);
        Assert.Contains("lg:col-span-4", source, StringComparison.Ordinal);
        Assert.Contains("lg:sticky", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GameDetail_ShouldEmbedImageCarouselAndSecondaryTabs()
    {
        var source = ReadSource(GameDetailPath);

        // Componente de carrusel fotográfico incrustado con bindings
        Assert.Contains("<GameImageCarousel", source, StringComparison.Ordinal);
        Assert.Contains("CoverImageUrl=\"@Game.CoverImageUrl\"", source, StringComparison.Ordinal);
        Assert.Contains("BackCoverImageUrl=\"@Game.BackCoverImageUrl\"", source, StringComparison.Ordinal);
        Assert.Contains("TableImageUrl=\"@Game.TableImageUrl\"", source, StringComparison.Ordinal);

        // Pestañas secundarias agrupadas para evitar scroll infinito
        Assert.Contains("_activeSecondaryTab", source, StringComparison.Ordinal);
        Assert.Contains("Guía de Fundas", source, StringComparison.Ordinal);
        Assert.Contains("Hub Multimedia", source, StringComparison.Ordinal);
        Assert.Contains("Consultorio de Reglas", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GameDetail_ShouldKeepStoresModuleAlwaysVisibleInSidebar()
    {
        var source = ReadSource(GameDetailPath);

        // La tarjeta de ofertas de tiendas debe estar presente en la columna lateral fija
        Assert.Contains("<StoreOffersCard", source, StringComparison.Ordinal);
        Assert.Contains("IsSidebar=\"true\"", source, StringComparison.Ordinal);
        Assert.Contains("Offers=\"@Game.PurchaseLinks\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GameImageCarousel_ShouldSupportThumbnailsAndLightbox()
    {
        var source = ReadSource(GameImageCarouselPath);

        // Soporte de navegación, miniaturas y modal Lightbox a pantalla completa
        Assert.Contains("PreviousSlide", source, StringComparison.Ordinal);
        Assert.Contains("NextSlide", source, StringComparison.Ordinal);
        Assert.Contains("SelectSlide", source, StringComparison.Ordinal);
        Assert.Contains("OpenLightbox", source, StringComparison.Ordinal);
        Assert.Contains("CloseLightbox", source, StringComparison.Ordinal);
        Assert.Contains("role=\"dialog\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-modal=\"true\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreOffersCard_ShouldSupportSidebarModeAndReferenceStoreFallbacks()
    {
        var source = ReadSource(StoreOffersCardPath);

        // Parámetro de barra lateral
        Assert.Contains("public bool IsSidebar { get; set; }", source, StringComparison.Ordinal);

        // Tiendas de referencia españolas con fallback determinista de búsqueda
        Assert.Contains("Zacatrus", source, StringComparison.Ordinal);
        Assert.Contains("Cuarto de Juegos", source, StringComparison.Ordinal);
        Assert.Contains("Dracotienda", source, StringComparison.Ordinal);
        Assert.Contains("Jugamos Otra", source, StringComparison.Ordinal);
        Assert.Contains("_isReferenceFallback", source, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreOffersCard_ShouldPresentHonestSearchLinksWhenReferenceFallback()
    {
        var source = ReadSource(StoreOffersCardPath);

        // Mensaje transparente sin falso stock ni tarjetas engañosas
        Assert.Contains("Sin precios ni stock confirmados", source, StringComparison.Ordinal);
        Assert.Contains("Buscar en @offer.StoreName", source, StringComparison.Ordinal);

        // No debe activar comprobación de stock de fondo si es fallback de catálogo
        Assert.Contains("!_isReferenceFallback", source, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreOffersCard_ShouldAlwaysRetainAmazonAffiliateOffer_WhenOtherStoreOffersExist()
    {
        var source = ReadSource(StoreOffersCardPath);

        // Debe garantizar que Amazon esté presente aunque existan ofertas de otras tiendas
        Assert.Contains("CreateAmazonSearchOffer", source, StringComparison.Ordinal);
        Assert.Contains("!_displayedOffers.Any(o => string.Equals(o.StoreName, \"Amazon\", StringComparison.OrdinalIgnoreCase))", source, StringComparison.Ordinal);

        // Las opciones sin precio explícito no deben marcarse como agotadas (StoreStockInfo.Unknown)
        Assert.Contains("StoreStockInfo.Unknown(\"Consultar en catálogo\")", source, StringComparison.Ordinal);

        // El botón para ofertas de búsqueda directa debe invitar a buscar en la tienda específica
        Assert.Contains("Buscar en @offer.StoreName", source, StringComparison.Ordinal);
    }
}
