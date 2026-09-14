# Incremento 41: Ingesta Masiva de Catálogo BGG (~8.000 títulos), Fotos GeekDo y Síntesis IA en Lotes

- **Identificador SDD:** `change-41-ingesta-bgg-catalogo`
- **Estado:** ✅ **Archivado**
- **Rama:** `inc/ingesta-bgg-catalogo` (worktree en `C:\repos\ludeka-wt\ingesta-bgg-catalogo`)
- **Módulo del Sistema:** `docs/specs/sistema/27-ingesta-masiva-bgg-galeria-geekdo-ia-lotes.md`
- **Pruebas Verificadas:** 930/930 tests en verde (+24 nuevas pruebas unitarias)
- **Objetivo Principal:** Dotar a Ludeka de un catálogo base de alta calidad y exhaustividad con ~8.000 títulos seleccionados por relevancia comunitaria (`usersrated >= 30`), galería visual de 3 fotos comunitarias más votadas (portada, contraportada y mesa) alojadas en Cloudflare R2 como WebP deterministas, y síntesis editorial generada por Google Gemini Flash en lotes de 5-10 juegos con control de cuota diaria y reanudación automática.

---

## 1. Componentes Clave Implementados

1. **Filtro y Descarga del Dump BGG:**
   - Procesamiento de volcado streaming CSV `bg_ranks.csv` con cabeceras `id,name,yearpublished,rank,bayesaverage,average,usersrated`.
   - Filtro `usersrated >= 30` (configurable), seleccionando aproximadamente los ~8.000 títulos más representativos con mínimo consumo de memoria.
2. **Tabla Intermedia de Staging (`BggCatalogStaging`):**
   - Aislamiento total entre la carga masiva y la tabla principal `Games`.
   - Control desacoplado por estados (`Pending`, `Fetched`, `Completed`, `Failed`, `Skipped`, `QuotaExceeded`).
   - Soporte para SQLite (local/tests) con migración defensiva de esquema y PostgreSQL/Supabase (producción).
3. **Galería Comunitaria de GeekDo Images + R2:**
   - Consumo de `https://api.geekdo.com/api/images?ajax=1&gallery=all&objectid={bggId}&objecttype=thing`.
   - Selección de las 3 imágenes con mayor número de votos (`numpositive`):
     - Portada frontal (`boxartfront` / `<image>`).
     - Contraportada (`boxartback`).
     - Foto en mesa o componentes (`gameplay` / `creative`).
   - Optimización en memoria con SkiaSharp y subida a R2 vía `IImageStorageService`:
     - `games/{bggId}/cover.webp` (+ `cover_thumb.webp`)
     - `games/{bggId}/back.webp`
     - `games/{bggId}/table.webp`
4. **Síntesis IA en Lotes con Gemini Flash:**
   - Envío agrupado de 5 a 10 juegos por petición JSON estructurada con schema typing.
   - Rendimiento maximizado sobre el cupo gratuito de 1.500 llamadas/día (hasta 7.500–15.000 juegos en 1 o 2 días).
   - Detección de código HTTP 429 / `RESOURCE_EXHAUSTED` para pausar la fase de IA y marcar los ítems como `QuotaExceeded` para reanudación al día siguiente sin errores no controlados.
5. **Reingeniería del HostedService Nocturno:**
   - Orquestador integral `NightlyCatalogingService`:
     - Fase 1: Extracción de títulos de publicaciones editoriales semanales (`INewsGameExtractor`).
     - Fase 2: Cola prioritaria de solicitudes manuales de usuarios (`PendingBggImport`).
     - Fase 3: Drenaje progresivo de staging (Thing XML, imágenes GeekDo a R2, síntesis IA en lotes y promoción a catálogo).
     - Fase 4: Incorporación periódica de nuevos lanzamientos mundiales de BGG.
6. **Monitorización en la Web y Galería Comunitaria:**
   - Métricas y visibilidad del avance de la ingesta en el panel de administración (`/admin/cola-catalogacion`) con botón de drenaje manual.
   - Galería visual en `GameDetail.razor` mostrando contraportada y fotografía en mesa.
