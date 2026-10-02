# Especificación de Requerimientos: Reclasificación y Vinculación Masiva de Expansiones desde Snapshots BGG (INC-99)

## 1. Requerimientos Funcionales

### RF-01: Reconciliación Masiva de Expansiones desde Snapshots
- El sistema debe proporcionar un método `ReconcileAndLinkExpansionsFromSnapshotsAsync` en `IBggRawSnapshotSyncService`.
- Debe iterar de forma paginada/por lotes sobre los snapshots persistidos en `BggRawSnapshots`.
- Para cada snapshot:
  - **Detección de Expansión por Tipo o Enlace Entrante**: Si el payload JSON presenta `"@type": "boardgameexpansion"` o contiene un enlace entrante `<link type="boardgameexpansion" id="{baseBggId}" inbound="true" />`:
    - El juego asociado en la tabla `Games` (coincidente en `BggId`) debe actualizarse a `GameType.Expansion`.
    - Si existe en `Games` el juego base con `BggId == baseBggId`:
      - Se debe establecer `expGame.SetBaseGameId(baseGame.Id)`.
  - **Detección de Expansiones Hijas por Enlaces Salientes**: Si el snapshot pertenece a un juego base y contiene enlaces salientes `<link type="boardgameexpansion" id="{childBggId}" />`:
    - Para cada `childBggId` existente en `Games`:
      - Si `childGame.BaseGameId == null`, debe actualizarse a `GameType.Expansion` y enlazarse con `childGame.SetBaseGameId(baseGame.Id)`.

### RF-02: Preservación de Idempotencia y Cero Peticiones Externas
- La reconciliación no debe realizar llamadas HTTP hacia la API de BoardGameGeek.
- Si un juego ya está clasificado como `GameType.Expansion` y ya tiene asignado el `BaseGameId` correcto, no debe emitir escrituras redundantes a la base de datos.
- La ejecución repetida del proceso debe ser 100% segura e idempotente.

### RF-03: Corrección en la Promoción de Staging
- En `BggMassIngestionService.PromoteReadyToCatalogBatchAsync`, antes de crear un nuevo `Game`:
  - Se debe consultar si existe un snapshot en `BggRawSnapshots` para el `BggId` correspondiente.
  - Si el snapshot indica que es una expansión, se debe instanciar con `type: GameType.Expansion`.
  - Si el juego base existe en `Games`, se debe asignar inmediatamente `baseGameId: baseGame.Id`.

### RF-04: Ejecución Desatendida en CLI (Ludeka.Jobs)
- Se debe registrar un runner `BggReconcileExpansionsJobRunner` bajo el nombre `JobNames.BggReconcileExpansions` (`bgg-reconcile-expansions`).
- Permitirá ejecutar la reconciliación masiva desatendida mediante:
  ```bash
  dotnet run --project src/Ludeka.Jobs -- bgg-reconcile-expansions
  ```
- Debe utilizar `IJobExecutionCoordinator.ExecuteWithWindowLeaseAsync` para evitar ejecuciones concurrentes.

### RF-05: Telemetría e Interfaz en `/admin/cola-catalogacion`
- En la tarjeta de Snapshots de `/admin/cola-catalogacion`:
  - Se debe añadir el botón «Reconciliar Expansiones desde Snapshots».
  - Al pulsar, ejecutará la reconciliación y reportará: total evaluados, total reclasificados como expansión y total vinculados a su juego base.
- En la cuadrícula de Staging:
  - Desglosar la tarjeta de «Fallidos» para reflejar con precisión los títulos estancados sin promover (`FailedFetchCount` / pendientes de revisión) diferenciándolos de advertencias en títulos ya publicados.

---

## 2. Requerimientos No Funcionales

### RNF-01: Eficiencia de Memoria y Concurrencia
- El escaneo de 17.464 snapshots debe realizarse en lotes (ej. 200–500 snapshots por bloque), liberando el contexto de Entity Framework Core para prevenir fugas de memoria y saturación del Change Tracker.

### RNF-02: Compatibilidad de Esquema Dual
- El proceso debe operar idénticamente sobre SQLite (desarrollo y pruebas) y PostgreSQL (producción en Supabase).
