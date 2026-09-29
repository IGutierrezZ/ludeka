# 28. Hub de Ingesta Social y Multimedia (Bandeja de Moderación Editable + Alta Exprés Multimodal + Directorio de Cuentas Monitorizadas)

> **Incrementos Asociados:** INC-42 (`change-42-ingesta-social-moderacion`), INC-70 (`change-70-ingesta-multimodal-sorteos`) e INC-80 (`change-80-moderacion-social-carteles-ia`)  
> **Estado:** Implementado, Verificado y Documentado  
> **Módulo:** Radar Comunitario, Ingesta Social Multimodal, Moderación Editorial en Carteles y Directorio de Fuentes  

---

## 1. Visión General y Propósito

El módulo de **Hub de Ingesta Social y Multimedia** proporciona una solución integral, soberana y libre de servicios de pago de scraping (como Apify) para alimentar de forma ágil y comunitaria el Radar de Sorteos (`/sorteos`), el Calendario de Novedades (`/novedades`), la Agenda de Eventos (`/eventos`) y los Vídeos Multimedia del catálogo de Ludeka.

Sus pilares fundamentales son:
1. **Alta Exprés Multimodal (INC-70):** Supera las restricciones anti-scraping de Instagram y la imposibilidad de seleccionar texto en dispositivos móviles combinando URL de origen, fotografía del post o reel y bases del sorteo (en texto o captura de pantalla).
2. **Visión Artificial con Gemini Flash:** OCR y extracción semántica automática sobre imágenes (`inlineData` base64) para detectar organizador, colaboradores (`@cuentas`), fechas límite, ámbito territorial y coordenadas de encuadre (`cropBoundingBox`).
3. **Composición Horizontal 16:9 con SkiaSharp (`SkiaSharpGiveawayCoverComposer`):** Generación automática de carátulas 1280x720 en WebP con fondo desenfocado oscuro, primer plano centrado con sombra, recorte inteligente de barras de estado móviles y badge de marca Ludeka.
4. **Modo Manual Avanzado:** Soporte directo para vídeos/reels o publicaciones sin descripción de texto legible, permitiendo al moderador indicar la URL, el juego del catálogo asociado y la tipología (`Tutorial`, `Gameplay`, `ReviewOpinion`), optimizando automáticamente la carátula o miniatura WebP en Cloudflare R2 vía `IImageStorageService`.
5. **Bandeja de Moderación 100% Editable (`/admin/ingesta-social`):** Ningún elemento capturado de redes se publica a ciegas. Todas las capturas ingresan en estado de borrador pendiente (`PendingReview`) y el moderador puede modificar cualquiera de sus datos (título, fechas límite o de estreno, recinto o ámbito territorial, juego vinculado o imagen) antes de pulsar "Aprobar y Publicar" o "Descartar".
6. **Directorio Central de Cuentas Monitorizadas (`/admin/canales-monitorizados`):** Padrón de cuentas de Instagram, canales de YouTube y webs de editoriales, divulgadores y tiendas, con sincronización automática en 1 clic desde el directorio de entidades de Ludeka.
7. **Cumplimiento Estricto de Diseño y Contrato de Cero Emojis:** Componentes accesibles construidos con Tailwind CSS y la iconografía oficial de Lucide (`Icon.razor`), respetando íntegramente las pruebas de maquetación editorial (`WebMarkupContractTests`).

---

## 2. Modelo de Dominio (`Ludeka.Core`)

### 2.1. Enums del Dominio Social

Ubicación: `Ludeka.Core.Enums`

- **`SocialSubmissionType`**:
  - `Giveaway`: Sorteo comunitario destinado a `/sorteos`.
  - `WeeklyRelease`: Novedad o lanzamiento comercial destinado a `/novedades`.
  - `BoardGameEvent`: Feria, jornada o festival lúdico destinado a `/eventos`.
  - `MediaItem`: Contenido multimedia (vídeo, tutorial o partida) vinculado a la ficha de un juego.
