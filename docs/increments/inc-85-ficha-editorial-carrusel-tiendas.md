# Incremento 85: Rediseño Editorial de Ficha de Juego: Carrusel Fotográfico, Maquetación a Dos Columnas, Dónde Comprar Permanente y Pestañas Secundarias

> **ID:** INC-85  
> **Slug:** `ficha-editorial-carrusel-tiendas`  
> **Rama:** `inc/ficha-editorial-carrusel-tiendas`  
> **Estado:** ⏳ En progreso  
> **Módulos Impactados:** Módulo 01 (`docs/specs/sistema/01-catalogo-juegos.md`), `src/Ludeka.Web/Components/Pages/GameDetail.razor`, `src/Ludeka.Web/Components/Shared/GameImageCarousel.razor`, `src/Ludeka.Web/Components/Shared/StoreOffersCard.razor`, `src/Ludeka.Infrastructure/Bgg/BggSimulationDataset.cs`  
> **Dependencias:** INC-72 (UX Catálogo y Selector 3 Vistas), INC-84 (Tema Madera Clara)

---

## 1. Contexto y Diagnóstico

Tras la auditoría visual y de experiencia de usuario en la ficha de detalle de juego (`/juegos/{slug}`), se identificaron varias fricciones críticas:
1. **Monotonía y pesadez visual:** La ficha se organizaba como un apilamiento vertical monótono de hasta 8 tarjetas rectangulares beige idénticas a ancho completo, provocando una lectura pesada y falta de jerarquía.
2. **Cementerio de cajas vacías:** En títulos sin ofertas de tiendas, fundas registradas, vídeos o dudas de reglas, la página mostraba hasta 4 cajas gigantes consecutivas indicando la ausencia de datos («No hay tiendas...», «Sin especificación de fundas...», «Aún no hay tutoriales...», «No hay dudas registradas...»).
3. **Ausencia de carrusel fotográfico inmersivo:** A pesar del selector básico de INC-72, los juegos sin imágenes secundarias en base de datos caían a una única foto fija pequeña. Faltaba una experiencia de carrusel fotográfico moderna con navegación táctil/flechas, contador de diapositivas (`1 / 3`), tira inferior de miniaturas y visor a pantalla completa.
4. **Módulo de tiendas oculto tras scroll kilométrico:** El bloque de compra quedaba relegado al fondo, impidiendo que el usuario consulte rápidamente precios, disponibilidad y mínimos históricos del Radar de Precios.

---

## 2. Solución Arquitectónica y Objetivos

1. **Maquetación Editorial a Dos Columnas en Escritorio (`lg:grid lg:grid-cols-12`):**
   - **Columna Principal (Izquierda, `lg:col-span-8`):** Centrada en la inmersión lúdica: carrusel fotográfico de alto impacto, veredicto y síntesis editorial ágil, semáforo dinámico de escalabilidad, y pestañas secundarias en la base.
   - **Columna Lateral Fija (Derecha, `lg:col-span-4`):** Centrada en la acción del usuario: barra ergonómica de Mi Ludoteca (*Tengo, Jugado, Quiero comprar, Registrar partida*), módulo permanente de **Dónde Comprar & Radar de Precios**, y ficha técnica rápida de mesa.
2. **Componente de Carrusel Fotográfico Interactivo (`GameImageCarousel.razor`):**
   - Visor principal amplio con transiciones suaves, contador de diapositivas (`1 / 3`), flechas anterior/siguiente, atajos de teclado y tira inferior de miniaturas (portada oficial, trasera, componentes en mesa).
   - Modal Lightbox para visualización a alta resolución al hacer clic.
   - Preservación estricta de las variables de contrato (`_selectedHeroImageKey`, `CurrentHeroImageUrl`, `BackCoverImageUrl`, `TableImageUrl`).
3. **Módulo Permanente de Dónde Comprar con Fallback de Tiendas Españolas:**
   - Si un juego no tiene enlaces específicos mapeados, `StoreOffersCard` ofrece automáticamente enlaces directos de búsqueda en comercios especializados de referencia (Zacatrus, Cuarto de Juegos, Dracotienda, Jugamos Otra), evitando la caja vacía y permitiendo al usuario consultar disponibilidad inmediata y registrar lecturas en el Radar de Precios.
4. **Pestañas Secundarias en la Base:**
   - Agrupación modular de `Fundas de Cartas`, `Hub Multimedia` y `Consultorio de Reglas Q&A` mediante una barra de pestañas compacta. Los estados vacíos se presentan con microtextos sugerentes y llamadas a la acción, sin saturar la página.
5. **Enriquecimiento del Catálogo Canónico:**
   - Inclusión de imágenes de trasera, mesa y ofertas de tiendas en `BggSimulationDataset.cs` para títulos de referencia.

---

## 3. Criterios de Aceptación

- [ ] La ficha de juego implementa maquetación a dos columnas en escritorio y flujo adaptativo en móvil.
- [ ] El carrusel fotográfico soporta navegación por botones, miniaturas, contador de diapositivas y modal ampliado.
- [ ] La sección «Dónde Comprar» permanece siempre visible en la columna lateral derecha sin requerir scroll kilométrico.
- [ ] Para juegos sin ofertas específicas, se proveen enlaces directos de búsqueda a tiendas de referencia.
- [ ] Fundas, vídeos y dudas de reglas se integran en un componente de pestañas secundarias en la base.
- [ ] 100% de las pruebas unitarias y de contrato pasando en verde.
