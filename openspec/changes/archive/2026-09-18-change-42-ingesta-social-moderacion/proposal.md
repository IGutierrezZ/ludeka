# Propuesta: change-42-ingesta-social-moderacion (Incremento 42: Hub de Ingesta Social y Multimedia)

## 1. Resumen Ejecutivo y Motivación

En Ludeka ("El Letterboxd de los juegos de mesa en español"), el pulso de la comunidad se nutre constantemente del ecosistema exterior: sorteos en Instagram de editoriales y divulgadores, lanzamientos de novedades semanales, ferias y grandes eventos, y piezas audiovisuales (tutoriales, partidas y reseñas en YouTube e Instagram).

Hasta la fecha, la incorporación de estos contenidos ha dependido de formularios manuales aislados o de procesos mock/simulados sin un flujo unificado de captura rápida. Además, herramientas externas de scraping como Apify conllevan costes recurrentes incompatibles con la filosofía de coste cero de infraestructura y fragilidad ante cambios de selectores web.

El **Incremento 42** implanta una arquitectura completa de **Hub de Ingesta Social y Multimedia** con tres pilares fundamentales:
1. **Alta Exprés ("Copiar, pegar y listo") + Modo Manual Avanzado:**
   - Modo Rápido: el moderador introduce una URL pública (post de Instagram, vídeo de YouTube, web). El sistema extrae metadatos OpenGraph/oEmbed, descarga la imagen y la optimiza en WebP hacia Cloudflare R2 vía `IImageStorageService`. Con Google Gemini Flash (o fallback heurístico local), analiza el texto para clasificar la publicación (`Sorteo`, `Novedad`, `Evento`, `Vídeo`) y extraer campos estructurados (juego vinculado, organizador/editorial, fechas límite o de evento, bases/requisitos).
   - Modo Manual Avanzado: diseñado para vídeos, reels o publicaciones sin descripción textual donde la IA no tiene texto de partida. El moderador especifica la URL, el juego del catálogo y la categoría (ej. "Ark Nova" + "Tutorial/Partida" u "Opinión"), y el sistema se encarga exclusivamente de capturar el fotograma/miniatura, optimizarlo en R2 y crear el borrador.
2. **Bandeja de Ingesta y Moderación 100% Editable:**
   - Ubicada en `/admin/ingesta-social`, presenta todas las capturas en estado pendiente (`PendingReview`).
   - El moderador tiene control total para **editar cualquier dato extraído por la IA antes de publicar**: corregir el título, modificar la fecha límite o del evento, reasignar o buscar el juego en el catálogo, ajustar el organizador o cambiar la tipología de destino.
   - Al pulsar "Aprobar y Publicar", el sistema instancia y persiste la entidad definitiva según su tipo (`Giveaway` para el Radar de Sorteos en `/sorteos`, `WeeklyRelease` para `/novedades`, `BoardGameEvent` para `/eventos`, o `MediaItem` para el Hub Multimedia y la ficha del juego), marcando el ítem como aprobado (`Approved`).
   - Al pulsar "Descartar", el ítem pasa a `Rejected` sin ensuciar las tablas maestras.
3. **Directorio de Cuentas y Canales Monitorizados (Cero Coste / Sin Apify):**
   - Panel en `/admin/canales-monitorizados` para registrar y gestionar perfiles de Instagram, canales de YouTube y webs de referencia de editoriales, creadores y tiendas.
   - Posibilidad de importar o sincronizar automáticamente con las entidades ya existentes de `Publisher`, `Creator` y `Store`.
   - Enlace directo a los perfiles para que el moderador copie posts recientes al vuelo con un clic.

---

## 2. Arquitectura y Alcance por Capas

