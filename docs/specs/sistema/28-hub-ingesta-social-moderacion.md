# 28. Hub de Ingesta Social y Multimedia (Bandeja de Moderación Editable + Alta Exprés + Directorio de Cuentas Monitorizadas)

> **Incremento Asociado:** INC-42 (`change-42-ingesta-social-moderacion`)  
> **Estado:** Implementado, Verificado y Documentado  
> **Módulo:** Radar Comunitario, Ingesta Social, Moderación Editorial y Directorio de Fuentes  

---

## 1. Visión General y Propósito

El módulo de **Hub de Ingesta Social y Multimedia** proporciona una solución integral, soberana y libre de servicios de pago de scraping (como Apify) para alimentar de forma ágil y comunitaria el Radar de Sorteos (`/sorteos`), el Calendario de Novedades (`/novedades`), la Agenda de Eventos (`/eventos`) y los Vídeos Multimedia del catálogo de Ludeka.

Sus pilares fundamentales son:
1. **Alta Exprés ("Copiar, pegar y listo"):** Extracción automática de metadatos OpenGraph y análisis semántico asistido por IA (Google Gemini Flash o heurística en español desacoplada) a partir de URLs públicas de Instagram, YouTube o sitios web.
2. **Modo Manual Avanzado:** Soporte directo para vídeos/reels o publicaciones sin descripción de texto legible, permitiendo al moderador indicar la URL, el juego del catálogo asociado y la tipología (`Tutorial`, `Gameplay`, `ReviewOpinion`), optimizando automáticamente la carátula o miniatura WebP en Cloudflare R2 vía `IImageStorageService`.
3. **Bandeja de Moderación 100% Editable (`/admin/ingesta-social`):** Ningún elemento capturado de redes se publica a ciegas. Todas las capturas ingresan en estado de borrador pendiente (`PendingReview`) y el moderador puede modificar cualquiera de sus datos (título, fechas límite o de estreno, recinto del evento, juego vinculado o imagen) antes de pulsar "Aprobar y Publicar" o "Descartar".
4. **Directorio Central de Cuentas Monitorizadas (`/admin/canales-monitorizados`):** Padrón de cuentas de Instagram, canales de YouTube y webs de editoriales, divulgadores y tiendas, con sincronización automática en 1 clic desde el directorio de entidades de Ludeka y accesos directos para capturar publicaciones con el organizador preconfigurado.
5. **Cumplimiento Estricto de Diseño y Contrato de Cero Emojis:** Componentes accesibles construidos con Tailwind CSS y la iconografía oficial de Lucide (`Icon.razor`), respetando íntegramente las pruebas de maquetación editorial (`WebMarkupContractTests`).

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
1. **`Giveaway`**: Genera la entidad `Giveaway` mediante `IGiveawayService.CreateOrMergeGiveawayAsync`, haciéndose visible en `/sorteos`.
2. **`WeeklyRelease`**: Genera `WeeklyRelease` mediante `IWeeklyReleaseService.CreateReleaseAsync`, integrándose en el calendario `/novedades`.
3. **`BoardGameEvent`**: Genera `BoardGameEvent` mediante `IBoardGameEventService.CreateEventAsync`, mostrándose en `/eventos`.
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
- Dispone de un analizador heurístico avanzado en español diseñado con expresiones regulares deterministas que detecta:
  - **Sorteos:** Búsqueda de "sorteo", "giveaway", "bases", "participa", fechas límite y organizadores colaboradores (ej. `@editorial x @creador`).
  - **Novedades:** Detección de "novedad", "lanzamiento", "ya a la venta", "preventa" y extracción de precios (`PVP: XX €`).
  - **Eventos:** Detección de "jornadas", "festival", "convención", "feria" y ubicaciones.
  - **Medios:** Clasificación de tutoriales, reseñas y partidas completas con badges recomendados para comensales.

### 4.3. Persistencia y Migraciones Duales
- Tablas `SocialInboxItems` y `MonitoredSocialAccounts` configuradas en `LudekaDbContext`.
- Índices optimizados en `Status`, `DetectedType`, `Platform` y `CreatedAt`.
- Migración defensiva SQLite en `SqliteSchemaMigrator` para desarrollo local y ejecución de tests en memoria.

---

## 5. Interfaz de Usuario Blazor (`Ludeka.Web`)

### 5.1. Bandeja de Moderación (`/admin/ingesta-social`)
- **Pestañas por estado:** `Pendientes` (con contador reactivo), `Publicados` y `Descartados`.
- **Filtros por tipología:** `Sorteos`, `Novedades`, `Eventos`, `Vídeos`.
- **Tarjetas editoriales responsivas:**
  - Miniatura con fallback SVG Lucide.
  - Badges semánticos de plataforma y tipo de contenido.
  - Título, organizador, colaboradores y juego vinculado.
  - Enlace externo a la publicación original.
  - Texto extraído original expandible mediante `<details>`.
  - Botones de acción: `[ ✏️ Editar ]`, `[ ✕ Descartar ]` y `[ ✅ Aprobar ]`.

### 5.2. Directorio de Canales (`/admin/canales-monitorizados`)
- Catálogo de fuentes con filtros por plataforma (`Instagram`, `YouTube`, `Twitter`, `TikTok`, `Web`) y tipología de entidad.
- Conmutador de estado activo/pausado en un solo clic.
- Botón **"Sincronizar Directorio"**: importa sin duplicados las redes de editoriales, creadores y tiendas ya existentes.
- Botón **"⚡ Publicación"**: abre el modal de alta exprés precargando el nombre de la cuenta para acelerar la ingesta.

### 5.3. Modales Compartidos
- **`SocialExpressIngestModal.razor`**: Asistente modal en dos pestañas (`Pegar URL y Listo (IA)` y `Modo Manual Avanzado` con buscador predictivo de juegos).
- **`SocialInboxEditModal.razor`**: Formulario de edición completa de borradores antes de su aprobación definitiva.

### 5.4. Puntos de Entrada Transversales
- Menú de moderación de `MainLayout.razor` con enlaces a la bandeja y al directorio de canales.
- Botones de acción rápida `[ ⚡ Alta Exprés ]` en las cabeceras de `Radar.razor`, `News.razor` y `Events.razor` para moderadores y fundadores.

---

## 6. Pruebas y Validación

- **Suite Automatizada:** 960 pruebas unitarias en verde (100% superado).
- **Pruebas de Componente y Contratos de Marcado:** Verificación con `WebMarkupContractTests` garantizando la ausencia total de emojis prohibidos y el uso riguroso del sistema de diseño editorial con Lucide Icons.
