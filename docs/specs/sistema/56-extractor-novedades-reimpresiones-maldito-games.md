# 56. Extractor Determinista de Novedades y Reimpresiones de Maldito Games

> **Estado:** Implementado, Verificado y Desplegado en Producción (INC-128)  
> **Incremento SDD:** [`inc-128-extractor-novedades-maldito`](../increments/archive/inc-128-extractor-novedades-maldito.md)  
> **Componentes Afectados:** `MalditoReleasesExtractor.cs`, `EditorialReleasesSyncService.cs`, `SqliteSchemaMigrator.cs`, `/novedades`  
> **Tests:** 2.652 pruebas unitarias pasando al 100% (incluye cobertura de parsing multisección, fechas en español, EAN desde CDN y cruce heurístico de títulos comerciales).

---

## 1. Propósito y Diagnóstico Arquitectónico

En la sincronización periódica de novedades editoriales oficiales de Ludeka (`EditorialReleasesSyncService`), Devir Iberia extraía correctamente sus lanzamientos, pero la tienda de Maldito Games (`tienda.malditogames.com`) no devolvía ningún elemento asociado en la base de datos ni en la cartelera de `/novedades`.

La investigación empírica sobre el DOM real de Magento en `tienda.malditogames.com` demostró que:
1. **La portada de Maldito Games organiza los juegos en 4 secciones editoriales claras**:
   - `Últimas novedades`: productos recién salidos con stock y precio.
   - `A puntito de llegar`: preventas inmediatas con fecha estimada precisa en español (`d 'de' MMMM`).
   - `Volverán a estar disponibles en breve`: reimpresiones oficiales confirmadas con fecha y precio.
   - `Lo que se viene`: galería visual de próximos proyectos con fecha por año (ej. `2027`).
2. **El extractor anterior ignoraba las 3 primeras secciones** y solo buscaba banners en `Lo que se viene`, mientras que la URL del catálogo usaba orden ascendente (traía juegos de 2016).
3. **Las imágenes de producto en el CDN de Magento incorporan el código EAN oficial de 13 dígitos**:
   En URLs del tipo `https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578818099-1200-face3d.jpg`, el prefijo de 13 dígitos numéricos es exactamente el EAN del juego.
4. **Al descartarse títulos por no coincidencia exacta en BGG** (por entidades HTML como `&amp;` o sufijos de edición como `Edición Kickstarter`), todos los ítems de Maldito eran eliminados por la regla de negocio de vinculación obligatoria.

---

## 2. Arquitectura de Extracción (`MalditoReleasesExtractor`)

Ubicación: `src/Ludeka.Infrastructure/Extractors/MalditoReleasesExtractor.cs`

### 2.1 Flujo Seccional de Portada
El extractor identifica los bloques delimitados por `<h2 class="titulo-home">` y procesa su contenido diferenciando la naturaleza del bloque:
- Para bloques de producto (`Últimas novedades`, `A puntito de llegar`, `Volverán a estar disponibles en breve`):
  - Itera sobre cada elemento `<li class="product-item">`.
  - Extrae URL, título decodificado, imagen, fecha (`fecha_home`) y PVP (`price`).
  - Si la sección contiene `"Volver"` y `"disponible"`, marca `IsReprint = true` deterministamente.
- Para el bloque `"Lo que se viene"`:
  - Extrae los banners de imagen `SQ_...` e infiere título y año (`IsMonthOnly = true`).
  - Solo añade elementos si no han sido capturados previamente con ficha estructurada de producto.

### 2.2 Parseo de Fechas en Español (`ParseSpanishDate`)
Implementa un motor determinista con soporte para:
1. Formato día y mes: `"8 de octubre"`, `"15 de octubre"` -> calcula `DateOnly(year, month, day)` con `IsMonthOnly = false`.
2. Formato mes y año: `"Octubre 2026"` -> `DateOnly(year, month, 1)` con `IsMonthOnly = true`.
3. Solo año: `"2027"` -> `DateOnly(2027, 1, 1)` con `IsMonthOnly = true`.
4. Fallback con cultura `es-ES`.

### 2.3 Extracción de EAN-13 desde URL de Imagen (`ExtractEanFromImageUrl`)
Aplica expresiones regulares compiladas para extraer el prefijo de 13 dígitos numéricos desde la ruta del CDN de Magento:
`/(?<ean>\d{13})(?:-[^/]+)?\.(?:jpg|jpeg|png|webp)`
Permite emparejar lanzamientos contra el catálogo local de juegos por código de barras de manera determinista e instantánea.

### 2.4 Catálogo Reciente Ordenado Descendentemente
La URL de consulta del catálogo se actualiza a:
`https://tienda.malditogames.com/juegos?product_list_order=creation_time&product_list_dir=desc`
recuperando las altas de productos más recientes en Magento en lugar de las más antiguas de 2016.

---

## 3. Orquestación y Cruce Heurístico (`EditorialReleasesSyncService`)

Ubicación: `src/Ludeka.Application/Features/Releases/EditorialReleasesSyncService.cs`

Para resolver títulos comerciales con entidades HTML o variaciones de edición, se incorporan métodos públicos y un flujo de cruce en 4 niveles:

1. **Cruce por EAN (Prioridad Máxima):** Si el item extraído contiene EAN (por ejemplo, desde la imagen de Maldito), se busca en `barcodeIndex` (`Ean` y `AdditionalBarcodes`).
2. **Cruce por Título Exacto Normalizado:** `Normalize(item.Title)` contra `titleIndex` (`SpanishTitle` y `OriginalTitle`).
3. **Cruce por Título Comercial Limpio (`CleanCommercialTitle`):**
   Retira sufijos comerciales frecuentes (`Edición Kickstarter`, `Edición Esencial`, `Edición Almirante con pintado Wash`, etc.).
4. **Cruce por Título Base (`ExtractBaseTitle`):**
   Extrae la parte principal anterior al guión o dos puntos (`Castle Combo - ¡Fuera de la mazmorra!` -> `Castle Combo`).
5. **Fallback BGG Escalonado:**
   Si el juego no existe en local, se consulta BGG iterativamente con `item.Title`, `cleanedTitle` y `baseTitle`, logrando la importación automática de títulos como `Viticulture`, `Speakeasy` o `Castle Combo`.

---

## 4. Persistencia y Compatibilidad SQLite (`SqliteSchemaMigrator`)

Ubicación: `src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs`

Se asegura que la columna `IsMonthOnly` esté presente en la tabla `WeeklyReleases` en entornos SQLite locales mediante comprobación idempotente de columnas (`PRAGMA table_info`), previniendo errores en desarrollo local.

---

## 5. Pruebas y Cobertura Automática

- **`MalditoReleasesExtractorTests`:**
  - Extracción seccional de portada (`Últimas novedades`, `A puntito de llegar`, `Volverán a estar disponibles en breve`, `Lo que se viene`).
  - Extracción determinista de EAN de 13 dígitos desde URLs del CDN.
  - Parseo robusto de fechas en castellano.
  - Decodificación y limpieza de entidades HTML.
- **`EditorialReleasesSyncServiceTests`:**
  - Cruce de novedades de Maldito Games con EAN extraído de imagen.
  - Vinculación exitosa por título comercial limpio (`CleanCommercialTitle`).
  - Vinculación exitosa por título base (`ExtractBaseTitle`).
  - Pruebas unitarias de purga de huérfanos e idempotencia.
- **Total Suite:** 2.652 pruebas unitarias superadas al 100%.
