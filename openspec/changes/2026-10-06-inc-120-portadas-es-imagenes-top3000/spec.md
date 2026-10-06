# Especificación: INC-120 Extracción de Portadas en Español desde Snapshots y Sincronización de Imágenes Comunitarias Top 3.000 BGG

## 1. Resumen Ejecutivo y Actores

Este incremento resuelve la visibilidad de imágenes en el catálogo de Ludeka, priorizando portadas localizadas en castellano y enriqueciendo las fichas con fotos comunitarias de contraportada y mesa para los 3.000 títulos más relevantes de BoardGameGeek.

### Actores
- **Usuario Visitante / Registrado:** Visualiza carátulas nítidas y reales (en castellano si existen) y puede alternar entre portada, contraportada y fotografía en mesa en el carrusel de la ficha.
- **Administrador de Sistema / Job Runner:** Dispone del trabajo autónomo `bgg-images-top3000` para ejecutar la sincronización masiva de medios de forma desatendida y tolerante a fallos.

---

## 2. Requerimientos Funcionales (RF)

### RF-01: Extracción de Carátula en Español desde Snapshots BGG
- **RF-01.1:** `BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson` debe examinar el nodo `<item type="boardgameversion">` de las versiones en español y extraer los campos `image` y `thumbnail` si están presentes.
- **RF-01.2:** Si la URL de imagen o miniatura comienza por el protocolo relativo `//`, debe normalizarse prefijando `https:`.
- **RF-01.3:** Si existen múltiples versiones en español y la versión elegida inicialmente carece de imagen pero otra versión española la contiene, el parser debe enriquecer la candidata para no perder la portada.
- **RF-01.4:** El DTO `BggSpanishVersionInfoDto` debe exponer `CoverImageUrl` y `ThumbnailUrl`.

### RF-02: Priorización de Portada Española en Barrido de Catálogo
- **RF-02.1:** Durante el barrido de versiones (`BggRawSnapshotSyncService.SweepCatalogFromVersionsCoreAsync`), si el snapshot contiene `CoverImageUrl` en español, se debe actualizar la entidad `Game` mediante `game.UpdateImages(vInfo.CoverImageUrl, vInfo.ThumbnailUrl ?? game.ThumbnailUrl)`.
- **RF-02.2:** La portada en español solo se asigna si la URL es válida; nunca debe sobreescribir una carátula internacional con un valor nulo o en blanco.
- **RF-02.3:** Si el juego tiene una URL que apunta a almacenamiento simulado roto (`.r2.dev/games/` sin archivo físico) o al SVG por defecto, la portada de la versión (o la internacional del snapshot) debe sustituirla de inmediato.

### RF-03: Sincronización de Medios Comunitarios para el Top 3.000 BGG
- **RF-03.1:** El servicio `IBggImagesSyncService` debe consultar los juegos ordenados por `BggRank` ascendente (`BggRank != null && BggRank <= 3000`).
- **RF-03.2:** Para cada juego que requiera actualización (carezca de contraportada, foto en mesa, o tenga URL simulada rota):
  - Si el snapshot local tiene portada en español, se toma como portada frontal prioritaria (`CoverImageUrl`).
  - Se consulta `GeekDoImagesClient.GetTopVotedImagesAsync` para obtener la contraportada (`BackCoverImageUrl`) y la foto en mesa (`TableImageUrl`). Si no había portada en español, se toma la portada frontal de la galería (`FrontCoverUrl`).
  - En entornos con credenciales válidas de Cloudflare R2 (`HasValidCredentials == true`), se descargan y suben variantes optimizadas en WebP.
  - En entornos sin credenciales de R2 (Zero-Cloud / desarrollo local / persistencia directa), se guardan directamente las URLs canónicas seguras del CDN de GeekDo/BGG (`https://cf.geekdo-images.com/...`), garantizando visualización inmediata sin depender de almacenamiento externo.
- **RF-03.3:** El proceso debe aplicar una pausa de cortesía respetuosa hacia GeekDo (~800-1.200 ms por juego consultado) y gestionar reintentos defensivos ante respuestas vacías o errores temporales de red.

### RF-04: Runner Autónomo en `Ludeka.Jobs`
- **RF-04.1:** Registro de `JobNames.BggImagesTop3000` con el identificador `"bgg-images-top3000"`.
- **RF-04.2:** Creación de `BggImagesTop3000JobRunner` coordinado mediante `IJobExecutionCoordinator` y latidos `heartbeat.BeatAsync`.
- **RF-04.3:** Ejecución en bucle monotónico en lotes de 20-50 títulos hasta recorrer el Top 3.000 completo.

---

## 3. Requerimientos No Funcionales (RNF)

- **RNF-01 (Idempotencia):** La ejecución sucesiva del sincronizador o barrido no debe provocar escrituras redundantes en la base de datos si las URLs ya son idénticas.
- **RNF-02 (Aislamiento de Errores):** El fallo en la descarga o procesamiento de un juego específico no debe abortar el lote completo; se registra la incidencia y se continúa con el siguiente.
- **RNF-03 (Compatibilidad Dual):** Operatividad plena e idéntica tanto en base de datos local SQLite (`ludeka.db`) como en PostgreSQL (Supabase) en producción.
- **RNF-04 (Rendimiento y Zero-Cloud):** En ausencia de R2, la persistencia directa de URLs de CDN de BGG no consume almacenamiento en disco ni memoria volátil, y es 100% visible para los navegadores web.
