# INC-72: UX y Catálogo: Retirada de Texto BGG, Carrusel de Fotos, Menú Móvil y Filtros Avanzados Multiselección

> **Estado:** ⏳ En progreso  
> **Fecha de Inicio:** 2026-09-27 · **Fecha de Cierre:** Pendiente  
> **Rama de Trabajo:** `inc/ux-catalogo-filtros-multiseleccion`  
> **Worktree:** `C:\repos\ludeka-wt\ux-catalogo-filtros-multiseleccion`  
> **Pruebas Automatizadas:** 1.938 pruebas unitarias en verde al inicio (línea base)  
> **Dependencias:** INC-58 (Catálogo), INC-59 (Filtros), INC-71 (Calidad BGG y Media URLs)  
> **Especificación Viva:** `docs/specs/sistema/01-catalogo-juegos.md` y `docs/specs/sistema/06-navegacion-global.md`  

---

## 1. Contexto y Detección

Tras la ingesta masiva de catálogo y la revisión visual en dispositivos móviles y escritorio, se identificaron cuatro áreas clave de fricción de experiencia de usuario (UX):
1. **Sección en inglés redundante:** En la ficha de detalle (`GameDetail.razor`), se muestra una sección «Sobre el juego» con la descripción en inglés importada en bruto de BGG. Dado que el catálogo ya cuenta con síntesis editorial en español generada por IA y datos estructurados, este texto anglosajón resulta discordante y debe retirarse.
2. **Galería de imágenes desaprovechada:** Aunque el backend ingesta y almacena tres fotos por juego (portada, trasera y foto en mesa), la ficha únicamente muestra la carátula principal. Se requiere un carrusel/selector interactivo que permita alternar entre portada, contraportada y mesa.
3. **Menú móvil no se repliega tras navegación:** En `MainLayout.razor`, el menú superior móvil implementado con `<details class="lg:hidden">` no se cierra al hacer clic en ningún enlace (ej. «Catálogo»), permaneciendo desplegado y bloqueando la vista.
4. **Catálogo móvil poco denso y filtros rígidos:**
   - En móvil, el catálogo muestra tarjetas sobredimensionadas; se solicita adaptarlo a 3 carteles por fila (`grid-cols-3`) manteniendo coherencia con las portadas de novedades.
   - La sección de filtros avanzados debe comportarse como un panel plegable/acordeón (oculto por defecto) que permita multiselección fluida (varios recuentos de jugadores, dureza/complejidad, estilos y categorías).

---

## 2. Requerimientos del Maintainer

1. **Ficha de Juego (`GameDetail.razor`):**
   - Retirar la sección en inglés «Sobre el juego» (`game.Description`).
   - Implementar selector interactivo de imágenes en la cabecera (miniaturas/pestañas de Frontal, Trasera y Mesa) cuando existan URLs en `CoverImageUrl`, `BackCoverImageUrl` o `TableImageUrl`.
2. **Navegación Móvil (`MainLayout.razor`):**
   - Garantizar que al pulsar sobre cualquier enlace del menú móvil, el desplegable `<details>` se cierre inmediatamente.
3. **Catálogo y Filtros (`Catalog.razor` y `GameFilterCriteria.cs`):**
   - En vista móvil, presentar el catálogo en una cuadrícula compacta de 3 carteles por fila (`grid-cols-3`).
   - Convertir los filtros avanzados en un acordeón desplegable que no sobrecargue la cabecera.
   - Permitir multiselección: varios recuentos de jugadores (ej. [2, 3] jugadores), estilos y tipos de confrontación.

---

## 3. Criterios de Aceptación

1. En `GameDetail.razor`, la sección en inglés desaparece y la cabecera permite visualizar e intercambiar entre portada, contraportada y componentes en mesa de forma interactiva y accesible.
2. En `MainLayout.razor`, hacer clic en «Catálogo» o cualquier otra ruta cierra automáticamente el menú móvil.
3. En `Home.razor` y `GameCard.razor`, la vista móvil adopta `grid-cols-3` con carteles proporcionados y badges compactos.
4. El panel de filtros avanzados se despliega/repliega con un botón conmutador y soporta multiselección en jugadores, dureza, tipo, estilos, espacio y duración.
5. El repositorio `IGameRepository` y su implementación SQLite soportan multiselección sin romper compatibilidad hacia atrás.
6. 100% de las pruebas unitarias en verde.

---

## 4. Cambios Técnicos Implementados

1. **Navegación Móvil (`MainLayout.razor`):**
   - Incorporado atributo nativo `onclick="this.closest('details')?.removeAttribute('open')"` en todos los enlaces del menú móvil desplegable (`<details>`). Se garantiza el cierre instantáneo tanto en navegación SSR completa como bajo Blazor Enhanced Navigation.

2. **Ficha de Juego (`GameDetail.razor`):**
   - Retirado el bloque HTML y la sección completa «Sobre el juego» (`game.Description`), eliminando los textos en inglés de BGG.
   - Implementado selector interactivo de imágenes en el hero con conmutador accesible para Portada, Trasera y Mesa (`_selectedHeroImageKey`), con miniaturas, indicadores de foco y textos alternativos descriptivos según la vista activa.

3. **Modelo de Dominio y Filtros (`GameComplexity.cs` y `GameFilterCriteria.cs`):**
   - Creado enum `GameComplexity` (`Light`, `Medium`, `Heavy`) en `Ludeka.Core.Enums`.
   - Ampliado el DTO `GameFilterCriteria` con colecciones multiselección (`PlayerCounts`, `PlayerCountsMatchAll`, `Styles`, `Confrontations`, `Types`, `Footprints`, `Complexities`, `MaxDurations`) manteniendo las propiedades escalares originales intactas para total compatibilidad.

4. **Motor de Persistencia y Búsqueda (`SqliteGameRepository.cs`):**
   - Incorporada lógica de filtrado por conjuntos en SQLite para facetas multiselección.
   - Implementada semántica estricta de concurrencia de jugadores: cuando se seleccionan múltiples comensales (ej. 2 y 3 jugadores), se exige que el juego soporte y funcione para todas las configuraciones simultáneas (`PlayerCountsMatchAll: true`).
   - Añadido cálculo heurístico y filtrado por dureza cognitiva (`Complexities`).

5. **Diseño Visual y Catálogo Móvil (`GameCard.razor` y `Home.razor`):**
   - Adaptada la cuadrícula del catálogo a `grid-cols-3` en pantallas móviles (`< 640px`) con espaciado optimizado (`gap-2 sm:gap-4 md:gap-6`).
   - Compactada la tarjeta `GameCard.razor` para alta densidad móvil (padding `p-2 sm:p-3.5`, títulos a dos líneas, badges proporcionales).
   - Diseñado el panel de filtros avanzados colapsable (`_showAdvancedFilters`), con botón conmutador en cabecera, contador dinámico de filtros activos y chips interactivos accesibles con multiselección para todas las dimensiones lúdicas.
   - Sincronización bidireccional completa con la cadena de consulta de la URL (`mesa`, `jugadores`, `duracion`, `dureza`, `estilos`, `confrontacion`, `tipos`).

6. **Batería de Pruebas Unitarias y de Contrato:**
   - Añadidas pruebas de persistencia en `SqliteGameRepositoryTests.cs` validando multiselección de jugadores, estilos lúdicos, espacio en mesa y dureza.
   - Añadidas pruebas de contrato en `CatalogFilterContractTests.cs` validando el panel colapsable, cierre de menú móvil, multiselección en URL y selector de imágenes de GameDetail.
   - Total de pruebas unitarias superadas: **1.945 pruebas en verde (0 errores)**.
