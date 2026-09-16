# Propuesta: change-12-mock-bgg-simulation (Incremento 12: Simulación y Mock de API BGG)

## 1. Resumen Ejecutivo y Motivación

La plataforma Ludeka depende de la API XMLAPI2 de BoardGameGeek para tres funcionalidades críticas:
1. **Búsqueda asistida en vivo** para vincular juegos nuevos con su identificador oficial de BGG.
2. **Importación de colecciones en 1 clic** (`Owned`, `Wishlist`, `WantToBuy`) a partir del usuario de BGG.
3. **Procesamiento de la cola comunitaria de auto-catalogación**, que nutre las fichas pendientes y promueve los juegos en las ludotecas de los usuarios.

Actualmente, BoardGameGeek exige autenticación con Application Token (Bearer) y aplica estrictos límites de tasa (*rate limiting*). En entornos de desarrollo local, pipelines de CI/CD, pruebas automatizadas y demostraciones sin conexión (o sin un token válido configurado), estas funcionalidades fallan o se degradan.

Esta propuesta formaliza el **Incremento 12** bajo la metodología **Spec-Driven Development (SDD)**, proveyendo:
- Un **dataset de 40 títulos canónicos del hobby** (30 juegos base + 10 expansiones oficiales) con datos reales, exhaustivos y de máxima calidad (ADN lúdico, semáforo de escalabilidad, fundas, enlaces de compra y sinergias).
- Un cliente simulador de primer nivel (`SimulatedBggClient`) que implementa `IBggClient` sin depender de internet ni de credenciales.
- Un **conmutador de configuración transparente** en `appsettings.json` (`Bgg:SimulateApi`) con fallback automático cuando no exista token.
- Actualización del semillado inicial (`seed-games.json` y `CatalogSeeder.cs`) para que cualquier entorno arranque con 30 juegos base y 10 expansiones oficiales.

---

## 2. Decisiones de Dominio y Arquitectura

### 2.1 Conmutador de Configuración en `BggOptions` (`Ludeka.Infrastructure.Bgg`)
Se amplía la clase de configuración `BggOptions`:
```csharp
public class BggOptions
{
    public const string SectionName = "Bgg";

    public string? ApiToken { get; set; }
    public string BaseUrl { get; set; } = "https://boardgamegeek.com/xmlapi2/";
    public string UserAgent { get; set; } = "LudekaApp/1.0 (https://ludeka.es; contacto@ludeka.es)";

    /// <summary>
    /// Activa el modo simulado/mock para la API de BGG.
    /// Si es true, o si ApiToken no está configurado, se utiliza SimulatedBggClient.
    /// </summary>
    public bool SimulateApi { get; set; } = true;

    /// <summary>
    /// Determina si debe utilizarse el cliente simulado en lugar del cliente HTTP real.
    /// </summary>
    public bool ShouldSimulate => SimulateApi || string.IsNullOrWhiteSpace(ApiToken);
}
```

### 2.2 Dataset de 40 Títulos del Hobby (`BggSimulationDataset`)
Ubicación: `src/Ludeka.Infrastructure/Bgg/BggSimulationDataset.cs`.

Contiene la definición completa, verificada e inmutable de 40 títulos clave del juego de mesa moderno en español:
1. **30 Juegos Base:**
   - *Catan* (13)
   - *Carcassonne* (822)
   - *Wingspan* (266192)
   - *Terraforming Mars* (167791)
   - *7 Wonders Duel* (173346)
   - *Ark Nova* (342942)
   - *Azul* (230802)
   - *Cascadia* (295947)
   - *Dune: Imperium* (316554)
   - *Everdell* (199792)
   - *Gloomhaven* (174430)
   - *Heat: Pedal to the Metal* (366013)
   - *Scythe* (169786)
   - *Spirit Island* (162886)
   - *Splendor* (148228)
   - *The Crew: Misión Mar Profundo* (324856)
   - *Brass: Birmingham* (224517)
   - *Clank!* (201808)
   - *Pandemic* (30549)
   - *Root* (237182)
   - *Patchwork* (163412)
   - *¡Aventureros al Tren! (Ticket to Ride)* (9209)
   - *Concordia* (138076)
   - *Love Letter* (129622)
   - *Viticulture Essential Edition* (180263)
   - *Agrícola* (31260)
   - *Las Ruinas Perdidas de Arnak* (312484)
   - *Dixit* (39856)
   - *Código Secreto* (178900)
   - *Great Western Trail* (193738)

