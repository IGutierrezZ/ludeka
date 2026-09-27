# 17. Localización Geográfica por País, Filtrado Territorial y Detección de Ubicación

## 1. Visión General y Propósito
Ludeka es una plataforma hispanohablante de ámbito global pensada para conectar a entusiastas de los juegos de mesa en España y toda Latinoamérica. No obstante, las transacciones comerciales, la compra de fundas, la asistencia a grandes ferias y la participación en sorteos son actividades intrínsecamente ligadas a la distribución física y geográfica de cada territorio.

6: El módulo de **Localización Geográfica por País y Filtrado Territorial (INC-29 e INC-73)** introduce:
7: 1. **Atribución Geográfica Universal:** Asociación de país y ámbito de envíos a tiendas especializadas (`Store`), sorteos comunitarios (`Giveaway`), ferias y eventos (`BoardGameEvent`), enlaces de compra de juegos (`GamePurchaseLink`), guías de compra de fundas (`SleeveItem`), perfiles de usuario (`AppUser`), preferencias persistentes (`UserPreference`), y editoriales y títulos comerciales en el catálogo (`Game`, `RegionalPublishers`, `LocalizedTitles`, INC-73).
8: 2. **Filtrado Territorial Estricto Opcional:** Si el usuario selecciona expresamente su país en su perfil o cabecera, la plataforma activa un filtro excluyente para que solo vea sorteos, eventos y tiendas correspondientes a su territorio o con cobertura internacional.
9: 3. **Regla de Sorteos y Eventos Internacionales:** Los eventos lúdicos y sorteos marcados con ámbito "Internacional" (`INT` / `🌎`) son **siempre visibles** para cualquier país seleccionado o modo global.
10: 4. **Envíos Multipaís en Tiendas:** Las tiendas que operan o envían pedidos a varios países (`ShippingCountries`) son visibles y computadas en todos y cada uno de los territorios autorizados.
11: 5. **Advertencia de Filtrado Territorial:** Obligación de avisar de manera explícita y transparente al usuario sobre el filtrado al configurar su país.
12: 6. **Políticas de Tiendas en Ficha de Juegos y Fundas:** En "Dónde Comprar" de la ficha del juego (`StoreOffersCard`) y en la guía de fundas (`SleeveGuideCard`), si no existen tiendas que envíen al país activo del usuario, **no se muestra ninguna tienda de otros países**, informando con un estado vacío limpio y contextual.
13: 7. **Detección Automática y Priorización Lúdica:** Detección en cliente por zona horaria de navegador (`Intl.DateTimeFormat`) para ordenar resultados priorizando el país del usuario sin excluir los demás cuando no hay filtro territorial restrictivo.
14: 8. **Localización Territorial de Editoriales y Títulos Comerciales (INC-73):** Identificación de sellos editoriales locales y títulos adaptados en los 7 países hispanohablantes soportados (`ES`, `MX`, `AR`, `CL`, `CO`, `PE`, `UY`). En la ficha del juego, se presenta la editorial local y el título comercial correspondiente al país del usuario (`GetPublisherForCountry` y `GetTitleForCountry`).
15: 
16: ---
17: 
18: ## 2. Catálogo Oficial Geográfico y Resolución Lingüística (`Ludeka.Core`)
19: 
20: ### 2.1 Entidades y Value Objects
21: - **`CountryInfo`**: Value object inmutable con `Code` (ISO-3166 alfa-2 o `INT`), `Name` canónico (ej. `España`, `México`, `Argentina`, `Chile`, `Colombia`, `Perú`, `Uruguay`, `Internacional`) y `FlagEmoji` (ej. 🇪🇸, 🇲🇽, 🇦🇷, 🇨🇱, 🇨🇴, 🇵🇪, 🇺🇾, 🌎).
22: - **`CountryCatalog`**: Catálogo central con lista inmutable de países soportados (`All`, `SpecificCountries`), métodos de normalización fonética y de acentos (`RemoveDiacritics`), resolución por sinónimos o aliases (`espana` -> `España`, `global` / `mundial` -> `Internacional`), resolución de banderas (`GetFlag`) y verificación de ámbito mundial (`IsInternational`).
23: - **`RegionalPublisherEntry` y `LocalizedTitleEntry`**: Value objects inmutables que encapsulan la relación entre un país (`CountryCode`) y el nombre de la editorial o título comercial específico (INC-73).
24: 
25: ### 2.2 Reglas e Invariantes de Dominio
26: - **`Game.cs` (INC-73):**
27:   - Propiedades `SpanishPublisher` (string?), `RegionalPublishers` (colección JSON de `RegionalPublisherEntry`) y `LocalizedTitles` (colección JSON de `LocalizedTitleEntry`).
28:   - Método `GetPublisherForCountry(string? countryCode)`: resuelve la editorial licenciante del país del usuario; si no existe, devuelve `SpanishPublisher` para España o `Publisher` internacional como fallback honesto.
29:   - Método `GetTitleForCountry(string? countryCode)`: resuelve el título comercial traducido para el país del usuario; si no existe, recurre a `SpanishTitle` y finalmente a `OriginalTitle`.
30: - **`Store.cs`**:
31:   - Propiedades `Country` (sede principal) y `ShippingCountries` (colección de países a los que envía).
32:   - Método `ShipsTo(country)`: valida si el país destino coincide con la sede, con algún país de `ShippingCountries` o si tiene cobertura internacional.
33: - **`Giveaway.cs`**:
34:   - Propiedades `Country` e `IsInternational` (`CountryCatalog.IsInternational(Country)`).
35:   - Método `IsAvailableInCountry(country)`: devuelve siempre `true` si no hay filtro o si es `IsInternational`; si hay filtro, requiere coincidencia exacta con el país.
36: - **`BoardGameEvent.cs`**:
37:   - Propiedades `Country` e `IsInternational`.
38:   - Método `IsCelebratedInCountry(country)`: devuelve siempre `true` si no hay filtro o si es `IsInternational`; en caso contrario verifica coincidencia territorial.
39: - **`GamePurchaseLink.cs` y `SleeveItem.cs`**:
40:   - Propiedades `Country` y `ShippingCountries`.
41:   - Método `ShipsTo(country)`: filtra enlaces para asegurar que solo se muestren tiendas y marcas de fundas que envían al territorio activo.
42: - **`AppUser.cs` y `UserPreference.cs`**:
43:   - Propiedad nullable `Country`. Si es `null`, representa ámbito "Global (sin filtro)".