- **`SocialInboxStatus`**:
  - `PendingReview`: Pendiente de revisión y edición por parte de un moderador.
  - `Approved`: Aprobado y materializado en la entidad de destino correspondiente.
  - `Rejected`: Descartado por el moderador sin publicación en el catálogo ni radar.
- **`MonitoredAccountType`**:
  - `Publisher`: Editorial de juegos de mesa.
  - `Creator`: Creador de contenido, divulgador, autor o ilustrador.
  - `Store`: Tienda especializada.
  - `Community`: Asociación lúdica, club o colectivo comunitario.

### 2.2. Entidades de Dominio

#### `SocialInboxItem` (`Ludeka.Core.Entities.SocialInboxItem`)
Representa cada publicación capturada pendiente de moderación o su histórico auditado:

```csharp
public class SocialInboxItem
{
    public Guid Id { get; private set; }
    public string SourceUrl { get; private set; }
    public SocialPlatform Platform { get; private set; }
    public SocialSubmissionType DetectedType { get; private set; }
    public SocialInboxStatus Status { get; private set; }

    public string Title { get; private set; }
    public string OrganizerOrAuthor { get; private set; }
    public string? Collaborator { get; private set; }

    public Guid? GameId { get; private set; }
    public string? GameTitle { get; private set; }

    public DateTimeOffset? EventOrReleaseDate { get; private set; }
    public DateTimeOffset? EventEndDate { get; private set; }
    public string? Location { get; private set; }
    public decimal? EstimatedPvp { get; private set; }

    public MediaCategory? MediaCategory { get; private set; }
    public string? PlayerCountBadge { get; private set; }

    public string? OriginalCaption { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public bool IsVideo { get; private set; }
    public string? AiAnalysisNotes { get; private set; }

    public Guid? CreatedEntityId { get; private set; }
    public string? ModeratorNotes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewedByUserId { get; private set; }
}
```

#### `MonitoredSocialAccount` (`Ludeka.Core.Entities.MonitoredSocialAccount`)
Representa una fuente de información o canal oficial comunitario para seguimiento:

```csharp
public class MonitoredSocialAccount
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public SocialPlatform Platform { get; private set; }
    public string HandleOrChannelId { get; private set; }
    public MonitoredAccountType AccountType { get; private set; }
    public string ProfileUrl { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTimeOffset? LastCheckedAt { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
```

---

## 3. Capa de Aplicación (`Ludeka.Application`)

### 3.1. Contratos e Interfaces
- **`ISocialMetadataExtractor`**: Extrae metadatos OpenGraph (título, descripción, imagen, vídeo) y miniaturas nativas de YouTube (`hqdefault.jpg`, oEmbed) vía HTTP sin APIs de pago.
- **`ISocialAiAnalysisService`**: Asistente inteligente con Google Gemini Flash y generador heurístico en español para clasificar publicaciones, extraer colaboradores con `@`, fechas relativas y precios.
- **`ISocialIngestionService`**: Orquesta el pipeline de ingesta exprés, ingesta manual avanzada, actualización de borradores, aprobación atómica (`ApproveAndPublishAsync`) y descarte (`RejectItemAsync`).
- **`IMonitoredAccountService`**: Gestión CRUD de canales monitorizados y sincronización idempotente desde `Publisher`, `Creator` y `Store`.