2. **10 Expansiones Oficiales:**
   - *Wingspan: Expansión Europa* (290448) — Base: Wingspan
   - *Wingspan: Expansión Oceanía* (300580) — Base: Wingspan
   - *Wingspan: Expansión Asia* (366161) — Base: Wingspan
   - *Terraforming Mars: Preludio* (247030) — Base: Terraforming Mars
   - *Terraforming Mars: Hellas & Elysium* (230914) — Base: Terraforming Mars
   - *Carcassonne: Posadas y Catedrales* (2993) — Base: Carcassonne
   - *Carcassonne: Constructores y Comerciantes* (8443) — Base: Carcassonne
   - *7 Wonders Duel: Pantheon* (202976) — Base: 7 Wonders Duel
   - *Dune: Imperium - El Auge de Ix* (342035) — Base: Dune: Imperium
   - *Everdell: Bellfaire* (265492) — Base: Everdell

Cada título cuenta con:
- Títulos original y español.
- Diseñador, editorial y año de publicación.
- Carátula y thumbnail oficiales de alta resolución.
- Sinopsis descriptiva rica en español.
- Doble valoración (BGG rating y Ludist rating).
- Clasificación de confrontación, estilo y modo solitario.
- Rango de edad (de caja y recomendada por comunidad).
- Nivel de dependencia del idioma y huella en mesa.
- Duración mínima, máxima y estimada por comensal.
- Semáforo de escalabilidad completo (1J a 7J+ con votos de imprescindible/recomendado/no recomendado).
- Guía de fundas de cartas (formatos, dimensiones, unidades).
- Enlaces de compra en tiendas especializadas con etiquetas de afiliados.
- En expansiones: tipo, necesidad de compra (`ExpansionNecessity`), etiquetas de impacto (`ExpansionImpactTag`), aportes específicos, deltas de jugadores y tiempo.

### 2.3 Implementación de `SimulatedBggClient` (`Ludeka.Infrastructure.Bgg`)
Implementa `IBggClient`:
1. `FetchGameByBggIdAsync(int bggId, CancellationToken ct = default)`:
   - Recupera el título del dataset mock por su `bggId`.
   - Si no se encuentra en los 40 títulos principales, genera una entidad sintética coherente o retorna `null` para simular juegos inexistentes.
2. `SearchGamesAsync(string query, CancellationToken ct = default)`:
   - Filtra los 40 títulos buscando coincidencias en `SpanishTitle`, `OriginalTitle` o `Designer` (sin distinguir mayúsculas, minúsculas ni acentos).
   - Retorna una lista de `BggSearchResultDto`.
3. `FetchUserCollectionAsync(string username, CancellationToken ct = default)`:
   - Devuelve colecciones personalizadas según el usuario:
     - `ludeka_demo`: Colección generalista equilibrada con 12 títulos (algunos propios en `Owned`, otros en `Wishlist` y `WantToBuy`), incluyendo tanto juegos semillados en base de datos como pendientes de catalogar.
     - `pareja_jugona`: Colección centrada en juegos excepcionales a 2 comensales (*7 Wonders Duel*, *Patchwork*, *Wingspan Asia*, *Cascadia*, *Carcassonne*, *Azul*).
     - `maraton_euro`: Colección de juegos euro pesados y medios (*Terraforming Mars*, *Brass: Birmingham*, *Ark Nova*, *Great Western Trail*, *Scythe*, *Concordia*, *Agrícola*, *Dune: Imperium*).
     - Cualquier otro nombre de usuario: Retorna una selección variada y consistente de 6 a 8 juegos para permitir pruebas exploratorias con cualquier nombre.

### 2.4 Cableado e Inyección de Dependencias (`Program.cs`)
En `src/Ludeka.Web/Program.cs`:
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

