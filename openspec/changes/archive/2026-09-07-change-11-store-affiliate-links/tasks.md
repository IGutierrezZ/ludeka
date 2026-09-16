# Tareas: change-11-store-affiliate-links (Incremento 11)

## Fase 1: Dominio y Entidades (`Ludeka.Core`)
- [x] 1.1 Crear el Value Object `GamePurchaseLink.cs` en `src/Ludeka.Core/ValueObjects/`.
- [x] 1.2 Añadir colección `PurchaseLinks` y métodos de gestión a `Game.cs`.
- [x] 1.3 Extender el constructor de `Game` manteniendo total compatibilidad con valores por defecto.
- [x] 1.4 Crear pruebas unitarias de dominio en `tests/Ludeka.UnitTests/Domain/GamePurchaseLinkTests.cs`.

## Fase 2: Persistencia e Infraestructura (`Ludeka.Infrastructure`)
- [x] 2.1 Configurar mapeo `OwnsMany(g => g.PurchaseLinks, b => b.ToJson())` en `LudekaDbContext.cs`.
- [x] 2.2 Añadir reconciliación de columna `PurchaseLinks` en `SqliteSchemaMigrator.cs`.
- [x] 2.3 Actualizar modelo de deserialización en `CatalogSeeder.cs`.
- [x] 2.4 Incorporar ofertas de tiendas con enlaces de afiliados de ejemplo en `seed-games.json`.
- [x] 2.5 Crear pruebas de persistencia y migración en `SqlitePurchaseLinksPersistenceTests.cs`.

## Fase 3: Aplicación y DTOs (`Ludeka.Application`)
- [x] 3.1 Añadir `PurchaseLinks` a `GameDetailDto.cs` y mapeo en `FromEntity`.
- [x] 3.2 Verificar o adaptar servicios de catálogo existentes sin breaking changes.

## Fase 4: Interfaz de Usuario Blazor (`Ludeka.Web`)
- [x] 4.1 Crear el componente `StoreOffersCard.razor` en `src/Ludeka.Web/Components/Shared/`.
- [x] 4.2 Integrar `StoreOffersCard` en `GameDetail.razor` para juegos base y expansiones.
- [x] 4.3 Añadir botón o acceso rápido contextual en la cabecera/acciones de la ficha.
- [x] 4.4 Verificar accesibilidad (WCAG 2.2 AA) y estilos semánticos CSS consistentes con las 4 paletas de color.

## Fase 5: Verificación Integral y Suite de Pruebas
- [x] 5.1 Ejecutar suite completa de pruebas unitarias (`dotnet test`).
- [x] 5.2 Comprobar migración automática y ausencia de errores de arranque en desarrollo.
- [x] 5.3 Generar informe de verificación `verify-report.md`.