### 3.2. Aprobación Atómica y Despacho por Tipología
Al aprobar un ítem en `SocialIngestionService`:
1. **`Giveaway`**:
   - **Validación Estricta de Vigencia (INC-80):** Requiere obligatoriamente que `EventEndDate` o `EventOrReleaseDate` esté informado y sea igual o posterior a la fecha actual (`rawDeadline >= UtcNow.Date`). Se descartan extensiones artificiales (`+7 días`): si no hay fecha o está vencida, el servicio arroja `InvalidOperationException` y la UI abre automáticamente la edición. Si la fecha cae en el día de hoy, se normaliza al fin del día (23:59:59 UTC) para evitar caducidad inmediata.
   - **Resolución Territorial Inteligente (INC-80):** Si el ítem no trae país o ámbito geográfico explícito, consulta secuencialmente si el organizador o colaborador coincide con una editorial (`Publisher.Country`), tienda (`Store.Country`) o creador (`Creator.Nationality`) registrado en la plataforma, normalizándolo con `CountryCatalog` antes de recurrir a `"España"` como fallback.
   - Genera la entidad `Giveaway` mediante `IGiveawayRepository.AddAsync` (o fusiona colaboradores y extiende plazo si ya existe con `FindDuplicateOrCollaborativeAsync`), haciéndose visible en `/sorteos`.
2. **`WeeklyRelease`**: Genera `WeeklyRelease` mediante `IWeeklyReleaseRepository.AddAsync`, integrándose en el calendario `/novedades`.
3. **`BoardGameEvent`**: Resuelve el país del evento mediante `ResolveCountryAsync` y genera `BoardGameEvent` mediante `IBoardGameEventRepository.AddAsync`, mostrándose en `/eventos`.
4. **`MediaItem`**: Si tiene `GameId`, genera la entidad `MediaItem` vinculada al juego con estado `Approved`, disponible en `/multimedia` y en la ficha técnica del juego.
5. El registro en la bandeja queda marcado como `Approved`, con auditoría de usuario revisor (`ReviewedByUserId`), marca de tiempo (`ReviewedAt`) y referencia foránea al recurso creado (`CreatedEntityId`).

---

## 4. Capa de Infraestructura (`Ludeka.Infrastructure`)

### 4.1. Extractor Ligero OpenGraph (`OpenGraphSocialMetadataExtractor`)
- Realiza peticiones HTTP GET respetuosas emulando User-Agent estándar de navegador de escritorio.
- Analiza etiquetas `<meta property="og:..." />`, `<meta name="twitter:..." />` y `<title>`, con soporte de comillas anidadas (simples dentro de dobles) sin truncamiento.
- Para publicaciones de Instagram, aísla el nombre del autor descartando el sufijo contextual (`on/en Instagram`).
- Para URLs de YouTube (`youtube.com` o `youtu.be`), extrae automáticamente el ID del vídeo y resuelve la miniatura canónica de alta definición `https://img.youtube.com/vi/{videoId}/hqdefault.jpg`, consultando adicionalmente la API pública de oEmbed para obtener el título y canal de forma inmediata.
- Dispone de suite unitaria dedicada (`OpenGraphSocialMetadataExtractorTests`) con 19 casos de prueba automatizados.

### 4.2. Asistente IA Híbrido (`GeminiSocialAnalysisService`)
- Integra Google Gemini Flash estructurado en JSON si la API Key está configurada.
- **Visión Multimodal (INC-70):** Mediante `AnalyzeImageAsync`, procesa imágenes (`inlineData` base64) de capturas de pantalla o publicaciones para realizar OCR exhaustivo y extraer datos del sorteo estructurados (título del juego/premio, organizador, colaboradores `@menciones`, fecha límite con zona horaria, ámbito territorial como "Península/España" y cuadro delimitador de recorte `cropBoundingBox`).
- Dispone de un analizador heurístico avanzado en español diseñado con expresiones regulares deterministas que detecta:
  - **Sorteos:** Búsqueda de "sorteo", "giveaway", "bases", "participa", fechas límite y organizadores colaboradores (ej. `@editorial x @creador`).
  - **Novedades:** Detección de "novedad", "lanzamiento", "ya a la venta", "preventa" y extracción de precios (`PVP: XX €`).
  - **Eventos:** Detección de "jornadas", "festival", "convención", "feria" y ubicaciones.
  - **Medios:** Clasificación de tutoriales, reseñas y partidas completas con badges recomendados para comensales.