### 2.5 Actualización del Semillado del Sistema
1. **`seed-games.json`:**
   - Incorpora los **30 juegos base** completos con datos enriquecidos.
2. **`CatalogSeeder.cs`:**
   - Sincroniza y añade los juegos base de `seed-games.json`.
   - Incorpora las **10 expansiones oficiales** completas en `SeedExpansionsAndSynergiesAsync`, sumando:
     - *7 Wonders Duel: Pantheon* (BggId 202976, sinergias con el juego base y receta "Panteón Divino").
     - *Dune: Imperium - El Auge de Ix* (BggId 342035, sinergias con acorazados y tecnologías, receta "Conflicto Interestelar").
     - *Everdell: Bellfaire* (BggId 265492, soporte para 5-6 jugadores, mercado y habilidades asimétricas, receta "El Gran Festival").

---

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Búsqueda asistida en modo simulado
  Dado que el sistema tiene configurado el modo simulado de BGG (Bgg:SimulateApi = true o sin token)
  Cuando el usuario busca el término "Dune" en el modal de búsqueda asistida
  Entonces el sistema devuelve los resultados correspondientes a "Dune: Imperium" con su BggId oficial (316554)
  Y no se realiza ninguna petición HTTP externa hacia boardgamegeek.com

Escenario: Importación de colección de usuario simulado "ludeka_demo"
  Dado que el usuario introduce el usuario BGG de prueba "ludeka_demo"
  Cuando pulsa "Importar Colección"
  Entonces se reciben los juegos del catálogo de prueba simulado
  Y los títulos existentes en el catálogo local se asignan a su ludoteca
  Y los títulos no catalogados se encolan automáticamente en la tabla de importaciones pendientes

Escenario: Procesamiento de cola con Mock BGG
  Dado un juego pendiente en la cola de auto-catalogación con BggId perteneciente al dataset mock
  Cuando el administrador procesa el lote de la cola
  Entonces el juego se cataloga automáticamente con todos sus datos reales y semáforo de comensales
  Y las colecciones de usuario en espera son promovidas con éxito

Escenario: Conmutación transparente por ausencia de token
  Dado un entorno donde Bgg:ApiToken está vacío y Bgg:SimulateApi no se especificó explícitamente
  Cuando cualquier servicio solicita IBggClient
  Entonces el contenedor de dependencias entrega una instancia funcional de SimulatedBggClient
  Y el sistema opera sin excepciones de autorización 401
```

---

## 4. Plan de Verificación y Pruebas Unitarias

1. **Pruebas unitarias de `SimulatedBggClient`:**
   - Búsqueda por título en español, título original y diseñador.
   - Búsqueda insensible a mayúsculas, minúsculas y tildes.
   - Búsqueda de término inexistente retorna lista vacía sin errores.
   - Recuperación de juego por `BggId` retorna entidad `Game` con todos sus Value Objects (`AgeRating`, `Duration`, `Scalability`, `Sleeves`, `PurchaseLinks`).
   - Recuperación de colecciones para usuarios conocidos (`ludeka_demo`, `pareja_jugona`, `maraton_euro`) y usuario desconocido (fallback).
2. **Pruebas de conmutación de configuración (`BggOptions`):**
   - `ShouldSimulate` es `true` si `SimulateApi = true`.
   - `ShouldSimulate` es `true` si `ApiToken` es nulo o vacío, incluso si `SimulateApi = false`.
   - `ShouldSimulate` es `false` solo si `SimulateApi = false` y `ApiToken` contiene un token válido.
3. **Pruebas de integración de servicios de aplicación con `SimulatedBggClient`:**
   - `BggSearchAssistedService` resuelve búsquedas directamente con el cliente simulado.
   - `BggImportService` importa colecciones simuladas particionando entre existentes y pendientes.
   - `BggCatalogQueueService` procesa juegos pendientes recuperándolos de `SimulatedBggClient`.
4. **Verificación del semillado:**
   - El semillado carga los 30 juegos base y 10 expansiones con sus relaciones sin duplicados ni errores de esquema.
