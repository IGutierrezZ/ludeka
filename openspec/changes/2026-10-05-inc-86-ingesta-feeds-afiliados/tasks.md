# Tareas de Implementación (Work Units)

## WU 01: Entidades de Dominio y Contratos de Repositorio
- [x] Crear entidades `AffiliateFeedSource` y `AffiliateEanDiscrepancyLog` en `Ludeka.Core.Entities`.
- [x] Crear contratos `IAffiliateFeedSourceRepository` e `IAffiliateEanDiscrepancyRepository` en `Ludeka.Application.Contracts`.
- [x] Pruebas unitarias de las entidades en `Ludeka.UnitTests/Domain/`.

## WU 02: Infraestructura y Persistencia Dual
- [x] Configurar mapeo EF Core en `LudekaDbContext`.
- [x] Implementar `SqliteAffiliateFeedSourceRepository` y `SqliteAffiliateEanDiscrepancyRepository`.
- [x] Actualizar reconciliador `SqliteSchemaMigrator` para crear las tablas en SQLite local.
- [x] Generar migración EF Core para PostgreSQL y actualizar `supabase_schema.sql` y `PostgresSchemaVerificationTests`.

## WU 03: Parsers de Feeds en Streaming
- [x] Definir DTOs `FeedProductItem` e interfaz `IFeedParser`.
- [x] Implementar `GoogleShoppingFeedParser` con `XmlReader` para parsing eficiente de items `<item>`.
- [x] Pruebas unitarias con feeds XML reales y simulados (precios, stock, EAN válido, EAN ausente, caracteres especiales).

## WU 04: Servicio de Sincronización y Lógica de Cruce
- [x] Implementar `CatalogFeedSyncService` en `Ludeka.Application.Features.Affiliates`:
  - Cruce $O(1)$ por EAN contra catálogo (`game.Ean` y `game.AdditionalBarcodes`).
  - Auto-asignación de EAN a juegos sin código por coincidencia de título.
  - Registro de discrepancia y guardado en `game.AdditionalBarcodes` cuando el EAN del feed difiere del actual.
  - Actualización idempotente de `game.PurchaseLinks`.
- [x] Pruebas unitarias completas de la lógica de cruce y discrepancias en `Ludeka.UnitTests/Application/`.

## WU 05: Runner en Ludeka.Jobs y Registro de Tarea
- [ ] Crear `CatalogFeedSyncJobRunner` en `Ludeka.Jobs.Runners` asociado al identificador `feed-sync`.
- [ ] Registrar runner en `JobNames` y en la inyección de dependencias de `Ludeka.Jobs`.
- [ ] Pruebas unitarias de composición y ejecución del runner en `Ludeka.UnitTests/Jobs/`.

## WU 06: Panel de Administración Web (`/admin/afiliados`)
- [ ] Crear componente Blazor `src/Ludeka.Web/Components/Pages/Admin/AffiliatesAdmin.razor`.
- [ ] Pestaña 1: Listado y edición de fuentes de feeds (URL, tienda, conmutador de estado, botón "Sincronizar ahora").
- [ ] Pestaña 2: Cola de discrepancias EAN con comparativa y botón "Promover EAN a principal".
- [ ] Añadir enlace al panel en la navegación de administración/moderación.
- [ ] Pruebas unitarias de contrato y renderizado en `Ludeka.UnitTests/Web/`.

## WU 07: Verificación Integral, CI/CD y Cierre
- [ ] Ejecutar suite completa de pruebas unitarias y de integración en verde.
- [ ] Abrir PR contra `main`, esperar CI al 100%, fusionar a `main`, verificar despliegue en Google Cloud Run y limpiar worktree.