---

## 3. Capa de Aplicación (`Ludeka.Application`)

- Contrato de servicio: [`IUserLocationService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IUserLocationService.cs)
  - `string? CurrentCountry { get; }`: País seleccionado manualmente.
  - `string? DetectedCountry { get; }`: País detectado por navegador.
  - `string? EffectiveCountry { get; }`: País preferido consolidado (`CurrentCountry ?? DetectedCountry`).
  - `bool HasActiveCountry { get; }`: Determina si el filtrado territorial estricto está activo.
  - Métodos `SetUserCountry(country)` y `SetDetectedCountry(country)`.
  - Métodos asíncronos con fallback: `GetEffectiveCountryAsync()`, `GetDetectedCountryAsync()`, `GetUserLocationAsync()`.
  - Algoritmo de ordenación prioritaria `PrioritizeByCountry<T>(items, countrySelector, preferredCountry)`:
    - Puntuación 2: Coincidencia territorial local exacta.
    - Puntuación 1: Ámbito Internacional / Global.
    - Puntuación 0: Resto de países.
- DTOs actualizados:
  - `StoreDto`, `StoreDetailDto`, `CreateStoreDto`, `UpdateStoreDto` (con `Country` y `ShippingCountries`).
  - `GiveawayDto`, `CreateGiveawayRequest` (con `Country`, `CountryFlag`, `IsInternational`).
  - `BoardGameEventDto`, `CreateBoardGameEventRequest`, `UpdateBoardGameEventRequest` (con `Country`).
  - `UserPreferenceDto` (con `Country`).

---

## 4. Persistencia e Infraestructura (`Ludeka.Infrastructure`)

- **Persistencia EF Core y SQLite:**
  - `LudekaDbContext`: Índices en `Stores.Country`, `Giveaways.Country`, `BoardGameEvents.Country` y mapeo de colección `ShippingCountries` mediante serialización JSON.
  - `SqliteSchemaMigrator`: Reconciliación idempotente de columnas `Country` y `ShippingCountries` en `Stores`, `Giveaways`, `BoardGameEvents`, `AppUsers` y `UserPreferences` para bases de datos SQLite existentes.
  - Repositorios actualizados (`SqliteStoreRepository`, `SqliteBoardGameEventRepository`, `SqliteGiveawayRepository`, `SqliteUserPreferenceService`).
  - `UserLocationService`: Servicio scoped que enlaza preferencias persistidas de usuarios autenticados con fallbacks en memoria y sesión.

---

## 5. Capa Web e Interfaz Blazor (`Ludeka.Web`)

- **Interoperabilidad JavaScript (`wwwroot/js/locationInterop.js`):**
  - Mapeo de zona horaria IANA del navegador (`Intl.DateTimeFormat().resolvedOptions().timeZone`) a países hispanohablantes (ej. `Europe/Madrid` -> `España`, `America/Mexico_City` -> `México`, `America/Argentina/...` -> `Argentina`).
  - Persistencia en `localStorage` (`ludeka_user_country`) para invitados y usuarios no logueados.
- **Componentes y Vistas:**
  - `LocationSelectorModal.razor`: Modal interactivo accesible desde la cabecera, con advertencia obligatoria, buscador de países, banderas emoji y botón de autodección por zona horaria.
  - `MainLayout.razor`: Botón de país en la barra superior (`🇪🇸 España` / `🌍 Global`) conectado al modal.
  - `MyLibrary.razor`: Pestaña "🎨 Apariencia & 🌍 País" con tarjeta de filtrado territorial, advertencia oficial, selector y autodección.
  - `StoreOffersCard.razor`: Filtrado estricto territorial; badges de bandera y destinos de envío; mensaje vacío limpio si no hay tiendas para el país activo.
  - `SleeveGuideCard.razor`: Filtrado territorial de fundas y enlaces de compra de packs según el país del usuario.
  - `GiveawayCard.razor`: Badge territorial con bandera y nombre de país o badge `🌎 Internacional`.
  - `StoresDirectory.razor`: Selector de país en la barra de filtros y badges de país y envíos multipaís en cada tarjeta de comercio.
  - `StoreDetail.razor`: Badge del país de la sede y lista de países de envío en el hero de la tienda.
  - `Events.razor` y `EventsManagement.razor`: Filtro territorial y columna de país en gestión.
  - `Radar.razor`: Filtro de sorteos por país y ámbito internacional.

---

## 6. Advertencia Oficial al Usuario
Texto exacto integrado en el modal y en las preferencias de perfil:
> *"⚠️ Al seleccionar un país, los sorteos, eventos y tiendas se filtrarán automáticamente para mostrar únicamente los disponibles en tu territorio. Si prefieres explorar toda la información de la comunidad global sin filtrar, déjalo sin seleccionar."*

---

103: ## 7. Pruebas Unitarias Automatizadas
104: El incremento incorpora pruebas automatizadas verificadas con xUnit, cubriendo la resolución territorial completa:
105: - `CountryCatalogTests`: Resolución fonética, sinónimos, aliases, banderas y ámbito mundial.
106: - `CountryFilteringEntityTests`: Lógica de negocio de `ShipsTo`, `IsAvailableInCountry` e `IsCelebratedInCountry`.
107: - `UserLocationServiceTests`: Fallback jerárquico de preferencias y algoritmo de ordenación por prioridad territorial.
108: - `RegionalPublisherMatcherTests` (INC-73): 7 pruebas de resolución territorial de editoriales en España, México, Argentina, Chile, Colombia, Perú y Uruguay.
109: - `SqliteCountryMigrationTests`: Migración idempotente del esquema SQLite con bases de datos en memoria.
110: 
111: **Total de la suite verificado tras INC-73:** **1.966 pruebas unitarias en verde al 100% (0 errores, 0 omitidas)**.
