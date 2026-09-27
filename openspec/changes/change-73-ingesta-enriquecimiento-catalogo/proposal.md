# Propuesta: change-73-ingesta-enriquecimiento-catalogo (Incremento 73: Ingesta Masiva BGG >100 opiniones, Anti-Duplicados, Enriquecimiento Integral y Localización Multipaís de Editoriales y Títulos)

## 1. Resumen Ejecutivo y Motivación

En Ludeka, el catálogo de juegos de mesa es el pilar central sobre el que se articulan la ludoteca personal, las estadísticas de jugador, los filtros de búsqueda, las fichas con ADN lúdico y los enlaces a tiendas afiliadas.

Tras la ingesta masiva inicial (~4.000 títulos mediante el umbral `usersrated >= 1000`) y las mejoras de experiencia de usuario del Incremento 72 (filtros multiselección, carrusel de tres fotos, retiro de textos en inglés), se abordan dos grandes bloques estratégicos:

### Bloque A: Ingesta Masiva y Calidad Profunda de Metadatos
1. **Ampliación del fondo de catálogo (Umbral >100 opiniones):**
   - Modificar el filtro del volcado masivo de BGG (`bg_ranks`) para admitir juegos con más de 100 opiniones (`minUsersRated = 100`), expandiendo el catálogo con joyas de nicho y producciones nacionales y latinoamericanas contrastadas.
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

### Bloque B: Localización Territorial Multipaís (Editoriales y Títulos en la Comunidad Ludeka)
8. **Padrón de Editoriales para Todos los Países Soportados por Ludeka:**
   - Ludeka da soporte territorial estandarizado a través de `CountryCatalog`: **España (ES), México (MX), Argentina (AR), Chile (CL), Colombia (CO), Perú (PE) y Uruguay (UY)**.
   - Hasta ahora, el directorio de editoriales se centraba principalmente en España (INC-54). En este incremento se amplía el padrón de `seed-directory.json` incorporando las editoriales más representativas de cada país hispanohablante:
     - **México:** Devir México, Fractal Juegos México, Taj Mahal Games, Kokonem Games.
     - **Argentina:** Bureau de Juegos, El Troquel, Maldón, Ruibal, Pulga Escapista, ToyCo, Tinkuy.
     - **Chile:** Fractal Juegos, Devir Chile, Ludoismo, Dentro de la Caja.
     - **Colombia:** Devir Colombia, Borrasca Juegos.
     - **Perú:** Malabares Juegos.
     - **Uruguay:** Bicho Canasto.
     - **España:** Mantenimiento y actualización de las 46 editoriales canónicas existentes.
9. **Detección y Mapeo Multipaís de Editoriales (`RegionalPublishers` y `SpanishPublisher`):**
   - En la API de BGG, cada juego dispone de múltiples enlaces `<link type="boardgamepublisher">` que reflejan tanto la editorial original extranjera (ej. *Lookout Games*, *Roxley*, *Czech Games Edition*, *Hans im Glück*) como los sellos que licencian el juego en cada territorio.
   - Se crea el resolvedor `RegionalPublisherMatcher` alimentado con el padrón multipaís. Al parsear el juego, se extraen y clasifican todas las editoriales locales asociadas por país:
     - En España: Maldito Games, Devir, Asmodee, Tranjis Games, etc.
     - En Argentina: Bureau de Juegos, Ruibal, Maldón, etc.
     - En México: Devir México, Fractal Juegos México, etc.
     - En Chile: Fractal Juegos, Devir Chile, etc.
   - En `Game`: se persiste la colección `RegionalPublishers` (mapeada a JSON) y se mantiene `SpanishPublisher` como acceso directo para compatibilidad, preservando `Publisher` para el sello original internacional.
   - Método `GetPublisherForCountry(string? country)`: devuelve la editorial local del país del usuario (o fallback al sello original).