### 2.1 Dominio (`Ludeka.Core`)
- **`SocialInboxItem`:** Entidad de bandeja de entrada de ingesta con:
  - `Id`: Guid único.
  - `SourceUrl`: URL de origen.
  - `Platform`: `Instagram`, `YouTube`, `Web`, `TwitterX`, `TikTok`.
  - `DetectedType`: `Giveaway`, `WeeklyRelease`, `BoardGameEvent`, `MediaItem`.
  - `Status`: `PendingReview`, `Approved`, `Rejected`.
  - `Title`: Título extraído o editado.
  - `OrganizerOrAuthor`: Organizador, autor, editorial o canal.
  - `Collaborator`: Colaborador opcional.
  - `GameId` / `GameTitle`: Identificador y título del juego del catálogo vinculado.
  - `EventOrReleaseDate`: Fecha asociada (fin de sorteo, fecha de lanzamiento o fecha de evento).
  - `EventEndDate`: Fecha fin (si es un evento de varios días).
  - `Location`: Ciudad o recinto (para eventos).
  - `EstimatedPvp`: Precio estimado (para novedades).
  - `MediaCategory`: Tutorial, Partida, Opinión (para `MediaItem`).
  - `PlayerCountBadge`: Badge de comensales (ej. "Partida a 2").
  - `OriginalCaption`: Texto/descripción en bruto capturado o proporcionado.
  - `ThumbnailUrl`: URL pública de la imagen WebP optimizada en R2 o imagen externa original.
  - `IsVideo`: Indica si el origen es un contenido de vídeo.
  - `CreatedEntityId`: Guid de la entidad destino generada tras la aprobación.
  - `ModeratorNotes`: Notas internas del moderador.
  - `CreatedAt`, `ReviewedAt`, `ReviewedByUserId`.
- **`MonitoredSocialAccount`:** Entidad para el directorio de cuentas:
  - `Id`, `Name`, `Platform`, `HandleOrChannelId`, `AccountType` (`Publisher`, `Creator`, `Store`, `Community`), `ProfileUrl`, `IsEnabled`, `LastCheckedAt`, `Notes`.

### 2.2 Aplicación (`Ludeka.Application`)
- **`ISocialInboxRepository`:** Persistencia y consultas para la bandeja de moderación (filtros por estado, tipo, paginación, recuento de pendientes).
- **`IMonitoredAccountRepository`:** CRUD y consultas para cuentas y canales monitorizados.
- **`ISocialMetadataExtractor`:** Extractor ligero de OpenGraph / oEmbed / metadatos públicos de YouTube e Instagram sin APIs de pago.
- **`ISocialAiAnalysisService`:** Servicio de análisis y estructuración con Google Gemini Flash (y generador heurístico para tests y entornos sin API key).
- **`ISocialIngestionService`:** Orquestador de alto nivel:
  - `CreateFromUrlAsync(string url, string? manualCaption, CancellationToken ct)`: Alta Exprés automática.
  - `CreateManualAdvancedAsync(SocialInboxManualInputDto input, CancellationToken ct)`: Modo manual asistido para vídeos o publicaciones sin texto.
  - `UpdateItemAsync(SocialInboxUpdateDto dto, CancellationToken ct)`: Edición de campos por el moderador.
  - `ApproveAndPublishAsync(Guid inboxItemId, string reviewerUserId, CancellationToken ct)`: Creación de la entidad definitiva (`Giveaway`, `WeeklyRelease`, `BoardGameEvent`, `MediaItem`) y transición de estado a `Approved`.
  - `RejectItemAsync(Guid inboxItemId, string reason, string reviewerUserId, CancellationToken ct)`: Descarte del ítem.
- **`IMonitoredAccountService`:** Gestión del directorio de cuentas y sincronización con editoriales, creadores y tiendas.

### 2.3 Infraestructura (`Ludeka.Infrastructure`)
- **`OpenGraphSocialMetadataExtractor`:** Extracción HTTP de metadatos OpenGraph (`og:title`, `og:image`, `og:description`), URLs canónicas de YouTube (`hqdefault.jpg`, oEmbed) e Instagram sin servicios de pago externos.
- **`GeminiSocialAnalysisService`:** Llamada a Google Gemini Flash con salida JSON fuertemente tipada para detectar tipo de contenido y rellenar campos estructurados, con fallback heurístico transparente si no hay API key o en modo simulado.
- **Pipeline de Imágenes con `IImageStorageService`:** Descarga de imágenes extraídas y subida como WebP determinista a Cloudflare R2 bajo `social-inbox/{id}/thumbnail.webp`.
- **Persistencia EF Core / SQLite:** Repositorios `SqliteSocialInboxRepository` y `SqliteMonitoredAccountRepository`, con registro en `LudekaDbContext`.

