# Propuesta de Cambio: Snapshots Crudos BGG, Refinamiento Integral de Catálogo, Ficha Editorial y Retorno de Sesión

> **ID del Cambio:** `change-90-bgg-raw-snapshots`  
> **Incremento Asociado:** INC-90  
> **Rama de Trabajo:** `inc/bgg-raw-snapshots`  
> **Fecha:** 2026-10-01  
> **Estado:** ⏳ Propuesta en revisión  
> **Autor:** Mesa Técnica Ludeka  

---

## 1. Motivación y Diagnóstico

Este incremento aborda la preservación de datos crudos de BoardGameGeek junto con un paquete integral de refinamientos de experiencia de usuario, permisos y navegación detectados en producción:

1. **Snapshots crudos de BGG:** BGG devuelve datos extensos que hoy se descartan al mapear a `Game`. Para permitir recálculos locales futuros de estilos, categorías o nuevos atributos sin volver a saturar la API externa de BGG ni inflar la tabla caliente `Games`, se requiere una tabla satélite desacoplada `BggRawSnapshots`.
2. **Ficha de Juego (`GameDetail.razor`):**
   - Enlace a BGG duplicado (aparece en la barra de acciones superior y en la línea de metadatos de título).
   - El botón «Cartel para Redes» se muestra a usuarios anónimos sin rol de moderador/administrador.
   - El modal de edición de ficha ([`GameEditorModal.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Shared/GameEditorModal.razor)) solo permite cambiar la carátula, omitiendo las imágenes de trasera y mesa necesarias para el carrusel de tres fotos de INC-85.
   - En la tarjeta de síntesis IA ([`AiSummaryCard.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Shared/AiSummaryCard.razor)) se expone el nombre técnico del modelo (`gemini-2.0-flash [Batch]`), innecesario para el usuario final.
3. **Autenticación y Retorno de Navegación:**
   - Al iniciar sesión mediante proveedores sociales en `/login/external`, se ignora el parámetro `returnUrl` del formulario y se fuerza siempre la redirección a portada (`/`).
4. **Catálogo y Tarjetas (`Home.razor` / `GameCard.razor`):**
   - La fila de filtros rápidos superior duplica opciones ya cubiertas y sobrecarga la interfaz.
   - Falta selector de ordenación («Ordenar por») en los filtros avanzados (actualmente siempre ordena por ranking BGG, imposibilitando ordenar por dureza/complejidad, duración o fecha).
   - En las tarjetas del catálogo, el indicador de jugadores es prolijo (`Ideal: 3-4 jugadores...`) en comparación con el formato compacto y elegante de la portada (`3-4J`).
   - Los badges inferiores de las tarjetas acumulan demasiado texto (`Euro`, `Solo`, `Mesa estándar`), cuando bastan los iconos con tooltip accesible y una leyenda de referencia, retirando la píldora redundante de modo solitario de la tarjeta para derivarla al filtro avanzado.

---

## 2. Alcance Propuesto por Slices

### Slice A — Ficha Editorial, Permisos y Retorno de Sesión
1. **Limpieza en `GameDetail.razor`:**
   - Unificar el acceso a BGG en un único enlace canónico.
   - Proteger «Cartel para Redes» para que solo sea visible a usuarios con permiso `CanEditGames`.
2. **Edición Multicarrusel en `GameEditorModal.razor`:**
   - Añadir soporte para subir y editar `BackCoverImageUrl` (trasera) y `TableImageUrl` (mesa) con persistencia en `Game`.
3. **Síntesis IA Editorial:**
   - En `AiSummaryCard.razor`, retirar la píldora técnica `@Summary.Model`, conservando la insignia editorial «Síntesis generada por IA» y la fecha.
4. **Preservación de `returnUrl` en Login:**
   - En `Program.cs` (`/login/external`), leer `form["returnUrl"]` y validar con `LoginRedirect.IsLocalUrl(...)` para redirigir a la página previa tras la autenticación.

### Slice B — Refinamiento del Catálogo y Tarjetas Lúdicas
1. **Simplificación de Filtros en `Home.razor`:**
   - Retirar la hilera de botones de filtros rápidos superiores (`Todos`, `Juegos Base`, etc.) para dejar el buscador principal y el panel de «Filtros avanzados».
2. **Selector de Ordenación («Ordenar por»):**
   - Extender `GameFilterCriteria` con `GameSortOrder` (`Rank` [defecto], `Rating`, `ComplexityAsc`, `ComplexityDesc`, `DurationAsc`, `DurationDesc`, `YearDesc`, `TitleAsc`).
   - Incorporar el selector en el panel de filtros avanzados de `Home.razor` y sincronizarlo con el parámetro `orden` en URL.
   - Aplicar el orden correspondiente en `SqliteGameRepository.SearchAsync`.
3. **Rediseño Compacto de `GameCard.razor`:**
   - Adoptar el formato compacto de jugadores de la portada (`<Icon Name="users" Size="11" /> 3-4J`).
   - Reducir los badges inferiores a iconos minimalistas con `title` emergente accesible.
   - Retirar la píldora «Solo» de las tarjetas individuales.
   - Añadir una leyenda accesible de iconos en el catálogo.

### Slice C — Tabla Satélite de Snapshots Crudos BGG y Poblado Defensivo
1. **Entidad y Persistencia Satélite `BggRawSnapshot`:**
   - Tabla `BggRawSnapshots` (`BggId`, `RawJson`, `ApiVersion`, `FetchedAtUtc`, `UpdatedAtUtc`).
   - Repositorio `IBggRawSnapshotRepository` con soporte dual SQLite/PostgreSQL.
2. **Auto-Captura en Pipeline:**
   - Almacenamiento automático del payload crudo devuelto por BGG al catalogar o importar juegos.
3. **Servicio y Panel de Poblado Retroactivo:**
   - `IBggRawSnapshotSyncService` con *rate-limiting* estricto (~1.200 ms) para recuperar snapshots de títulos ya existentes en `Games`.
   - Botón administrativo y barra de telemetría en `/admin/cola-catalogacion` protegido con `CanEditGames`.

---

## 3. Criterios de Aceptación y Pruebas (TDD)

1. En la ficha de juego solo figura un enlace a BGG y el cartel para redes solo es accesible para moderadores.
2. Tras iniciar sesión externa con `returnUrl`, el usuario aterriza exactamente en la URL local de origen.
3. `GameEditorModal` permite actualizar carátula, trasera y mesa, impactando inmediatamente en `GameImageCarousel`.
4. El catálogo prescinde de filtros rápidos redundantes y permite ordenar por dureza, duración, ranking y año.
5. Las tarjetas del catálogo muestran jugadores en formato compacto `X-YJ`, iconos inferiores con tooltip sin texto y sin etiqueta «Solo».
6. La tabla `BggRawSnapshots` almacena snapshots crudos fieles sin alterar el rendimiento de la tabla caliente `Games`.
7. El 100% de la suite de pruebas unitarias existente se mantiene en verde y se añaden pruebas de regresión para cada slice.
