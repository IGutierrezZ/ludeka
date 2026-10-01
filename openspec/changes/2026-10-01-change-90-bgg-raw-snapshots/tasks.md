# Lista de Tareas: INC-90 — Snapshots Crudos BGG, Refinamiento Integral de Catálogo, Ficha Editorial y Retorno de Sesión

> **ID del Cambio:** `change-90-bgg-raw-snapshots`  
> **Incremento Asociado:** INC-90  
> **Estado:** ⏳ Planificado y en progreso  

---

## Tareas de Implementación por Slices

### Fase 1: Slice A — Ficha de Juego, Permisos y Retorno de Sesión
- [ ] 1.1 Unificar el enlace a BGG en [`GameDetail.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Pages/GameDetail.razor) eliminando el botón redundante de la botonera superior y conservando la insignia canónica en los metadatos.
- [ ] 1.2 Proteger el botón «Cartel para Redes» en [`GameDetail.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Pages/GameDetail.razor) mediante la guarda de autorización `CanEditGames` para moderadores/fundadores.
- [ ] 1.3 Retirar el badge técnico `@Summary.Model` en [`AiSummaryCard.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Shared/AiSummaryCard.razor), manteniendo únicamente la insignia editorial y fecha.
- [ ] 1.4 Extender [`GameEditorModal.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Shared/GameEditorModal.razor) para gestionar y subir a R2 la portada frontal, trasera de caja y despliegue en mesa para alimentar el carrusel de INC-85.
- [ ] 1.5 Corregir el endpoint `/login/external` en [`Program.cs`](file:///f:/repos/Ludeka/src/Ludeka.Web/Program.cs) para respetar el parámetro `returnUrl` sanitizado con `LoginRedirect.IsLocalUrl(...)`.
- [ ] 1.6 Reordenar las pestañas secundarias en [`GameDetail.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Pages/GameDetail.razor) para situar el Hub Multimedia en primer lugar (y activo por defecto), y reestructurar la jerarquía visual de la columna principal colocando el semáforo de escalabilidad tras el carrusel, seguido del multimedia/fundas, y desplazando la síntesis editorial al fondo.

### Fase 2: Slice B — Refinamiento del Catálogo y Tarjetas Lúdicas
- [ ] 2.1 Definir el enum `GameSortOrder` en `Ludeka.Core.Enums` y extender `GameFilterCriteria` con la propiedad `SortBy`.
- [ ] 2.2 Implementar la ordenación dinámica (ranking, puntuación, dureza, duración, año y título) en [`SqliteGameRepository.SearchAsync`](file:///f:/repos/Ludeka/src/Ludeka.Infrastructure/Data/SqliteGameRepository.cs).
- [ ] 2.3 Retirar la fila de filtros rápidos superiores en [`Home.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Pages/Home.razor) y agregar el selector «Ordenar por» en el panel de filtros avanzados con persistencia en URL (`orden`).
- [ ] 2.4 Refactorizar [`GameCard.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Shared/GameCard.razor): formatear jugadores en modo compacto `3-4J`, reducir los badges inferiores a solo iconos con tooltip y retirar la píldora «Solo».
- [ ] 2.5 Añadir leyenda accesible de iconografía en el catálogo.

### Fase 3: Slice C — Tabla Satélite de Snapshots Crudos BGG y Poblado Defensivo
- [ ] 3.1 Crear la entidad `BggRawSnapshot` y configurar su mapeo en `LudekaDbContext` (soporte dual `jsonb` en PostgreSQL y `TEXT` en SQLite).
- [ ] 3.2 Implementar el repositorio `IBggRawSnapshotRepository` y `SqliteBggRawSnapshotRepository`.
- [ ] 3.3 Integrar la auto-captura transparente de snapshots en el pipeline de consulta de BGG (`BggXmlApiClient` / `BggCatalogQueueService`).
- [ ] 3.4 Implementar el servicio `BggRawSnapshotSyncService` con *rate limiting* (~1.200 ms) y cancelación cooperativa.
- [ ] 3.5 Añadir la tarjeta de control de snapshots con métricas y botón de sincronización por lotes en `/admin/cola-catalogacion` protegido con `CanEditGames`.

### Fase 4: Verificación Automatizada y Regresión (TDD)
- [ ] 4.1 Añadir pruebas unitarias para `BggRawSnapshot` y el repositorio satélite.
- [ ] 4.2 Añadir pruebas unitarias para ordenación dinámica en `SqliteGameRepository`.
- [ ] 4.3 Añadir pruebas de contrato y renderizado para `GameCard`, `AiSummaryCard` y `GameEditorModal`.
- [ ] 4.4 Añadir pruebas para la redirección de `returnUrl` en autenticación externa.
- [ ] 4.5 Ejecutar la suite completa de pruebas unitarias (`dotnet test tests/Ludeka.UnitTests`) y certificar 100% verde sin regresiones.
