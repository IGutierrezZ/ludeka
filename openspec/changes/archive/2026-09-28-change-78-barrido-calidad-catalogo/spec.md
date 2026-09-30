# Especificación de Requerimientos: INC-78 — Barrido Completo de Calidad de Catálogo (~10.000 Juegos Promovidos)

## 1. Requerimientos Funcionales

### RF-01: Paginación Determinista por Cursor de Catálogo
- El sistema debe permitir consultar títulos de la tabla `Games` mediante paginación por cursor basada en `BggId` (`WHERE BggId > @afterBggId ORDER BY BggId ASC LIMIT @limit`).
- Debe garantizar que ningún juego se repite ni se omite durante una pasada completa.
- Debe retornar una lista vacía cuando no existan más títulos con `BggId` superior al cursor dado.

### RF-02: Barrido y Auditoría de Calidad por Lote
- Para cada juego retornado en el lote de barrido:
  1. Si `BggCatalogStagingItem` existe y contiene `<dna ` en `RawThingXml`:
     - Se extrae estilo inferido (`GetInferredStyle()`), confrontación (`GetInferredConfrontation()`), huella y duración total.
     - Se actualiza `Game.UpdateDna` si el estilo difiere de `Game.Style` o si el estilo actual es `Eurogame`.
     - Se actualiza `Game.UpdateDuration` si los minutos estimados por jugador o totales difieren.
     - Se actualizan fundas (`Sleeves`), editorial en español y editoriales regionales si el juego carece de ellas.
     - Se actualiza escalabilidad comunitaria si la entidad en catálogo carece de votos y staging sí los tiene.
  2. Si `BggCatalogStagingItem` no tiene `<dna ` o no existe:
     - Se realiza petición a BGG XMLAPI2 (`FetchGameByBggIdAsync`).
     - Se actualizan todos los metadatos correspondientes y se guarda la traza `<dna ` en staging.
  3. Si tras la evaluación no hubo ninguna modificación en la entidad `Game`:
     - Se contabiliza como `Skipped` y se omite la llamada a `_gameRepo.UpdateAsync(game)`.
  4. Si hubo modificaciones:
     - Se persiste con `_gameRepo.UpdateAsync(game)` y se incrementa `UpdatedCount`.

### RF-03: Mando y Observabilidad en UI Administrativa
- En `/admin/cola-catalogacion`, se debe proporcionar un botón específico: **«Barrido Completo de Calidad (~10.000)»**.
- Al iniciarse, ejecuta el barrido continuo en segundo plano mostrando:
  - Total evaluados vs total catálogo (`GetTotalCatalogCountAsync`).
  - Total corregidos/actualizados.
  - Total omitidos por estar ya correctos.
  - Total con error si los hubiera.
- Un botón **«Detener Barrido»** que cancela el `CancellationToken` de forma segura.

### RF-04: Ejecución Desatendida en Cloud Run Jobs
- El runner `BackfillQualityJobRunner` en `Ludeka.Jobs` debe ejecutar el barrido continuo por cursor desde `afterBggId = 0` hasta completar el catálogo, registrando en log el progreso de cada lote de 50.

---

## 2. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Paginación por cursor de BggId recorre secuencialmente el catálogo
  Dado un catálogo con 120 juegos con BggId de 1 a 120
  Cuando se solicita un lote con afterBggId = 0 y limit = 50
  Entonces se retornan 50 juegos con BggId de 1 a 50
  Y el LastBggIdProcessed es 50
  Cuando se solicita el siguiente lote con afterBggId = 50 y limit = 50
  Entonces se retornan 50 juegos con BggId de 51 a 100
  Y el LastBggIdProcessed es 100
  Cuando se solicita el siguiente lote con afterBggId = 100 y limit = 50
  Entonces se retornan 20 juegos con BggId de 101 a 120
  Y HasMore es falso

Escenario: Un juego temático con estilo Eurogame histórico es corregido en el barrido
  Dado un juego en catálogo "Dune: Imperium" con BggId 316554, Style = Eurogame y Duración = 15 min
  Y datos en staging o BGG indicando Style = Ameritrash y Duración = 30 min/jugador
  Cuando se ejecuta el lote de barrido que contiene a "Dune: Imperium"
  Entonces el juego se actualiza a Style = Ameritrash y Duración = 30 min/jugador
  Y UpdatedCount se incrementa en 1

Escenario: Un juego ya correcto no genera escrituras en base de datos
  Dado un juego en catálogo "Catán" que ya posee Style = Eurogame, Duración = 25 min/jugador y Escalabilidad con votos
  Cuando se ejecuta el lote de barrido que contiene a "Catán"
  Entonces no se muta la entidad y SkippedCount se incrementa en 1
```
