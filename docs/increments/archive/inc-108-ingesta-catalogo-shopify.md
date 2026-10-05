# Incremento 108: Ingesta de Catálogos de Tiendas Shopify (/products.json) y Captura de Precios y EANs

> **ID:** INC-108  
> **Slug:** `ingesta-catalogo-shopify`  
> **Rama:** `inc/ingesta-catalogo-shopify`  
> **Estado:** ✅ Archivado  
> **Módulos Impactados:** Módulo 01 (`docs/specs/sistema/01-catalogo-y-fichas.md`), Módulo 25 (`docs/specs/sistema/25-motor-afiliados-y-atribucion-comunitaria.md`), `src/Ludeka.Core/Entities/AffiliateFeedSource.cs`, `src/Ludeka.Application/Features/Affiliates/`, `src/Ludeka.Web/Components/Pages/AffiliatesAdmin.razor`  
> **Dependencias:** INC-86 (Feeds Catálogo Afiliados y Cruce EAN)  
> **Pruebas Automatizadas:** 2.416 pruebas unitarias + 10 de integración en verde (100% de éxito).

---

## 1. Contexto y Objetivos

Tras la entrega de INC-86 con el motor de ingesta de Google Shopping XML y el panel administrativo en `/admin/afiliados`, se requiere alimentar el radar de precios y el cruce de códigos de barras EAN-13 con tiendas reales de España sin depender de acuerdos manuales inmediatos ni scraping agresivo.

La auditoría técnica del comercio electrónico nacional ha revelado que varias tiendas de referencia del sector operan sobre **Shopify** y exponen de forma pública su catálogo paginado en JSON (`/products.json?limit=250&page=N`):
1. **Cuarto de Juegos** (`cuartodejuegos.es`, Madrid).
2. **Ludus Belli** (`ludusbelli.com`, Móstoles / Madrid).
3. **Mi Juego Bonito** (`mijuegobonito.com`, España).

### Objetivos:
1. Añadir el formato `FeedFormat.ShopifyJson` al modelo de dominio y al panel `/admin/afiliados`.
2. Crear `ShopifyJsonCatalogParser` para transformar los productos de Shopify en `AffiliateFeedItem` extrayendo título, precio, stock (`available`), enlace directo (`/products/{handle}`) y EAN-13 (desde `sku`, `barcode` o URL de imagen).
3. Integrar la lectura paginada en `CatalogFeedSyncService` conectada al flujo de cruce y registro de precios en `PriceRadarService`.
4. Sembrar las fuentes iniciales de las 3 tiendas para alimentar el catálogo.
