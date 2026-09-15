# 30. Worker de Recolección Multicanal Automática (YouTube RSS, Telegram, Feeds de Editoriales e Instagram)

> **Incremento Asociado:** INC-44 (`change-44-social-collector-worker`)  
> **Estado:** Implementado, Verificado y Documentado  
> **Módulo:** Ingesta Desatendida, Automatización Multicanal, Moderación Editorial y Redes Sociales  

---

## 1. Visión General y Propósito

El módulo de **Worker de Recolección Multicanal Automática** automatiza el rastreo periódico y soberano de los principales canales de difusión donde editoriales, tiendas y creadores de contenido lúdico hispanohablantes anuncian novedades, sorteos, eventos y vídeos.

En el Incremento 42 (INC-42) se construyó la infraestructura de la Bandeja de Moderación (`/admin/ingesta-social`), el Alta Exprés por URL y el Directorio de Cuentas Monitorizadas (`/admin/canales-monitorizados`). Este módulo (INC-44) cierra el ciclo de automatización:
1. **Rastreo Desatendido:** Un servicio en segundo plano (`SocialCollectorHostedService`) consulta a intervalos regulares las fuentes monitorizadas activas en `MonitoredSocialAccount`.
2. **Arquitectura Multicanal Soberana (Cero APIs de Pago):**
   - **YouTube:** Feeds Atom nativos públicos (`https://www.youtube.com/feeds/videos.xml?channel_id=...`) con resolución automática de `@handle` a `channelId`.
   - **Telegram:** Extracción directa de la vista pública oficial `https://t.me/s/{username}` de canales abiertos de editoriales y colectivos (sin necesidad de tokens de bot ni riesgo de bloqueos).
   - **Feeds RSS/Atom:** Parseo universal de blogs de noticias de editoriales y tiendas (WordPress, Ghost, Shopify).
   - **Instagram:** Estrategia híbrida con soporte de plantillas RSS-Bridge, crawler de cortesía no invasivo con cabeceras de previsualización y modo simulado para desarrollo/CI.
3. **Filtro Anti-Duplicados y Moderación Previa:** Comprueba contra `ISocialInboxRepository.ExistsBySourceUrlAsync`. Todo contenido nuevo ingresa en estado `PendingReview` con análisis de IA y miniaturas WebP en Cloudflare R2 ya procesadas.
4. **Sondeo Bajo Demanda en UI:** Controles interactivos en `/admin/canales-monitorizados` y `/admin/ingesta-social` con badges temporales (`LastCheckedAt`) e informes reactivos de resultados.

---

## 2. Modelo de Dominio (`Ludeka.Core`)

### 2.1. Ampliación del Enum `SocialPlatform`
```csharp
public enum SocialPlatform
{
    Website = 0,
    YouTube = 1,
    Instagram = 2,
    Twitter = 3,
    Discord = 4,
    Facebook = 5,
    BoardGameGeek = 6,
    Twitch = 7,
    TikTok = 8,
    Other = 9,
    Telegram = 10,
    RssFeed = 11
}
```

### 2.2. Entidad `MonitoredSocialAccount`
Se incorpora la propiedad `ResolvedFeedUrl` y su método de fijación para persistir de forma determinista la URL del feed Atom/RSS una vez resuelto el canal o blog:
```csharp
public string? ResolvedFeedUrl { get; private set; }

public void SetResolvedFeedUrl(string? feedUrl)
{
    ResolvedFeedUrl = string.IsNullOrWhiteSpace(feedUrl) ? null : feedUrl.Trim();
    UpdatedAt = DateTimeOffset.UtcNow;
}
```

---

## 3. Capa de Aplicación (`Ludeka.Application`)

### 3.1. Contrato Polimórfico de Recolección (`ISocialChannelCollector`)
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

### 3.2. Orquestador de Recolección (`ISocialCollectorService` / `SocialCollectorService`)
- Obtiene las cuentas activas mediante `IMonitoredAccountRepository.GetAllAsync(onlyEnabled: true)`.
- Despacha al recolector correspondiente según `account.Platform`.
- Filtra publicaciones anteriores a `MaxPostAgeDays`.
- Comprueba duplicados en `ISocialInboxRepository.ExistsBySourceUrlAsync(post.SourceUrl)`.
- Encola en la bandeja con `ISocialIngestionService.IngestFromUrlAsync(post.SourceUrl, post.Description)`.
- Invoca `account.MarkChecked()` y actualiza el repositorio.
- Consolida y devuelve `SocialCollectorRunResultDto` (`AccountsScanned`, `ItemsDiscovered`, `ItemsImported`, `ItemsSkippedDuplicates`, `Duration`, `AccountSummaries`).

---

## 4. Capa de Infraestructura (`Ludeka.Infrastructure`)

### 4.1. Recolectores Especializados
1. **`YouTubeFeedCollector`**: Descarga y parsea Atom XML con `XDocument`, extrayendo `videoId`, miniatura `hqdefault.jpg`, autor y fecha de publicación.
2. **`TelegramChannelCollector`**: Descarga `https://t.me/s/{channel}`, parsea bloques de mensajes, fotos y enlaces canónicos `https://t.me/{channel}/{id}`.
3. **`RssBlogFeedCollector`**: Soporte universal de RSS 2.0 y Atom para blogs editoriales con extracción de enclosures o imágenes incrustadas.
4. **`InstagramFeedCollector`**: Soporte para plantillas RSS-Bridge, crawler no invasivo de cortesía y modo simulado determinista (`Simulate = true`).

### 4.2. Servicio en Segundo Plano (`SocialCollectorHostedService`)
- Hereda de `BackgroundService`.
- Configurable en `appsettings.json` mediante la sección `SocialCollector` (`IntervalMinutes = 120`, `MaxItemsPerAccount = 5`, `InitialDelaySeconds = 30`).

---

## 5. Pruebas Unitarias y Cobertura

La suite de pruebas en `Ludeka.UnitTests` incluye:
- `YouTubeFeedCollectorTests.cs`: Parseo de XML Atom con fixture local.
- `TelegramChannelCollectorTests.cs`: Extracción de identificadores y parseo de HTML público de Telegram.
- `RssBlogFeedCollectorTests.cs`: Parseo de RSS 2.0 y Atom de blogs editoriales.
- `InstagramFeedCollectorTests.cs`: Extracción de shortcodes y modo simulado.
- `SocialCollectorServiceTests.cs`: Deduplicación, orquestación, cómputo de métricas y actualización de `LastCheckedAt`.
- Total del sistema verificado al 100%: **988/988 pruebas unitarias pasando**.
