# 01. Catálogo y Ficha Inteligente

## 1. Visión General y Propósito
El módulo de catálogo gestiona los juegos de mesa registrados en Ludeka, proveyendo metadatos editoriales completos, normalización de URLs amigables para SEO (*slugs*), píldoras de ADN Lúdico, el Semáforo Dinámico de Escalabilidad, guía de fundas y enlaces a tiendas especializadas.

---

## 2. Modelo de Dominio (`Ludeka.Core`)

### 2.1 Entidad Raíz: `Game`
Ubicación: [`src/Ludeka.Core/Entities/Game.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/Game.cs)

- **Identificadores:** `Id` (Guid), `BggId` (int), `Slug` (string único normalizado sin diacríticos ni caracteres especiales).
- **Títulos:** `OriginalTitle` (string), `SpanishTitle` (string comercial en España con fallback automático a `OriginalTitle`), `LocalizedTitles` (colección JSON de [`LocalizedTitleEntry`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/LocalizedTitleEntry.cs) por país, INC-73).
- **Créditos y Publicación:** `Designer`, `Publisher` (sello editorial original internacional), `SpanishPublisher` (editorial licenciante en España, INC-73), `RegionalPublishers` (colección JSON de [`RegionalPublisherEntry`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/RegionalPublisherEntry.cs) clasificadas por país hispanohablante, INC-73), `YearPublished`.
- **Imágenes:** `CoverImageUrl` (alta definición con `fetchpriority="high"`), `ThumbnailUrl`.
- **Calificaciones:** `BggRating` (double), `BggRank` (int?), `LudistRating` (double [0.0 - 10.0] ponderado por la comunidad hispana).
- **ADN Lúdico:**
  - `Confrontation`: Enum `ConfrontationType` (`Competitive`, `Cooperative`, `SemiCooperative`, `HiddenRoles`, `TeamVsTeam`).
  - `Style`: Enum `GameStyle` (`Eurogame`, `Ameritrash`, `PartyGame`, `Filler`, `Abstract`, `Wargame`, `Deckbuilder`, `Drafting`, `DungeonCrawler`, `Legacy`, `RollAndWrite`, `SocialDeduction`, `Trivia`, `Dexterity`, `EngineBuilding`, `WorkerPlacement`).
  - `IsOfficialSolo`: booleano indicativo de variante oficial para 1 persona.
  - `Age`: Value Object [`AgeRating`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/AgeRating.cs) (`BoxAge` vs. `CommunityAge`).
  - `Language`: Enum `LanguageDependence` (`None`, `Low`, `Moderate`, `High`, `Critical`).
  - `Footprint`: Enum `TableFootprint` (`SmallTable`, `StandardTable`, `LargeTable`, `TableMonster`).
  - `Duration`: Value Object [`GameDuration`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/GameDuration.cs) (`MinMinutes`, `MaxMinutes`, `EstimatedPerPlayerMinutes`).
- **Semáforo de Escalabilidad:** Colección de [`ScalabilityEntry`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/ScalabilityEntry.cs) (1 a 7+ comensales).
  - Estados: `MustPlay` (🟢 Imprescindible), `Recommended` (🟡 Recomendado), `NotRecommended` (🔴 No recomendado).
  - Votos: `BestVotes`, `RecommendedVotes`, `NotRecommendedVotes`.
  - Fallback determinista en ausencia de encuesta comunitaria: asigna `Recommended` al rango de jugadores base e `MustPlay` si `min == max` (INC-73).
  - Etiqueta calculada en tiempo real: `IdealPlayersLabel` (ej. *"Ideal a 2 y 4 jugadores"*).
- **Guía de Fundas de Cartas:** Colección de [`SleeveItem`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/SleeveItem.cs) (`FormatName`, `WidthMm`, `HeightMm`, `CardCount`, `AffiliateUrl`).
- **Enlaces de Compra en Tiendas:** Colección de [`GamePurchaseLink`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/GamePurchaseLink.cs) (`StoreName`, `AffiliateUrl`, `Price`, `Currency`, `InStock`, `Badge`, `AffiliateTag`).

---

## 3. Capa de Aplicación (`Ludeka.Application`)

- **Contrato:** [`IGameRepository`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IGameRepository.cs)
  - `QuickSearchAsync(term, limit, ct)` (INC-82: búsqueda relámpago con límite SQL, orientada a modales y autocompletado en 1-2 ms)
  - `GetBySlugAsync(slug, ct)`
  - `GetByBggIdAsync(bggId, ct)`
  - `SearchAsync(criteria, page, pageSize, ct)` (actualizado en INC-73 para indexar editoriales y en INC-82 para paginación nativa SQL)
  - `GetByPublisherAsync(publisher, page, pageSize, ct)` (ampliado en INC-73 para enlazar juegos con sellos regionales)
  - `GetGamesPendingQualityBackfillAsync(limit, ct)` (INC-73: selector de títulos para enriquecimiento retroactivo)
  - `GetTopGamesAsync(limit, ct)`
- **Servicio y Caché:**
  - [`CatalogService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Catalog/CatalogService.cs): `GetQuickSearchAsync` enriquecido en INC-82 para consultar prioritariamente `QuickSearchAsync` en base de datos.
  - [`CachedCatalogService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Catalog/CachedCatalogService.cs): Decorador sobre `ICatalogService` con `IMemoryCache` (TTL de 10 minutos e invalidación reactiva por slug).
- **DTOs y Resultados:** [`GameSummaryDto`](file:///c:/repos/Ludeka/src/Ludeka.Application/DTOs/GameSummaryDto.cs), [`GameDetailDto`](file:///c:/repos/Ludeka/src/Ludeka.Application/DTOs/GameDetailDto.cs) (con helpers `GetPublisherForCountry` y `GetTitleForCountry`), [`CatalogResult`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Catalog/ICatalogService.cs) (con propiedad calculada `TotalPages`), [`GameFilterCriteria`](file:///c:/repos/Ludeka/src/Ludeka.Application/DTOs/GameFilterCriteria.cs) (soporte facetado con `Footprint`, `PlayerCount`, `MaxDurationMinutes`, `TypeFilter` y blindaje integral del hash SHA-256 en `CachedCatalogService` sobre todas las colecciones multiselección de estilo, confrontación, duración, comensales, huella y complejidad cognitiva).

---

## 4. Persistencia y Base de Datos (`Ludeka.Infrastructure`)

- Tabla `Games` mapeada mediante EF Core en `LudekaDbContext`.
- Value Objects y colecciones mapeados como JSON en SQLite:
  - `Scalability` (`OwnsMany.ToJson()`)
  - `Sleeves` (`OwnsMany.ToJson()`)
  - `PurchaseLinks` (`OwnsMany.ToJson()`)
  - `RegionalPublishers` (`OwnsMany.ToJson()`, INC-73)
  - `LocalizedTitles` (`OwnsMany.ToJson()`, INC-73)
- Índice único en `Slug` e índice en `BggId`.
- Reconciliación preventiva en tiempo de ejecución: [`SqliteSchemaMigrator`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs) con migración EF Core `AddBggQualityAndLocalizationFields` (INC-73).
- **Búsqueda Quirúrgica SQL (`QuickSearchAsync`, INC-82):** Filtrado directo con `EF.Functions.Like` sobre `SpanishTitle`, `OriginalTitle`, `SpanishPublisher`, `Publisher` y `Designer`, ordenado por coincidencia exacta/prefijo y limitado con `.Take(limit)` ejecutado 100% en motor SQLite (1-2 ms).
- **Paginación Nativa SQL (`SearchAsync`, INC-82):** Empuje del `SearchTerm` al query SQL y ejecución nativa en base de datos de `await query.CountAsync(ct)` y `.Skip().Take().ToListAsync(ct)` cuando no intervienen filtros que requieran deserialización JSON en memoria (`EspecialParejas`, `PlayerCounts`, `Complexities`). Reduce el tiempo de respuesta de navegación de catálogo de ~800 ms a ~2 ms y evita materializar 10.000 entidades completas en RAM.

---

## 5. Componentes de Interfaz (`Ludeka.Web`)

- [`Home.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Pages/Home.razor): Buscador reactivo, filtros por estilo y comensales, carrusel de destacados. INC-36: cabecera editorial compartida (`PageHeaderEditorial`), búsqueda en bloque propio y tira de filtros con `scrollbar-none`. INC-58: paginación real completa (24 títulos por página), selector conmutable de modo de vista (Cuadrícula ↔ Lista), contador de títulos y sincronización bidireccional de parámetros en URL (`page`, `view`, `q`, presets) con soporte de historial (`LocationChanged`). INC-59 e INC-72: barra de refinamiento facetado accesible con multiselección completa («¿Me cabe en la mesa?», comensales 1 a 7+, duración ≤30 a ≤120 min, estilos y dureza). INC-82: cancelación cooperativa con `CancellationTokenSource` y `IDisposable` al cambiar rápidamente de filtros o página para abortar consultas intermedias obsoletas.
- [`GameDetail.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Pages/GameDetail.razor): Ficha inteligente completa. INC-36: tokenización editorial. INC-72: visor de imágenes sin textos crudos en inglés. INC-73: resolución contextual de la editorial y título adaptados al país del usuario. INC-85: rediseño editorial completo a dos columnas asimétricas (`lg:grid-cols-12`: 8 columnas para contenido inmersivo y 4 columnas laterales fijas `lg:sticky` para acción y compra), integración del carrusel fotográfico, módulo permanente de tiendas con radar de precios y agrupación de secciones secundarias en pestañas compactas (*Guía de Fundas*, *Hub Multimedia* y *Consultorio de Reglas*).
- [`GameImageCarousel.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/GameImageCarousel.razor) (INC-85): Carrusel fotográfico interactivo con visor principal 4:3/16:9, soporte para carátula oficial, contraportada y fotos de componentes en mesa, contador de diapositivas (`1 / 3`), navegación táctil y por teclado, tira inferior de miniaturas, visor a pantalla completa (**Lightbox modal**) y optimizaciones anti-CLS estrictas (`fetchpriority="high"`, dimensiones 320x320 px y `loading="lazy"` en miniaturas).
- [`StoreOffersCard.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/StoreOffersCard.razor): Módulo de ofertas comerciales y radar de precios adaptado en INC-85 para barra lateral (`IsSidebar="true"`) con fallback automático y determinista a comercios especializados españoles (Zacatrus, Cuarto de Juegos, Dracotienda, Jugamos Otra) mediante URLs de búsqueda en catálogo ante ausencia de precios directos, garantizando cero estados vacíos.
- [`GameCard.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/GameCard.razor): Tarjeta de catálogo para vista en cuadrícula con badge de 3 segundos, portada WebP optimizada (INC-57), selector de estado, badge visual de huella en mesa (INC-59) y título contextualizado según el país del usuario (INC-73).
- [`GameListItem.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/GameListItem.razor): Fila editorial para vista en lista (INC-58) con miniatura WebP 64x64px anti-CLS, metadatos comparativos (jugadores idóneos, duración, huella de mesa), puntuación BGG/Ludeka y enlace accesible WCAG 2.2 AA.
- [`PurchaseLinksSection.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/PurchaseLinksSection.razor): Enlaces de compra contextuales con código de afiliado y enlace a transparencia.
