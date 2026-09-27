# Propuesta: change-73-ingesta-enriquecimiento-catalogo (Incremento 73: Ampliación de Ingesta Masiva BGG >100 opiniones, Descarte de Duplicados, Enriquecimiento Integral y Localización Territorial de Editoriales y Títulos)

## 1. Resumen Ejecutivo y Motivación

En Ludeka, el catálogo de juegos de mesa es el pilar central sobre el que se articulan la ludoteca personal, las estadísticas de jugador, los filtros de búsqueda, las fichas con ADN lúdico y los enlaces a tiendas afiliadas.

Tras la ingesta masiva inicial (~4.000 títulos mediante el umbral `usersrated >= 1000`) y las mejoras de experiencia de usuario del Incremento 72 (filtros multiselección, carrusel de tres fotos, retiro de textos en inglés), se abordan dos grandes bloques estratégicos:

### Bloque A: Ingesta Masiva y Calidad Profunda de Metadatos
1. **Ampliación del fondo de catálogo (Umbral >100 opiniones):**
   - Modificar el filtro del volcado masivo de BGG (`bg_ranks`) para admitir juegos con más de 100 opiniones (`minUsersRated = 100`), expandiendo el catálogo con joyas de nicho y producciones nacionales contrastadas.
2. **Blindaje anti-duplicados y actualización incremental:**
   - Detección estricta por `BggId`: los juegos ya existentes en la tabla `Games` no se reinsertan (evitando colisiones en el índice único), sino que se actualizan de forma aditiva con los nuevos metadatos.
3. **Escalabilidad comunitaria con fallback determinista:**
   - Extracción de la encuesta `<poll name="suggested_numplayers">` y generación de recomendaciones (`MustPlay`, `Recommended`) según el rango oficial cuando la encuesta carece de votos comunitarios.
4. **Ingesta de tamaños de fundas (Sleeves):**
   - Extracción de enlaces `boardgamecardsleeve` para informar las fundas de cartas requeridas y conectar con los enlaces de compra de INC-66.
5. **Corrección de huella en mesa (`TableFootprint`):**
   - Inferencia analítica (`SmallTable`, `StandardTable`, `TableMonster`) según categorías lúdicas, componentes y duraciones.
6. **Consistencia de duraciones:**
   - Tiempos reales de partida (`MinPlayTimeMinutes`, `MaxPlayTimeMinutes`) y estimación realista por jugador (`EstimatedPerPlayerMinutes`).
7. **Backfill retroactivo:**
   - Mecanismo por lotes para enriquecer el catálogo actual sin necesidad de reingesta destructiva.

### Bloque B: Localización Territorial (Editoriales en España y Títulos Adaptados)
8. **Detección y Mapeo de la Editorial en España (`SpanishPublisher`):**
   - En BGG, cada juego dispone de múltiples enlaces `<link type="boardgamepublisher">` que reflejan tanto la editorial original extranjera (ej. *Lookout Games*, *Roxley*, *Czech Games Edition*, *Hans im Glück*) como las editoriales que licencian el juego internacionalmente.
   - En España, títulos icónicos se publican por editoriales locales (ej. *Maldito Games* publica *Brass* y *Ark Nova*; *Devir* publica *Catán*, *Carcassonne* y *Terraforming Mars*; *Asmodee* publica *7 Wonders*; *Tranjis Games* publica *Virus!*).
   - Analizar los enlaces de editorial cruzándolos con el padrón exhaustivo del Directorio Lúdico Español (`seed-directory.json`) para extraer e informar `SpanishPublisher`.
   - En la ficha de juego (`GameDetail.razor`):
     - Mostrar de forma preferente la editorial en España, con enlace directo a su ficha en el directorio (`/directorios/editoriales/{slug}`).
     - Indicar de forma secundaria la editorial original internacional (ej. *"Editorial en España: Maldito Games · Editorial original: Roxley"*).
   - En el directorio de editoriales (`PublisherDetail.razor`): los juegos quedarán correctamente vinculados a su editorial española, listando el catálogo real que cada editorial edita y distribuye en nuestro territorio.
9. **Títulos en España vs Títulos Internacionales (`SpanishTitle` vs `OriginalTitle`):**
   - En BGG, el nombre primario suele ser anglosajón, mientras que en `<name type="alternate">` o en versiones españolas figuran los títulos comercializados en España (ej. *"Los Colonos de Catán"*, *"Toma 6"*, *"Ciudadelas"*, *"La Tripulación"*).
   - Extraer y preservar el título comercial en español y mostrar el título original como referencia cuando difieran.
   - Adaptar la vista según el país del usuario (`UserPreferences.Country` / INC-29 / INC-62): priorizar el título en español para usuarios en España y países hispanohablantes.
10. **Búsquedas y Filtros Multidimensionales:**
    - Permitir que el buscador del catálogo encuentre juegos tanto por su título español como por su título original, y tanto por su editorial española como por su editorial original.

---

