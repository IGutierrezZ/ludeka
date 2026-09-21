# Propuesta — INC-53: Ingesta Masiva Autónoma de Catálogo BGG (~8.000 Juegos) sin Manipulación Manual

> **Fase:** `sdd-propose` · **Fecha:** 2026-09-21  
> **Worktree:** `C:\repos\ludeka-wt\ingesta-masiva-autonoma-bgg`, rama `inc/ingesta-masiva-autonoma-bgg`, base `main` en `6d3b510`  
> **Entrada:** `openspec/changes/2026-09-21-change-53-ingesta-masiva-autonoma-bgg/explore.md`

**Convenio de lectura.** Cada afirmación técnica lleva marca explícita:
> **[V]** hecho verificado en esta fase, con `fichero:línea` o fuente citada ·  
> **[R]** recomendación de esta propuesta, sujeta a validación en `sdd-design` ·  
> **[NV]** afirmación **no verificada**, declarada como hueco a cerrar antes de `sdd-apply`.

---

## 1. Problema (qué está roto y por qué importa ahora)

Tras el primer despliegue en producción, al ejecutarse el Cloud Run Job `ludeka-job-nightly-cataloging`, su Fase 3 de Staging reportó:
`Ciclo de drenaje: 0 detalles, 0 imágenes, 0 síntesis IA, 0 promovidos` **[V]** (`docs/increments/inc-53-ingesta-masiva-autonoma-bgg.md:14-15`).

La tabla `BggCatalogStaging` se encuentra vacía **[V]**.

En el diseño original del Módulo 27 (INC-41), el aprovisionamiento de Staging se concibió recibiendo un flujo local ya disponible:
`IBggMassIngestionService.IngestRanksDumpAsync(Stream dumpStream, int minUsersRated = 30, ...)` **[V]** (`src/Ludeka.Application/Contracts/IBggMassIngestionService.cs:16`).
Esto requería que un operador buscase, descargase o generase un archivo `bg_ranks.csv` en su máquina para luego suministrarlo.

El maintainer clarificó taxativamente el principio de producto:
> *«La idea no era generar yo un CSV, creía que podríamos traerlos de BGG con un filtro.»* **[V]** (`inc-53-ingesta-masiva-autonoma-bgg.md:20`).

**Principio rector:** El sistema debe ser **100% autónomo**. Ni el maintainer ni ningún administrador deben manipular ficheros locales.

---

## 2. Objetivo y resultado esperado

Dotar a Ludeka de un mecanismo autónomo de descarga, filtrado y poblado de la tabla intermedia `BggCatalogStaging`:

1. **Descarga y Streaming Autónomo:** El backend descarga directamente el dataset público de clasificaciones de BGG (actualizado diariamente) mediante streaming HTTP continuo con descompresión al vuelo y filtrado comunitario (`usersrated >= 30`), sin volcar a disco efímero y con consumo de RAM acotado (< 30 MB) **[R]**.
2. **Acción Interactiva en Administración:** En `/admin/cola-catalogacion`, un botón en la sección de Staging permite al maintainer lanzar la descarga con indicador visual de progreso reactivo **[R]**.
3. **Ejecución Desatendida en Cloud Run Jobs:** `Ludeka.Jobs` ofrece el subcomando `seed-staging` y auto-siembra condicional al inicio de `nightly-cataloging` cuando Staging esté vacío **[R]**.
4. **Idempotencia y No Regresión:** Si la tabla ya contiene títulos, el proceso actualiza estadísticas de votos y ranking sin alterar registros ya avanzados o promovidos **[V]** (`BggCatalogStagingRepository.cs`).

---

## 3. Alcance

### 3.1. Dentro

| # | Entregable | Módulo / Capa |
|---|---|---|
| A1 | Método `DownloadAndIngestLatestRanksAsync` en `IBggMassIngestionService` y `BggMassIngestionService`, con resolución resiliente de fecha N días atrás (Fastly CDN / GitHub Raw) | `Ludeka.Application` |
| A2 | Soporte en `BggDumpParser` para procesar flujos continuos no buscables (`CanSeek == false`) provenientes de `HttpClient` | `Ludeka.Application` |
| A3 | Parametrización en `BggMassIngestionOptions` (`RanksDumpUrlPattern`, `MaxFallbackDays`, `Simulate`) | `Ludeka.Application` |
| A4 | Revalidación de sesión y permisos (`ModeratorPermission.CanEditGames`) según INC-46 para la acción interactiva | `Ludeka.Application` |
| A5 | Botón de acción e indicador de progreso interactivo en `CatalogQueueAdmin.razor` | `Ludeka.Web` |
| A6 | Comando `seed-staging` y auto-siembra condicional en `Ludeka.Jobs` | `Ludeka.Jobs` |
| A7 | Suite completa de pruebas unitarias cubriendo resolución de fechas, streaming, permisos y simulación | `Ludeka.UnitTests` |

