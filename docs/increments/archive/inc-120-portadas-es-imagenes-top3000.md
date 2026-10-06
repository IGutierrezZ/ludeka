# INC-120: Extracción de Portadas en Español desde Snapshots y Sincronización de Imágenes Comunitarias Top 3.000 BGG

> **Incremento:** INC-120  
> **Estado:** ✅ Archivado  
> **Rama / Worktree:** `inc/bgg-imagenes-top3000` (`F:\repos\ludeka-wt\bgg-imagenes-top3000`)  
> **Fecha:** 2026-10-06  

---

## 1. Motivación y Objetivos

1. **Visibilidad Real de Medios:** Erradicar los fallos 404 causados por URLs simuladas huérfanas en catálogo y garantizar que las fichas de los juegos muestren imágenes reales de portada, contraportada y fotos de partida en mesa.
2. **Prioridad Editorial Localizada:** Si un juego cuenta con versión oficial en español en los snapshots de BGG, promover su portada en castellano como carátula principal del juego frente a la versión internacional en inglés.
3. **Cobertura de Galería Comunitaria (Top 3.000):** Dotar a los 3.000 títulos más representativos del catálogo caliente de fotografías de contraportada (`BackCoverImageUrl`) y mesa (`TableImageUrl`) recuperadas desde la API de GeekDo.

---

## 2. Componentes Afectados

- `src/Ludeka.Application/DTOs/BggVersionDtos.cs`: Inclusión de `CoverImageUrl` y `ThumbnailUrl` en `BggSpanishVersionInfoDto`.
- `src/Ludeka.Application/Features/Bgg/BggRawSnapshotParser.cs`: Extracción de `<image>` y `<thumbnail>` desde `<item type="boardgameversion">`, fusión de portadas multiedición y normalización de URLs relativas.
- `src/Ludeka.Infrastructure/Bgg/BggRawSnapshotSyncService.cs`: Promoción de portada española y recuperación de URLs rotas en el barrido de catálogo.
- `src/Ludeka.Application/Contracts/IBggImagesSyncService.cs` y `src/Ludeka.Infrastructure/Bgg/BggImagesSyncService.cs`: Orquestador de sincronización de medios para el Top 3.000 con estrategia Zero-Cloud anti-404.
- `src/Ludeka.Jobs/JobNames.cs` y `src/Ludeka.Jobs/Runners/BggImagesTop3000JobRunner.cs`: Runner autónomo `bgg-images-top3000`.
- `tests/Ludeka.UnitTests/`: Cobertura exhaustiva de extracción de imágenes de versión, asignación de medios comunitarios y composición del contenedor (2.595 tests superados).

---

## 3. Estado de Verificación

- **Suite Automatizada:** 2.595 superadas, 0 fallidas, 0 omitidas (100% verde).
- **Informe de Verificación:** `openspec/changes/2026-10-06-inc-120-portadas-es-imagenes-top3000/verification-report.md`.
- **Trabajo Autónomo:** `bgg-images-top3000` listo para despliegue y ejecución desatendida.
