# Incremento 83: Integración de Favicon Oficial e Iconos de Marca para el Navegador, Dispositivos Móviles y PWA

> **ID:** INC-83  
> **Slug:** `favicon-icono-ludeka`  
> **Rama:** `inc/favicon-icono-ludeka`  
> **Estado:** ✅ Archivado  
> **Módulos Impactados:** Módulo 09 (`docs/specs/sistema/09-arquitectura-y-despliegue.md`), `src/Ludeka.Web/Components/App.razor`, `src/Ludeka.Web/wwwroot/`  
> **Dependencias:** INC-16 (PWA Base), PR #146.

---

## 1. Contexto y Diagnóstico

1. **Ausencia de Favicon y Enlaces de Icono en el Navegador:**
   - La aplicación Blazor carecía por completo de etiquetas `<link rel="icon">` en el `<head>` de `App.razor`.
   - No existía ningún archivo `favicon.ico`, `favicon.svg` ni miniaturas PNG en la raíz de `wwwroot`.
   - Como resultado, cualquier navegador web (Chrome, Edge, Firefox, Safari) mostraba el icono genérico por defecto (globo terráqueo o documento en blanco) al abrir Ludeka, y las solicitudes automáticas a `/favicon.ico` devolvían código HTTP 404.
2. **Inconsistencias y Proporciones en Activos PWA:**
   - En `wwwroot/icons/`, los archivos `icon-192.png` y `icon-512.png` eran copias no cuadradas (138×150 px) del logotipo oscuro con fondo transparente, violando el estándar de iconos de la especificación W3C Web App Manifest.
   - En modo claro del navegador o fondos de escritorio claros, el isotipo oscuro sin fondo contrastado carecía de legibilidad.
3. **Falta de Iconos de Contacto Táctil (Apple Touch Icon):**
   - No existía `apple-touch-icon.png` (180×180 px) con fondo opaco en la raíz de `wwwroot`, provocando que al guardar la aplicación en la pantalla de inicio en iOS o iPadOS se generase una captura por defecto con fondo negro o distorsionada.

---

## 2. Objetivos y Solución Implementada

1. **Generación de Activos Oficiales de Marca:**
   - Creación de un *squircle badge* con el fondo de marca Charcoal (`#18181b`), borde de acento en ámbar cálido (`#d97706`) y el isotipo oficial de Ludeka (hexágono cian con las siglas «LDK» en blanco y el dado isométrico 3D en su interior), garantizando máxima legibilidad y contraste tanto en pestañas oscuras como claras.
   - Generación de `favicon.ico` multi-resolución conteniendo flujos PNG de 16×16, 32×32 y 48×48 px.
   - Generación de `favicon.svg` escalable para navegadores modernos.
   - Generación de `favicon-16x16.png`, `favicon-32x32.png`, `favicon-48x48.png` y `apple-touch-icon.png` (180×180 px).
   - Generación de iconos PWA cuadrados `icon-192.png` (192×192 px), `icon-512.png` (512×512 px) e `icon-maskable.png` (512×512 px con margen de seguridad adaptativo).
2. **Cableado en la Arquitectura Web:**
   - Declaración exhaustiva de los metadatos y enlaces de iconos en el `<head>` de `src/Ludeka.Web/Components/App.razor`.
   - Incorporación de los nuevos activos en la lista `PRECACHE_ASSETS` de `service-worker.js` para su disponibilidad offline sin fallos en modo PWA.
   - Actualización de `manifest.webmanifest` con los tamaños y variantes adaptativas.
3. **Aislamiento de la Prueba de Exportación de Semillas:**
   - Corrección en `SeedGamesExporter.cs` para emplear un archivo temporal durante las aserciones de serialización, evitando la mutación accidental y destructiva del archivo de catálogo `seed-games.json` durante las suites de test.
4. **Verificación Automática:**
   - Creación de `FaviconAndBrandIconsContractTests.cs` (12 pruebas unitarias) validando la presencia, tamaño no nulo y declaración HTML/manifest/service-worker de todos los iconos.

---

## 3. Criterios de Aceptación y Resultados (TDD)

- **Criterio 1:** El navegador muestra el icono oficial de Ludeka en la pestaña sin recurrir al icono genérico. (Verificado con `AppRazor_ContainsFaviconAndTouchIconLinks`).
- **Criterio 2:** `favicon.ico`, `favicon.svg`, `favicon-32x32.png` y `apple-touch-icon.png` existen en `wwwroot` y son no vacíos. (Verificado con `Wwwroot_ContainsOfficialBrandIcons`).
- **Criterio 3:** `service-worker.js` y `manifest.webmanifest` incluyen los activos de favicon e iconos PWA cuadrados y maskable. (Verificado).
- **Criterio 4:** Suite unitaria completa pasando al 100% (2.108 pruebas superadas, 0 fallos).