### 4.3. Persistencia y Migraciones Duales
- Tablas `SocialInboxItems` y `MonitoredSocialAccounts` configuradas en `LudekaDbContext`.
- Índices optimizados en `Status`, `DetectedType`, `Platform` y `CreatedAt`.
- Migración defensiva SQLite en `SqliteSchemaMigrator` para desarrollo local y ejecución de tests en memoria.

### 4.4. Compositor Editorial de Portadas 16:9 (`SkiaSharpGiveawayCoverComposer`)
- Transforma fotografías de publicaciones (1:1 o verticales 9:16) en carátulas horizontales 16:9 (1280x720) en formato WebP optimizado.
- Aplica fondo desenfocado oscuro (`#0B0F17` con sigma de 28px) a partir de la imagen original.
- Primer plano centrado preservando relación de aspecto con esquinas redondeadas (radio 24px) y sombra perimetral difusa.
- Recorte inteligente opcional de barras de estado o elementos de interfaz móvil guiado por coordenadas normalizadas de visión (`NormalizedBoundingBoxDto`).
- Sello de marca editorial Ludeka discreto integrado en la esquina inferior derecha.

---

## 5. Interfaz de Usuario Blazor (`Ludeka.Web`)

### 5.1. Bandeja de Moderación (`/admin/ingesta-social`)
- **Pestañas por estado:** `Pendientes` (con contador reactivo), `Publicados` y `Descartados`.
- **Filtros por tipología:** `Sorteos`, `Novedades`, `Eventos`, `Vídeos`.
- **Cuadrícula de Carteles Compactos (INC-80):**
  - Distribución responsiva en cuadrícula compacta: 2 columnas en móvil, 3 en pantallas pequeñas, 4 en medianas y 5 a 6 en pantallas grandes (`grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6`).
  - Tarjetas verticales con proporción de cartel (`aspect-[3/4]`), portada con zoom sutil al pasar el cursor y badges compactos en esquinas superiores.
  - Alerta visual destacada cuando la publicación no fue procesada por IA (`!item.IsAiProcessed`), mostrando un badge de aviso («Sin IA»).
  - Estado visual de vigencia en sorteos: badge rojo destacado si carece de fecha («Sin fecha») o si está en el pasado («Vencido: dd/MM»), o ámbar si está vigente («Fin: dd/MM»), junto con el distintivo de ámbito territorial (`🌍 País`).
  - Interacción táctil y de ratón directa: pulsar o hacer clic en cualquier parte del cartel abre el modal de edición completa.
  - Al pulsar «Aprobar» sobre un sorteo sin fecha o vencido, se abre directamente la edición avisando del requisito.
  - Acciones compactas al pie de la tarjeta: Editar, Descartar (con diálogo de confirmación y motivo) y Aprobar.
  - Botón administrativo **"Purgar Simulados"** en la cabecera para limpiar de forma determinista publicaciones de prueba (`sim_*` o `/simulated/`) generadas por entornos de desarrollo.

### 5.2. Directorio de Canales (`/admin/canales-monitorizados`)
- Catálogo de fuentes con filtros por plataforma (`Instagram`, `YouTube`, `Twitter`, `TikTok`, `Web`) y tipología de entidad.
- Conmutador de estado activo/pausado en un solo clic.
- Botón **"Sincronizar Directorio"**: importa sin duplicados las redes de editoriales, creadores y tiendas ya existentes.
- Botón **"⚡ Publicación"**: abre el modal de alta exprés precargando el nombre de la cuenta para acelerar la ingesta.

### 5.3. Modales Compartidos
- **`SocialExpressIngestModal.razor`**: Asistente modal en tres modos de ingesta:
  1. *Sorteos (Multimodal)*: Diseñado específicamente para superar las restricciones de Instagram. Acepta URL de origen, archivo de foto de portada (con recorte y composición 16:9 automática) y bases del sorteo (vía texto plano o captura de pantalla móvil con OCR mediante Gemini Flash Vision). Soporta `@onpaste` para pegar capturas directamente desde el portapapeles.
  2. *Pegar URL y Listo (IA)*: Extracción automática para YouTube, noticias web y blogs.
  3. *Modo Manual Avanzado*: Con buscador predictivo de juegos y selección explícita de tipologías.
