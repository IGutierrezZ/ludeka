# Especificación: url-express-ingestion

Capacidad de captura y extracción automatizada y asistida desde URLs públicas (Instagram, YouTube, Web) sin costes de APIs de terceros (descartando Apify).

---

## 1. Requerimientos Funcionales

### R1.1: Extracción de Metadatos Públicos (OpenGraph & oEmbed)
- El sistema debe aceptar URLs válidas de Instagram (posts, reels), YouTube (vídeos estándar, shorts) y sitios web genéricos de editoriales/eventos.
- Sin emplear proxies de pago ni servicios de scraping como Apify, el sistema intentará resolver metadatos mediante:
  1. Enlaces directos oEmbed (`https://www.youtube.com/oembed?url=...`).
  2. Parseo HTML de cabeceras OpenGraph (`<meta property="og:title">`, `og:description`, `og:image`).
  3. Miniatura nativa directa para vídeos de YouTube (`https://img.youtube.com/vi/{id}/hqdefault.jpg`).
- Si la extracción de texto falla o la plataforma bloquea la lectura directa sin login (caso frecuente en Instagram), el sistema debe permitir al usuario pegar el texto/caption copiado manualmente en el modal exprés.

### R1.2: Clasificación y Extracción Semántica con Google Gemini Flash
- El texto obtenido (o introducido) se analiza mediante un prompt estructurado contra Google Gemini Flash (usando `GeminiOptions` y fallback heurístico local para entornos de prueba sin ApiKey).
- La IA debe devolver un esquema fuertemente tipado que determine:
  - `DetectedType`: `Giveaway` (Sorteo), `WeeklyRelease` (Novedad), `BoardGameEvent` (Evento), `MediaItem` (Vídeo/Tutorial/Reseña).
  - `Title`: Título conciso en español.
  - `OrganizerOrAuthor`: Organizador, canal o editorial responsable.
  - `Collaborator`: Colaboradores o cuentas mencionadas (si procede).
  - `GameTitle`: Título del juego de mesa objeto de la publicación.
  - `EventOrReleaseDate`: Fecha límite de participación (para sorteos), fecha de lanzamiento (para novedades) o fecha de inicio (para eventos).
  - `EventEndDate`: Fecha de conclusión del evento (si abarca varios días).
  - `Location`: Lugar físico de celebración (ciudad/recinto) si es un evento.
  - `EstimatedPvp`: Precio estimado si se menciona en una novedad comercial.
  - `MediaCategory`: Clasificación como `Tutorial`, `Playthrough` (partida) o `Review` (opinión).
  - `PlayerCountBadge`: Badge sugerido (ej. "Partida a 2", "En solitario").

### R1.3: Procesamiento Gráfico a Cloudflare R2 con SkiaSharp
- La imagen o miniatura detectada debe descargarse en memoria, optimizarse a formato WebP (máximo 1000px, 82% calidad) y subirse al almacenamiento R2 mediante `IImageStorageService` bajo la clave `social-inbox/{id}/thumbnail.webp`.
- Si la descarga de imagen no prospera, se conservará la URL original como respaldo o se asignará un placeholder editorial.

### R1.4: Modo Manual Avanzado (Vídeos y Posts sin Texto)
- Si una publicación es un vídeo o reel sin descripción en el pie, o el moderador prefiere registrarlo directamente:
  - El moderador indica: URL + Tipo + Juego del catálogo + Categoría y badge opcional.
  - El sistema no invoca análisis semántico de texto sobre la nada; únicamente resuelve la miniatura/fotograma, la optimiza a R2 y genera el borrador en la bandeja con los datos introducidos.

---

## 2. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Extracción automática exitosa de un sorteo de Instagram
  Dado que el moderador introduce la URL "https://www.instagram.com/p/Cxyz123/"
  Y el texto de la publicación indica "¡Gran sorteo de Wingspan con Maldito Games! Participa hasta el 25 de Octubre"
  Cuando se invoca el alta exprés
  Entonces el ítem se clasifica como "Giveaway"
  Y el organizador se detecta como "Maldito Games"
  Y el juego sugerido es "Wingspan"
  Y la fecha límite se fija en 25 de Octubre
  Y la miniatura queda optimizada en R2

Escenario: Fallo de scraping web con caption pegado a mano
  Dado que la URL de Instagram no permite lectura de HTML por restricción de login
  Cuando el moderador pega manualmente el texto del sorteo en el campo de descripción
  Entonces el sistema ejecuta la extracción con Gemini sobre dicho texto
  Y genera el ítem con los campos estructurados en la bandeja de moderación

Escenario: Modo manual avanzado para un tutorial de YouTube
  Dado un vídeo de YouTube "https://www.youtube.com/watch?v=abc123xyz"
  Cuando el moderador selecciona "Modo Manual Avanzado", elige el juego "Ark Nova" y tipo "MediaItem (Tutorial)"
  Entonces el sistema extrae la carátula de YouTube "https://img.youtube.com/vi/abc123xyz/hqdefault.jpg"
  Y crea el ítem en la bandeja vinculado a "Ark Nova" sin invocar extracción de texto innecesaria
```
