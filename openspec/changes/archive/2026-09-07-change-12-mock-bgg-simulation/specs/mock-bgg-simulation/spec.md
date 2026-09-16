# Especificación de Requerimientos: mock-bgg-simulation (Incremento 12)

## 1. Contexto y Propósito

El sistema Ludeka provee funciones de ingesta y enriquecimiento de catálogo, importación de colecciones de usuario y gestión comunitaria de importaciones mediante la API XMLAPI2 de BoardGameGeek.
Para garantizar la autonomía, estabilidad, reproducibilidad en CI/CD y experiencia de desarrollo y testing sin depender de la disponibilidad externa ni de credenciales de BGG, este incremento introduce una suite de simulación completa basada en un dataset tipado de 40 títulos (30 juegos base canónicos + 10 expansiones oficiales).

---

## 2. Requerimientos Funcionales

### RF-01: Dataset Canónico de 40 Títulos del Hobby
- El sistema debe contar con una fuente de datos estructurada con 40 títulos lúdicos icónicos con datos reales:
  - **30 Juegos Base:**
    - Catan (13)
    - Carcassonne (822)
    - Wingspan (266192)
    - Terraforming Mars (167791)
    - 7 Wonders Duel (173346)
    - Ark Nova (342942)
    - Azul (230802)
    - Cascadia (295947)
    - Dune: Imperium (316554)
    - Everdell (199792)
    - Gloomhaven (174430)
    - Heat: Pedal to the Metal (366013)
    - Scythe (169786)
    - Spirit Island (162886)
    - Splendor (148228)
    - The Crew: Misión Mar Profundo (324856)
    - Brass: Birmingham (224517)
    - Clank! (201808)
    - Pandemic (30549)
    - Root (237182)
    - Patchwork (163412)
    - ¡Aventureros al Tren! (Ticket to Ride) (9209)
    - Concordia (138076)
    - Love Letter (129622)
    - Viticulture Essential Edition (180263)
    - Agrícola (31260)
    - Las Ruinas Perdidas de Arnak (312484)
    - Dixit (39856)
    - Código Secreto (178900)
    - Great Western Trail (193738)
  - **10 Expansiones Oficiales:**
    - Wingspan: Expansión Europa (290448)
    - Wingspan: Expansión Oceanía (300580)
    - Wingspan: Expansión Asia (366161)
    - Terraforming Mars: Preludio (247030)
    - Terraforming Mars: Hellas & Elysium (230914)
    - Carcassonne: Posadas y Catedrales (2993)
    - Carcassonne: Constructores y Comerciantes (8443)
    - 7 Wonders Duel: Pantheon (202976)
    - Dune: Imperium - El Auge de Ix (342035)
    - Everdell: Bellfaire (265492)
- Cada título debe incluir:
  - Metadatos básicos: BggId, OriginalTitle, SpanishTitle, Designer, Publisher, YearPublished, CoverImageUrl, ThumbnailUrl, Description, BggRating, BggRank, LudistRating.
  - Clasificación lúdica: ConfrontationType, GameStyle, IsOfficialSolo.
  - Requisitos de mesa: AgeRating (BoxAge, CommunityAge), LanguageDependence, TableFootprint, GameDuration (Min, Max, PerPlayer).
  - Semáforo de escalabilidad: Lista de `ScalabilityEntry` (1J a 7J+ con BestVotes, RecommendedVotes, NotRecommendedVotes y Status calculado).
  - Guía de fundas: Lista de `SleeveItem` (FormatName, WidthMm, HeightMm, CardCount).
  - Enlaces de compra: Lista de `GamePurchaseLink` (StoreName, AffiliateUrl, Price, Currency, InStock, Badge, AffiliateTag).
  - En expansiones: `GameType` (Expansion / StandaloneExpansion), `ExpansionNecessity`, lista de `ExpansionImpactTag`, `WhatItBringsSummary`, `ExtraPlayerCount`, `ExtraDurationMinutes`.

### RF-02: Cliente BGG Simulado (`SimulatedBggClient`)
- Debe implementar la interfaz `IBggClient`:
  - `Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default)`:
    - Retorna una instancia `Game` con todos sus Value Objects correspondientes al `bggId`.
    - Retorna `null` si el `bggId` no existe en el catálogo simulado.
  - `Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default)`:
    - Busca coincidencias parciales sin distinción de mayúsculas/minúsculas ni acentos en `SpanishTitle`, `OriginalTitle` y `Designer`.
    - Retorna DTOs `BggSearchResultDto` ordenados por relevancia.
  - `Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(string username, CancellationToken ct = default)`:
    - Para `ludeka_demo`: devuelve colección equilibrada (12 juegos) con mezcla de posesión (`IsOwned`), deseados (`IsWishlist`) y lista de compra (`IsWantToBuy`).
    - Para `pareja_jugona`: devuelve colección de títulos estelares a 2 jugadores.
    - Para `maraton_euro`: devuelve colección de eurogames medios y pesados.
    - Para cualquier otro usuario: devuelve una colección por defecto de 6 a 8 juegos canónicos.

### RF-03: Conmutador de Configuración y Resiliencia
- `BggOptions` debe incorporar:
  - `public bool SimulateApi { get; set; } = true;`
  - `public bool ShouldSimulate => SimulateApi || string.IsNullOrWhiteSpace(ApiToken);`
- En `Program.cs`, el registro de `IBggClient` debe resolver `SimulatedBggClient` si `ShouldSimulate` es `true`, o `BggXmlApiClient` si `ShouldSimulate` es `false`.

### RF-04: Semillado Inicial Ampliado
- `seed-games.json` debe contener los 30 juegos base legendarios con todas sus propiedades reales.
- `CatalogSeeder.cs` debe semillar las 10 expansiones oficiales con sus sinergias par-a-par (`ExpansionSynergy`) y recetas de mesa (`ExpansionRecipe`).

---

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Búsqueda asistida en modo simulado por título en español
  Dado que el sistema opera en modo simulado
  Cuando se busca "catán" o "catan"
  Entonces se obtiene el resultado "Catán" con BggId 13 y año 1995

Escenario: Búsqueda asistida en modo simulado por autor
  Dado que el sistema opera en modo simulado
  Cuando se busca "Uwe Rosenberg"
  Entonces se obtienen títulos diseñados por el autor como "Patchwork" y "Agrícola"

Escenario: Recuperación de juego por BggId en modo simulado
  Dado que se invoca FetchGameByBggIdAsync con el identificador 316554
  Entonces se retorna la entidad "Dune: Imperium" con semáforo de comensales, fundas y enlaces de compra

Escenario: Importación de colección de usuario simulado "ludeka_demo"
  Dado que el usuario solicita importar la colección del usuario "ludeka_demo"
  Cuando se ejecuta el servicio de importación
  Entonces se obtienen 12 elementos de colección
  Y los juegos presentes en la base de datos se asignan a la ludoteca del usuario
  Y los juegos ausentes en la base de datos se encolan en la tabla de importaciones pendientes

Escenario: Conmutación transparente cuando no hay token configurado
  Dado que Bgg:SimulateApi es false pero Bgg:ApiToken es una cadena vacía
  Cuando se evalúa BggOptions.ShouldSimulate
  Entonces el valor es true
```
