# Diseño Técnico: INC-37 — Modo Producción: APIs Reales, Atribución BGG, Comunidad y Motor Privado de Afiliados

## 1. Arquitectura y Componentes Afectados

```
[ Ludeka.Web ]
  │
  ├─ Components/Layout/MainLayout.razor (Footer con Atribución BGG, Discord y Telegram)
  ├─ Components/Shared/StoreOffersCard.razor (Integración IAffiliateUrlResolver)
  ├─ Components/Shared/SleeveGuideCard.razor (Integración IAffiliateUrlResolver)
  └─ Program.cs (Registro de opciones y servicios)

[ Ludeka.Application ]
  │
  ├─ Contracts/IAffiliateUrlResolver.cs (Contrato de resolución de enlaces privados)
  ├─ Options/AffiliateOptions.cs (Configuración tipada de tiendas y tags)
  ├─ Options/CommunityNotificationOptions.cs (DiscordInviteUrl, TelegramChannelUrl)
  ├─ Features/Affiliates/AffiliateUrlResolver.cs (Motor de inyección y normalización de URLs)
  ├─ Features/Community/CommunityNotificationService.cs (Fix de cálculo semanal)
  └─ Features/Bgg/BggImportService.cs (Gestión de errores limpios de BGG)

[ Ludeka.Infrastructure ]
  │
  ├─ Bgg/BggOptions.cs (Ajuste de ShouldSimulate)
  ├─ Bgg/BggXmlApiClient.cs (Propagación limpia de errores HTTP/Timeout)
  ├─ Services/GeminiOptions.cs & GeminiGameSummaryService.cs (Sin fallback heurístico si Simulate=false)
  └─ YouTube/YouTubeOptions.cs & YouTubeSearchService.cs (Sin dataset mock en fallo si Simulate=false)
```

---

## 2. Definición de Contratos y Modelos

### `IAffiliateUrlResolver.cs`
```csharp
namespace Ludeka.Application.Contracts;

public interface IAffiliateUrlResolver
{
    /// <summary>
    /// Transforma una URL de tienda añadiendo el parámetro privado de afiliación según las reglas configuradas.
    /// </summary>
    string ResolveAffiliateUrl(string rawUrl, string? storeName = null);
}
```

### `AffiliateOptions.cs`
```csharp
namespace Ludeka.Application.Options;

public class AffiliateOptions
{
    public const string SectionName = "Affiliates";

    public Dictionary<string, StoreAffiliateRule> Stores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class StoreAffiliateRule
{
    public string ParamName { get; set; } = "ref";
    public string AffiliateTag { get; set; } = string.Empty;
    public string? DomainMatch { get; set; }
}
```

### Reglas por defecto en `AffiliateOptions`:
- `Zacatrus`: `ParamName: "ref"`, `DomainMatch: "zacatrus.es"`
- `Mathom`: `ParamName: "aff"`, `DomainMatch: "mathom.es"`
- `DungeonMarvels`: `ParamName: "ref"`, `DomainMatch: "dungeonmarvels.com"`
- `CuartoDeJuegos`: `ParamName: "ref"`, `DomainMatch: "cuartodejuegos.es"`
- `Tablerum`: `ParamName: "partner"`, `DomainMatch: "tablerum.es"`

---

## 3. Ajuste de Opciones de Integraciones Externas

### `BggOptions.cs`
```csharp
public bool ShouldSimulate => SimulateApi; // Ya no forzar simulación si SimulateApi es false
```

### `GeminiGameSummaryService.cs`
Cuando `_options.ShouldSimulate` sea `false`:
- Si `_options.ApiKey` está vacía o la llamada a la API falla, no llamar a `GenerateHeuristicSummary`.
- Retornar un `AiSummaryResult` fallido con mensaje de error específico.
- Enviar notificación de incidencia al repositorio de reportes de error o registrar en log estructurado de auditoría.

### `YouTubeSearchService.cs`
Cuando `_options.ShouldSimulate` sea `false`:
- Si la llamada HTTP a YouTube falla (por cuota 403 o error 5xx), retornar lista vacía `new List<YouTubeSearchResultDto>()` y loguear la advertencia. No devolver `FilterSimulated(YouTubeSimulationDataset.GetCuratedOrGeneratedVideos(...))`.

---

## 4. Diseño de UI del Footer Global (`MainLayout.razor`)

El pie de página se estructurará con:
1. **Comunidad Ludeka:**
   - Botón directo de **Discord** con icono Lucide `<Icon Name="message-circle" Size="16" />` (o icono representativo) y texto "Unirse a Discord".
   - Botón directo de **Telegram** con icono `<Icon Name="send" Size="16" />` y texto "Canal de Telegram".
2. **Atribución Legal Oficial:**
   - Bloque editorial: *"Datos de catálogo, fichas y clasificaciones sincronizados con BoardGameGeek."*
   - Insignia con texto/logo *"Powered by BoardGameGeek"* y enlace `https://boardgamegeek.com/`.
3. **Transparencia y Afiliación:**
   - Enlace a `/transparencia` con aviso: *"Ludeka participa en programas de afiliación lúdica recomendando tiendas especializadas sin coste adicional para el usuario."*
