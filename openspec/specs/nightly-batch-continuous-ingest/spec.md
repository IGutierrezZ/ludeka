# Especificación: Integración en Lote Nocturno y Panel de Administración

## 1. Propósito
Garantizar que el descubrimiento de novedades y tendencias BGG opere de forma desatendida cada noche en `NightlyCatalogingService`, además de permitir su ejecución manual con retroalimentación visual inmediata en el panel `/admin/cola-catalogacion`.

## 2. Requerimientos Funcionales

### RF-05: Fase 1.5 en el Orquestador Nocturno
- El método `ExecuteNightlyCatalogingAsync` debe ejecutar una nueva fase previa al procesamiento de la cola:
  - Invoca `_discoveryService.DiscoverAndEnqueueBggTrendsAsync(ct)`.
  - Captura excepciones sin abortar el ciclo general (tolerante a fallos de red hacia BGG).
  - Almacena el conteo de descubrimientos BGG en `NightlyCatalogingExecutionLog.BggDiscoveryCount` y `NightlyCatalogingResultDto.BggDiscoveryCount`.

### RF-06: Botón de Escaneo Manual en UI de Administración
- En `/admin/cola-catalogacion` (`CatalogQueueAdmin.razor`):
  - Añadir botón con icono Lucide `Sparkles` o `TrendingUp`: *"Escanear Novedades BGG"*.
  - Al pulsar, muestra estado de carga (`IsDiscoveringBgg = true`).
  - Al finalizar, muestra banner de éxito: *"Escaneo completado: X nuevos descubrimientos encolados, Y ya catalogados."*
  - Refresca automáticamente la tabla de la cola.

### RF-07: Filtro y Badges por Origen en la Cola
- El selector de filtro de la tabla de cola debe incorporar:
  - `BggNewReleases`: "Novedades BGG" (Badge ámbar con icono `Sparkles`).
  - `BggHotness`: "Tendencia BGG (Hotness)" (Badge fucsia con icono `TrendingUp`).
- No utilizar emojis en las plantillas Razor (cumpliendo contrato `WebMarkupContractTests`).

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Ejecución de la Fase 1.5 en el lote nocturno
  Dado un ciclo nocturno iniciado con cupo diario de 20 juegos
  Cuando se ejecuta la Fase 1.5 de auto-descubrimiento BGG
  Entonces se identifican las tendencias de BGG
  Y el resultado del ciclo incluye BggDiscoveryCount > 0
  Y la bitácora nocturna persiste el número de juegos descubiertos

Escenario: Escaneo manual desde el panel de administración
  Dado un administrador autenticado en /admin/cola-catalogacion
  Cuando pulsa el botón "Escanear Novedades BGG"
  Entonces se ejecuta el servicio de descubrimiento
  Y se muestra el resumen del escaneo con los títulos agregados a la cola
  Y la tabla se recarga reflejando los nuevos elementos con sus badges correspondientes
```
