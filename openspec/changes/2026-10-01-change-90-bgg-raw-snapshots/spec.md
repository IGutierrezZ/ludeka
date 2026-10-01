# Especificación Técnica: INC-90 — Snapshots Crudos BGG, Refinamiento Integral de Catálogo, Ficha Editorial y Retorno de Sesión

> **ID del Cambio:** `change-90-bgg-raw-snapshots`  
> **Incremento Asociado:** INC-90  
> **Rama de Trabajo:** `inc/bgg-raw-snapshots`  
> **Estado:** ⏳ Especificación en revisión  

---

## 1. Resumen Ejecutivo

Este incremento formaliza la arquitectura de persistencia desacoplada para snapshots crudos de BoardGameGeek (`BggRawSnapshots`), asegurando que ninguna información original de BGG se pierda tras la catalogación inicial, junto con una batería de correcciones críticas y mejoras de UX en la ficha de juego, el retorno de autenticación y la experiencia de filtrado y ordenación en el catálogo.

---

## 2. Requerimientos Funcionales y de Dominio

### Slice A: Ficha Editorial, Permisos y Retorno de Sesión

#### REQ-A1: Unificación de Enlaces a BGG en la Ficha de Juego
* **Problema:** En [`GameDetail.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Pages/GameDetail.razor) existen dos enlaces hacia BoardGameGeek: uno en la barra de acciones superior y otro en la línea de metadatos del juego (`Ver en BGG`).
* **Solución:** Retirar el botón duplicado de la botonera superior y consolidar la insignia canónica en la fila de metadatos junto al año y el ranking BGG (`Ver en BGG`), evitando redundancia visual.

#### REQ-A2: Restricción de Autorización en «Cartel para Redes»
* **Problema:** El botón «Cartel para Redes» en la barra de acciones de `GameDetail.razor` se renderiza de forma incondicional para cualquier visitante, incluso usuarios anónimos sin rol administrativo.
* **Solución:** Proteger el botón con la guarda de autorización `@if (CurrentUserService.IsFoundingTeam || (CurrentUserService.IsInRole("Moderator") && CurrentUserService.HasPermission(ModeratorPermission.CanEditGames)))`, haciéndolo visible exclusivamente para moderadores y administradores autorizados.

#### REQ-A3: Edición Multicarrusel en `GameEditorModal`
* **Problema:** [`GameEditorModal.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Shared/GameEditorModal.razor) solo permite actualizar `CoverImageUrl`, impidiendo editar la trasera de caja (`BackCoverImageUrl`) y la foto en mesa (`TableImageUrl`), requeridas para el carrusel de tres imágenes de INC-85.
* **Solución:**
  * Ampliar la pestaña de imágenes del editor para permitir previsualizar y editar las tres URLs (`CoverImageUrl`, `BackCoverImageUrl`, `TableImageUrl`) con subida a Cloudflare R2 vía `IImageStorageService`.
  * Persistir las tres imágenes en `Game` mediante `IGameEditorService`.

#### REQ-A4: Limpieza Editorial en la Tarjeta de Síntesis IA
* **Problema:** [`AiSummaryCard.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Shared/AiSummaryCard.razor) muestra una píldora con el identificador técnico del modelo (`@Summary.Model`), como `gemini-2.0-flash [Batch]`, que genera ruido cognitivo para el usuario no técnico.
* **Solución:** Retirar la pastilla de `@Summary.Model`, conservando la insignia editorial «Síntesis generada por IA» y la fecha de análisis.

#### REQ-A5: Preservación de `returnUrl` en Autenticación Externa
* **Problema:** En el endpoint `/login/external` de [`Program.cs`](file:///f:/repos/Ludeka/src/Ludeka.Web/Program.cs#L337), `AuthenticationProperties.RedirectUri` está fijado a `"/"`, descartando cualquier `returnUrl` que el usuario haya traído al loguearse.
* **Solución:** Leer `form["returnUrl"]`, sanitizarlo mediante `LoginRedirect.IsLocalUrl(returnUrl) ? returnUrl! : "/"` y asignarlo a `AuthenticationProperties.RedirectUri`.

#### REQ-A6: Reordenación de Pestañas Secundarias y Jerarquía Vertical en Ficha
* **Problema:** En [`GameDetail.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Pages/GameDetail.razor), las pestañas secundarias sitúan la «Guía de Fundas» como primera pestaña cuando el «Hub Multimedia» aporta mayor valor inmediato al visitante. Además, el bloque editorial de síntesis IA precede al semáforo de escalabilidad, relegando la información más objetiva y consultada.
* **Solución:**
  * **Pestañas secundarias:** El Hub Multimedia se sitúa como primera pestaña por defecto (`_activeSecondaryTab = "media"`), seguido de Guía de Fundas y Consultorio de Reglas.
  * **Jerarquía vertical:** Tras el carrusel fotográfico, el orden visual pasa a ser:
    1. Semáforo Dinámico de Escalabilidad (jugadores y votos).
    2. Expansiones (si aplica) y Pestañas Secundarias (Hub Multimedia, Fundas, Reglas).
    3. Síntesis generada por IA / Veredicto fundador al final de la columna principal.

