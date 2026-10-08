# Propuesta: INC-132 Saneamiento de Novedades Devir y Maldito, Galería Fotográfica y Job de Barrido de Catálogo

## 1. Contexto y Diagnóstico

Al sincronizar novedades editoriales desde Devir y Maldito Games, se han detectado tres anomalías en producción:

1. **Duplicados y títulos espurios en Devir:**
   - En la página de próximos lanzamientos de Devir (`https://devir.es/proximos-lanzamientos`), coexisten secciones con cuadrículas de productos detallados (precio, EAN, autor) y secciones de tarjetas simples.
   - El parser `TileCardRegex` evalúa la sección completa con modo `Singleline` cruzando límites de etiquetas HTML, lo que provoca que un párrafo intermedio con `<strong>Autor: </strong>` sea capturado como título independiente, heredando el EAN del producto previo y asignando precios erróneos (como 22,50 € provenientes de ofertas de afiliados de terceros en lugar del PVP oficial de 25,00 €).
   - Se capturan meses pasados (como septiembre) y secciones de juegos de rol (como "Septiembre 2026 - Juegos de Rol").

2. **Carencia de galería de imágenes completas en ficha de juego:**
   - Actualmente solo se almacena la imagen 3D (`face3d.jpg`) de la caja.
   - La ficha de producto de Devir (`SourceUrl`, ej. `https://devir.es/the-hanging-gardens`) incluye un bloque `<script type="text/x-magento-init">` con `mage/gallery/gallery` que contiene la galería oficial completa: `face3d.jpg` (caja 3D), `components1.jpg` (componentes/en mesa) y `backflat.jpg` (contraportada de la caja).

3. **Bloqueo en sincronización de Maldito Games y caída del circuito Blazor:**
   - La sincronización serial invoca al asistente de IA (Google Gemini) para cada título no emparejado en el catálogo local sin un timeout defensivo corto por ítem.
   - Al procesar Devir primero (~25 llamadas a IA), el tiempo acumulado superó el límite de 5 minutos de Cloud Run / WebSocket de Blazor Server (`WebSocket closed with status code: 1006`), abortando la conexión antes de llegar o persistir Maldito Games.

## 2. Solución Propuesta

### Fase 1: Saneamiento del Extractor de Devir (`DevirReleasesExtractor.cs`)
- **Filtro temporal estricto:** Descartar automáticamente cualquier sección cuya fecha sea anterior al mes en curso (`releaseDate < DateOnly(now.Year, now.Month, 1)`). Solo conservar el mes actual y posteriores.
- **Filtro temático estricto:** Excluir secciones y títulos de juegos de rol ("juegos de rol", "rol", libros básicos, pantallas).
- **Prevención de falsos duplicados:** No ejecutar `ParseTileCardItems` si la sección ya generó ítems detallados con precio. Delimitar `TileCardRegex` a contenedores discretos `mgz-element-inner` y filtrar lista negra de tokens (`Autor:`, `Ilustrador:`, `Libro básico`, etc.).
- **Extracción de galería fotográfica:** Si el ítem dispone de `SourceUrl`, parsear el JSON de `mage/gallery/gallery` para extraer `components1` (`TableImageUrl`) y `backflat` (`BackCoverImageUrl`).

### Fase 2: Robustez en Sincronización y Desbloqueo de Maldito Games
- En `EditorialReleasesSyncService.cs`, aplicar timeout defensivo estricto (4 segundos con `CancellationTokenSource`) en cada consulta a IA (`SuggestMatchAsync`), con fallback inmediato al motor heurístico determinista sin bloquear el hilo.
- Aislamiento de ejecución por editorial para que el fallo o demora de una editorial no cancele la otra.
- Actualización de `matchedGame.UpdateMediaUrls(...)` al sincronizar novedades cuando se descubran imágenes de mesa y contraportada.

### Fase 3: Job Autónomo de Barrido de Catálogo Devir (`DevirImagesBackfillJobRunner`)
- Crear un nuevo runner en `src/Ludeka.Jobs/Runners/DevirImagesBackfillJobRunner.cs` (`JobNames.DevirImagesBackfill = "devir-images-backfill"`).
- Crawlear el catálogo de juegos de mesa de Devir (`https://devir.es/juegos-de-mesa`), mapear con los juegos de Devir en Ludeka y enriquecer su galería fotográfica en la base de datos (caja 3D, componentes y contraportada).

## 3. Criterios de Aceptación
1. No se generan ítems con títulos como `Autor:` o `Libro básico`.
2. No se importan meses pasados ni productos de rol.
3. Las novedades de Devir con ficha de producto capturan foto de mesa y contraportada.
4. Maldito Games y Devir se sincronizan de forma resiliente sin tiempos de espera que desconecten el circuito WebSocket.
5. El runner de Ludeka.Jobs permite actualizar masivamente las imágenes de los juegos de Devir.
6. Suite de pruebas unitarias 100% en verde.