### 3.2. Fuera (explícito)

- Alteración del ciclo posterior de drenaje (Fase 3: Thing, GeekDo, Gemini Flash y promoción a `Games` ya funcionan y están cubiertos en INC-41 e INC-48).
- Modificación del esquema de base de datos relacional de `BggCatalogStagingItem` (ya contiene todos los campos necesarios).
- Descarga directa de volcados oficiales XML de BGG que requieran autenticación de pago.

### 3.3. Capacidades (contrato con `sdd-spec`)

- **`autonomous-bgg-catalog-ingestion`**: Especificación del ciclo de vida de descarga, resiliencia temporal de fechas, filtrado comunitario de relevancia (`usersrated >= 30`), streaming HTTP sin sobrecarga de memoria, y orquestación desatendida.

---

## 4. Decisiones Técnicas y Arquitectura

### 4.1. Origen Remoto de Datos: `beefsack/bgg-ranking-historicals`
- Se utiliza el mirror público consolidado mantenido por la comunidad lúdica:
  `https://raw.githubusercontent.com/beefsack/bgg-ranking-historicals/master/{yyyy-MM-dd}.csv` **[V]**.
- **Ventajas:**
  - Servido a través de la CDN de Fastly (`raw.githubusercontent.com`), sin cuotas de rate limiting de API.
  - Generado de forma automática diariamente mediante workflows de GitHub Actions **[V]** (comprobado commit de hoy 2026-09-21T00:58:07Z).
  - Pesa ~7,1 MB y contiene ~31.300 filas. Con el filtro `usersrated >= 30` de `BggDumpParser`, extrae de forma limpia los ~8.000 juegos relevantes **[V]**.

### 4.2. Estrategia de Fallback Temporal de Fechas
- Si la fecha de hoy UTC devuelve HTTP 404 (porque el workflow diario aún no se ha ejecutado a primera hora), el cliente prueba secuencialmente los días anteriores (`DateTime.UtcNow.AddDays(-1)`, etc.) hasta un máximo configurable (5 días).

### 4.3. Streaming HTTP en Memoria Acotada (< 30 MB RAM)
- Se utiliza `HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)` **[R]**.
- Se transmite el `Stream` resultante directamente a `BggDumpParser.ParseRanksDumpAsync` y de ahí en lotes de 100 a `_stagingRepo.UpsertBatchAsync`.
- Se evita completamente `ReadAsStringAsync()`, `ReadAllBytes()` o buffers masivos.

---

## 5. Criterios de Aceptación

1. **Cero Manipulación Local:** Un administrador puede presionar el botón en `/admin/cola-catalogacion` y Staging se puebla con los ~8.000 títulos sin subir ningún archivo.
2. **Auto-Siembra en Jobs:** Al ejecutarse `nightly-cataloging`, si Staging tiene 0 elementos, se dispara automáticamente la descarga e ingesta previa al drenaje.
3. **Subcomando Autónomo:** Es posible ejecutar `dotnet Ludeka.Jobs.dll seed-staging` directamente en el entorno de despliegue.
4. **Idempotencia:** Múltiples ejecuciones actualizan estadísticas sin generar duplicados ni alterar estados en curso.
5. **Todas las Pruebas Verdes:** La suite completa de pruebas unitarias e integración se mantiene al 100% (1.593+ unitarias, 10 de integración).

---

## 6. Plan de Entrega en PRs (Cadena Apilada)

Siguiendo la regla de no exceder las 400 líneas por PR:

- **PR 1 (Core & Application):** Opciones de configuración, soporte de streams no buscables en `BggDumpParser`, método `DownloadAndIngestLatestRanksAsync` en `BggMassIngestionService` con pruebas unitarias.
- **PR 2 (UI & Jobs):** Botón e indicador de progreso en `CatalogQueueAdmin.razor`, runner/comando `seed-staging` y auto-siembra en `Ludeka.Jobs`.
- **PR 3 (Cierre & Documentación):** Verificación final, actualización de la especificación viva (`27-ingesta-masiva-bgg-galeria-geekdo-ia-lotes.md` y `34-trabajos-en-segundo-plano-cloud-run.md`) y archivado SDD.
