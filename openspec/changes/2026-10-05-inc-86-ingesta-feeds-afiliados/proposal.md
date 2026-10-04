# Propuesta: Ingesta de Feeds Comerciales, Auto-asignación de EAN y Panel de Afiliados

## Motivación y Contexto
Tras la implementación de los identificadores EAN-13 en la entidad `Game` y el enrutador de afiliación `/r/` (INC-86 Fases 0 y 1), junto con el barrido de versiones de BGG (INC-105), el catálogo de Ludeka cuenta con la infraestructura base de códigos de barras.
Sin embargo, actualmente:
1. La actualización de precios y disponibilidad (`InStock`) de las tiendas se realiza manualmente o mediante scraping esporádico con alto riesgo de bloqueo.
2. Los datos de EAN provenientes de BGG contienen a menudo códigos UPC-A de ediciones estadounidenses o están incompletos.
3. Las tiendas comerciales nacionales (Zacatrus, Dungeon Marvels, Mathom, etc.) disponen de feeds estructurados (Google Merchant / Shopping XML y CSV) con el inventario real, precios actualizados y el EAN-13 físico de la edición española.

Esta fase implementa la ingesta desatendida de feeds comerciales, el cruce y auto-asignación de EAN, la detección de discrepancias y el panel de administración de fuentes de afiliados.

## Alcance Propuesto
1. **Entidad y Persistencia de Fuentes de Feeds (`AffiliateFeedSource`):**
   - Configuración en base de datos de URLs de feeds, formato (`GoogleShoppingXml`, `GenericCsv`), intervalo y estado.
   - Entidad para registrar discrepancias de EAN (`AffiliateEanDiscrepancyLog`).
2. **Motor de Ingesta en Streaming (`IFeedParser`):**
   - Lectura eficiente con `XmlReader` / `StreamReader` de bajo consumo de memoria.
   - Normalización de EAN-13 con `BarcodeValidator`.
3. **Servicio de Sincronización y Cruce (`CatalogFeedSyncService`):**
   - Cruce determinista por EAN existente contra `Game`.
   - Auto-asignación de EAN si el juego no tiene código y coincide de forma unívoca por título/slug en español.
   - Detección de discrepancias: si el feed trae un EAN distinto al registrado (origen BGG), se añade a `AdditionalBarcodes` para no perder la búsqueda comercial y se genera un registro en la cola de discrepancias para revisión editorial.
   - Actualización atómica de `GamePurchaseLink` en la ficha del juego.
4. **Runner Desatendido (`CatalogFeedSyncJobRunner` en `Ludeka.Jobs`):**
   - Ejecutable por Cloud Run Jobs con protección por concesiones distribuidas (`IJobExecutionCoordinator`).
5. **Panel de Gestión de Afiliados (`/admin/afiliados`):**
   - Gestión de fuentes de catálogo, botón de sincronización manual y resolución de discrepancias EAN con un clic.
