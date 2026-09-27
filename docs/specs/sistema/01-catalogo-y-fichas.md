# 01. Catálogo y Ficha Inteligente

## 1. Visión General y Propósito
El módulo de catálogo gestiona los juegos de mesa registrados en Ludeka, proveyendo metadatos editoriales completos, normalización de URLs amigables para SEO (*slugs*), píldoras de ADN Lúdico, el Semáforo Dinámico de Escalabilidad, guía de fundas y enlaces a tiendas especializadas.

---

## 2. Modelo de Dominio (`Ludeka.Core`)

10: ### 2.1 Entidad Raíz: `Game`
11: Ubicación: [`src/Ludeka.Core/Entities/Game.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/Game.cs)
12: 
13: - **Identificadores:** `Id` (Guid), `BggId` (int), `Slug` (string único normalizado sin diacríticos ni caracteres especiales).
14: - **Títulos:** `OriginalTitle` (string), `SpanishTitle` (string comercial en España con fallback automático a `OriginalTitle`), `LocalizedTitles` (colección JSON de [`LocalizedTitleEntry`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/LocalizedTitleEntry.cs) por país, INC-73).
15: - **Créditos y Publicación:** `Designer`, `Publisher` (sello editorial original internacional), `SpanishPublisher` (editorial licenciante en España, INC-73), `RegionalPublishers` (colección JSON de [`RegionalPublisherEntry`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/RegionalPublisherEntry.cs) clasificadas por país hispanohablante, INC-73), `YearPublished`.
16: - **Imágenes:** `CoverImageUrl` (alta definición con `fetchpriority="high"`), `ThumbnailUrl`.
17: - **Calificaciones:** `BggRating` (double), `BggRank` (int?), `LudistRating` (double [0.0 - 10.0] ponderado por la comunidad hispana).
18: - **ADN Lúdico:**
19:   - `Confrontation`: Enum `ConfrontationType` (`Competitive`, `Cooperative`, `SemiCooperative`, `HiddenRoles`, `TeamVsTeam`).
20:   - `Style`: Enum `GameStyle` (`Eurogame`, `Ameritrash`, `PartyGame`, `Filler`, `Abstract`, `Wargame`, `Deckbuilder`, `Drafting`, `DungeonCrawler`, `Legacy`, `RollAndWrite`, `SocialDeduction`, `Trivia`, `Dexterity`, `EngineBuilding`, `WorkerPlacement`).
21:   - `IsOfficialSolo`: booleano indicativo de variante oficial para 1 persona.
22:   - `Age`: Value Object [`AgeRating`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/AgeRating.cs) (`BoxAge` vs. `CommunityAge`).
23:   - `Language`: Enum `LanguageDependence` (`None`, `Low`, `Moderate`, `High`, `Critical`).
24:   - `Footprint`: Enum `TableFootprint` (`SmallTable`, `StandardTable`, `LargeTable`, `TableMonster`).
25:   - `Duration`: Value Object [`GameDuration`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/GameDuration.cs) (`MinMinutes`, `MaxMinutes`, `EstimatedPerPlayerMinutes`).
26: - **Semáforo de Escalabilidad:** Colección de [`ScalabilityEntry`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/ScalabilityEntry.cs) (1 a 7+ comensales).
27:   - Estados: `MustPlay` (🟢 Imprescindible), `Recommended` (🟡 Recomendado), `NotRecommended` (🔴 No recomendado).
28:   - Votos: `BestVotes`, `RecommendedVotes`, `NotRecommendedVotes`.
29:   - Fallback determinista en ausencia de encuesta comunitaria: asigna `Recommended` al rango de jugadores base e `MustPlay` si `min == max` (INC-73).
30:   - Etiqueta calculada en tiempo real: `IdealPlayersLabel` (ej. *"Ideal a 2 y 4 jugadores"*).
31: - **Guía de Fundas de Cartas:** Colección de [`SleeveItem`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/SleeveItem.cs) (`FormatName`, `WidthMm`, `HeightMm`, `CardCount`, `AffiliateUrl`).
32: - **Enlaces de Compra en Tiendas:** Colección de [`GamePurchaseLink`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/GamePurchaseLink.cs) (`StoreName`, `AffiliateUrl`, `Price`, `Currency`, `InStock`, `Badge`, `AffiliateTag`).
33: 
34: ---
35: 
36: ## 3. Capa de Aplicación (`Ludeka.Application`)
37: 
38: - **Contrato:** [`IGameRepository`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IGameRepository.cs)
39:   - `GetBySlugAsync(slug, ct)`
40:   - `GetByBggIdAsync(bggId, ct)`
41:   - `SearchAsync(criteria, page, pageSize, ct)` (actualizado en INC-73 para indexar `SpanishPublisher` y `RegionalPublishers`)
42:   - `GetByPublisherAsync(publisher, page, pageSize, ct)` (ampliado en INC-73 para enlazar juegos con sellos regionales)
43:   - `GetGamesPendingQualityBackfillAsync(limit, ct)` (INC-73: selector de títulos para enriquecimiento retroactivo)
44:   - `GetTopGamesAsync(limit, ct)`
45: - **Servicio y Caché:**
46:   - [`CatalogService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Catalog/CatalogService.cs)
47:   - [`CachedCatalogService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Catalog/CachedCatalogService.cs): Decorador sobre `ICatalogService` con `IMemoryCache` (TTL de 10 minutos e invalidación reactiva por slug).
48: - **DTOs y Resultados:** [`GameSummaryDto`](file:///c:/repos/Ludeka/src/Ludeka.Application/DTOs/GameSummaryDto.cs), [`GameDetailDto`](file:///c:/repos/Ludeka/src/Ludeka.Application/DTOs/GameDetailDto.cs) (con helpers `GetPublisherForCountry` y `GetTitleForCountry`), [`CatalogResult`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Catalog/ICatalogService.cs) (con propiedad calculada `TotalPages`), [`GameFilterCriteria`](file:///c:/repos/Ludeka/src/Ludeka.Application/DTOs/GameFilterCriteria.cs) (INC-59 e INC-72: soporte facetado con `Footprint`, `PlayerCount`, `MaxDurationMinutes`, `TypeFilter` y blindaje de hash en `CachedCatalogService`).
49: 
50: ---
51: 
52: ## 4. Persistencia y Base de Datos (`Ludeka.Infrastructure`)
53: 
54: - Tabla `Games` mapeada mediante EF Core en `LudekaDbContext`.
55: - Value Objects y colecciones mapeados como JSON en SQLite:
56:   - `Scalability` (`OwnsMany.ToJson()`)
57:   - `Sleeves` (`OwnsMany.ToJson()`)
58:   - `PurchaseLinks` (`OwnsMany.ToJson()`)
59:   - `RegionalPublishers` (`OwnsMany.ToJson()`, INC-73)
60:   - `LocalizedTitles` (`OwnsMany.ToJson()`, INC-73)
61: - Índice único en `Slug` e índice en `BggId`.
62: - Reconciliación preventiva en tiempo de ejecución: [`SqliteSchemaMigrator`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs) con migración EF Core `AddBggQualityAndLocalizationFields` (INC-73).
63: - Filtrado SQL nativo en `SqliteGameRepository.SearchAsync` para `Footprint`, `TypeFilter`, duración y texto, complementado con evaluación de escalabilidad comunitaria para `PlayerCount` y `EspecialParejas` (INC-59).
64: 
65: ---
66: 
67: ## 5. Componentes de Interfaz (`Ludeka.Web`)
68: 
69: - [`Home.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Pages/Home.razor): Buscador reactivo, filtros por estilo y comensales, carrusel de destacados. INC-36: cabecera editorial compartida (`PageHeaderEditorial`), búsqueda en bloque propio y tira de filtros con `scrollbar-none`. INC-58: paginación real completa (24 títulos por página), selector conmutable de modo de vista (Cuadrícula ↔ Lista), contador de títulos y sincronización bidireccional de parámetros en URL (`page`, `view`, `q`, presets) con soporte de historial (`LocationChanged`). INC-59 e INC-72: barra de refinamiento facetado accesible con multiselección completa («¿Me cabe en la mesa?», comensales 1 a 7+, duración ≤30 a ≤120 min, estilos y dureza).
70: - [`GameDetail.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Pages/GameDetail.razor): Ficha inteligente completa. INC-36: tokenización editorial. INC-72: carrusel visual de 3 fotos (portada, trasera, mesa) sin descripciones crudas en inglés. INC-73: resolución contextual de la editorial adaptada al país del usuario (`IUserPreferenceService` + `GetPublisherForCountry`) con enlace directo a `/editoriales/{slug}`, mención de la editorial internacional original si difiere, desglose accesible de ediciones territoriales con icono `<Icon Name="globe" Size="12" />`, y título comercial localizado con referencia al original.
71: - [`GameCard.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/GameCard.razor): Tarjeta de catálogo para vista en cuadrícula con badge de 3 segundos, portada WebP optimizada (INC-57), selector de estado, badge visual de huella en mesa (INC-59) y título contextualizado según el país del usuario (INC-73).
72: - [`GameListItem.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/GameListItem.razor): Fila editorial para vista en lista (INC-58) con miniatura WebP 64x64px anti-CLS, metadatos comparativos (jugadores idóneos, duración, huella de mesa), puntuación BGG/Ludeka y enlace accesible WCAG 2.2 AA.
73: - [`PurchaseLinksSection.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/PurchaseLinksSection.razor): Enlaces de compra contextuales con código de afiliado y enlace a transparencia.
