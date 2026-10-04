# Incremento 105: Ingesta de Versiones BGG (versions=1), Persistencia de Snapshots y Barrido Determinista de Títulos en Español y EAN

> **ID:** INC-105  
> **Slug:** `bgg-versiones-titulos-ean`  
> **Rama:** `inc/bgg-versiones-titulos-ean`  
> **Estado:** ✅ Archivado  
> **Módulos Impactados:** Módulo 01 (`docs/specs/sistema/01-catalogo-y-fichas.md`), Módulo 47 (`docs/specs/sistema/47-snapshots-crudos-bgg-expansiones-sincronizacion.md`), `src/Ludeka.Core/`, `src/Ludeka.Application/`, `src/Ludeka.Infrastructure/`, `src/Ludeka.Jobs/`, `src/Ludeka.Web/`  
> **Dependencias:** INC-90 (BGG Raw Snapshots), INC-97 (Peticiones en bloque BGG Batch Fetch), INC-98 (Persistencia automática de snapshots), INC-100 (Reconciliación de expansiones)

---

## 1. Contexto y Diagnóstico

1. **Pérdida sistemática de títulos en español:** El parser de BGG (`ExtractSpanishTitle` en `BggXmlParser.cs`) buscaba únicamente títulos alternativos que contuvieran explícitamente palabras como `"español"`, `"spanish"` o `"castellano"` (para limpiar coletillas como `"(Edición en español)"` presentes en datasets de prueba). En la API real de BGG (`/xmlapi2/thing`), BGG nunca incluye tales sufijos ni atributos de idioma en `<name type="alternate">`, por lo que juegos míticos traducidos como *Alta Tensión* quedaban etiquetados como *Power Grid*.
2. **Imposibilidad de inferencia ciega desde snapshots estándar:** Los snapshots crudos actuales solo almacenan el payload de `/thing?id=...&stats=1`. Al no existir atributo de idioma en `<name>`, un algoritmo determinista no puede saber sin contexto externo si el título español es `"Alta Tensión"`, `"Funkenschlag"` o `"Vysoké napětí"`.
3. **Oportunidad 2x1 con versiones BGG (`versions=1`):** Al consultar `/xmlapi2/thing?id=...&versions=1`, BGG entrega el desglose de ediciones físicas (`<item type="boardgameversion">`), donde la edición española asocia formalmente `<link type="language" value="Spanish" />`, su editorial local (ej. Edge Entertainment / Devir) y, en numerosos casos, el código de barras comercial EAN-13 / UPC en `<barcode>` o `<productcode>`.
4. **Desacoplo mediante tabla satélite:** Almacenar íntegramente el bloque de versiones en la tabla satélite `BggRawSnapshots` permite que cualquier barrido posterior del catálogo se realice en local sin realizar llamadas HTTP externas repetitivas.

---

## 2. Objetivos y Solución Técnica

1. **Cliente BGG con soporte de versiones (`versions=1`):**
   - Ampliar `IBggClient`, `BggXmlApiClient` y `SimulatedBggClient` para soportar la solicitud de versiones (`includeVersions: true` o endpoint equivalente) por lotes de hasta 20 IDs.
   - Preservar la auto-persistencia en `BggRawSnapshots`, de modo que el payload JSON estructurado contenga el nodo `"versions"`.
2. **Parser Analítico Puro (`BggRawSnapshotParser`):**
   - Método `HasVersions(string rawJson)` para detectar si el snapshot contiene versiones.
   - Método `ExtractSpanishVersionInfo(string rawJson)` para extraer deterministamente:
     - `SpanishTitle`: Título oficial de la edición en español.
     - `SpanishPublisher`: Editorial de la edición española.
     - `YearPublished`: Año de la edición local.
     - `Ean`: Código de barras validado / normalizado (EAN-13 / UPC).
3. **Modelo de Dominio (`Game.cs` y `BarcodeValidator.cs`):**
   - Soporte de propiedad `Ean` (EAN-13 normalizado) y métodos `UpdateEan` y `UpdateSpanishTitle` con sincronización de `LocalizedTitles`.
   - `BarcodeValidator` puro en `Ludeka.Core.ValueObjects` para validar y normalizar códigos de barras comerciales.
4. **Servicio de Sincronización y Barrido (`BggRawSnapshotSyncService`):**
   - `SyncVersionsBatchAsync`: Descarga y actualiza snapshots con versiones para los títulos que carecen de ellas.
   - `SweepCatalogFromVersionsAsync`: Recorre los snapshots locales con versiones y actualiza `SpanishTitle`, `SpanishPublisher`, `Ean` y `LocalizedTitles` en el catálogo caliente sin tocar la red.
5. **Consola de Administración y Runner Autónomo:**
   - Visualización del estado de versiones en `CatalogQueueAdmin.razor` y acción interactiva de barrido.
   - Runner `BggVersionsSweepJobRunner` en `Ludeka.Jobs` para ejecución desatendida.

---

## 3. Plan de Trabajo (Work Units)

- [x] **WU 01: Dominio y ValueObjects:** `BarcodeValidator`, propiedad `Ean` en `Game.cs` y métodos de actualización de títulos/EAN. Pruebas unitarias de dominio.
- [x] **WU 02: Parser analítico de versiones:** `ExtractSpanishVersionInfo` y `HasVersions` en `BggRawSnapshotParser`. Pruebas con fixtures de BGG reales y simulados.
- [x] **WU 03: Infraestructura de cliente BGG:** Parámetro `includeVersions` en `BggXmlApiClient` y `SimulatedBggClient`, auto-persistencia en `BggRawSnapshots`. Pruebas de integración/cliente.
- [x] **WU 04: Orquestación de sincronización y barrido:** `SyncVersionsBatchAsync` y `SweepCatalogFromVersionsAsync` en `BggRawSnapshotSyncService`. Pruebas de aplicación.
- [x] **WU 05: Runner en Ludeka.Jobs y panel en CatalogQueueAdmin:** Exposición interactiva y telemetría de barrido. Pruebas de runner y UI.
- [x] **WU 06: Verificación integral y cierre:** Suite completa en verde, PR, monitorización de CI/CD y cierre.
