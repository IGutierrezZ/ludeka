# Especificación de Requerimientos: nightly-game-discovery

## 1. Definición del Módulo y Propósito
El módulo **Nightly Game Discovery & Smart Cataloging** unifica la detección proactiva de lanzamientos a partir de publicaciones editoriales y la ingesta programada nocturna de juegos de mesa en Ludeka, garantizando un crecimiento sostenido del catálogo con los títulos más relevantes del ranking mundial de BGG y respetando estrictamente los límites de cuota de red y de IA.

---

## 2. Requerimientos Funcionales (FR)

### FR-01: Extractor Heurístico y Semántico de Novedades (`INewsGameExtractor`)
- El sistema debe analizar el título y contenido de cada novedad editorial (`WeeklyRelease`).
- Debe aplicar patrones de extracción como:
  - `"anuncia (la edición en castellano de |el lanzamiento de )?[\"“«]?(?<title>[^\"”».,;]+)[\"”».,;]?"`
  - `"publicará [\"“«]?(?<title>[^\"”».,;]+)[\"”».,;]?"`
  - `"lanzamiento de [\"“«]?(?<title>[^\"”».,;]+)[\"”».,;]?"`
  - `"nueva expansión de [\"“«]?(?<title>[^\"”».,;]+)[\"”».,;]?"`
  - Limpieza de prefijos de editorial (ej. `"Devir: "`, `"Maldito Games - "`).
- **Verificación local:** Si el título extraído coincide de forma exacta o difusa con un juego ya existente en Ludeka (`IGameRepository`), vincula automáticamente la novedad asignando `WeeklyRelease.GameId = game.Id`.
- **Verificación externa en BGG:** Si no existe en Ludeka, consulta la API de búsqueda de BGG (`/xmlapi2/search?type=boardgame&query={title}`). Si BGG devuelve resultados, selecciona la mejor coincidencia y la registra en `PendingBggImports` con `Origin = CatalogQueueOrigin.NewsDiscovery` y `ExtractedTitle`.

### FR-02: Orquestador Nocturno Inteligente (`INightlyCatalogingService`)
- El orquestador ejecuta una rutina por lotes controlada por un cupo diario configurable (`DailyCatalogingLimit = 20`).
- **Fase 1 (Novedades):** Procesa lanzamientos pendientes sin juego vinculado buscando coincidencias.
- **Fase 2 (Cola Prioritaria):** Extrae hasta `DailyCatalogingLimit` elementos pendientes de `PendingBggImports` (status `Pending`), los descarga de BGG, genera su síntesis estructurada con IA (`IAiGameSummaryService`), los inserta en `IGameRepository` y actualiza el estado a `Completed`.
- **Fase 3 (Relleno con Top de BGG):** Si los juegos catalogados en la Fase 2 son inferiores a `DailyCatalogingLimit` (ej. se catalogaron 6, restan 14):
  - Obtiene el listado de juegos más destacados de BGG (`IBggClient.FetchTopGamesAsync`).
  - Descarta los que ya existen en el catálogo local de Ludeka o en la cola.
  - Ingesta los primeros `N` títulos restantes hasta completar exactamente el cupo diario de 20 juegos, asignándoles `Origin = CatalogQueueOrigin.TopBggBackfill`.
- **Fase 4 (Bitácora de Ejecución):** Registra un `NightlyCatalogingExecutionLog` con el resumen de la ejecución.

### FR-03: Cadencia y Rate Limiting Protector
- Entre peticiones consecutivas hacia BGG o el motor de IA, el orquestador aplica una pausa mínima configurable (`MinDelaySecondsBetweenCalls = 2.5` segundos por defecto) para prevenir códigos HTTP 429.
- En caso de fallo transitorio en un juego, marca el ítem como `Failed` con el mensaje de error y continúa con el siguiente sin abortar la ejecución completa.

### FR-04: Panel de Supervisión y Control (`/admin/cola-catalogacion`)
- Vista exclusiva para moderadores (`CanEditGames`) y Mesa Fundadora.
- Tarjetas con métricas en tiempo real: cupo diario configurado, elementos pendientes en cola, descubrimientos por novedades, títulos completados en la última ejecución.
- Botón para ejecutar el lote en caliente con barra de progreso o indicador de carga.
- Tabla detallada de la cola de catalogación con badges identificativos de origen:
  - 👤 `Usuario` (`UserImport`)
  - 📰 `Novedad Editorial` (`NewsDiscovery`)
  - 🏆 `Top BGG` (`TopBggBackfill`)
- Historial de ejecuciones nocturnas con marcas temporales y títulos catalogados.

---

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Detección y vinculación de novedad con juego ya existente en catálogo
  Dado que existe en Ludeka el juego "Wingspan" con ID "g-001"
  Y se procesa una novedad editorial con título "Devir anuncia la reimpresión de Wingspan"
  Cuando el extractor de novedades analiza el texto
  Entonces detecta el título "Wingspan"
  Y actualiza la novedad asignando GameId = "g-001"
  Y no inserta ningún registro en la cola de BGG

Escenario: Detección y encolado de juego no existente a partir de publicación editorial
  Dado que en Ludeka no existe el juego "Apiary"
  Y BGG Search devuelve el juego "Apiary" con BggId 370132
  Cuando se analiza una novedad con título "Devir anuncia la edición en castellano de Apiary"
  Entonces se inserta un registro en "PendingBggImports" con BggId 370132
  Y el origen del registro es "NewsDiscovery"
  Y el campo ExtractedTitle es "Apiary"

Escenario: Ejecución nocturna con cola parcial y relleno automático hasta el cupo
  Dado que la cola tiene 5 juegos pendientes
  Y el cupo diario configurado es de 20 juegos
  Cuando se ejecuta el servicio de catalogación nocturna
  Entonces procesa e ingesta los 5 juegos de la cola
  Y a continuación descarga e ingesta 15 juegos del Top de BGG que no estaban en Ludeka
  Y el total de nuevos juegos catalogados en la ejecución es exactamente 20
  Y se crea un registro de log con 5 procesados de cola y 15 de relleno Top BGG

Escenario: Respeto estricto del límite diario sin sobre-ingesta
  Dado que la cola de catalogación contiene 35 juegos pendientes
  Y el cupo diario configurado es de 20 juegos
  Cuando se ejecuta la catalogación nocturna
  Entonces se procesan únicamente los primeros 20 juegos de la cola
  Y los 15 juegos restantes permanecen en estado "Pending" para la siguiente noche
  Y no se consulta el Top de BGG por haberse alcanzado el cupo completo
```
