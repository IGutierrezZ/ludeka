# Informe de Archivado — INC-53: Ingesta Masiva Autónoma de Catálogo BGG (~8.000 Juegos) sin Manipulación Manual

> **Fase:** `sdd-archive` · **Fecha:** 2026-09-21  
> **Worktree:** `C:\repos\ludeka-wt\ingesta-masiva-autonoma-bgg`, rama `inc/ingesta-masiva-autonoma-bgg`  
> **Autor:** Antigravity (Arquitecto Principal de Sistemas)  
> **Estado:** ✅ **ARCHIVADO Y LISTO PARA APERTURA DE PR**

---

## 1. Módulos de la Especificación Viva Actualizados

De acuerdo con la regla obligatoria de SDD para la fase `sdd-archive`, se ha volcado todo el conocimiento de dominio, arquitectura e implementación a la especificación viva del sistema en `docs/specs/sistema/`:

1. **`docs/specs/sistema/27-ingesta-masiva-bgg-galeria-geekdo-ia-lotes.md`:**
   - Incorporación de la visión y arquitectura de descarga 100% autónoma y desatendida de volcados de BGG.
   - Especificación de las nuevas opciones de configuración (`RanksDumpUrlPattern`, `MaxFallbackDays = 5`).
   - Mecanismo de streaming de red con `ResponseHeadersRead` y soporte para streams no buscables (`inputStream.CanSeek == false`) en `BggDumpParser`.
   - Implementación del botón interactivo de administración en `CatalogQueueAdmin.razor` con gobernanza de seguridad `ModeratorPermission.CanEditGames` (INC-46).
   - Documentación de la integración del runner de consola y la auto-siembra condicional nocturna.
   - Actualización del recuento de pruebas del módulo.

2. **`docs/specs/sistema/34-trabajos-en-segundo-plano-cloud-run.md`:**
   - Incorporación del quinto runner fino de Cloud Run Jobs: `SeedStagingJobRunner` (`seed-staging`).
   - Documentación del cálculo de clave de ventana diaria UTC y la idempotencia gestionada por `IJobExecutionCoordinator`.
   - Actualización de la lista `JobNames.All` y la tabla de runners.

3. **`docs/specs/sistema/README.md`:**
   - Actualización del total verificado de la suite de pruebas a **1.604 pruebas unitarias** y **10 pruebas de integración** (total: **1.614 pruebas en verde**).
   - Actualización de los resúmenes ejecutivos de los módulos 27 y 34 para reflejar las capacidades introducidas por el INC-53.

---

## 2. Trazabilidad del Incremento

- **Documento de Incremento:** Trasladado desde `docs/increments/inc-53-ingesta-masiva-autonoma-bgg.md` hacia `docs/increments/archive/inc-53-ingesta-masiva-autonoma-bgg.md`.
- **Roadmap Central:** Sincronizados `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md` marcando el incremento como `✅ Archivado`.
- **Directorio OpenSpec:** Registrado para archivo en `openspec/changes/archive/2026-09-21-change-53-ingesta-masiva-autonoma-bgg`.
