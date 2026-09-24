# Documento Vivo ODD — INC-59: Filtros del Catálogo y Tamaño en Mesa

> **Feature:** `filtros-y-tamano-mesa`  
> **Fichero:** `odd/tasks/inc-59-filtros-y-tamano-mesa.md` (fuente de verdad operativa)  
> **Rama:** `inc/filtros-y-tamano-mesa`  
> **Worktree:** `C:\repos\ludeka-wt\filtros-y-tamano-mesa`  
> **Creado:** 2026-09-24 · **Ruta:** rama `inc/filtros-y-tamano-mesa` → PR a `main`  

---

## 1. Objetivo

Habilitar la exploración y filtrado del catálogo lúdico de Ludeka (~8.000 títulos en `/catalogo`) atendiendo a las restricciones físicas y situacionales reales de una partida:
1. **Espacio en mesa (`TableFootprint`):** responder a «¿me cabe en la mesa?» con las categorías de dominio (*Mesa pequeña / cafetería*, *Mesa estándar de comedor*, *Monstruo de mesa / despliegue masivo*).
2. **Número de comensales (`PlayerCount`):** conectar el selector de 1 a 7+ jugadores evaluando el semáforo dinámico de escalabilidad.
3. **Duración de partida (`MaxDurationMinutes`):** ofrecer un selector flexible de duración (≤30, ≤45, ≤60, ≤90, ≤120 min).
4. **Sincronización robusta en URL:** persistir los criterios en query string (`mesa`, `jugadores`, `duracion`) compatibles con la paginación de INC-58, y solventar la omisión de `TypeFilter` en la caché L1.

---

## 2. Problema y Diagnóstico Previo (Auditoría Empírica)

Tras auditar exhaustivamente `GameFilterCriteria`, `SqliteGameRepository`, `CachedCatalogService` y `Home.razor`:

1. **`TableFootprint` ausente en el motor de filtrado:**  
   La entidad `Game` y los componentes de visualización (`QuickBadges`, `GameListItem`) conocen y muestran `TableFootprint`, pero `GameFilterCriteria` carece de la propiedad `Footprint`, `SqliteGameRepository.SearchAsync` no la filtra y no existe control en la interfaz.
2. **`PlayerCount` desconectado en la interfaz:**  
   `GameFilterCriteria` y `SqliteGameRepository` ya implementan el filtrado por número de comensales evaluando la escalabilidad comunitaria (`ScalabilityStatus != NotRecommended`), pero `Home.razor` no ofrecía ningún selector para activarlo (solo el preset fijo `EspecialParejas` a 2J).
3. **Duración rígida:**  
   Solo existía el preset hardcodeado `≤ 45 min` (`Preset.PartidasRapidas`), sin posibilidad de acotar a 30, 60, 90 o 120 minutos.
4. **Riesgo latente de colisión en caché L1 (`CachedCatalogService`):**  
   `ComputeCriteriaHash` omitía `TypeFilter` al calcular el hash SHA256, lo que provocaba que una consulta con `TypeFilter=GameType.BaseGame` y otra con `TypeFilter=GameType.Expansion` pudieran compartir la misma clave si el resto de parámetros coincidían.
5. **Monocultura visual en cuadrícula:**  
   `GameListItem` (lista) muestra la huella de mesa, pero `GameCard` (cuadrícula) no disponía de chip para evaluarla rápidamente sin abrir la ficha.

---

## 3. Alcance Autorizado

### Dentro de Alcance:
- **Modelo y Capa de Aplicación:**
  - Añadir `TableFootprint? Footprint = null` a `GameFilterCriteria`.
  - Actualizar `CachedCatalogService.ComputeCriteriaHash` para incorporar tanto `Footprint` como `TypeFilter`.
- **Persistencia en SQLite:**
  - Incorporar la cláusula `query = query.Where(g => g.Footprint == criteria.Footprint.Value)` en `SqliteGameRepository.SearchAsync`.
- **Interfaz y Experiencia de Usuario (`Home.razor`):**
  - Barra de refinamiento facetado accesible con diseño editorial limpio y anti-slop.
  - Selector de Tamaño en Mesa (Cualquiera, Mesa pequeña, Mesa estándar, Monstruo de mesa) con iconos Lucide (`armchair`, `utensils`, `castle`).
  - Selector de Comensales (Cualquiera, 1, 2, 3, 4, 5, 6, 7+) con badges accesibles.
  - Selector de Duración máxima (Cualquiera, ≤30, ≤45, ≤60, ≤90, ≤120 min).
  - Sincronización bidireccional en URL (`mesa`, `jugadores`, `duracion`), manteniendo `page`, `view`, `q` y presets.
  - Botón reactivo «Limpiar filtros» cuando existan refinamientos aplicados.
  - Reseteo automático a `page = 1` ante cambios en cualquier filtro.
- **Componente de Tarjeta (`GameCard.razor`):**
  - Añadir chip compacto en el pie de la tarjeta para reflejar `TableFootprint` en vista cuadrícula.
- **Pruebas y Verificación:**
  - Pruebas unitarias de repositorio para `Footprint`, `PlayerCount` y `MaxDurationMinutes`.
  - Pruebas de colisión de hash en `CachedCatalogServiceTests`.
  - Pruebas de contrato web en `CatalogFilterContractTests.cs` (accesibilidad, marcado, parámetros de URL).
  - Verificación del 100% de la suite con `dotnet test Ludeka.sln` (>1.678 pruebas en verde).