#### REQ-A7: Retirada de Edición Territorial Duplicada en Ficha
* **Problema:** En `GameDetail.razor`, la editorial local figura dos veces: en la línea superior (`Diseñado por X • Editorial en España: Y (Orig: Z)`) y de nuevo en un bloque inferior redundante (`Ediciones territoriales: [ES] Y`).
* **Solución:** Retirar el bloque inferior de ediciones territoriales, conservando la línea superior limpia y compacta.

---

### Slice B: Refinamiento del Catálogo y Tarjetas Lúdicas

#### REQ-B1: Retirada de Filtros Rápidos Superiores en Catálogo
* **Problema:** En [`Home.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Pages/Home.razor), los botones de filtros rápidos (`Todos`, `Juegos Base`, `Expansiones`, `Especial Parejas`, `Mesa Familiar`, `Modo Solitario`, `≤ 45 min`) duplican la funcionalidad del panel de filtros avanzados y saturan la cabecera.
* **Solución:** Retirar la botonera de presets rápidos, manteniendo limpia la cabecera del catálogo con el campo de búsqueda principal y el botón colapsable de «Filtros avanzados».

#### REQ-B2: Selector de Ordenación («Ordenar por») en Filtros Avanzados
* **Problema:** El catálogo ordena invariablemente por Ranking BGG. No existe mecanismo para ordenar por dureza (complejidad), duración o año.
* **Solución:**
  * Crear enum `GameSortOrder` con valores: `Rank` (defecto), `RatingDesc`, `ComplexityAsc`, `ComplexityDesc`, `DurationAsc`, `DurationDesc`, `YearDesc`, `TitleAsc`.
  * Extender `GameFilterCriteria` con `GameSortOrder SortBy`.
  * Implementar el orden dinámico en `SqliteGameRepository.SearchAsync` (tanto en la consulta directa como en la paginación en dos fases).
  * Añadir el selector «Ordenar por» en el panel de filtros avanzados de `Home.razor`, sincronizado con el parámetro de URL `orden`.

#### REQ-B3: Rediseño Minimalista de `GameCard.razor` y Leyenda de Iconos
* **Problema:** En las tarjetas del catálogo ([`GameCard.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Shared/GameCard.razor)), el badge de comensales muestra textos largos como `Ideal: 3-4 jugadores...`, los badges inferiores repiten textos extensos (`Euro`, `Solo`, `Mesa estándar`) y el badge «Solo» genera confusión.
* **Solución:**
  * Aplicar el formato compacto de jugadores idéntico al de portada (`<Icon Name="users" Size="11" /> 3-4J`).
  * Reducir los badges inferiores a **solo iconos** con texto accesible en tooltip/`title` al pasar el cursor.
  * Retirar la pastilla «Solo» de la tarjeta individual (el usuario interesado en solitario oficial utiliza el filtro avanzado).
  * Añadir una leyenda accesible de iconografía en la vista de catálogo (desplegable o pie de página) para que los visitantes reconozcan cada símbolo.

#### REQ-B4: Elevación del Catálogo y Retirada de Cabecera Redundante
* **Problema:** En [`Home.razor`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Pages/Home.razor), el bloque `<PageHeaderEditorial>` («Descubre tu próxima partida...») ocupa espacio vertical innecesario cuando el usuario ya ha navegado deliberadamente a la sección de catálogo.
* **Solución:** Suprimir el encabezado editorial para situar la barra de búsqueda y las tarjetas en la zona superior de impacto visual sin scroll forzado.