### 2.4 Interfaz de Usuario Blazor (`Ludeka.Web`)
- **`SocialInboxModeration.razor` (`/admin/ingesta-social`):**
  - Panel principal con métricas de pendientes, pestañas de filtrado (Todos, Sorteos, Novedades, Eventos, Vídeos) y buscador.
  - Tarjetas o filas con vista previa de imagen, datos extraídos, estado y enlaces al post original.
  - Modal de edición completo antes de aprobar (permite cambiar juego vinculado con buscador en vivo, fechas, título, organizador, etc.).
  - Acciones rápidas "Aprobar y Publicar" y "Descartar".
- **Modal de Alta Exprés (`SocialExpressIngestModal.razor`):**
  - Pestaña 1: "Pegar URL y Listo" (campo de URL + texto opcional si el moderador quiere pegar el caption).
  - Pestaña 2: "Modo Manual Avanzado" (URL + selector de tipo + selector de juego del catálogo + título).
- **`MonitoredAccountsDirectory.razor` (`/admin/canales-monitorizados`):**
  - Directorio visual con filtros por plataforma (`Instagram`, `YouTube`, `Web`) y tipo (`Editorial`, `Creador`, `Tienda`).
  - Botón "Sincronizar desde Directorio" (trae perfiles de `Publisher`, `Creator`, `Store`).
  - Alta rápida de nueva cuenta y enlaces directos a sus perfiles.
- **Enlaces de Navegación:**
  - Acceso desde la barra de administración y botón "⚡ Alta Exprés" en las cabeceras de Sorteos, Eventos y Novedades.

---

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Alta exprés automática desde URL de Instagram con sorteo
  Dado un moderador autenticado con permiso de moderación
  Cuando introduce la URL de un post de Instagram que describe el sorteo de "Ark Nova" con fecha límite
  Entonces el sistema extrae la miniatura, la optimiza a WebP en R2
  Y Gemini clasifica el contenido como "Giveaway", extrayendo organizador, juego y fecha límite
  Y el ítem queda registrado en la bandeja con estado "PendingReview"

Escenario: Modo manual avanzado para vídeo o post sin descripción
  Dado un vídeo de YouTube que es una partida de "Cascadia" a 2 jugadores pero carece de descripción útil
  Cuando el moderador selecciona "Modo Manual Avanzado", elige el juego "Cascadia", tipo "MediaItem (Partida)" y badge "Partida a 2"
  Entonces el sistema extrae la miniatura de YouTube a R2 y guarda el ítem en la bandeja listo para revisión

Escenario: Edición de datos en la bandeja antes de aprobar
  Dado un ítem en la bandeja clasificado como sorteo con un título erróneo o fecha aproximada
  Cuando el moderador pulsa "Editar", rectifica la fecha límite y el organizador, y pulsa "Guardar"
  Entonces los datos del ítem se actualizan en la bandeja sin publicarse aún

Escenario: Aprobación y publicación de sorteo en el Radar
  Dado un ítem en la bandeja de tipo "Giveaway" con todos sus datos verificados
  Cuando el moderador pulsa "Aprobar y Publicar"
  Entonces se inserta un nuevo registro en la tabla "Giveaways"
  Y el sorteo aparece de inmediato en "/sorteos"
  Y el ítem de la bandeja queda en estado "Approved" con el ID del sorteo creado

Escenario: Directorio de cuentas monitorizadas y sincronización
  Dado que existen 5 editoriales y 3 creadores con enlaces de Instagram y YouTube en la base de datos
  Cuando el moderador pulsa "Sincronizar desde Directorio" en "/admin/canales-monitorizados"
  Entonces se crean las entradas correspondientes en el directorio de cuentas monitorizadas sin duplicar existentes
```
