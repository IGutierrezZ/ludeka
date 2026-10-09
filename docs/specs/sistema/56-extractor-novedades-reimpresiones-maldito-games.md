# 56. Extractor Determinista de Novedades y Reimpresiones de Maldito Games

> **Estado:** Implementado, Verificado y Desplegado en Producción (INC-128 e INC-142)  
> **Incrementos SDD:** [`inc-128-extractor-novedades-maldito`](../increments/archive/inc-128-extractor-novedades-maldito.md), [`inc-142-maldito-filtros-backfill-catalogo`](../increments/archive/inc-142-maldito-filtros-backfill-catalogo.md)  
> **Componentes Afectados:** `MalditoReleasesExtractor.cs`, `EditorialReleasesSyncService.cs`, `MalditoImagesBackfillJobRunner.cs`, `DevirImagesBackfillJobRunner.cs`, `SqliteSchemaMigrator.cs`, `/novedades`  
> **Tests:** 2.795 pruebas unitarias pasando al 100% (incluye filtros estrictos seccionales, galería de producto, validación EAN-13 módulo 10 y runners de barrido).

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

## 5. Filtros Estrictos de Novedades y Enriquecimiento de Galería (INC-142)

En INC-142 se perfeccionó la lógica de ingesta tras el análisis del ciclo de vida editorial de Maldito Games:

1. **Filtro Estricto de Secciones:**
   - **Conservadas:** Exclusivamente `A puntito de llegar` (próximos lanzamientos en preventa con fecha estimada) y `Volverán a estar disponibles en breve` (reimpresiones oficiales confirmadas).
   - **Descartadas:** `Últimas novedades` (juegos que ya están en distribución física y tiendas) y `Lo que se viene` (banners conceptuales a largo plazo sin fecha concreta ni precio).
2. **Enriquecimiento Multinivel desde Ficha de Producto:**
   - Para cada producto de las secciones válidas, se descarga su ficha en segundo plano y se parsea el script `mage/gallery/gallery`.
   - Se prioriza la imagen 3D (`*-face3d.jpg`) como portada principal de alta resolución, asignando vistas de componentes/mesa y contraportada (`*-backflat.jpg`) si existen.
   - Se extrae el código EAN-13 desde el atributo `data-product-sku` o desde la URL de las fotos del CDN (`devirinvestments.s3.eu-west-1.amazonaws.com`).
   - Se extrae el PVP oficial desde `<meta property="product:price:amount">` o selectores de precio de Magento.
3. **Runners de Barrido y Enriquecimiento de Catálogo (`maldito-images-backfill` y `devir-images-backfill`):**
   - Nuevo runner autónomo `MalditoImagesBackfillJobRunner` para recorrer la paginación del catálogo general (`/juegos?p={page}`) en local, resolviendo el bloqueo de Cloudflare en entornos Cloud Run.
   - Cruce bidireccional por EAN-13 validado (módulo 10) y por título normalizado.
   - Enriquecimiento automático de recursos multimedia (caja 3D, mesa, contraportada), código EAN faltante y alta/actualización de la oferta oficial con su PVP en `PurchaseLinks`.
   - Corrección simétrica en `DevirImagesBackfillJobRunner` para no omitir juegos con fotos existentes si les falta EAN o la oferta oficial de Devir.

---

## 6. Pruebas y Cobertura Automática

- **`MalditoReleasesExtractorTests`:**
  - Filtro estricto: confirmación de inclusión de preventas/reimpresiones y exclusión de novedades y proyectos futuros.
  - Extracción de galería JSON Magento (`mage/gallery/gallery`), imágenes 3D, mesa y contraportada.
  - Parseo de PVP y EAN desde SKU o CDN.
  - Paginación del catálogo general y detección de página siguiente con regex.
- **`MalditoImagesBackfillJobRunnerTests`:**
  - Emparejamiento por EAN y por título normalizado.
  - Asignación de recursos multimedia y oferta oficial de tienda con PVP.
  - Tolerancia a fallos transitorios en páginas individuales.
  - Validación matemática estricta de códigos de barras comerciales mediante `BarcodeValidator.TryNormalizeEan13`.
- **`DevirImagesBackfillJobRunnerTests`:**
  - Verificación de no omisión cuando falta EAN o la oferta de compra con PVP oficial.
- **Total Suite:** 2.795 pruebas unitarias superadas al 100%.
