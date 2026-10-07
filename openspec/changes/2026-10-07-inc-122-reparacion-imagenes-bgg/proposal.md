# Propuesta de Incremento: INC-122 — Reparación de Calidad y Completitud de Imágenes BGG (Portadas, Contraportadas y En Mesa)

## 1. Contexto y Diagnóstico
Tras la puesta en marcha de la sincronización de imágenes comunitarias (INC-120), se detectaron cuatro anomalías críticas que impactan directamente en la experiencia de producción:
1. **Resolución degradada a 64×64 píxeles:** `GeekDoImagesClient` leía una propiedad inexistente en la API de GeekDo (`images.original.src`), cayendo en el fallback `item.ImageUrl`, que en GeekDo apunta a miniaturas micro de 64×64 píxeles (`__micro/.../fit-in/64x64/`), estiradas después a 320px+ en el navegador. La imagen de alta definición (1024×1024) reside en `imageurl_lg`.
2. **Ausencia sistemática de contraportada (solo 2 imágenes):** La consulta HTTP a GeekDo no incluía ordenación por votos (`sort=hot`) ni filtrado de categoría (`tag=BoxBack`), solicitando por defecto las 10 fotos más recientes de cualquier usuario. Al no existir fotos de trasera en esas 10 fotos, `BackCoverImageUrl` quedaba a `null`, impidiendo que el carrusel mostrara la tercera diapositiva.
3. **Sustitución indebida de la portada oficial por fotos de usuarios:** En juegos donde la edición en español de BGG no tiene carátula propia subida a su subárbol de versión, el sincronizador recurría a la primera foto comunitaria reciente de GeekDo antes de verificar la carátula oficial de alta resolución del snapshot raíz (`snapshot.RawJson` / `<image>`), mostrando componentes o fotos caseras en vez de la caja oficial.
4. **Constricción geométrica en el carrusel:** `GameImageCarousel.razor` forzaba una proporción apaisada `aspect-[4/3]`, constriñendo las carátulas verticales o cuadradas a una franja pequeña con bandas laterales vacías, agravado por dimensiones fijas `width="320"` y `height="320"`.

## 2. Solución Propuesta
1. **Refactorización de `GeekDoImagesClient`:**
   - Deserializar fielmente el contrato real de GeekDo: `imageurl_lg` (alta resolución 1024×1024), `numrecommend` (votos positivos) e `imageid`.
   - Implementar consulta dirigida con `sort=hot`:
     - Consulta específica de contraportada con `tag=BoxBack&sort=hot&showcount=5`.
     - Consulta específica de mesa/componentes con `tag=Components` o `tag=Play` (o galería general con `sort=hot&showcount=25`).
     - Priorizar `imageurl_lg` para todas las variantes fotográficas.
2. **Jerarquía Defensiva de Portada en `BggImagesSyncService`:**
   - Prioridad 1: Portada oficial de edición española (`vInfo.CoverImageUrl`) si existe.
   - Prioridad 2: Portada oficial de caja internacional desde el snapshot raíz de BGG (`rootCover`).
   - Prioridad 3: Foto comunitaria `tag=BoxFront` en alta resolución de GeekDo solo si las anteriores son nulas o corruptas.
   - Forzar re-evaluación/reemplazo de imágenes que contengan URLs corruptas con `__micro` o dimensiones de 64px.
3. **Diseño y Dimensionamiento Editorial del Carrusel (`GameImageCarousel.razor`):**
   - Permitir que el contenedor del carrusel se adapte a cajas verticales y cuadradas (`aspect-square sm:aspect-[4/3]` o contenedor con altura generosa `min-h-[380px]`), centrando la imagen sin constricciones artificiales ni bandas grises excesivas.
   - Retirar los atributos restrictivos fijos `width="320" height="320"` en la imagen activa.
   - Garantizar que si existen las 3 imágenes (Portada, Contraportada y En mesa), se muestren las 3 píldoras interactivas correspondientes.
4. **Pruebas y Verificación:**
   - Actualizar las pruebas unitarias de `GeekDoImagesClientTests` con el payload JSON real de la API de GeekDo.
   - Probar `BggImagesSyncServiceTests` validando que la contraportada se asigna correctamente y que la portada raíz prevalece sobre fotos comunitarias cuando no hay edición española.
   - Comprobar que los tests de interfaz y contrato se mantienen al 100% en verde.

## 3. Criterios de Aceptación
- [ ] `GeekDoImagesClient` recupera URLs de alta resolución `imageurl_lg` para trasera y mesa.
- [ ] La consulta de contraportada localiza la caja trasera con `tag=BoxBack&sort=hot`.
- [ ] `BggImagesSyncService` no sobreescribe la portada oficial con fotos de usuarios.
- [ ] El carrusel muestra con nitidez las 3 diapositivas (Portada, Contraportada, En mesa) para juegos con medios comunitarios.
- [ ] La suite completa de pruebas unitarias (`dotnet test`) se ejecuta al 100% en verde.
