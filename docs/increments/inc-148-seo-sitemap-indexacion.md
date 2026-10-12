# INC-148: Infraestructura SEO, Sitemap XML Dinámico, Robots.txt, Marcado Semántico JSON-LD y Verificación Google Search Console

## 1. Contexto y Problema Detectado

Al realizar una auditoría de presencia e indexación en motores de búsqueda (`site:ludeka.es`), Google no devuelve ningún resultado indexado. La investigación técnica sobre la web en producción (`https://ludeka.es`) y el código del proyecto ha confirmado tres causas directas:

1. **Ausencia de `robots.txt` y `sitemap.xml` (HTTP 404):**
   - Actualmente tanto `https://ludeka.es/robots.txt` como `https://ludeka.es/sitemap.xml` devuelven un código de estado 404.
   - Sin un archivo `sitemap.xml`, los rastreadores de Google carecen de un mapa estructurado de URLs para descubrir el catálogo de más de 18.000 juegos y los directorios de editoriales, tiendas y creadores.
2. **Carencia de Metadatos y Datos Estructurados JSON-LD:**
   - Las páginas no declaran metaetiquetas `description`, enlaces canónicos (`rel="canonical"`) ni etiquetas Open Graph / Twitter Card completas.
   - Las fichas de juego carecen de marcado semántico Schema.org (`BoardGame` y `FAQPage`). Sin este marcado, los motores no pueden interpretar las propiedades clave (número de jugadores, duración recomendada, edad, autores) para alimentar los paneles de preguntas habituales («People Also Ask») y los resultados enriquecidos (*rich snippets*).
3. **Falta de Verificación de Propiedad en Google Search Console:**
   - El dominio no ha sido validado ante Google para notificar activamente la existencia del sitio y enviar el sitemap oficial.

---

## 2. Objetivos del Incremento

1. **Verificación de Google Search Console en `App.razor`:**
   - Incorporar en el `<head>` de `App.razor` la metaetiqueta de verificación oficial proporcionada:
     `<meta name="google-site-verification" content="wr4JpkGdk8SvvoxnHOeGBuT-OvzoEmngGbKCzT-PnCU" />`.
   - Incorporar metadatos generales predeterminados (meta descripción global de Ludeka, `og:site_name`, `og:locale`).

2. **Archivo Canónico `robots.txt` (`wwwroot/robots.txt`):**
   - Permitir el acceso general a los motores de búsqueda (`Allow: /`).
   - Excluir rutas privadas o administrativas (`Disallow: /admin`, `Disallow: /cuenta/`, `Disallow: /login`).
   - Declarar la URL canónica del sitemap: `Sitemap: https://ludeka.es/sitemap.xml`.

3. **Generador y Endpoint Dinámico de `sitemap.xml` (`/sitemap.xml`):**
   - Implementar un servicio y endpoint HTTP minimal API `GET /sitemap.xml` con `Content-Type: application/xml; charset=utf-8` y caché controlada.
   - Incluir las rutas estáticas y editoriales prioritarias:
     - `/` (prioridad 1.0, cambio diario)
     - `/catalogo` (prioridad 0.9, cambio diario)
     - `/novedades`, `/sorteos`, `/eventos` (prioridad 0.8, cambio diario/semanal)
     - `/editoriales`, `/creadores`, `/tiendas`, `/clasificaciones` (prioridad 0.7, cambio semanal)
     - `/transparencia` (prioridad 0.5, cambio mensual)
   - Incluir dinámicamente todas las URLs de las fichas de juego activas (`/juegos/{slug}`) y de directorios (`/editoriales/{slug}`, `/creadores/{slug}`, `/tiendas/{slug}`).

4. **Metadatos SEO y Datos Estructurados JSON-LD en `GameDetail.razor`:**
   - Generar dinámicamente en el `<HeadContent>`:
     - Metaetiqueta `description` con información editorial concisa (título, año, jugadores, duración, diseñador y editorial).
     - Enlace canónico `<link rel="canonical" href="https://ludeka.es/juegos/{slug}" />`.
     - Etiquetas Open Graph y Twitter Cards (`og:title`, `og:description`, `og:image`, `og:url`, `twitter:card`).
     - Bloque `<script type="application/ld+json">` con especificación Schema.org de doble entidad:
       - `@type: BoardGame`: nombre, nombres alternativos, imagen, min/max jugadores, edad mínima, duración aproximada, autor, editorial.
       - `@type: FAQPage`: preguntas y respuestas estructuradas que capturan búsquedas frecuentes (número de jugadores recomendado, tiempo de partida, edad sugerida y mecánica/diseño).

5. **Pruebas Automatizadas Unitarias y de Integración:**
   - Batería de pruebas en `Ludeka.UnitTests` que verifiquen:
     - Formato XML estricto y conforme al estándar Sitemaps 0.9.
     - Presencia de rutas estáticas y dinámicas en el sitemap.
     - Generación correcta del marcado JSON-LD (`BoardGame` y `FAQPage`) y de metaetiquetas en fichas.
     - Disponibilidad y formato de `robots.txt`.

---

## 3. Plan de Tareas (ODD)

- [x] **Tarea 1 (Verificación y Robots):** Añadir la etiqueta de Google Search Console en `App.razor` y crear `src/Ludeka.Web/wwwroot/robots.txt`.
- [x] **Tarea 2 (Servicio y Endpoint de Sitemap XML):** Diseñar `ISitemapService` / `SitemapService` y exponer `GET /sitemap.xml` con cabeceras de caché XML.
- [x] **Tarea 3 (Metadatos y Marcado Semántico JSON-LD en Ficha):** Incorporar `<HeadContent>` en `GameDetail.razor` con Open Graph, canonical, `Schema.org/BoardGame` y `FAQPage`.
- [x] **Tarea 4 (Pruebas Unitarias de Regresión y Contrato):** Desarrollar la suite de tests `SitemapServiceTests` y `SeoMetadataTests`.
- [x] **Tarea 5 (Verificación de Suite Completa):** Ejecutar la suite de pruebas del proyecto acreditando cero regresiones (2.884 pruebas unitarias en verde).
- [ ] **Tarea 6 (Cierre, PR y Despliegue):** Abrir PR con `scripts/sdd-worktree.ps1 pr seo-sitemap-indexacion`, esperar CI en verde, mergear y verificar despliegue en Google Cloud Run.
