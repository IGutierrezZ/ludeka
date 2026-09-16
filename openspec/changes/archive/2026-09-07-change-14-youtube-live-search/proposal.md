# Propuesta: change-14-youtube-live-search (Incremento 14: Búsqueda Quirúrgica y Enlace de YouTube en Tiempo Real)

## 1. Resumen Ejecutivo y Motivación

En Ludeka ("El Letterboxd de los juegos de mesa en español"), el Hub Multimedia es la ventana audiovisual donde la comunidad descubre cómo se juega a cada título y disfruta de partidas reales en mesa. Para mantener este contenido actualizado con la máxima calidad y evitar cargas manuales tediosas:
1. **Punto del MVP 5.2 (Pipeline de Ingesta Quirúrgica de YouTube):** Se requiere un motor de búsqueda automatizado con patrones estrictos en español que localice:
   - **🎬 Tutoriales de reglas completas** (8–25 min).
   - **🎲 Partidas completas** con su número de comensales identificado de forma obligatoria.
   - **⚡ "Cómo funciona / Vistazo rápido"** (≤ 2–3 min): Nuevo formato para vídeos breves que condensan las mecánicas del juego para que el usuario determine rápidamente si el título es de su agrado antes de aprender las reglas al detalle.
2. **Foco Central en Canales de Editoriales, Creadores y Tiendas:**
   - La búsqueda en YouTube no es indiscriminada: sitúa el foco y máxima prioridad algorítmica en los canales oficiales de **Editoriales** (*Devir, Tranjis, Maldito, Asmodee, 2Tomatoes, TCG Factory*), **Creadores/Divulgadores** (*Análisis Parálisis, Meepletopía, El Agujero de Hobbit, Mesa de Guerra, Pareja de Ases, Sentido Antihorario, Jugador Inicial*) y **Tiendas Especializadas** (*Zacatrus!, etc.*).
   - Este foco sienta las bases directas para conectarse con el **Incremento 19 (Directorio de Editoriales, Creadores y Tiendas)**.
3. **Operación en Vivo con YouTube Data API v3:**
   - Diseñado para operar **100% en vivo** contra la API oficial de Google mediante `YouTube:ApiKey` en `appsettings.json`.
   - Se mantiene un dataset mock estático exclusivamente como salvaguardas de desarrollo para que la batería de tests unitarios automatizados (`dotnet test`) de CI se ejecute en verde sin depender de internet ni consumir cuota en local si no se configura la clave.
4. **Moderación Móvil en 1 Clic:**
   - Dotar al panel de moderación (`/admin/moderacion-medios` y `/moderacion-media`) de un flujo ágil de búsqueda asistida con previsualización inmediata y guardado a la cola de moderación o aprobación directa.

---

## 2. Decisiones de Dominio y Arquitectura

### 2.1 Modelo de Dominio y Tipos Multimedia (`Ludeka.Core`)
- Ampliación del enum `MediaType`:
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
- **Extractor de Jugadores para Partidas (`PlayerCountExtractor`):**
  - Expresiones regulares multivariante (`"a 2"`, `"a 3"`, `"en solitario"`, `"a dos jugadores"`) con fallback garantizado a la escalabilidad óptima del juego para satisfacer la invariante de `MediaItem.PlayerCountBadge`.

### 2.2 Foco de Canales Hispanos (`IChannelFocusProvider`)
- Abstracción que provee la lista de canales oficiales prioritarios de editoriales, creadores y tiendas.
- En INC-14 arranca con el registro canónico de los 25+ canales hispanos de referencia; en INC-19 se conectará dinámicamente a las entidades registradas en la base de datos.

### 2.3 Contratos y DTOs (`Ludeka.Application`)
- `YouTubeSearchResultDto`: VideoId, Title, Url, EmbedUrl, ThumbnailUrl, ChannelTitle, DurationSeconds, FormattedDuration, SuggestedType, ExtractedPlayerBadge, RelevanceScore, IsReferenceChannel.
- `YouTubeIngestRequestDto`: Solicitud de incorporación a catálogo o moderación.
- `IYouTubeSearchService`:
  - `SearchVideosForGameAsync(Guid gameId, CancellationToken ct = default)`
  - `SearchTutorialsAsync(string gameTitle, CancellationToken ct = default)`
  - `SearchPlaythroughsAsync(string gameTitle, CancellationToken ct = default)`
  - `SearchQuickOverviewsAsync(string gameTitle, CancellationToken ct = default)`
  - `IngestVideoAsync(YouTubeIngestRequestDto request, CancellationToken ct = default)`
  - `AutoSuggestAndIngestForGameAsync(Guid gameId, bool autoApprove = false, CancellationToken ct = default)`

### 2.4 Infraestructura y Cliente YouTube (`Ludeka.Infrastructure`)
- `YouTubeOptions`: `ApiKey`, `BaseUrl`, `Simulate`.
- `YouTubeSearchService`: Cliente `HttpClient` contra `https://www.googleapis.com/youtube/v3/search` y `videos`.
- Parseo de duración ISO 8601 (`PT#M#S`).
- Ponderación de relevancia basada en el padrón de canales de editoriales, creadores y tiendas.

### 2.5 Panel de Moderación y UI Blazor (`Ludeka.Web`)
- `@page "/admin/moderacion-medios"` en `MediaModeration.razor`.
- Botón *"🔍 Buscar vídeos en YouTube"* con selector de juego y pestañas:
  - `⚡ Cómo funciona (~2 min)`
  - `🎬 Tutoriales (8–25 min)`
  - `🎲 Partidas Completas`
- Previsualización y acción en 1 toque.
- Pestaña `⚡ Cómo Funciona` en `MultimediaHub.razor`.

---

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Búsqueda quirúrgica de vídeos "Cómo funciona" (2 min)
  Dado un juego en el catálogo como "Catan"
  Cuando el moderador solicita sugerencias de YouTube
  Entonces el sistema ofrece vídeos clasificados como QuickOverview con duración menor a 3 minutos
  Y permite incorporarlos directamente a la pestaña "Cómo funciona" del Hub Multimedia

Escenario: Foco prioritario en canales de editoriales, creadores y tiendas
  Dado un juego de Devir como "Carcassonne"
  Cuando se buscan vídeos en YouTube
  Entonces los vídeos subidos por canales oficiales registrados (como Devir TV, Zacatrus o Análisis Parálisis) aparecen antes que canales genéricos o no verificados

Escenario: Búsqueda de partidas completas con extracción de comensales
  Dado un vídeo con título "Partida a 2 jugadores a Wingspan"
  Cuando el moderador lo previsualiza e ingesta
  Entonces el sistema asigna automáticamente el badge "Partida a 2" sin errores de validación de dominio

Escenario: Consulta en vivo con YouTube Data API v3
  Dado que se ha configurado la clave en YouTube:ApiKey
  Cuando se busca un juego en el panel de moderación
  Entonces el sistema realiza una llamada HTTP en vivo a la API oficial de YouTube y devuelve los resultados en tiempo real
```

---

## 4. Riesgos y Mitigaciones

| Riesgo | Impacto | Mitigación |
|---|---|---|
| **Cuota diaria de YouTube API (10.000 unidades/día)** | Medio | Búsquedas quirúrgicas bajo demanda del moderador con parámetros `maxResults=5`, evitando consultas superfluas. Salvaguardas offline para tests automáticos. |
| **Falta de badge de comensales en partidas** | Alto | Regex robusto con fallback a la escalabilidad del juego para nunca violar la invariante de dominio. |
| **Vídeos duplicados** | Bajo | Comprobación de existencia de URL previa a la inserción. |
