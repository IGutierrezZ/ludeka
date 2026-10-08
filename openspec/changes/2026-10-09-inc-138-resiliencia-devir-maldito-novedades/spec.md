# Especificación: INC-137 — Resiliencia en Extracción de Novedades (Devir y Maldito Games) y Despliegue de Jobs

## 1. Requisitos Funcionales

- **REQ-01 (Sanitización de Cabeceras HTTP de Navegador)**:
  - Las peticiones HTTP emitidas hacia `devir.es` y `tienda.malditogames.com` deben incluir cabeceras de navegación estándar que emulen navegación humana (`Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8`, `Accept-Language: es-ES,es;q=0.9,en;q=0.8`, `Sec-Ch-Ua`, `Sec-Fetch-Dest: document`, `Sec-Fetch-Mode: navigate`).
  - No debe existir duplicidad ni concatenación de cabeceras `User-Agent`. Se debe emplear un único identificador consistente.
  - Para páginas de paginación o navegación interna (p. ej. `p=2`), se debe inyectar la cabecera `Referer` adecuada (`https://devir.es/catalogo/juegos-de-mesa` o `https://tienda.malditogames.com/`).

- **REQ-02 (Reintento Comedido en Paginación Devir)**:
  - En `DevirReleasesExtractor.ExtractCatalogPageAsync`, si una respuesta HTTP devuelve un código de estado fallido (particularmente 403 Forbidden, 429 Too Many Requests o errores 5xx transitorios), el extractor debe realizar **a lo sumo 1 reintento** tras una pausa de 1.500 ms.
  - El DTO resultante `DevirCatalogPageResultDto` debe indicar explícitamente si la página fue obtenida con éxito (`Success == true`), para distinguir una página vacía por finalización de catálogo de un fallo por rechazo del servidor.

- **REQ-03 (Tolerancia y Throttle en DevirImagesBackfillJobRunner)**:
  - Entre peticiones consecutivas de páginas del catálogo de Devir, el runner debe aplicar un retardo cortés de 750 ms a 1.000 ms para mitigar la activación de reglas WAF por ráfagas.
  - El runner no debe abortar de inmediato el recorrido total ante un único fallo de página si esta responde con error no recuperable; debe registrar la incidencia y permitir hasta 2 fallos consecutivos antes de interrumpir la ejecución.

- **REQ-04 (Desacoplo y Aislamiento en Extractor de Maldito Games)**:
  - `MalditoReleasesExtractor.ExtractReleasesAsync` debe solicitar la portada (`DefaultMalditoHomeUrl`) y el catálogo (`DefaultMalditoCatalogUrl`) de forma aislada e independiente.
  - Si la consulta de catálogo falla o excede el timeout, los elementos de la portada deben parsearse y devolverse intactos sin interrumpir la extracción.
  - Si una petición devuelve 403, 429 o fallo transitorio, se debe aplicar a lo sumo 1 reintento comedido tras 1.500 ms con cabeceras de navegación apropiadas.

- **REQ-05 (Garantía de Moderación en EditorialReleasesSyncService)**:
  - Todo elemento extraído de Maldito Games o Devir que no coincida con un juego del catálogo local debe persistirse en la entidad `WeeklyRelease` con estado `WeeklyReleaseStatus.PendingModeration`, asociando la sugerencia generada por el asistente IA si está disponible o la justificación por defecto si no lo está.

- **REQ-06 (Despliegue Continuo de DevirImagesBackfill)**:
  - El trabajo `devir-images-backfill` debe estar registrado en el bucle de despliegue de Cloud Run Jobs en `.github/workflows/ci-cd.yml` junto con el resto de jobs de la plataforma.

## 2. Invariantes del Sistema

- **INV-01**: Cero atribución de IA en commits.
- **INV-02**: Todas las novedades ya moderadas o rechazadas previamente deben conservar su estado histórico.
- **INV-03**: No realizar reintentos infinitos ni bucles pesados contra Cloudflare que puedan provocar un baneo de IP permanente del cluster Cloud Run.
