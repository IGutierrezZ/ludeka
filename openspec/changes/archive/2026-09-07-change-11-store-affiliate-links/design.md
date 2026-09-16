# Diseño Técnico: change-11-store-affiliate-links (Incremento 11)

## 1. Arquitectura y Componentes

### 1.1 Modelo de Dominio (`Ludeka.Core`)
- **`Ludeka.Core.ValueObjects.GamePurchaseLink`**:
  ```csharp
  public record GamePurchaseLink(
      string StoreName,
      string AffiliateUrl,
      decimal? Price = null,
      string Currency = "€",
      bool InStock = true,
      string? Badge = null,
      string? AffiliateTag = null);
  ```
- **Extensión de `Ludeka.Core.Entities.Game`**:
  - `public List<GamePurchaseLink> PurchaseLinks { get; private set; } = [];`
  - Constructor extendido con `IEnumerable<GamePurchaseLink>? purchaseLinks = null`.
  - Métodos `AddPurchaseLink`, `ClearPurchaseLinks`, `UpdatePurchaseLinks`.

### 1.2 Persistencia (`Ludeka.Infrastructure`)
- **`LudekaDbContext.cs`**:
  ```csharp
  game.OwnsMany(g => g.PurchaseLinks, b => b.ToJson());
  ```
- **`SqliteSchemaMigrator.cs`**:
  - Reconciliar columna `PurchaseLinks` en la tabla `Games`:
    `("PurchaseLinks", "TEXT NOT NULL DEFAULT '[]'")`
  - Actualización preventiva:
    `UPDATE "Games" SET "PurchaseLinks" = '[]' WHERE "PurchaseLinks" IS NULL;`

### 1.3 Carga y Datos Semilla (`Ludeka.Infrastructure.Seeding`)
- **`CatalogSeeder.cs`**:
  - Incorporar clase interna `SeedPurchaseLinkModel`:
    ```csharp
    private class SeedPurchaseLinkModel
    {
        public string StoreName { get; set; } = string.Empty;
        public string AffiliateUrl { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public string Currency { get; set; } = "€";
        public bool InStock { get; set; } = true;
        public string? Badge { get; set; }
        public string? AffiliateTag { get; set; }
    }
    ```
  - Mapear `PurchaseLinks` al instanciar `Game`.
- **`seed-games.json`**:
  - Añadir ofertas iniciales para los juegos del catálogo precargado (ej. *Wingspan*, *Catan*, *Terraforming Mars*, *Dune: Imperium*, etc.) apuntando a tiendas reales de referencia con parámetro `?ref=ludeka`.

### 1.4 Aplicación (`Ludeka.Application`)
- **`GameDetailDto.cs`**:
  - Añadir propiedad `IReadOnlyList<GamePurchaseLink> PurchaseLinks`.
  - Asignar en `FromEntity`: `g.PurchaseLinks.AsReadOnly()`.

### 1.5 Interfaz de Usuario (`Ludeka.Web`)
- **`StoreOffersCard.razor`**:
  - Componente autónomo ubicado en `Components/Shared/`.
  - Parámetros: `IReadOnlyList<GamePurchaseLink>? Offers`, `string GameTitle`.
  - Renderizado de tarjetas individuales por tienda con diseño editorial:
    - Logotipo/Icono contextual (🛒 para tiendas online generales, 🏬 para tiendas locales especializadas, 📦 para distribuidores/marketplace).
    - Nombre de la tienda y distintivo (`Badge`).
    - Precio en formato español (`Price.Value.ToString("0.00") + " " + Currency`).
    - Indicador de stock con color semántico accesible.
    - Botón `Ver en tienda ↗` con atributos `target="_blank" rel="noopener noreferrer sponsored"`.
  - Bloque inferior explicativo de apoyo ético con enlace a `/transparencia`.
- **Integración en `GameDetail.razor`**:
  - Ubicación en el cuerpo principal de la ficha (sección editorial / utilidades junto a `SleeveGuideCard`).
  - Enlace o botón rápido en la barra de acciones o cabecera de la ficha ("🛒 Dónde comprar") que dirija directamente al bloque.
