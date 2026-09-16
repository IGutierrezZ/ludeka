# Exploración: change-14-youtube-live-search (Incremento 14: Búsqueda Quirúrgica y Enlace de YouTube en Tiempo Real)

## 1. Estado Actual de la Solución y Análisis de Brecha

### 1.1 Modelo de Dominio y Datos Existentes para Multimedia
- **Entidad `MediaItem` ([`MediaItem.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/MediaItem.cs)):**
  - Soporta actualmente los tipos: `Tutorial`, `Playthrough`, `InstagramPost`, `ShortReel`.
  - **Nueva necesidad detectada:** Se requiere un formato específico para vídeos cortos explicativos: `QuickOverview` (o `HowItWorks` — *"⚡ Cómo funciona en 2 minutos"*). No es un tutorial exhaustivo de reglas de 15 minutos ni una partida completa, sino un vistazo rápido de dinámicas y sensaciones para decidir si el juego encaja con el jugador.
  - Plataformas: `YouTube`, `Instagram`, `TikTok`.
  - Propiedades: `Title`, `Url`, `EmbedUrl`, `ThumbnailUrl`, `AuthorChannel`, `DurationSeconds`, `PlayerCountBadge`, `Status`, `IsBroken`, `PublishedAt`.
  - **Invariante crítica:** Para `MediaType.Playthrough`, `PlayerCountBadge` es obligatorio por contrato de dominio (`if (type == MediaType.Playthrough && string.IsNullOrWhiteSpace(playerCountBadge)) throw new ArgumentException(...)`).
  - Cuenta con generador automático de `EmbedUrl` con protección `youtube-nocookie.com`.

### 1.2 Estado de Moderación y Carga de Medios
- **`MediaService` ([`MediaService.cs`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Media/MediaService.cs)):**
  - Gestiona aprobación, rechazo, detección de enlaces rotos y asignación de huérfanos.
  - Actualmente, la carga de vídeos depende del semillado inicial ([`CatalogSeeder.cs`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Seeding/CatalogSeeder.cs)) o de inserciones manuales en la base de datos.
  - **Brecha detectada:** No existe ningún mecanismo para buscar de forma automatizada vídeos en YouTube en tiempo real para un juego específico contra la YouTube Data API v3.

### 1.3 Panel de Moderación Actual ([`MediaModeration.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Pages/MediaModeration.razor))
- El panel permite moderar vídeos pendientes, huérfanos y aprobados, y lanzar la comprobación de enlaces rotos.
- **Brecha detectada:** Los moderadores no disponen de una acción en 1 clic para autocompletar la ficha de un juego con sugerencias de YouTube (tutoriales, partidas y vídeos de "cómo funciona") con previsualización inmediata.

### 1.4 Modo Real de YouTube vs. Modo de Seguridad Offline
- **Propósito del modo real:** Conectar directamente con **YouTube Data API v3** en vivo (`YouTube:ApiKey` en `appsettings.json`), realizando búsquedas en tiempo real en la plataforma de Google.
- **Propósito del modo simulado (offline):** Funciona exclusivamente como un **salvaguardas técnico** para la integración continua (CI) y la batería de tests automatizados (`dotnet test`), evitando que fallen por falta de conexión o cuota de API cuando no se introduce una clave. En cuanto se configure una clave, el sistema operará **100% en vivo contra YouTube Real**.

---

## 2. Decisiones Técnicas y Opciones de Diseño

### 2.1 Ampliación de Dominio: `MediaType.QuickOverview`
Se añade el valor `QuickOverview` al enum `MediaType`:
```csharp
public enum MediaType
{
    Tutorial,
    Playthrough,
    InstagramPost,
    ShortReel,
    QuickOverview // ⚡ "Cómo funciona / Vistazo en 2 min"
}
```
Esto habilita:
- En la ficha del juego (`MultimediaHub.razor`): Nueva pestaña o píldora destacada *"⚡ Cómo funciona (~2 min)"*.
- En la búsqueda de YouTube: Búsqueda orientada a vídeos breves de mecánicas.

