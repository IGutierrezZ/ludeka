# INC-95: Pantalla Dedicada de Tendencias BGG Top 50 con Movimiento Diario y Desacople de Catálogo

> **Estado:** ⏳ En progreso  
> **Rama:** `inc/pantalla-tendencias`  
> **Worktree:** `F:\repos\ludeka-wt\pantalla-tendencias`  
> **Fecha:** 2026-10-01  

---

## 1. Contexto y Justificación

En el incremento INC-93 se incorporó la persistencia diaria de las 50 tendencias mundiales de BoardGameGeek (`DailyTrendingGames`) y se añadió un criterio de ordenación `GameSortOrder.Trending` en el catálogo general (`/catalogo?orden=tendencia`).

No obstante, esta aproximación generaba dos fricciones de experiencia de usuario y arquitectura:
1. **Confusión en el total de títulos:** Al tratarse de un criterio de ordenación (con `LEFT JOIN`), la vista de `/catalogo?orden=tendencia` mostraba los ~500 juegos del catálogo en lugar de circunscribirse al Top 50 real de BGG Hotness.
2. **Apertura no deseada de filtros avanzados:** En `Home.razor`, cualquier ordenación distinta a la predeterminada computaba en `ActiveAdvancedFiltersCount`, provocando la auto-apertura del acordeón de filtros avanzados al entrar desde el enlace de la portada.
3. **Falta de visibilidad histórica inmediata:** No existía una pantalla especializada que mostrara los 50 puestos ordenados en formato lista ni el movimiento o delta de cada juego (si ha subido, bajado o se mantiene) respecto a la instantánea del día anterior.

---

## 2. Objetivos y Alcance

1. **Nueva Pantalla `/tendencias` (`TrendingGames.razor`):**
   - Muestra de forma estricta los 50 juegos en tendencia de la fotografía diaria más reciente.
   - Formato lista editorial accesible con numeración de ranking clara (`#1` a `#50`), carátula, título, año y enlace a la ficha de juego si está catalogado.
   - Indicador visual de movimiento (delta) respecto al snapshot diario inmediatamente anterior:
     - 🔺 **Sube (`RankMovement.Up`)**: Flecha arriba verde/ámbar indicando los puestos ganados.
     - **= Mantiene (`RankMovement.Same`)**: Signo igual neutro si conserva el puesto.
     - 🔻 **Baja (`RankMovement.Down`)**: Flecha abajo si pierde puestos.
     - ✨ **Novedad (`RankMovement.New`)**: Insignia "Entra" para títulos que no figuraban en el Top 50 del día anterior.
   - Botón de navegación prominente: «Ir al catálogo» hacia `/catalogo`.
   - Fecha de referencia visible de la foto de tendencias.

2. **Servicio y Caso de Uso de Aplicación (`ITrendingService` / `TrendingService`):**
   - Método `GetTrendingComparisonAsync(CancellationToken ct)` que recupera la última fecha disponible en `DailyTrendingGames` y la fecha previa más reciente con datos, computando las posiciones y variaciones para cada uno de los 50 puestos.
   - DTOs inmutables: `TrendingGameItemDto`, `TrendingComparisonDto` y enum `RankMovement`.

3. **Desacople en Catálogo (`Home.razor`) y Portada (`HomeDashboard.razor`):**
   - Retirada de la ordenación «En tendencia» en los controles del catálogo.
   - Redirección defensiva de `/catalogo?orden=tendencia` a `/tendencias` para enlaces externos o marcadores existentes.
   - Desacople del contador `ActiveAdvancedFiltersCount` respecto a criterios de ordenación para evitar auto-aperturas del panel de filtros.
   - Actualización del enlace del Carril 1 en portada para apuntar a `/tendencias`.

4. **Pruebas y Verificación:**
   - Pruebas unitarias de cálculo de movimiento y resiliencia temporal en `TrendingServiceTests`.
   - Pruebas de contrato web en `TrendingPageContractTests` y actualización de contratos en `CatalogFilterContractTests` y `HomeDashboardTests`.
