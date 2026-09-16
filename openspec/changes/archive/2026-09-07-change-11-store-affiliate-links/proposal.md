# Propuesta: change-11-store-affiliate-links (Incremento 11: Enlaces de Compra en Tiendas y Afiliados para Juegos)

## 1. Resumen Ejecutivo y Motivación

En Ludeka disponemos de una sección técnica de **Guía de Fundas (Sleeves)** con cálculo automático de paquetes y enlaces de compra contextuales (afiliados para fundas). Sin embargo, en la ficha del propio juego (tanto para el juego base como para sus expansiones) **no existía ninguna sección dedicada a enlaces de compra de tiendas físicas u online** donde los usuarios puedan adquirir la caja del juego y donde el proyecto pueda monetizar éticamente a través de enlaces de afiliación (Zacatrus, Cuarto de Juegos, Amazon, Crash Comics, Tablerum, etc.).

Tal como establece nuestro **Manifiesto de Transparencia (`/transparencia`)**:
> *"Nuestras únicas vías de ingreso provienen de enlaces de compra contextuales (afiliación con tiendas de confianza y Amazon) y aportaciones voluntarias de mecenazgo en Ko-fi."*

Esta propuesta formaliza el **Incremento 11** bajo la metodología **Spec-Driven Development (SDD)**, añadiendo soporte integral de primer nivel para ofertas de compra de tiendas especializadas con enlaces de afiliados transparentes, precios orientativos, estado de stock y distintivos de servicio.

---

## 2. Decisiones de Dominio y Arquitectura

### 2.1 Value Object `GamePurchaseLink` (`Ludeka.Core.ValueObjects`)
Representa una oferta o enlace de compra de una tienda especializada para un juego o expansión:
- `string StoreName`: Nombre comercial de la tienda (ej. `"Zacatrus"`, `"Cuarto de Juegos"`, `"Amazon"`, `"Generación X"`, `"Crash Comics"`).
- `string AffiliateUrl`: Enlace web con el identificador o parámetro de afiliado configurado (ej. `https://zacatrus.es/wingspan.html?ref=ludeka`).
- `decimal? Price`: Precio en euros si está disponible (ej. `49.99m`), opcional.
- `string Currency`: Moneda (`"EUR"` o `"€"` por defecto).
- `bool InStock`: Indica si el producto está en existencias inmediatas (`true`) o en reserva / bajo pedido (`false`).
- `string? Badge`: Distintivo editorial opcional (ej. `"Envío 24h"`, `"Mejor Precio"`, `"Tienda Local"`, `"Reserva"`).
- `string? AffiliateTag`: Identificador de la red de afiliación (ej. `"Direct"`, `"Awin"`, `"Amazon Associates"`).

### 2.2 Extensión de la Entidad `Game` (`Ludeka.Core.Entities`)
- Propiedad encapsulada:
  `public List<GamePurchaseLink> PurchaseLinks { get; private set; } = [];`
- Métodos de manipulación de dominio:
  - `AddPurchaseLink(GamePurchaseLink link)`
  - `UpdatePurchaseLinks(IEnumerable<GamePurchaseLink> links)`
  - `ClearPurchaseLinks()`
- Parámetro opcional en el constructor de `Game`:
  `IEnumerable<GamePurchaseLink>? purchaseLinks = null` (por defecto `null`), garantizando cero breaking changes en tests y constructores previos.

---

## 3. Persistencia e Infraestructura (`Ludeka.Infrastructure`)

### 3.1 Mapeo EF Core 10 (`LudekaDbContext`)
- Mapeo nativo JSON en columna de base de datos:
  `game.OwnsMany(g => g.PurchaseLinks, b => b.ToJson());`
- Alineado idénticamente con el patrón de `Scalability` y `Sleeves`.

### 3.2 Migración Defensiva de Esquema (`SqliteSchemaMigrator`)
- Incorporación de la columna `PurchaseLinks` (`TEXT NOT NULL DEFAULT '[]'`) a la tabla `Games` durante el arranque de la aplicación si no existiera previamente.
- Evita cualquier error o incoherencia en bases de datos SQLite locales de desarrollo o contenedores ya iniciados.

### 3.3 Datos Semilla Iniciales (`CatalogSeeder` y `seed-games.json`)
- Ampliación del modelo `SeedGameModel` para deserializar ofertas de tiendas de ejemplo en los 15 juegos precargados (con tiendas reales del ecosistema español: Zacatrus, Amazon, Cuarto de Juegos, etc.).

---

## 4. Capa de Aplicación (`Ludeka.Application`)

### 4.1 Proyección DTO (`GameDetailDto`)
- Campo `IReadOnlyList<GamePurchaseLink> PurchaseLinks` en el record `GameDetailDto`.
- Mapeo automático en `GameDetailDto.FromEntity(Game g)`.

---

## 5. Diseño Visual y Componentes UI (`Ludeka.Web`)

### 5.1 Componente `StoreOffersCard.razor`
- Tarjeta editorial sobria (estilo Letterboxd / editorial lúdico, sin estridencias ni estética publicitaria de spam).
- Cumplimiento de accesibilidad WCAG 2.2 AA y atributos de seguridad:
  `target="_blank" rel="noopener noreferrer sponsored"`.
- Desglose ordenado de opciones:
  - Nombre de la tienda con distintivo lúdico (🛒 / 🏬).
  - Precio destacado en tipografía nítida si está tipificado.
  - Indicador de stock (`En Stock` en verde suave o `Bajo pedido`).
  - Botón de acción directo: `"Ver en Tienda ↗"`.
- Microtexto ético y enlace al manifiesto de transparencia:
  *"Comprando a través de estos enlaces apoyas a Ludeka de forma 100% transparente y sin coste extra para ti."*
- Estado vacío amigable cuando un juego no tenga ofertas vinculadas (con invitación opcional para sugerir tiendas locales).

### 5.2 Ubicación en `GameDetail.razor`
- Posicionamiento estratégico:
  1. Bloque visible dedicado en la vista de detalle de juego y expansión (junto a la Guía de Fundas).
  2. Botón de acceso directo / ancla opcional en la cabecera o barra de acciones para que el usuario pueda saltar directamente a las ofertas disponibles.

---

## 6. Verificación y Calidad
- Pruebas unitarias de dominio para `GamePurchaseLink` y colección de `Game`.
- Pruebas de integración de persistencia SQLite con `LudekaDbContext` y `SqliteSchemaMigrator`.
- Pruebas de carga del catálogo con `GameDetailDto`.
- Verificación de renderizado y ausencia de regresiones en la suite completa de tests de la solución.
