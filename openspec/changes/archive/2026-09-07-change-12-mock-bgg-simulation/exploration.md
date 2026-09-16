# Exploración: change-12-mock-bgg-simulation (Incremento 12: Simulación y Mock de API BGG)

## 1. Estado Actual de la Solución y la Integración con BGG

### 1.1 Arquitectura de Integración BGG
- **Contrato (`IBggClient` en `Ludeka.Application.Contracts`):**
  Define tres operaciones fundamentales:
  1. `Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default);`
  2. `Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(string username, CancellationToken ct = default);`
  3. `Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default);`
- **Consumidores en la Capa de Aplicación:**
  - `BggSearchAssistedService`: Búsqueda reactiva asistida desde el modal para añadir juegos con su BGG ID oficial. Si no hay resultados de BGG, realiza un fallback a búsqueda local en `_gameRepo`.
  - `BggImportService`: Importa las listas (`Owned`, `Wishlist`, `WantToBuy`) de un usuario de BGG. Cruza con el catálogo local (`_gameRepo.GetByBggIdAsync`): si el juego existe, lo vincula a la ludoteca del usuario; si no existe, lo encola en `PendingBggImports`.
  - `BggCatalogQueueService`: Procesa lotes de juegos pendientes en la cola comunitaria de auto-catalogación. Llama a `_bggClient.FetchGameByBggIdAsync(pending.BggId)` y persiste el juego en el catálogo local, promoviendo las colecciones de usuario en espera.
- **Implementación Actual (`BggXmlApiClient` en `Ludeka.Infrastructure.Bgg`):**
  - Conecta en vivo por HTTP a los endpoints XMLAPI2 de BoardGameGeek (`/thing`, `/collection`, `/search`).
  - Utiliza `TokenBucketRateLimiter` (máximo 2 peticiones/segundo) y reintentos con retardo progresivo ante respuestas 202 (Accepted) o 429 (Rate Limit).
  - Admite un token de autorización Bearer (`Bgg:ApiToken`).
- **Configuración Actual (`appsettings.json` y `BggOptions`):**
  - La sección `Bgg` contiene `ApiToken: ""` (vacío por defecto).
  - En entornos de desarrollo, tests, CI/CD o demostraciones sin token registrado de BGG (o sin conexión a internet), las llamadas a BGG pueden fallar con `401 Unauthorized` o timeouts, impidiendo probar de forma interactiva el flujo de importación, la búsqueda asistida y el vaciado de la cola comunitaria.

### 1.2 Catálogo Actual y Semillado
- `seed-games.json` (`src/Ludeka.Infrastructure/Seeding/seed-games.json`):
  - Contiene actualmente **15 juegos base** (Wingspan, 7 Wonders: Duel, Catán, Carcassonne, Terraforming Mars, Azul, Gloomhaven, Cascadia, Dune: Imperium, The Crew, Los Castillos de Borgoña, Ark Nova, Código Secreto, Dixit, El Monstruo de Colores).
- `CatalogSeeder.cs` (`src/Ludeka.Infrastructure/Seeding/CatalogSeeder.cs`):
  - Carga los juegos de `seed-games.json`.
  - En `SeedExpansionsAndSynergiesAsync`, siembra actualmente **7 expansiones** (3 de Wingspan: Europa, Oceanía, Asia; 2 de Terraforming Mars: Preludio, Hellas & Elysium; 2 de Carcassonne: Posadas y Catedrales, Constructores y Comerciantes).
  - En `SeedPendingBggImportsAsync`, siembra 5 juegos en espera con distintas demandas (Ark Nova, Gloomhaven, Spirit Island, Heat: Pedal to the Metal, Clank! Catacombs).
