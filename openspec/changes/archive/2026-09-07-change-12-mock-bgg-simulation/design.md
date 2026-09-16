# Diseño Técnico: change-12-mock-bgg-simulation (Incremento 12)

## 1. Arquitectura y Componentes

### 1.1 Configuración (`Ludeka.Infrastructure.Bgg.BggOptions`)
Se amplía la clase `BggOptions`:
```csharp
public class BggOptions
{
    public const string SectionName = "Bgg";

    public string? ApiToken { get; set; }
    public string BaseUrl { get; set; } = "https://boardgamegeek.com/xmlapi2/";
    public string UserAgent { get; set; } = "LudekaApp/1.0 (https://ludeka.es; contacto@ludeka.es)";
    public bool SimulateApi { get; set; } = true;

    public bool ShouldSimulate => SimulateApi || string.IsNullOrWhiteSpace(ApiToken);
}
```

### 1.2 Dataset Estructurado en Memoria (`Ludeka.Infrastructure.Bgg.BggSimulationDataset`)
- Clase estática / de catálogo `BggSimulationDataset`:
  - Mantiene la lista inmutable `IReadOnlyList<Game> AllTitles`.
  - `Game? FindByBggId(int bggId)`: Busca por identificador BGG y genera una instancia clonada desacoplada.
  - `IReadOnlyList<BggSearchResultDto> Search(string query)`: Búsqueda flexible con normalización de caracteres (`RemoveDiacritics`).
  - `IReadOnlyList<BggCollectionItemDto> GetUserCollection(string username)`: Diccionario de perfiles precargados:
    - `"ludeka_demo"`: 12 juegos (Wingspan, Catan, Carcassonne, Cascadia, Azul, Dune: Imperium, Everdell, Heat: Pedal to the Metal, Scythe, Spirit Island, Splendor, The Crew).
    - `"pareja_jugona"`: 8 juegos (7 Wonders Duel, Patchwork, Wingspan Asia, Cascadia, Azul, Carcassonne, Dixit, The Crew).
    - `"maraton_euro"`: 10 juegos (Terraforming Mars, Brass: Birmingham, Ark Nova, Great Western Trail, Scythe, Concordia, Agrícola, Dune: Imperium, Viticulture, Los Castillos de Borgoña).
    - `Default`: 6 juegos canónicos.

### 1.3 Implementación de `SimulatedBggClient` (`Ludeka.Infrastructure.Bgg.SimulatedBggClient`)
- Implementa `IBggClient`:
  ```csharp
  public class SimulatedBggClient : IBggClient
  {
      public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default);
      public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default);
      public Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(string username, CancellationToken ct = default);
  }
  ```

### 1.4 Inyección de Dependencias (`Ludeka.Web.Program`)
- Registro de servicios en el contenedor IoC de ASP.NET Core:
  ```csharp
  builder.Services.Configure<BggOptions>(builder.Configuration.GetSection(BggOptions.SectionName));
  builder.Services.AddHttpClient<BggXmlApiClient>();
  builder.Services.AddSingleton<SimulatedBggClient>();

  builder.Services.AddScoped<IBggClient>(sp =>
  {
      var options = sp.GetRequiredService<IOptions<BggOptions>>().Value;
      return options.ShouldSimulate
          ? sp.GetRequiredService<SimulatedBggClient>()
          : sp.GetRequiredService<BggXmlApiClient>();
  });
  ```

### 1.5 Semillado Inicial (`Ludeka.Infrastructure.Seeding`)
- **`seed-games.json`**:
  - Contiene los 30 juegos base canónicos con metadatos reales, escalabilidad, fundas y enlaces de compra.
- **`CatalogSeeder.cs`**:
  - `SeedExpansionsAndSynergiesAsync`: Incorpora las 10 expansiones oficiales (añadiendo *7 Wonders Duel: Pantheon*, *Dune: Imperium - El Auge de Ix* y *Everdell: Bellfaire*) con sus respectivas sinergias (`ExpansionSynergy`) y recetas de mesa (`ExpansionRecipe`).

---

## 2. Estrategia de Pruebas Unitarias e Integración
1. **`BggOptionsTests`**: Verificación de lógica de conmutación según combinaciones de `SimulateApi` y `ApiToken`.
2. **`SimulatedBggClientTests`**: Pruebas completas de búsqueda, recuperación y colecciones simuladas.
3. **`BggSimulationIntegrationTests`**: Pruebas de integración de `BggSearchAssistedService`, `BggImportService` y `BggCatalogQueueService` usando `SimulatedBggClient`.