### 2.2 Foco Central en Canales de Editoriales, Creadores y Tiendas (Sinergia con INC-19)
- El ecosistema lúdico en español tiene tres grandes pilares con canales oficiales de YouTube:
  1. **Editoriales:** *Devir TV, Tranjis Games, Maldito Games, Asmodee Ibérica, 2Tomatoes, TCG Factory, Arrakis Games, Mercurio*, etc.
  2. **Creadores / Divulgadores:** *Análisis Parálisis, Meepletopía, El Agujero de Hobbit, Mesa de Guerra, Pareja de Ases, Sentido Antihorario, Jugador Inicial, La Mazmorra de Pacheco, Consola y Tablero, Océano de Juegos*, etc.
  3. **Tiendas Especializadas:** *Zacatrus!, Jugamos Otra, Cuarto de Juegos*, etc.
- Se implementará el servicio proveedor `IChannelFocusProvider`:
  - En esta fase (INC-14), dispondrá de un catálogo semilla enriquecido con los canales verificados de estos tres colectivos.
  - Cuando se implemente el **Incremento 19 (Directorio de Editoriales, Creadores y Tiendas)**, el foco se alimentará dinámicamente de los enlaces de YouTube registrados en las fichas de la base de datos.
  - Al ejecutar la búsqueda en YouTube, los vídeos subidos por canales de este padrón oficial obtendrán **prioridad máxima** y aparecerán en cabeza.

### 2.3 Patrones Quirúrgicos de Búsqueda en YouTube Data API v3
El servicio aplicará tres consultas especializadas:
1. **Tutoriales (Reglas Completas):**
   - Consulta: `"{Título del juego} cómo jugar tutorial español"`
   - Filtro de duración: 8 a 25 minutos.
2. **Partidas Completas (Gameplay en Mesa):**
   - Consulta: `"{Título del juego} partida completa español"`
   - Extracción de comensales obligatoria para `PlayerCountBadge`: Expresión regular que detecta `"a 2"`, `"a 3"`, `"en solitario"` con fallback a la escalabilidad ideal del juego (ej. `"Partida a 2"`).
3. **Cómo Funciona / Vistazo Rápido (En 2 Minutos):**
   - Consulta: `"{Título del juego} cómo funciona mecánicas en 2 minutos"` o `"{Título del juego} cómo se juega rápido"`
   - Filtro de duración: Vídeos cortos (≤ 180 segundos / 3 min).

### 2.4 Abstracción de Servicio: `IYouTubeSearchService`
```csharp
public interface IYouTubeSearchService
{
    Task<IReadOnlyList<YouTubeSearchResultDto>> SearchVideosForGameAsync(Guid gameId, CancellationToken ct = default);
    Task<IReadOnlyList<YouTubeSearchResultDto>> SearchTutorialsAsync(string gameTitle, CancellationToken ct = default);
    Task<IReadOnlyList<YouTubeSearchResultDto>> SearchPlaythroughsAsync(string gameTitle, CancellationToken ct = default);
    Task<IReadOnlyList<YouTubeSearchResultDto>> SearchQuickOverviewsAsync(string gameTitle, CancellationToken ct = default);
    Task<MediaItemDto> IngestVideoAsync(YouTubeIngestRequestDto request, CancellationToken ct = default);
    Task<IReadOnlyList<MediaItemDto>> AutoSuggestAndIngestForGameAsync(Guid gameId, bool autoApprove = false, CancellationToken ct = default);
}
```

### 2.5 Integración UI en el Panel de Moderación y en la Ficha Blazor
1. **`MediaModeration.razor`:**
   - Botón `"🔍 Buscar vídeos en YouTube"`.
   - Diálogo modal con selector de juego y pestañas:
     - `⚡ Cómo funciona (2 min)`
     - `🎬 Tutoriales (8–25 min)`
     - `🎲 Partidas Completas`
   - Previsualización embebida instantánea e incorporación en 1 clic (`📥 A moderación` o `✅ Aprobar ya`).
2. **`MultimediaHub.razor`:**
   - Incorporación de la pestaña `⚡ Cómo Funciona` mostrando los vídeos breves de mecánicas.

---

## 3. Estrategia de Pruebas y Verificación
- Tests unitarios con clientes mock de `HttpClient` para YouTube Data API v3 respondiendo payloads reales de YouTube.
- Pruebas del extractor de badges de comensales (`PlayerCountExtractorTests`).
- Pruebas del ponderador de relevancia según el foco de canales de editoriales, creadores y tiendas.
- Pruebas de integración del nuevo `MediaType.QuickOverview`.
