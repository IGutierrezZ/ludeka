# Informe de Verificación: INC-120 Extracción de Portadas en Español y Sincronización Top 3.000 BGG

> **Incremento:** INC-120  
> **Fecha:** 6 de octubre de 2026  
> **Estado:** Aprobado (100% de pruebas en verde)  
> **Suite de Pruebas:** 2.595 superadas, 0 fallidas, 0 omitidas  

---

## 1. Resumen de la Verificación

Se ha verificado de forma exhaustiva e integral la implementación del incremento INC-120 en el worktree aislado `F:\repos\ludeka-wt\bgg-imagenes-top3000` sobre la rama `inc/bgg-imagenes-top3000`.

La funcionalidad resuelve la visibilidad de imágenes en el catálogo caliente de Ludeka, priorizando portadas localizadas en castellano extraídas de snapshots de versiones satélite e incorporando un proceso de sincronización de medios comunitarios (portada, contraportada y fotografía en mesa) para los 3.000 juegos más relevantes de BoardGameGeek.

---

## 2. Cobertura de Requerimientos

| Requerimiento | Descripción | Estado | Evidencia de Verificación |
|---|---|---|---|
| **RF-01.1 / RF-01.4** | Extracción de `image` y `thumbnail` de versiones en español y enriquecimiento de `BggSpanishVersionInfoDto`. | **Verificado** | `BggRawSnapshotParserVersionsTests` (37/37 tests verdes). |
| **RF-01.2** | Normalización de URLs con protocolo relativo `//` a `https:`. | **Verificado** | Pruebas unitarias de parser con URLs relativas normalizadas. |
| **RF-01.3** | Fusión de candidatas españolas cuando la versión con mejor EAN no tiene portada pero otra versión española sí. | **Verificado** | Test de enriquecimiento multicandidata en `BggRawSnapshotParserVersionsTests`. |
| **RF-02.1 / RF-02.2** | Promoción de carátula en español en el barrido de catálogo (`SweepCatalogFromVersionsCoreAsync`). | **Verificado** | `SweepCatalogFromVersionsAsync_ShouldPromoteSpanishCover_WhenSpanishVersionHasCoverImage` en `BggRawSnapshotSyncServiceTests`. |
| **RF-02.3** | Detección y recuperación de URLs rotas de Cloudflare R2 simulado (`.r2.dev/games/`) hacia URLs canónicas de BGG. | **Verificado** | `SweepCatalogFromVersionsAsync_ShouldRecoverBrokenOrSimulatedCover_FromRootSnapshot_WhenNoSpanishCover` en `BggRawSnapshotSyncServiceTests`. |
| **RF-03.1 / RF-03.2** | Servicio `IBggImagesSyncService` para enriquecimiento de fotos comunitarias Top 3.000 con soporte Zero-Cloud. | **Verificado** | `BggImagesSyncServiceTests` (4/4 tests verdes, cubriendo modo Zero-Cloud, recuperación de portada simulada, idempotencia y paginación keyset). |
| **RF-04.1 / RF-04.3** | Trabajo autónomo `bgg-images-top3000` en `Ludeka.Jobs` con coordinación por ventana de leases y latidos. | **Verificado** | `BggImagesTop3000JobRunnerTests` (3/3 tests verdes) y `LudekaJobsCompositionTests` (15 runners verificados). |

---

## 3. Resultados de la Suite Automatizada

- **Comando ejecutado:** `dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj`
- **Total de pruebas ejecutadas:** 2.595
- **Superadas:** 2.595 (100%)
- **Fallidas:** 0
- **Omitidas:** 0
- **Regresiones detectadas:** 0

---

## 4. Estrategia de Persistencia y Resiliencia Zero-Cloud

Se ha validado la corrección de la causa raíz de las imágenes rotas (404):
- En ausencia de credenciales válidas de Cloudflare R2 (`HasValidCredentials == false`), el nuevo sincronizador no intenta almacenar archivos en memoria efímera ni devuelve URLs públicas de R2 inexistentes.
- En su lugar, almacena directamente las URLs canónicas seguras del CDN de GeekDo/BGG (`https://cf.geekdo-images.com/...`), garantizando que las imágenes se visualicen de forma inmediata y persistente.
- El barrido de versiones local recupera proactivamente cualquier portada que previamente hubiese quedado apuntando a URLs efímeras de R2 simulado.
