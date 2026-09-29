# Incremento 82: Optimización de Rendimiento en Catálogo, Paginación Nativa SQL y Búsqueda Resiliente con Debounce

> **ID:** INC-82  
> **Slug:** `opt-catalogo-busqueda`  
> **Rama:** `inc/opt-catalogo-busqueda`  
> **Estado:** ⏳ En progreso  
> **Módulos Impactados:** Módulo 01 (`docs/specs/sistema/01-catalogo-base.md`), Módulo 14 (`docs/specs/sistema/14-youtube-live-search.md`), Módulo 28 (`docs/specs/sistema/28-hub-ingesta-social-moderacion.md`)  
> **Dependencias:** INC-81, PR #143.

---

## 1. Contexto y Diagnóstico del Problema

1. **Desconexión y caída del circuito SignalR en modales de búsqueda:**
   - En `YouTubeSearchModal.razor` (y similar en los modales de ingesta social `SocialExpressIngestModal.razor` y `SocialInboxEditModal.razor`), el campo de texto ejecutaba búsquedas en cada pulsación (`oninput` / `onkeydown` / `onkeyup`) **sin ningún mecanismo de debounce ni cancelación de peticiones previas**.
   - Cada pulsación invocaba `CatalogService.GetQuickSearchAsync`, disparando ráfagas concurrentes de consultas pesadas.
   - En Blazor Server desplegado en Google Cloud Run (`ludeka.es`), estas ráfagas provocaban saturación de CPU/memoria y bloqueo del *heartbeat* de SignalR (o reinicios de contenedor por OOM), mostrando el modal «Reconectando con el servidor...» y reseteando el circuito, lo que cerraba el modal inesperadamente.
2. **Materialización masiva prematura en SQLite (`SqliteGameRepository.SearchAsync`):**
   - La consulta del catálogo ejecutaba `var list = await query.ToListAsync(ct);` antes de aplicar el filtrado por texto (`SearchTerm`) y antes de paginar.
   - Si el catálogo contiene miles de juegos, SQLite leía y deserializaba en memoria toda la tabla con todas sus colecciones JSON (`Scalability`, `Sleeves`, `LocalizedTitles`, `RegionalPublishers`, etc.), solo para después realizar `list.Skip(...).Take(20)`. El 99,8% de los datos deserializados en RAM se descartaba inmediatamente.
   - El conteo total se calculaba en memoria con `list.Count` en lugar de una consulta `SELECT COUNT(*)` nativa en base de datos.
3. **Lentitud de carga en Home y Catálogo:**
   - La primera carga y cualquier cambio de filtros requerían volcar la base de datos completa a memoria, provocando latencia innecesaria y consumo desproporcionado de memoria en el servidor.

---

## 2. Objetivos del Incremento

1. **Búsqueda rápida nativa en SQL (`QuickSearchAsync`):**
   - Diseñar e implementar `QuickSearchAsync(string term, int limit = 5, CancellationToken ct = default)` en `IGameRepository` y `SqliteGameRepository`.
   - Ejecutar la búsqueda mediante `EF.Functions.Like` directamente en SQL sobre `SpanishTitle` y `OriginalTitle` (y si procede editoriales) con `.Take(limit)` a nivel de base de datos, tardando 1-2 ms y recuperando únicamente 5 registros sin tocar la memoria innecesariamente.
   - Integrar `CatalogService.GetQuickSearchAsync` con esta ruta especializada.
2. **Debounce y cancelación de peticiones en la interfaz:**
   - En `YouTubeSearchModal.razor`, implementar un temporizador de *debounce* (250 ms) y `CancellationTokenSource` para cancelar búsquedas obsoletas mientras el usuario escribe, desacoplando `HandleSearchInput` del `onkeydown` (reservado solo para `Enter`).
   - Implementar `IDisposable` para la liberación limpia de recursos.
   - Aplicar el mismo patrón resiliente en los modales de ingesta de redes sociales.
3. **Paginación y conteo nativos en SQL para Home y Catálogo:**
   - En `SqliteGameRepository.SearchAsync`, cuando no haya filtros que requieran deserialización JSON en memoria (`PlayerCounts`, `EspecialParejas`, `Complexities`), ejecutar `CountAsync(ct)` y la paginación `.Skip((page-1)*pageSize).Take(pageSize)` directamente en SQLite vía SQL.
   - Mover el filtro de `SearchTerm` básico (títulos, diseñador, editoriales) a `EF.Functions.Like` en el `IQueryable` de EF Core antes de cualquier materialización.
4. **Protección contra concurrencia en `Home.razor`:**
   - Añadir cancelación de peticiones en vuelo en `LoadCatalogAsync` para evitar ráfagas al alternar filtros rápidamente.
5. **Cobertura con pruebas automáticas (TDD):**
   - Pruebas unitarias para `QuickSearchAsync` en el repositorio SQLite.
   - Pruebas unitarias para `CatalogService.GetQuickSearchAsync`.
   - Pruebas de integración o de componente verificando la cancelación y debounce.

---

## 3. Criterios de Aceptación (TDD)

- **Criterio 1:** `QuickSearchAsync` en `SqliteGameRepository` devuelve como máximo el límite solicitado ejecutando la consulta en SQL sin materializar la tabla entera.
- **Criterio 2:** `SearchAsync` ejecuta paginación nativa en SQL cuando no se requieren filtros JSON de escalabilidad o complejidad.
- **Criterio 3:** `YouTubeSearchModal.razor` cancela búsquedas previas al continuar escribiendo y respeta el retardo de 250 ms.
- **Criterio 4:** La suite de pruebas pasa al 100% sin regresiones.
