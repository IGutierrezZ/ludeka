# Especificación: Galería Comunitaria GeekDo y Almacenamiento R2 (geekdo-gallery-r2)

## 1. Contexto y Requerimientos

La ficha de juego de Ludeka necesita enriquecerse con material gráfico real más allá de la portada frontal estándar. La comunidad de BGG aporta miles de fotografías de alta calidad votadas en GeekDo (`api.geekdo.com/api/images`).

### Requerimientos Funcionales
- **RF-01 (Consulta a API de GeekDo):** Consultar `https://api.geekdo.com/api/images?ajax=1&gallery=all&objectid={bggId}&objecttype=thing` de forma no autenticada y con cabeceras User-Agent respetuosas.
- **RF-02 (Selección de las 3 Mejores Fotos):**
  1. **Portada / Frontal:** Buscar imágenes clasificadas como `boxartfront` con el mayor número de votos positivos (`numpositive`). Si no hay en GeekDo, usar la imagen canónica `<image>` del XML de Thing.
  2. **Contraportada / Trasera:** Buscar imágenes clasificadas como `boxartback` o `boxback` con mayor `numpositive`.
  3. **En mesa / Componentes:** Buscar imágenes clasificadas como `gameplay`, `creative` o `components` con mayor `numpositive`.
- **RF-03 (Optimización y Variantes WebP):**
  - Cada imagen seleccionada se descarga como stream en memoria.
  - Se invoca `IImageStorageService.UploadGameImageVariantsAsync` (INC-40):
    - Portada: sube `games/{bggId}/cover.webp` (máx. 1000px) y `games/{bggId}/cover_thumb.webp` (máx. 400px).
    - Contraportada: sube `games/{bggId}/back.webp` (máx. 1000px).
    - Mesa: sube `games/{bggId}/table.webp` (máx. 1200px).
- **RF-04 (Tolerancia a Fallos y Ausencia):** Si un juego no tiene contraportada o foto en mesa en GeekDo, se almacena `null` en dicho campo y el estado de imágenes se marca como `Completed` (o `Skipped` parcial) sin interrumpir el flujo.
- **RF-05 (Modo Simulado / Offline):** El cliente de GeekDo debe contar con simulación determinista para tests unitarios y desarrollo local.

---

## 2. Criterios de Aceptación (Gherkin)

```gherkin
Característica: Selección y almacenamiento de galería comunitaria

  Escenario: Extracción exitosa de portada, trasera y mesa
    Dado un juego con BggId 224517 en staging
    Cuando se consulta la API de GeekDo Images
    Entonces se extrae la URL de portada frontal con mayor numpositive
    Y se extrae la URL de contraportada con mayor numpositive
    Y se extrae la URL de foto en mesa con mayor numpositive
    Y las 3 imágenes se optimizan a WebP y se guardan en R2 con URLs deterministas

  Escenario: Juego sin trasera comunitaria
    Dado un juego antiguo con BggId 12345 que solo tiene fotos de portada y mesa en GeekDo
    Cuando se procesa la galería de imágenes
    Entonces se suben la portada y la mesa
    Y la URL de contraportada se registra como nula
    Y el estado de imágenes se marca como Completed
```
