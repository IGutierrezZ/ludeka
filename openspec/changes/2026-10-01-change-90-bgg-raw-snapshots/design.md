# Documento de Diseño Arquitectónico: INC-90 — Snapshots Crudos BGG, Refinamiento Integral de Catálogo, Ficha Editorial y Retorno de Sesión

> **ID del Cambio:** `change-90-bgg-raw-snapshots`  
> **Incremento Asociado:** INC-90  
> **Estado:** ⏳ Diseño en revisión  

---

## 1. Decisiones de Arquitectura (ADRs)

### Decisión D1: Almacenamiento Satélite Desacoplado para `BggRawSnapshots`
* **Contexto:** BGG devuelve payloads de 20-100 KB por juego. Guardarlos en la tabla `Games` multiplicaría el tamaño de la tabla caliente y acoplaría el modelo de dominio a esquemas externos.
* **Diseño:**
  1. Se crea la entidad de infraestructura/satélite `BggRawSnapshot`:
     ```csharp
     public class BggRawSnapshot
     {
         public int BggId { get; private set; }
         public string RawJson { get; private set; } = string.Empty;
         public int ApiVersion { get; private set; } = 2;
         public DateTimeOffset FetchedAtUtc { get; private set; }
         public DateTimeOffset? UpdatedAtUtc { get; private set; }
     }
     ```
  2. Mapeo en EF Core:
     * Tabla: `BggRawSnapshots`.
     * Clave primaria: `BggId`.
     * Columna `RawJson`: mapeada como `HasColumnType("jsonb")` en PostgreSQL y `TEXT` en SQLite.
  3. `Games` permanece completamente inalterada en su huella de I/O de disco.

### Decisión D2: Extensión de Criterios y Repositorio para Ordenación Dinámica (`GameSortOrder`)
* **Contexto:** Actualmente `SqliteGameRepository.SearchAsync` aplica un orden fijo por `BggRank` y `BggRating`.
* **Diseño:**
  1. Definición del enum en `Ludeka.Core.Enums`:
     ```csharp
     public enum GameSortOrder
     {
         Rank = 0,
         RatingDesc = 1,
         ComplexityAsc = 2,
         ComplexityDesc = 3,
         DurationAsc = 4,
         DurationDesc = 5,
         YearDesc = 6,
         TitleAsc = 7
     }
     ```
  2. En `SqliteGameRepository.SearchAsync`:
     * Aplicar `ApplySorting(query, criteria.SortBy)` tanto en la consulta directa como en el ordenamiento del índice ID en la paginación en dos fases.
     * Mapeo de dureza: se ordena utilizando la propiedad computada de complejidad o `Weight`.

### Decisión D3: Gestión Integral de Imágenes del Carrusel en `GameEditorModal`
* **Contexto:** `Game` tiene `CoverImageUrl`, `BackCoverImageUrl` y `TableImageUrl`, pero `GameEditorModal` solo exponía la carátula principal.
* **Diseño:**
  1. En `GameEditorModal.razor`, la pestaña de imágenes pasa a denominarse «Galería y Carátulas» y expone 3 secciones visuales independientes:
     * Carátula frontal (`CoverImageUrl`).
     * Trasera de caja (`BackCoverImageUrl`).
     * Despliegue en mesa (`TableImageUrl`).
  2. Cada sección dispone de previsualización en vivo, subida a Cloudflare R2 vía `IImageStorageService` con recorte y compresión WebP, y opción de borrado.
  3. Al guardar, se invoca `GameEditorService.UpdateGameImagesAsync(...)` garantizando la coherencia inmediata del carrusel.

### Decisión D4: Sanitización y Propagación Segura de `returnUrl` en Login Externo
* **Contexto:** En `Program.cs`, `/login/external` forzaba `RedirectUri = "/"`.
* **Diseño:**
  ```csharp
  var returnUrl = form["returnUrl"].ToString();
  var redirectTarget = LoginRedirect.IsLocalUrl(returnUrl) ? returnUrl : "/";
  return Results.Challenge(new AuthenticationProperties { RedirectUri = redirectTarget }, [registration.Scheme]);
  ```
  Esto garantiza que tras la autenticación con Google, Discord o Facebook, el framework redirija al usuario a la página local exacta desde la que solicitó el acceso.

---

## 2. Diagrama de Flujo: Poblado de Snapshots BGG con *Rate Limiting*

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Moderador / Administrador
    participant AdminUI as CatalogQueueAdmin.razor (/admin/cola-catalogacion)
    participant SyncService as BggRawSnapshotSyncService
    participant Repo as SqliteBggRawSnapshotRepository
    participant BggClient as BggXmlApiClient
    participant BGG as API BGG (/xmlapi2/thing)

    Admin->>AdminUI: Clic en "Iniciar Sincronización de Snapshots"
    AdminUI->>SyncService: SyncMissingSnapshotsBatchAsync(batchSize: 20, delayMs: 1200)
    SyncService->>Repo: GetMissingBggIdsAsync(limit: 20)
    Repo-->>SyncService: Lista de BggIds sin snapshot

    loop Para cada BggId en lote
        SyncService->>BggClient: FetchRawThingXmlAsync(bggId)
        BggClient->>BGG: GET /xmlapi2/thing?id={bggId}&stats=1
        BGG-->>BggClient: Respuesta XML completa
        BggClient-->>SyncService: XML en bruto
        SyncService->>SyncService: Transformar XML a JSON estructurado fiel
        SyncService->>Repo: UpsertAsync(BggRawSnapshot)
        SyncService->>SyncService: Task.Delay(1200ms)
        SyncService-->>AdminUI: Reportar progreso (1/20, 2/20...)
    end

    SyncService-->>AdminUI: Lote completado con métricas actualizadas
```

---

## 3. Diagrama de Flujo: Retorno de Sesión tras Login

```mermaid
sequenceDiagram
    autonumber
    actor Usuario as Visitante en /juegos/brass-birmingham
    participant Browser as Navegador Web
    participant LoginUI as Login.razor (/login?returnUrl=...)
    participant Endpoint as POST /login/external
    participant OAuth as Proveedor OAuth (Google/Discord)
    participant Cookie as Cookie de Sesión Ludeka

    Usuario->>Browser: Clic en "Iniciar Sesión"
    Browser->>LoginUI: Navega a /login?returnUrl=%2Fjuegos%2Fbrass-birmingham
    LoginUI->>Endpoint: Submit con provider="Google" y returnUrl="/juegos/brass-birmingham"
    Endpoint->>OAuth: Results.Challenge con RedirectUri="/juegos/brass-birmingham"
    OAuth-->>Browser: Consentimiento de cuenta exitoso
    Browser->>Endpoint: Callback OAuth
    Endpoint->>Cookie: Emite cookie de sesión propia de Ludeka
    Endpoint-->>Browser: Redirección HTTP 302 a "/juegos/brass-birmingham"
    Browser-->>Usuario: Usuario aterriza identificado en la ficha del juego
```
