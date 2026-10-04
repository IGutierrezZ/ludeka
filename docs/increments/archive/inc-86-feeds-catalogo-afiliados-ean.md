# Incremento 86: Ingesta de Feeds de Catálogo Comerciales, Gestión de Afiliados en Moderación y Mapeo EAN de Juegos

> **ID:** INC-86  
> **Slug:** `feeds-catalogo-afiliados-ean`  
> **Rama:** `inc/ingesta-feeds-afiliados`  
> **Estado:** ✅ Archivado (Fase 2 & 3: Ingesta de Feeds, Auto-asignación EAN, Job Nocturno y Panel Web)  
> **Módulos Impactados:** Módulo 01 (`docs/specs/sistema/01-catalogo-y-fichas.md`), Módulo 20 (`docs/specs/sistema/20-verificacion-stock-tiempo-real-tiendas.md`), Módulo 25 (`docs/specs/sistema/25-motor-afiliados-y-atribucion-comunitaria.md`), Módulo 14 (`docs/specs/sistema/14-gestion-usuarios-permisos-y-auditoria.md`), `src/Ludeka.Core/Entities/Game.cs`, `src/Ludeka.Application/Contracts/`, `src/Ludeka.Jobs/Jobs/`, `src/Ludeka.Web/Components/Pages/AffiliatesAdmin.razor`  
> **Dependencias:** INC-85 (Ficha Editorial y Dónde Comprar), INC-27 (Verificación Stock Tiempo Real), INC-37 (Motor Afiliados)

---

## 1. Contexto y Diagnóstico

Tras la resolución del falso stock en la ficha de juego (PR #154) y la adopción de un fallback transparente sin engaños visuales, surge la necesidad de alimentar el catálogo con **datos comerciales verídicos y actualizados**:
1. **Ausencia de Código EAN en Catálogo:** La entidad `Game` carece actualmente de campos para almacenar códigos de barras internacionales (EAN-13 / GTIN / UPC). Los juegos se indexan por `BggId` y títulos, lo cual impide el cruce automatizado contra los inventarios de los comercios (que nunca indexan por nombre exacto de BGG sino por código de barras de la edición física en castellano).
2. **Gestión Estática de Afiliados:** La configuración de tiendas y redes de afiliados reside en `appsettings.json` y `AffiliateOptions`. La administración y moderación no dispone de un panel visual para añadir nuevas tiendas asociadas, configurar URLs privadas de feeds (Google Merchant XML o CSV) o pausar integraciones.
3. **Inviabilidad del Scraping en Caliente:** Consultar tiendas web en tiempo real durante la navegación del usuario satura los recursos, sufre bloqueos por WAF/Cloudflare (HTTP 403) y provoca demoras. La solución industrial estándar adoptada por los agregadores de referencia consiste en la **ingesta desatendida y periódica de feeds de catálogo**.

---

## 2. Solución Arquitectónica y Objetivos

1. **Soporte de Código EAN-13 en la Entidad `Game`:**
   - Incorporar a `Game` el código `Ean` principal de la edición de referencia en español, así como soporte para colección de códigos de barras adicionales (`Barcodes`) para ediciones multilingües o reimpresiones.
   - Habilitar la edición de EAN en el modal de edición de fichas (`GameEditorModal.razor`) y explorar la extracción automatizada desde versiones de BGG XMLAPI2 (`thing?id=...&versions=1`).
2. **Entidad y Repositorio de Fuentes de Afiliados (`AffiliateFeedSource`):**
   - Modelar en `Ludeka.Core` la entidad `AffiliateFeedSource`:
     - `StoreName`, `FeedFormat` (GoogleShoppingXml, PrestaShopCsv, ShopifyJson, AwinApi), `FeedUrl`, `AffiliateTag`, `Country`, `IsEnabled`, `SyncIntervalHours`, `LastSyncUtc`, `LastSyncStatus`, `MatchedProductsCount`.
   - Persistencia dual SQLite y PostgreSQL mediante EF Core.
3. **Panel de Gestión de Afiliados en Moderación (`/admin/afiliados`):**
   - Interfaz en Blazor Web App integrada en el panel de administración bajo permiso RBAC `CanManageStoreLinks` (`PermisoGestionarTiendas`).
   - Formulario para dar de alta nuevas fuentes de feeds con prueba de conexión previa (descarga de cabeceras).
   - Listado interactivo con conmutador de estado (activar/pausar feed), estadísticas de la última sincronización y botón para forzar sincronización manual bajo demanda.
4. **Runner Nocturno Desatendido (`CatalogFeedSyncJobRunner` en `Ludeka.Jobs`):**
   - Proceso batch programado en Cloud Run Jobs (`feed-sync`).
   - Descarga en streaming con bajo consumo de memoria ($O(1)$) y procesamiento concurrente controlado.
   - Cruce determinista $O(1)$ por EAN-13 contra la base de datos de Ludeka.
   - Sincronización idempotente en `GamePurchaseLink`: actualización de precio actual, divisa, disponibilidad real (`InStock`) y generación de URL de producto enriquecida con el tag de afiliación de la tienda.
   - Registro en bitácora de auditoría y telemetría de sincronización (ofertas actualizadas, precios modificados, registros omitidos).

---

## 3. Criterios de Aceptación Previstos

- [x] La entidad `Game` expone y persiste el código de barras EAN-13 con validación de formato (13 dígitos numéricos) y soporte de códigos adicionales.
- [x] La entidad `AffiliateFeedSource` permite configurar múltiples feeds comerciales con sus URLs y parámetros.
- [x] El panel `/admin/afiliados` permite listar, dar de alta, editar y probar fuentes de catálogo de tiendas y resolver discrepancias EAN.
- [x] El runner `CatalogFeedSyncJobRunner` procesa feeds XML Google Shopping en streaming mapeando por EAN y actualizando `GamePurchaseLink` sin bloquear la aplicación web.
- [x] La ficha de juego muestra ofertas reales con precio y stock verificado cuando el juego coincide por EAN con un producto del feed.
- [x] Cobertura con pruebas unitarias completas para parsers de feeds (XML), cruce por EAN, runner desatendido y panel web (2.390 pruebas unitarias en verde).
