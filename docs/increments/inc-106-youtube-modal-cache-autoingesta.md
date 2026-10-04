# Incremento 106: Optimización del Modal de YouTube (Permanencia, Estado Visual y Caché) e Ingesta Automática del Top 4.000

> **ID:** INC-106  
> **Slug:** `youtube-modal-cache-autoingesta`  
> **Rama:** `inc/youtube-modal-cache-autoingesta`  
> **Estado:** ⏳ En progreso  
> **Módulos Impactados:** Módulo 04 (`docs/specs/sistema/04-hub-multimedia-redes.md`), `src/Ludeka.Core/`, `src/Ludeka.Application/`, `src/Ludeka.Infrastructure/`, `src/Ludeka.Jobs/`, `src/Ludeka.Web/`  
> **Dependencias:** INC-14 (Búsqueda quirúrgica en YouTube), INC-23 (Categorización editorial de vídeos), INC-47 (Orquestador de trabajos desatendidos)

---

## 1. Contexto y Diagnóstico

1. **Cierre intempestivo del modal al aprobar o mandar a pendientes:** En `MultimediaHub.razor`, el callback `HandleYouTubeVideoIngestedAsync` forzaba `_isYouTubeSearchOpen = false;` inmediatamente tras procesar un único vídeo, cerrando la ventana en la cara del usuario y cortando el flujo de trabajo de curación.
2. **Pérdida destructiva de resultados cargados:** En `YouTubeSearchModal.razor`, cada reapertura o reevaluación de parámetros purgaba `_quickOverviews`, `_tutorials` y `_playthroughs`, perdiendo los resultados obtenidos de la API y forzando al usuario a reescribir y volver a disparar la búsqueda.
3. **Falta de estado visual por vídeo:** En el listado de resultados no se indicaba visualmente si un vídeo específico ya había sido aprobado o enviado a pendientes en la sesión actual, permitiendo pulsaciones redundantes y desorientación.
4. **Ausencia de capa de caché en búsquedas de YouTube:** `YouTubeSearchService` realizaba peticiones en vivo a la API de YouTube sin almacenar en memoria (`IMemoryCache`) las respuestas por consulta. Cada pestaña o búsqueda repetida consumía 100 unidades de cuota por categoría.
5. **Carencia de vídeos en el catálogo y límite estricto de cuota de YouTube:** Miles de juegos clave del catálogo carecen aún de vídeos asociados. La API de YouTube impone un límite gratuito estándar de 10.000 unidades diarias (cada búsqueda `search.list` cuesta 100 unidades). Se requiere un sistema gradual que ingeste los mejores 3 vídeos auto-aprobados para el top 4.000 de juegos sin saturar la cuota ni impedir las búsquedas interactivas de los moderadores.

---

## 2. Objetivos y Solución Técnica

1. **UX del Modal y Permanencia:**
   - En `MultimediaHub.razor`, mantener abierto el modal tras procesar un vídeo, refrescando el Hub en segundo plano.
   - En `YouTubeSearchModal.razor`, mantener un registro reactivo de vídeos procesados (`_ingestedVideos`), transformando los botones de acción en indicadores visuales ("Aprobado", "En Pendientes") deshabilitados para evitar duplicidades.
   - Preservar los resultados cargados si el juego seleccionado o el término de búsqueda no ha cambiado.
2. **Capa de Caché en Memoria (`IMemoryCache`):**
   - Incorporar caché con TTL de 30-60 minutos en `YouTubeSearchService` indexada por término normalizado y categoría para eliminar consultas de red redundantes.
3. **Búsqueda Consolidada de 1 Sola Llamada (Ahorro de Cuota del 66%):**
   - Implementar método de búsqueda consolidada para auto-ingesta que obtenga con una sola petición `search.list` (100 unidades) los candidatos de un juego y clasifique en memoria el mejor resumen corto, tutorial y partida completa.
4. **Sistema de Auto-Ingesta Desatendida del Top 4.000 (Presupuesto de 60 juegos/día):**
   - Servicio `IYouTubeCatalogAutoIngestService` que localice juegos en el top 4.000 por `BggRank` sin vídeos aprobados en `MediaItem`.
   - Límite diario estricto configurable a **60 juegos al día** (consumiendo 6.000 unidades de cuota diaria y reservando 4.000 unidades para uso interactivo en vivo).
   - Integración con `Ludeka.Jobs` (`youtube-auto-ingest`) y servicio en segundo plano con ventana diaria mediante `IJobExecutionCoordinator`.

---

## 3. Plan de Trabajo (Work Units)

- [ ] **WU 01: Caché en memoria y búsqueda consolidada en YouTubeSearchService:** Inyección de `IMemoryCache`, pruebas unitarias con mock de HTTP, método `SearchConsolidatedCandidatesAsync` con ahorro de cuota.
- [ ] **WU 02: Repositorio y filtrado del top 4.000 sin vídeos:** Consulta en `IGameRepository` / `SqliteGameRepository` para obtener juegos del top 4.000 por `BggRank` que no dispongan de vídeos en `MediaItem`. Pruebas unitarias de repositorio.
- [ ] **WU 03: Servicio de Auto-Ingesta y control de cuota:** `IYouTubeCatalogAutoIngestService` con límite de 60 juegos/día, auto-aprobación de hasta 3 vídeos y registro de auditoría. Pruebas unitarias de servicio.
- [ ] **WU 04: Runner en Ludeka.Jobs y servicio en segundo plano:** Runner `YouTubeAutoIngestJobRunner` y opciones con ventana diaria de ejecución.
- [ ] **WU 05: Refactorización UX del Modal de YouTube:** Permanencia del modal en `MultimediaHub.razor`, badges de estado por vídeo procesado en `YouTubeSearchModal.razor` y preservación de estado al reabrir. Pruebas de componentes y contratos.
- [ ] **WU 06: Verificación integral, PR y cierre:** Ejecución de suite completa de pruebas, verificación de CI, merge a `main`, verificación de CD y archivado a especificación viva.