### Fuera de Alcance:
- Recomendador algorítmico o búsqueda semántica.
- Modificación de la taxonomía del enum `TableFootprint` (se mantiene la canónica: `SmallTable`, `StandardTable`, `TableMonster`).
- Filtrado en ludoteca privada (`/cuenta/ludoteca` o `/mi-ludoteca`).

---

## 4. Decisiones de Arquitectura y Diseño

| ID | Decisión | Fundamento Técnico |
|---|---|---|
| **D-01** | Mantener taxonomía canónica `TableFootprint` y fallback defensivo | Los tres estados (`SmallTable`, `StandardTable`, `TableMonster`) cubren con precisión la realidad de las mesas lúdicas. Los juegos sin dato BGG se mapean a `StandardTable` por defecto en seeding. |
| **D-02** | Filtrado SQL nativo en EF Core para `Footprint` | `Footprint` es columna directa en la tabla `Games`. La cláusula `Where(g => g.Footprint == ...)` se traslada a SQL sin necesidad de procesamiento en memoria. |
| **D-03** | Reparación del hash SHA256 en `CachedCatalogService` | Agregar `TypeFilter` y `Footprint` al cálculo de hash garantiza unicidad de caché L1 sin colisiones inter-facetas. |
| **D-04** | Interfaz editorial con píldoras y chips accesibles | Respeta la estética sobria de Ludeka sin desplegables pesados. Cumple WCAG 2.2 AA con roles `group`, `aria-label`, `aria-pressed` y foco visible. |
| **D-05** | URL como fuente única de la verdad sincronizada | Los parámetros `mesa`, `jugadores` y `duracion` permiten enlaces compartibles y soporte nativo de navegación histórica (`popstate`). |
| **D-06** | Chip de huella en `GameCard.razor` | Permite responder «¿me cabe en la mesa?» inmediatamente en ambas vistas (cuadrícula y lista). |

---

## 5. Checklist de Tareas (IDs Estables)

- [x] **ODD-1 — Dominio, Persistencia y Caché L1**
  - [x] 1.1 Extender `GameFilterCriteria` con `TableFootprint? Footprint = null`.
  - [x] 1.2 Implementar cláusula de filtrado por `Footprint` en `SqliteGameRepository.SearchAsync`.
  - [x] 1.3 Incluir `Footprint` y `TypeFilter` en `CachedCatalogService.ComputeCriteriaHash`.
  - [x] 1.4 Pruebas unitarias en `SqliteGameRepositoryTests.cs` y `CachedCatalogServiceTests.cs`.
- [x] **ODD-2 — Interfaz y Refinamiento en Catálogo (`Home.razor`)**
  - [x] 2.1 Diseñar e integrar controles accesibles para Tamaño en Mesa (`Cualquiera`, `Mesa pequeña`, `Mesa estándar`, `Monstruo de mesa`).
  - [x] 2.2 Diseñar e integrar selectores de Comensales (`1`..`7+`) y Duración máxima (`≤30`..`≤120` min).
  - [x] 2.3 Sincronizar lectura y persistencia en URL (`mesa`, `jugadores`, `duracion`), reset de `page = 1` y botón «Limpiar filtros».
- [x] **ODD-3 — Exposición Visual en Tarjetas del Catálogo (`GameCard.razor`)**
  - [x] 3.1 Integrar chip/badge de `TableFootprint` en el pie de `GameCard.razor`.
  - [x] 3.2 Verificar armonía y paridad visual con `GameListItem.razor` y `QuickBadges.razor`.
- [x] **ODD-4 — Pruebas de Contrato y Verificación de No-Regresión**
  - [x] 4.1 Crear `tests/Ludeka.UnitTests/Web/CatalogFilterContractTests.cs` con pruebas de accesibilidad, marcado y URL.
  - [x] 4.2 Ejecutar suite completa `dotnet test Ludeka.sln` y verificar 100% verde (1.687 pruebas superadas).
- [x] **ODD-5 — Especificación Viva, SDD y PR**
  - [x] 5.1 Actualizar especificación viva `01-catalogo-y-fichas.md` y `README.md`.
  - [x] 5.2 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
  - [ ] 5.3 Abrir PR hacia `main` (`scripts/sdd-worktree.ps1 pr filtros-y-tamano-mesa`).

---

## 6. Verificación Final y Resultados

- **Línea Base Inicial:** 1.668 unitarias + 10 de integración en verde (1.678 total).
- **Pruebas Unitarias Finales:** 1.677 superadas (0 fallos).
- **Pruebas de Integración Finales:** 10 superadas (0 fallos).
- **Total Automatizado:** 1.687 pruebas superadas al 100%.
- **Compilación de Ludeka.sln:** 0 errores.
- **Exploración y Filtros:** Operativos en `/catalogo` con controles de Tamaño en Mesa (`TableFootprint`: *Mesa pequeña*, *Estándar*, *Mesa grande*), Jugadores (1 a 7+) y Duración (≤30, ≤45, ≤60, ≤90, ≤120 min), chip informativo en `GameCard.razor` para vista en cuadrícula, sincronización bidireccional en URL y blindaje de claves de caché L1 contra colisiones.
