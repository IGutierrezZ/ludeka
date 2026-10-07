# Especificación de Requerimientos: INC-122 — Reparación de Calidad y Completitud de Imágenes BGG

## 1. Requerimientos Funcionales

- **RF-01 (Contrato Fiel de GeekDo):** `GeekDoImagesClient` debe deserializar el objeto JSON real devuelto por `https://api.geekdo.com/api/images`. En particular:
  - `imageurl_lg`: URL de alta resolución (1024×1024). Debe ser la fuente primaria para cualquier imagen devuelta.
  - `imageurl`: miniatura micro (64×64). Queda terminantemente prohibido utilizarla como imagen completa en `FrontCoverUrl`, `BackCoverUrl` o `TableOrGameplayUrl`.
  - `numrecommend`: número entero de recomendaciones/votos para ordenación.
  - `caption`: texto descriptivo de la imagen.

- **RF-02 (Recuperación Dirigida de Contraportadas):** Para garantizar que se recupera la trasera de caja y que el carrusel cuenta con las 3 imágenes:
  - Se debe consultar `tag=BoxBack&sort=hot&showcount=5` para obtener de forma determinista la contraportada más votada de la caja.
  - Si la consulta de `tag=BoxBack` no arroja resultados, se puede intentar un fallback sobre la galería general ordenada por votos (`sort=hot&showcount=25`) buscando términos como "box back", "back cover", "trasera" o "bottom of box" en el `caption`.

- **RF-03 (Recuperación Dirigida de Fotografía en Mesa / Componentes):**
  - Se debe consultar `tag=Components` o `tag=Play` con `sort=hot&showcount=5` (o galería con `sort=hot&showcount=25`) para obtener fotos representativas de componentes y partida desplegada en mesa en alta resolución (`imageurl_lg`).

- **RF-04 (Blindaje de Portada Canónica):** En `BggImagesSyncService`:
  - Si existe versión en español con carátula propia (`vInfo.CoverImageUrl`), se mantiene como prioridad 1.
  - Si no existe carátula en español en el snapshot de versiones, la carátula oficial de la caja es la del snapshot raíz (`rootCover` / `<image>`).
  - Solo si ambas son nulas o corruptas se admitirá una foto comunitaria con `tag=BoxFront` en alta resolución (`imageurl_lg`).
  - Las URLs que contengan `__micro` o apunten a miniaturas de 64px deben considerarse corruptas (`IsCorruptedOrSimulatedCover`) para forzar su re-sincronización a alta resolución.

- **RF-05 (Adaptación Editorial del Carrusel en Blazor):**
  - `GameImageCarousel.razor` debe permitir que las cajas verticales y cuadradas respiren, sustituyendo la relación apaisada forzada `aspect-[4/3]` por una proporción natural con altura mínima generosa (`min-h-[360px] sm:min-h-[420px]` o `aspect-square sm:aspect-[4/3]`) con centrado limpio.
  - Retirar los atributos restrictivos `width="320" height="320"`.
  - Mostrar siempre las 3 píldoras (Portada, Trasera, En mesa) cuando existan las URLs en el modelo.

## 2. Requerimientos No Funcionales

- **RNF-01 (Rendimiento y Rate Limiting):** El cliente de GeekDo debe mantener rate-limiting defensivo y reintentos para no saturar la API comunitaria.
- **RNF-02 (Zero-Cloud / CDN Directo):** En entornos sin credenciales R2, almacenar URLs directas HTTPS `https://cf.geekdo-images.com/...__large/...`.
- **RNF-03 (Compatibilidad y Regresión):** Todas las pruebas unitarias existentes (2.599+) deben mantenerse en verde.
