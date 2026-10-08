# Incremento 128: Extractor Determinista de Novedades y Reimpresiones de Maldito Games

- **ID del Incremento:** `INC-128`
- **Slug:** `maldito-releases-extractor`
- **Rama:** `inc/maldito-releases-extractor`
- **Fecha:** 2026-10-08
- **Estado:** ⏳ En revisión (PR pendiente)
- **Épica / Contexto:** Sincronización de Novedades Editoriales Oficiales (Devir y Maldito Games).

---

## 1. Descripción del Problema y Objetivos
En la sincronización de novedades editoriales oficiales (`/novedades`), Devir Iberia extrae y sincroniza correctamente sus juegos, mientras que Maldito Games no devuelve ningún resultado en la base de datos ni en la interfaz.

La causa raíz radica en tres factores:
1. **Extracción en secciones no pertinentes:** `MalditoReleasesExtractor` buscaba únicamente banners en `Lo que se viene` (con fechas lejanas `2027` y títulos deducidos del nombre del fichero) y consultaba `/juegos?product_list_order=creation_time` (que en Magento ordena ascendentemente por defecto y trae juegos de 2016). Ignoraba las secciones clave de la portada: `A puntito de llegar`, `Volverán a estar disponibles en breve` (reimpresiones oficiales) y `Últimas novedades`.
2. **Formato de fechas en español no soportado:** Las fechas de Maldito vienen en formato texto (ej. `22 de octubre`, `15 de octubre`) en `<span class="fecha_home">`. El extractor anterior solo contemplaba años de 4 dígitos.
3. **Descarte estricto por falta de cruce EAN/BGG:** Al no incluir EAN en el texto del HTML y tener títulos con entidades HTML sin decodificar (`Nemo&#039;s War`, `&amp;`) o coletillas comerciales, la búsqueda en BGG fallaba y `EditorialReleasesSyncService` descartaba todos los elementos extraídos por falta de `GameId`.

### Objetivos:
- Extraer todas las novedades y reimpresiones de la portada de Maldito Games (`A puntito de llegar`, `Volverán a estar disponibles en breve`, `Últimas novedades`).
- Parsear fechas en castellano a `DateOnly`.
- Extraer el código EAN de 13 dígitos desde la URL de la imagen del producto (`.../8436578818099-1200-face3d.jpg`) para permitir el cruce determinista directo con el catálogo y BGG.
- Decodificar entidades HTML y limpiar coletillas en la búsqueda de respaldo en BGG.
- Reparar la reconciliación SQLite en `SqliteSchemaMigrator` para la columna `IsMonthOnly`.
- Validar mediante pruebas unitarias exhaustivas.

---

## 2. Implementación Realizada

1. **`SqliteSchemaMigrator.cs`**:
   - Incorporada la migración segura de columna `IsMonthOnly` en la tabla `WeeklyReleases` para SQLite local.

2. **`MalditoReleasesExtractor.cs`**:
   - Extracción seccional por bloques: `Últimas novedades`, `A puntito de llegar`, `Volverán a estar disponibles en breve` (marcando `IsReprint = true`), y `Lo que se viene`.
   - Método `ParseSpanishDate` que reconoce `"d 'de' MMMM"`, `"MMMM yyyy"`, `"yyyy"`, con inferencia de año actual/siguiente.
   - Extracción determinista de EAN de 13 dígitos desde la URL de la imagen (`ExtractEanFromImageUrl`).
   - Decodificación de entidades HTML y colapso de espacios en blanco redundantes.
   - URL de catálogo actualizada con `product_list_dir=desc`.

3. **`EditorialReleasesSyncService.cs`**:
   - Métodos estáticos de normalización: `CleanCommercialTitle` (retira sufijos de edición) y `ExtractBaseTitle` (separa título base antes de dos puntos o guión).
   - Cruce en catálogo local priorizando EAN, título normalizado, título comercial limpio y título base.
   - Fallback multinivel en BGG (`item.Title`, `cleanedTitle`, `baseTitle`).

4. **Suite de Pruebas Unitarias**:
   - `MalditoReleasesExtractorTests`: pruebas para cada sección, formatos de fecha, EAN en URL de CDN y decodificación de entidades HTML.
   - `EditorialReleasesSyncServiceTests`: pruebas de cruce por EAN extraído, títulos comerciales con coletillas y títulos base con subtítulos.
   - 2652 pruebas unitarias superadas con 0 errores.

