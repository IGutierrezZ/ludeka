# INC-44: Worker de Recolección Multicanal Automática (YouTube, Telegram, RSS Feeds de Editoriales e Instagram)

> **Estado:** ✅ Archivado  
> **Fecha de Inicio:** 2026-09-15  
> **Fecha de Cierre:** 2026-09-15  
> **Rama de Trabajo:** `inc/social-collector-worker`  
> **Worktree:** `C:\repos\ludeka-wt\social-collector-worker`  
> **Dependencias:** INC-42 (Hub de Ingesta Social, Bandeja de Moderación y Directorio de Cuentas Monitorizadas)  
> **Especificación Viva del Sistema:** [30. Worker de Recolección Multicanal Automática](file:///c:/repos/Ludeka/docs/specs/sistema/30-recolector-canales-sociales.md) y [28. Hub de Ingesta Social](file:///c:/repos/Ludeka/docs/specs/sistema/28-hub-ingesta-social-moderacion.md)

---

## 1. Motivación y Visión

En el Incremento 42 (INC-42) se construyó el **Hub de Ingesta Social y Multimedia**, dotando a Ludeka de:
1. Una Bandeja de Moderación editable (`/admin/ingesta-social`).
2. Alta Exprés por URL y Alta Manual Avanzada.
3. Un Directorio central de Cuentas Monitorizadas (`/admin/canales-monitorizados`).

No obstante, en dicho estado la captura de publicaciones continuaba dependiendo de la introducción manual de URLs por parte de moderadores o miembros de la comunidad.

Con **INC-44** se ha implementado un **Worker Desatendido de Recolección Omnicanal** (`SocialCollectorHostedService`), que rastrea periódicamente los cuatro grandes medios de difusión donde las editoriales, tiendas y divulgadores hispanohablantes comparten novedades y contenidos:

1. **Canales de YouTube:** Feeds Atom públicos oficiales (`https://www.youtube.com/feeds/videos.xml?channel_id=...`) con resolución automática de `@handle` a `channelId`.
2. **Canales Públicos de Telegram:** Extracción de la vista web pública oficial `https://t.me/s/{channel}` donde editoriales como Devir, Zacatrus o Maldito publican comunicados, preventas y alertas sin necesidad de login ni riesgo de bloqueos.
3. **Feeds RSS/Atom de Editoriales y Blogs:** Parseo universal de blogs de editoriales y tiendas (WordPress, Ghost, Shopify) en formato RSS 2.0 y Atom.
4. **Cuentas de Instagram:** Soporte para plantillas de RSS-Bridge, crawler de cortesía no invasivo y modo simulado para desarrollo/CI.

Todos los descubrimientos se someten a filtro anti-duplicados (`ISocialInboxRepository.ExistsBySourceUrlAsync`) y se procesan por el pipeline de ingesta existente (`ISocialIngestionService`), depositándose en la bandeja de moderación (`SocialInboxItems`) en estado `PendingReview` con análisis de IA y optimización de carátulas WebP en Cloudflare R2 ya completados.

---

## 2. Alcance Implementado y Verificado

### 2.1. Contrato Polimórfico de Recolectores (`ISocialChannelCollector`)
```csharp
public interface ISocialChannelCollector
{
    bool CanHandle(SocialPlatform platform);
    Task<IReadOnlyList<DiscoveredSocialPostDto>> CollectRecentPostsAsync(
        MonitoredSocialAccount account,
        int maxItems = 5,
        CancellationToken ct = default);
}
```

### 2.2. Recolectores Especializados por Canal
1. **`YouTubeFeedCollector`**:
   - Resuelve el identificador de canal (`UC...`) a partir del handle si es necesario.
   - Parsea el feed Atom oficial mediante `XDocument`.
2. **`TelegramChannelCollector`**:
   - Descarga la vista pública `https://t.me/s/{channelUsername}`.
   - Extrae el texto del mensaje, fecha, enlaces canónicos `https://t.me/{canal}/{id}` y fotos asociadas.
3. **`RssBlogFeedCollector`**:
   - Parsea feeds RSS 2.0 y Atom de blogs y webs oficiales de editoriales (`SocialPlatform.RssFeed` y `SocialPlatform.Website`).
4. **`InstagramFeedCollector`**:
   - Soporte de URL de plantilla de puente RSS (`RssBridgeUrlTemplate`), crawler no invasivo y modo simulado para tests.

### 2.3. Orquestador de Recolección (`ISocialCollectorService` / `SocialCollectorService`)
- Recorre las cuentas activas (`IMonitoredAccountRepository.GetAllAsync(onlyEnabled: true)`).
- Despacha al recolector correspondiente.
- Descarta URLs ya registradas en la base de datos de moderación.
- Llama a `ISocialIngestionService.IngestFromUrlAsync` para extraer metadatos, ejecutar análisis de IA y subir imágenes WebP a R2.
- Actualiza `account.MarkChecked()` en el repositorio.
- Consolida métricas de ejecución (`SocialCollectorRunResultDto`).

### 2.4. Servicio en Segundo Plano (`SocialCollectorHostedService`)
- Ubicación: `Ludeka.Infrastructure.Background`.
- `BackgroundService` configurable en `appsettings.json` mediante `SocialCollectorOptions`.

### 2.5. Interfaz de Usuario y Sondeo Bajo Demanda
- En `/admin/canales-monitorizados`:
  - Nuevas opciones de plataforma en filtros y altas: `Telegram` y `Feed RSS / Blog`.
  - Botón de cabecera *"Sondear Canales"* con ejecución reactiva y resumen de resultados.
  - Botón *"Sondear"* por fila/tarjeta de canal con fecha y hora del último chequeo (`LastCheckedAt`).
- En `/admin/ingesta-social`:
  - Botón de refresco/sondeo rápido y visualización de procedencia multicanal.

### 2.6. Pruebas Unitarias y Cobertura
- Pruebas con fixtures locales de XML Atom de YouTube, HTML de Telegram `t.me/s/`, feeds RSS 2.0 y modo simulado de Instagram.
- Pruebas del orquestador `SocialCollectorService` (deduplicación, métricas, actualización de cuentas).
- Verificación de contratos de marcado web (`WebMarkupContractTests`).
- 988/988 pruebas unitarias superadas (+20 pruebas nuevas).