- **`SocialInboxEditModal.razor` (Enriquecido en INC-80)**:
  - Formulario de edición completa de borradores antes de su aprobación definitiva, con soporte para ámbito territorial (ej. Península, España, Internacional).
  - **Aviso de Extracción sin IA y Botón «Reintentar con IA»:** Si la publicación cayó en fallback heurístico o alta manual, muestra un banner explicativo y permite ejecutar `ReanalyzeWithAiAsync` al vuelo.
  - **Panel de Texto Original Extraído (`OriginalCaption`):** Muestra el texto capturado para que el moderador coteje bases, fechas y condiciones directamente sin salir de la ventana.
  - **Validación Visual de Fecha Fin:** Indicador en rojo y bloqueo de aprobación si el sorteo carece de fecha válida de hoy o posterior.
  - **Botón «Ver Original»:** Enlace seguro con `target="_blank"` a la URL de la publicación original en Instagram, YouTube o web.
  - **Botón «Aprobar y Publicar» directo:** Permite guardar cualquier cambio y publicar la entidad en el catálogo/radar en una sola interacción desde el propio modal.

### 5.4. Puntos de Entrada Transversales
- Menú de moderación de `MainLayout.razor` con enlaces a la bandeja y al directorio de canales.
- Botones de acción rápida `[ ⚡ Alta Exprés ]` en las cabeceras de `Radar.razor`, `News.razor` y `Events.razor` para moderadores y fundadores.

### 4.5. Persistencia Aislada y Blindaje ante Colecciones JSON Propias en EF Core (PR #139)
- **Desacoplamiento de Lecturas:** Eliminados los `.Include(i => i.Game)` de `SqliteSocialInboxRepository`, `SqliteGiveawayRepository` y `SqliteWeeklyReleaseRepository`. Las entidades de moderación y radar almacenan directamente `GameId` y `GameTitle`, sin requerir la carga en memoria del agregado `Game`.
- **Actualización Atómica y Aislada en `UpdateAsync`:** Los repositorios `SqliteSocialInboxRepository`, `SqliteGiveawayRepository`, `SqliteWeeklyReleaseRepository` y `SqliteMediaRepository` recuperan la entidad existente en el `scope` del DbContext y actualizan exclusivamente sus valores escalares mediante `scope.Context.Entry(existing).CurrentValues.SetValues(entity)`. Esto erradica el fallo de claves sombra ordinales (`ScalabilityEntry.__synthesizedOrdinal`) cuando se actualizan o aprueban elementos asociados a juegos con colecciones JSON propias (`OwnsMany(..., b => b.ToJson())`).
- **Protección en `AddAsync`:** Ante entidades que conserven una referencia no nula a `Game`, se establece de forma explícita `Entry(game).State = EntityState.Unchanged`, evitando que EF Core intente registrarlas como dependencias nuevas del contexto.

---

## 6. Pruebas y Validación

- **Suite Automatizada de la Solución:** 2.079 pruebas unitarias y 10 de integración en verde (100% superado), incluyendo la suite específica `SocialIngestionServiceTests` para validar los flujos de actualización, edición, rechazo de duplicados, validación estricta de fecha fin y resolución de país desde directorio, junto con el compositor SkiaSharp (`GiveawayCoverComposerTests`), análisis multimodal con Gemini Vision (`GeminiVisionSocialAnalysisTests`), flujo orquestado de ingesta (`MultimodalGiveawayIngestionTests`) y blindaje anti-vacíos de Instagram (`CommunityWriteGuardTests`).
- **Pruebas de Componente y Contratos de Marcado:** Verificación con `WebMarkupContractTests` garantizando la ausencia total de emojis prohibidos y el uso riguroso del sistema de diseño editorial con Lucide Icons.