## 2. Metodología de Implementación (Directa sin TDD Estricto)

Por instrucción expresa del mantenedor, este incremento **no** se ejecuta bajo el ciclo estricto TDD (sin obligatoriedad de alternar commits red/green unitarios previos). Se adopta una implementación directa y robusta por componentes y capas, respaldada por la verificación automatizada al 100% de la suite de pruebas al cierre del incremento.

---

## 3. Arquitectura y Alcance por Capas

### 3.1 Dominio (`Ludeka.Core`)
- **`BggCatalogStagingItem`**:
  - `MinPlayTimeMinutes`, `MaxPlayTimeMinutes`, `InferredFootprint`, `ScalabilityJson`, `SleevesJson`.
  - `SpanishPublisher` (string?).
  - Métodos `GetScalability()`, `GetSleeves()`, `UpdateInferredFootprint()`, `UpdateSpanishPublisher()`.
- **`Game`**:
  - Nueva propiedad: `public string? SpanishPublisher { get; private set; }`.
  - Métodos de actualización aditiva:
    - `UpdateScalability(IEnumerable<ScalabilityEntry> scalability)`
    - `UpdateDuration(GameDuration duration)`
    - `UpdateFootprint(TableFootprint footprint)`
    - `UpdateSleeves(IEnumerable<SleeveItem> sleeves)`
    - `UpdateSpanishPublisher(string? spanishPublisher)`

### 3.2 Aplicación (`Ludeka.Application`)
- **`BggMassIngestionOptions`**:
  - `MinUsersRated = 100`.
- **`BggDumpParser`**:
  - `minUsersRated = 100`.
- **`IBggMassIngestionService` y `BggMassIngestionService`**:
  - Extracción de editoriales BGG y resolución de la editorial en España mediante matching con el catálogo canónico de editoriales nacionales (`SpanishPublisherMatcher`).
  - Extracción de títulos en español desde nombres alternativos de BGG y síntesis IA.
  - Promoción y deduplicación por `BggId`: inserción de nuevos títulos y actualización aditiva de existentes (incluyendo `SpanishPublisher`).
  - Métodos `BackfillCatalogQualityBatchAsync` y `RunScheduledBackfillCatalogQualityBatchAsync`.
- **`IGameRepository`**:
  - `GetGamesPendingQualityBackfillAsync(int limit = 50, CancellationToken ct = default)`.
  - Actualización de `GetByPublisherAsync` y `SearchAsync` para buscar tanto por `Publisher` como por `SpanishPublisher`.

### 3.3 Infraestructura (`Ludeka.Infrastructure`)
- **`BggXmlParser`**:
  - Extraer todos los publicadores `<link type="boardgamepublisher">`.
  - Cruzar con el catálogo de editoriales españolas para asignar `spanishPublisher` y mantener `originalPublisher`.
  - Fallback determinista de escalabilidad sin votos.
  - Inferencia de `TableFootprint` y tiempos reales.
- **`LudekaDbContext`**:
  - Configurar `SpanishPublisher` en `Game` y en `BggCatalogStagingItem`.
  - Migración EF Core `AddStagingQualityFields` (y columna `SpanishPublisher` en `Games`).
- **`SqliteGameRepository`**:
  - Búsqueda y filtrado compatible con `SpanishPublisher` y `SpanishTitle`.

### 3.4 Interfaz de Usuario (`Ludeka.Web`)
- **`GameDetail.razor`**:
  - En la cabecera / metadatos:
    - Si `SpanishPublisher` está informado y difiere de `Publisher`, mostrar:
      - Editorial en España con enlace a `/directorios/editoriales/{slug}`.
      - Editorial original internacional.
    - Si el título en español difiere del original, mostrar el título original en subtítulo discreto.
- **`GameCard.razor`**:
  - Mostrar la editorial relevante (`SpanishPublisher ?? Publisher`).
- **`PublisherDetail.razor`**:
  - Muestra todos los juegos publicados por la editorial en España gracias a la búsqueda por `SpanishPublisher`.

---

## 4. Criterios de Aceptación

1. **Umbral >100:** `MinUsersRated = 100` por defecto en opciones y volcado.
2. **Idempotencia:** Juegos preexistentes en catálogo se actualizan de forma aditiva por `BggId` sin generar errores de clave única.
3. **Escalabilidad y Fundas:** Fichas con semáforo poblado y datos de fundas persistidos.
4. **Huella y Tiempos Reales:** Huella diferenciada (`SmallTable`, `StandardTable`, `TableMonster`) y duraciones reales de BGG.
5. **Editorial en España:** Juegos publicados en España por editoriales nacionales identifican `SpanishPublisher` y se asocian a las fichas del Directorio de Editoriales.
6. **Títulos en Español:** Fichas muestran el título comercial en español y conservan el título original.
7. **Búsquedas:** Buscar por el nombre de la editorial española devuelve sus juegos.
8. **Pruebas:** 100% de la suite de pruebas automatizadas en verde.
