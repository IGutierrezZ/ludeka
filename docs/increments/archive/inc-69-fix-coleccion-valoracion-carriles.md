# INC-69: Fix en Colección y Valoración (EF Core Tracking), Flujo de Jugado y Carriles de Portada

> **Estado:** ✅ Archivado (1.925 pruebas unitarias verificadas)  
> **Fecha de Inicio:** 2026-09-26  
> **Fecha de Cierre:** 2026-09-26  
> **Rama de Trabajo:** `inc/fix-coleccion-valoracion-carriles` (PR #121 merged)  
> **Worktree:** `C:\repos\ludeka-wt\fix-coleccion-valoracion-carriles`  
> **Dependencias:** INC-02 (colección y valoración), INC-30 (estado jugado independiente), INC-23/35 (portada editorial)  
> **Especificación Viva:** [02. Ludoteca Personal y Préstamos](file:///c:/repos/Ludeka/docs/specs/sistema/02-ludoteca-y-prestamos.md), [15. Dashboard de Inicio Editorial](file:///c:/repos/Ludeka/docs/specs/sistema/15-dashboard-inicio-editorial.md), [23. Portada Editorial](file:///c:/repos/Ludeka/docs/specs/sistema/23-portada-editorial.md)  

---

## 1. Cómo se descubrió y contexto del fallo

1. **Error en producción al guardar valoración y marcar colección simultáneamente:** Al valorar juegos o actualizar el estado de colección (ej. Terraforming Mars), la aplicación producía un fallo 500 al persistir en base de datos.
2. **Conflicto de seguimiento en EF Core:** En las entidades `UserCollectionItem`, `UserGameReview` y `GameLoan`, al llamar a `Context.Update(item)` sobre una instancia desasociada que incluía la propiedad de navegación `.Game`, EF Core marcaba todo el grafo (`Game` y sus entidades complejas JSON propiedad: `Scalability`, `Sleeves`, `AiSummary`) como `EntityState.Modified`, generando excepciones de concurrencia o conflictos de seguimiento.
3. **Flujo de interacción al añadir a ludoteca:** Si un usuario marcaba un juego como "En mi ludoteca" (propiedad) sin tenerlo jugado, no existía una guía para incentivar la valoración o registrar si ya lo había probado.
4. **Diseño de tarjetas y carriles de portada en móvil y escritorio:**
   - La cabecera de carril (`RailHeader.razor`) mostraba una píldora redundante (`@Count @CountLabel`, ej. "29 títulos" o "0 sorteos").
   - Las tarjetas de juegos en portada tenían dimensiones que no encuadraban uniformemente la información: títulos con saltos variables, diseñador desalineado, indicador de jugadores poco compacto y un ancho en móvil que impedía percibir con claridad que la sección era desplazable.
   - En pantallas de escritorio no táctiles, los carriles no permitían desplazamiento mediante arrastre con el ratón (drag-to-scroll).

---

## 2. Solución Arquitectónica Implementada

### A. Aislamiento de Persistencia en EF Core (Clean Tracking)
- En `SqliteUserCollectionRepository`, `SqliteUserReviewRepository` y `SqliteGameLoanRepository`, los métodos de actualización (`UpdateAsync` / `UpsertAsync`) consultan la entidad existente por clave primaria dentro del `DbContextScope` y actualizan exclusivamente las propiedades escalares (`Status`, `IsPlayed`, `Rating`, `Comment`, `PlayCount`, `UpdatedAt`, etc.).
- Se previene que el grafo de navegación `Game` entre en seguimiento en el contexto, eliminando de raíz cualquier colisión con los tipos JSON propios.
- Se añadieron pruebas unitarias en `SqliteLibraryPersistenceTests.cs` verificando actualizaciones con `.Game` asignado.

### B. Flujo Guiado de "En mi ludoteca" y Manejo Robusto de Errores
- En `GameDetail.razor`, al hacer clic en "Tengo" sin haber marcado previamente "Jugado":
  - Se abre un modal dialog accesible con la pregunta: *«¿Has jugado al juego [Nombre]?»*.
  - Si el usuario selecciona **«Sí, lo he jugado»**: se marca automáticamente `IsPlayed = true`, se guarda en la colección y se abre el formulario de valoración.
  - Si selecciona **«No, aún no»**: se marca únicamente en la ludoteca y se muestra un banner amigable recordando: *«¡Anotado en tu ludoteca! Recuerda venir a marcarlo como jugado y valorarlo cuando lo estrenes en mesa.»*.
- Se encapsularon todas las operaciones de colección, valoración y préstamos en bloques `try/catch` con un banner informativo de feedback (`_actionFeedbackMessage`) descartable, evitando fallos no capturados en el UI.

### C. Refinamiento Editorial de Carriles y Tarjetas
- **Cabeceras:** Se retiró el badge numérico redundante de `RailHeader.razor`.
- **Tarjetas Móviles (`HomeGameCard.razor`):**
  - Dimensiones optimizadas a `w-[28vw] min-w-[105px] max-w-[125px] sm:w-44 md:w-48`. En resoluciones móviles estándar (~360px a 430px) entran exactamente 3 tarjetas y media, otorgando el efecto visual de corte (affordance) que invita a deslizar horizontalmente.
  - Formato compacto de jugadores: `👥 1-4J`.
  - Altura mínima uniforme en el contenedor del título (`min-h-[2.25rem]`) y anclaje del diseñador en el borde inferior (`mt-auto`) para un encuadre milimétrico y consistente en toda la fila.
- **Arrastre de Carriles en Escritorio (`rail-scroll.js`):**
  - Implementación en vanilla JS sin dependencias externas: escucha eventos `mousedown`, `mousemove`, `mouseup` y `mouseleave` en contenedores con atributo `data-rail-scroll`.
  - Manejo preventivo de clics accidentales en enlaces o tarjetas mientras se arrastra.
  - Registrado globalmente en `App.razor`.

---

## 3. Verificación Automatizada

- **Pruebas Unitarias de Persistencia:** Nuevos tests en `SqliteLibraryPersistenceTests`:
  - `UpdateAsync_WithLoadedGameNavigation_UpdatesOnlyCollectionItemWithoutTrackingConflict`
  - `UpsertAsync_WithLoadedGameNavigation_UpdatesOnlyReviewWithoutTrackingConflict`
- **Total verificado:** 1.925 pruebas unitarias xUnit pasando al 100% (0 errores, 0 fallos).
- **Compilación de la solución:** Exitosa en los 6 proyectos (.NET 10).

---

## 4. Work Units Completadas

- [x] **Tarea 1 (Infraestructura / Persistencia):** Aislamiento de actualización de entidades en `SqliteUserCollectionRepository`, `SqliteUserReviewRepository` y `SqliteGameLoanRepository` para evitar marcar `Game` como modificado.
- [x] **Tarea 2 (Tests de Persistencia):** Creación de pruebas unitarias específicas reproduciendo la condición de navegación desasociada en `SqliteLibraryPersistenceTests.cs`.
- [x] **Tarea 3 (Web / UX):** Diálogo guiado interactivo de jugado al pulsar "Tengo", modal de valoración condicional y banner de feedback en `GameDetail.razor`.
- [x] **Tarea 4 (Web / UI Carriles):** Retirada de píldora de conteo en `RailHeader.razor`, rediseño de encuadre en `HomeGameCard.razor` (3.5 tarjetas en móvil, `1-4J`, altura fija de título).
- [x] **Tarea 5 (Web / Interacción Escritorio):** Implementación de `rail-scroll.js` para arrastre con ratón en carruseles y cableado en `App.razor`.
- [x] **Tarea 6 (PR & Limpieza):** Creación de PR #121, integración en `main` y limpieza de worktree mediante `scripts/sdd-worktree.ps1 done fix-coleccion-valoracion-carriles`.