- **Carencias identificadas:**
  - El catálogo de semillado no incluye todavía los 30 juegos base canónicos requeridos por el Incremento 12 (falta incorporar títulos clave como *Brass: Birmingham*, *Scythe*, *Root*, *Pandemic*, *Patchwork*, *Ticket to Ride*, *Concordia*, *Love Letter*, *Viticulture*, *Agrícola*, *Las Ruinas Perdidas de Arnak*, *Great Western Trail*, *Splendor*, *Everdell*, *Clank!*, *Heat: Pedal to the Metal* y *Spirit Island*).
  - Faltan 3 expansiones oficiales para completar las **10 expansiones reales** requeridas (*7 Wonders Duel: Pantheon*, *Dune: Imperium - El Auge de Ix*, *Everdell: Bellfaire*).
  - No existe un cliente mock/simulador reutilizable para `IBggClient` fuera de los mocks locales en tests unitarios.

---

## 2. Necesidades y Oportunidades de Mejora

1. **Simulador Integral Offline (`SimulatedBggClient`):**
   - Debe implementar fielmente `IBggClient` para que los servicios de aplicación (`BggSearchAssistedService`, `BggImportService`, `BggCatalogQueueService`) operen con total naturalidad sin saber si están contra la API real o contra el simulador.
   - Debe proveer datos precisos y completos para 40 títulos (30 juegos base + 10 expansiones), con todos sus Value Objects (`AgeRating`, `GameDuration`, `ScalabilityEntry`, `SleeveItem`, `GamePurchaseLink`, etc.).
   - Debe simular usuarios de prueba representativos (`ludeka_demo`, `pareja_jugona`, `maraton_euro`) para poder realizar pruebas de punta a punta de importación de colecciones desde la interfaz de usuario en cualquier momento.

2. **Conmutación Transparente y Resiliencia (Zero-Crash Fallback):**
   - Incorporar `SimulateApi` en `BggOptions`.
   - Si `SimulateApi` es `true` o si `ApiToken` está vacío, conmutar automáticamente a `SimulatedBggClient`.
   - Evitar errores 401 o excepciones de red en desarrollo local o ejecuciones de prueba.

3. **Catálogo Semilla Expandido (30 Juegos Base + 10 Expansiones):**
   - Actualizar `seed-games.json` con los 30 juegos base legendarios con todas sus propiedades reales (título en español, autor, editorial, semáforo de comensales, fundas y enlaces de compra con afiliados).
   - Actualizar `CatalogSeeder` para semillar las 10 expansiones con sus badges de necesidad, etiquetas de impacto, sinergias par-a-par y recetas de mesa.

---

## 3. Matriz de Viabilidad Técnica

| Componente | Capa | Estado Actual | Modificación / Adición en Inc-12 |
|---|---|---|---|
| `BggOptions` | `Ludeka.Infrastructure` | Solo tiene `ApiToken`, `BaseUrl`, `UserAgent` | Añadir `SimulateApi` (bool) y propiedad calculada `ShouldSimulate` |
| `SimulatedBggClient` | `Ludeka.Infrastructure.Bgg` | No existe | Nueva clase que implementa `IBggClient` con dataset de 40 títulos |
| `BggSimulationDataset` | `Ludeka.Infrastructure.Bgg` | No existe | Almacén / generador in-memory estructurado con 30 juegos base y 10 expansiones |
| `appsettings.json` | `Ludeka.Web` | `"ApiToken": ""` | Añadir `"SimulateApi": true` |
| Inyección de dependencias | `Ludeka.Web/Program.cs` | Registra `BggXmlApiClient` directamente | Registrar `SimulatedBggClient` y resolver condicionalmente según `ShouldSimulate` |
| `seed-games.json` | `Ludeka.Infrastructure/Seeding` | 15 juegos base | Ampliar a 30 juegos base completos |
| `CatalogSeeder.cs` | `Ludeka.Infrastructure/Seeding` | 7 expansiones | Ampliar a 10 expansiones (añadiendo Pantheon, El Auge de Ix y Bellfaire con sinergias y recetas) |
| Pruebas Unitarias | `Ludeka.UnitTests` | 238 tests pasando | Nuevos tests unitarios y de integración para `SimulatedBggClient`, conmutador y flujos de importación |