10. **Títulos Comerciales Multipaís (`SpanishTitle`, `LocalizedTitles` vs `OriginalTitle`):**
    - En BGG, `<name type="alternate">` y las versiones específicas contienen los nombres comerciales con los que se vende el juego en el mercado hispanohablante.
    - Se extrae `SpanishTitle` y se almacena la colección de variantes en `LocalizedTitles`.
    - Método `GetTitleForCountry(string? country)`: devuelve el nombre comercial adaptado según el país del usuario.
11. **Experiencia de Usuario en Ficha y Tarjetas (`GameDetail.razor` y `GameCard.razor`):**
    - En la ficha de juego (`GameDetail.razor`):
      - Si el usuario tiene un país configurado o detectado (ej. España, Argentina, México, Chile...), se muestra destacada la **Editorial en {País}** con enlace directo a su ficha en `/directorios/editoriales/{slug}`.
      - Si difiere de la editorial creadora internacional, se indica: *"Editorial original: {Publisher}"*.
      - Se ofrece un desglose visual accesible de otras ediciones en países de la comunidad (*"Disponible en: 🇪🇸 Maldito Games · 🇦🇷 Bureau de Juegos · 🇲🇽 Devir México"*).
      - El título se muestra en su versión comercial hispanohablante, con el título original anglosajón como subtítulo si es diferente.
12. **Integración con Directorios y Buscador:**
    - En `/directorios/editoriales/{slug}`: las editoriales de cualquier país (tanto españolas como argentinas, mexicanas o chilenas) listan de forma inmediata todos los juegos de su catálogo.
    - En `SqliteGameRepository.SearchAsync`: el buscador localiza juegos cruzando `SpanishTitle`, `OriginalTitle`, `Publisher`, `SpanishPublisher` y los nombres en `RegionalPublishers`.
    - En `SqliteGameRepository.GetByPublisherAsync`: localiza juegos publicados por la editorial en cualquiera de los países soportados.

---

## 2. Metodología de Implementación

Por instrucción expresa del mantenedor, este incremento **no** se ejecuta bajo el ciclo estricto TDD (sin alternancia forzada de commits red/green unitarios paso a paso). Se adopta una **implementación directa y robusta por componentes y capas**, respaldada por la verificación automatizada al 100% de la suite de pruebas al cierre del incremento.

---

## 3. Arquitectura y Alcance por Capas

### 3.1 Dominio (`Ludeka.Core`)
- **`RegionalPublisherEntry`**:
  - Value object / record: `(string CountryCode, string CountryName, string PublisherName, string? PublisherSlug)`.
- **`LocalizedTitleEntry`**:
  - Value object / record: `(string CountryCode, string Title)`.
- **`BggCatalogStagingItem`**:
  - `MinPlayTimeMinutes`, `MaxPlayTimeMinutes`, `InferredFootprint`, `ScalabilityJson`, `SleevesJson`.
  - `SpanishPublisher` (string?).
  - `RegionalPublishersJson` (string?).
  - Métodos `GetScalability()`, `GetSleeves()`, `GetRegionalPublishers()`, `UpdateInferredFootprint()`, `UpdateSpanishPublisher()`.
- **`Game`**:
  - Propiedad: `public string? SpanishPublisher { get; private set; }`.
  - Colección: `public List<RegionalPublisherEntry> RegionalPublishers { get; private set; } = [];`.
  - Colección: `public List<LocalizedTitleEntry> LocalizedTitles { get; private set; } = [];`.
  - Métodos utilitarios:
    - `GetPublisherForCountry(string? country = null)`
    - `GetTitleForCountry(string? country = null)`
    - `UpdateScalability(IEnumerable<ScalabilityEntry> scalability)`
    - `UpdateDuration(GameDuration duration)`
    - `UpdateFootprint(TableFootprint footprint)`
    - `UpdateSleeves(IEnumerable<SleeveItem> sleeves)`
    - `UpdateRegionalPublishers(IEnumerable<RegionalPublisherEntry> publishers, string? spanishPublisher = null)`
    - `UpdateLocalizedTitles(IEnumerable<LocalizedTitleEntry> titles)`

### 3.2 Aplicación (`Ludeka.Application`)
- **`BggMassIngestionOptions`**:
  - `MinUsersRated = 100`.