#### REQ-B5: Corrección de Búsqueda Insensible a Mayúsculas en PostgreSQL (`EF.Functions.ILike`)
* **Problema:** En PostgreSQL (producción), `EF.Functions.Like` genera el operador SQL `LIKE`, que es estrictamente sensible a mayúsculas y minúsculas (*case-sensitive*). Al buscar términos en minúsculas (ej. "ark") o variaciones de capitalización, el motor omite los juegos existentes ("Ark Nova").
* **Solución:** En [`SqliteGameRepository.cs`](file:///f:/repos/Ludeka/src/Ludeka.Infrastructure/Data/SqliteGameRepository.cs), cuando la base de datos sea PostgreSQL (`IsNpgsql()`), emplear `EF.Functions.ILike` para todas las comparaciones de texto en `SearchAsync` y `QuickSearchAsync`, garantizando una búsqueda insensible a mayúsculas/minúsculas.

---

### Slice C: Snapshots Crudos BGG y Poblado Defensivo

#### REQ-C1: Entidad de Dominio y Persistencia Satélite `BggRawSnapshot`
* **Definición:** Entidad `BggRawSnapshot`:
  * `int BggId`: Clave primaria e identificador canónico en BGG.
  * `string RawJson`: Representación JSON estructurada y completa del XML de `/xmlapi2/thing`.
  * `int ApiVersion`: Versión de API (valor `2`).
  * `DateTimeOffset FetchedAtUtc`: Fecha y hora de captura original.
  * `DateTimeOffset? UpdatedAtUtc`: Fecha y hora de refresco.
* **Mapeo:** Tabla `BggRawSnapshots`. En PostgreSQL mapear `RawJson` a `jsonb`; en SQLite a `TEXT`.

#### REQ-C2: Auto-Captura en Pipeline de Catalogación
* En cada consulta exitosa a BGG en `BggXmlApiClient` (o servicios orquestadores de cola), persistir o actualizar de forma transparente el `BggRawSnapshot` correspondiente.

#### REQ-C3: Servicio de Poblado Retroactivo (*Backfill*) con *Rate Limiting*
* **Servicio:** `IBggRawSnapshotSyncService` que busca títulos en `Games` sin registro en `BggRawSnapshots` y los descarga secuencialmente aplicando una pausa de cortesía configurable (por defecto 1.200 ms entre llamadas).
* **Interfaz Administrativa:** En `/admin/cola-catalogacion`, incorporar tarjeta con métricas (total catálogo, capturados y pendientes) y botón para iniciar/pausar la sincronización en vivo, protegido por `CanEditGames`.

#### REQ-C4: Extracción y Vinculación Automática de Expansiones en BGG
* **Extracción en Parser:** En `BggXmlParser.cs`, detectar el atributo `type="boardgameexpansion"` en el `<item>` raíz para clasificar la entidad como `GameType.Expansion`.
* **Identificación del Juego Base:** Para items de tipo expansión, extraer el BGG ID del juego base padre desde `<link type="boardgameexpansion" id="..." value="..." inbound="true" />`.
* **Identificación de Expansiones Vinculadas:** Para juegos base (`type="boardgame"`), extraer los enlaces de salida `<link type="boardgameexpansion" id="..." value="..." />` hacia sus expansiones oficiales.
* **Auto-vinculación en Persistencia:**
  * Al catalogar o sincronizar una expansión, si su juego base ya existe en la base de datos (`Games.Any(g => g.BggId == baseGameBggId)`), enlazar automáticamente `BaseGameId = baseGame.Id`.
  * Al catalogar un juego base, si existen expansiones huérfanas en la base de datos que referencien su BGG ID, vincularlas asignando el nuevo `BaseGameId`.

#### REQ-C5: Detección y Carga Asistida de Expansiones desde Snapshots Crudos
* **Aprovechamiento de Snapshots:** Dado que `BggRawSnapshot.RawJson` contiene todos los enlaces de expansiones sin truncar, proveer un método en el repositorio/servicio de snapshots para listar las expansiones reportadas por BGG para cualquier juego base.
* **Acción Administrativa:** En la administración de catálogo o cola de catalogación, permitir inspeccionar qué expansiones oficiales existen en BGG para un juego y encolar su importación masiva o individual hacia `Games`.

---

## 3. Criterios de Aceptación y Casos de Prueba (Gherkin)

### Escenario 1: Retorno Exitoso de Sesión tras Login Externo
```gherkin
Dado un usuario no autenticado en la ruta "/juegos/brass-birmingham"
Cuando pulsa en Iniciar Sesión y completa el flujo OAuth externo
Entonces es redirigido exactamente a "/juegos/brass-birmingham"
Y no a la página de portada "/"
```

### Escenario 2: Protección de Cartel para Redes
```gherkin
Dado un visitante anónimo o usuario sin permisos de moderación
Cuando visita la ficha de cualquier juego "/juegos/{slug}"
Entonces el botón "Cartel para Redes" no es visible en la interfaz
```

### Escenario 3: Ordenación del Catálogo por Dureza y Duración
```gherkin
Dado el catálogo público de juegos en "/catalogo"
Cuando el usuario abre los filtros avanzados y selecciona ordenar por "Mayor dureza"
Entonces los resultados se ordenan de mayor a menor complejidad cognitiva
Y la URL se sincroniza con el parámetro "orden=dureza-desc"
```

### Escenario 4: Persistencia y Aislamiento de Snapshot Crudo BGG
```gherkin
Dado un juego catalogado desde BGG con ID 224517
Cuando se procesa el XML devuelto por BGG
Entonces se almacena un registro en "BggRawSnapshots" con el payload JSON completo
Y la tabla "Games" permanece ligera sin almacenar el blob crudo
```

### Escenario 5: Vinculación Bidireccional de Expansiones de BGG
```gherkin
Dado un juego base catalogado con BGG ID 167791 ("Terraforming Mars")
Y una expansión catalogada con BGG ID 218127 ("Terraforming Mars: Hellas & Elysium") cuyo enlace inbound apunta a 167791
Cuando el sistema procesa o sincroniza los metadatos desde BGG
Entonces la expansión se clasifica como GameType.Expansion
Y su "BaseGameId" queda enlazado al Guid del juego base "Terraforming Mars"
Y en la ficha de "Terraforming Mars" la expansión aparece listada en la sección de ecosistema
```