- **`BggDumpParser`**:
  - `minUsersRated = 100`.
- **`IBggMassIngestionService` y `BggMassIngestionService`**:
  - Resolución de editoriales regionales con `RegionalPublisherMatcher` cruzando contra el padrón de España, México, Argentina, Chile, Colombia, Perú y Uruguay.
  - Extracción de títulos comerciales desde BGG.
  - Promoción aditiva anti-duplicados por `BggId`: creación limpia de nuevos y actualización aditiva de existentes (incluyendo editoriales regionales).
  - Métodos `BackfillCatalogQualityBatchAsync` y `RunScheduledBackfillCatalogQualityBatchAsync`.
- **`IGameRepository`**:
  - `GetGamesPendingQualityBackfillAsync(int limit = 50, CancellationToken ct = default)`.
  - Actualización de `GetByPublisherAsync` y `SearchAsync` para buscar en `Publisher`, `SpanishPublisher` y `RegionalPublishers`.

### 3.3 Infraestructura (`Ludeka.Infrastructure`)
- **`seed-directory.json`**:
  - Incorporación de las editoriales líderes de México, Argentina, Chile, Colombia, Perú y Uruguay.
- **`RegionalPublisherMatcher`**:
  - Catálogo estático y normalizador de coincidencias de editoriales por país.
- **`BggXmlParser`**:
  - Extrae todos los `<link type="boardgamepublisher">` y resuelve las editoriales regionales por país y la editorial original.
  - Fallback determinista de escalabilidad sin votos.
  - Inferencia de `TableFootprint` y tiempos reales.
- **`LudekaDbContext`**:
  - Configurar `RegionalPublishers` y `LocalizedTitles` mapeados con `.ToJson()` en `Game`.
  - Configurar `SpanishPublisher` como columna indexada.
  - Columnas correspondientes en `BggCatalogStaging`.
  - Migración EF Core `AddStagingQualityFields` (y extensión de columnas).
- **`SqliteGameRepository`**:
  - Búsqueda y filtrado compatible con editoriales regionales y títulos localizados.

### 3.4 Interfaz de Usuario (`Ludeka.Web`)
- **`GameDetail.razor`**:
  - Detección del país del usuario vía `ICurrentUserService` o `UserPreferences`.
  - Presentación destacada de la editorial local del país con enlace al directorio `/directorios/editoriales/{slug}`.
  - Editorial original como referencia secundaria si difiere.
  - Desglose de ediciones en otros países si existen.
  - Título adaptado al idioma/país, con subtítulo de título original si difiere.
- **`GameCard.razor`**:
  - Muestra la editorial relevante según el país del contexto.
- **`PublisherDetail.razor`**:
  - Asegura que cualquier editorial del directorio (nacional o internacional) muestre sus juegos asociados.

---

## 4. Criterios de Aceptación

1. **Umbral >100:** `MinUsersRated = 100` por defecto en opciones y volcado masivo.
2. **Idempotencia:** Juegos preexistentes en catálogo se actualizan de forma aditiva por `BggId` sin generar errores de clave única.
3. **Escalabilidad y Fundas:** Fichas con semáforo poblado y datos de fundas persistidos.
4. **Huella y Tiempos Reales:** Huella diferenciada (`SmallTable`, `StandardTable`, `TableMonster`) y duraciones reales de BGG.
5. **Editoriales Multipaís:** Juegos asociados a sus editoriales en España, México, Argentina, Chile, Colombia, Perú y Uruguay cuando estén disponibles en BGG.
6. **Directorio de Editoriales Multipaís:** `seed-directory.json` incluye editoriales de los países soportados y sus fichas listan sus juegos.
7. **Ficha de Juego Adaptada:** Muestra editorial local y original, y título adaptado según preferencia de país.
8. **Búsquedas:** El buscador del catálogo encuentra juegos por título en español, título original, editorial original y editoriales locales.
9. **Pruebas:** 100% de la suite de pruebas automatizadas en verde (>1.950 tests).
