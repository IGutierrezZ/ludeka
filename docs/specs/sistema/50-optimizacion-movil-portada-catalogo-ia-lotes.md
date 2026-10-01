# 50. Optimización Móvil de Portada y Catálogo, Acciones Rápidas de Colección y Procesamiento Continuo de IA en Lotes

> **Módulo:** UI Editorial Móvil, Ergonomía de Catálogo y Pipeline Masivo de IA en Staging  
> **Estado:** ✅ Completado y Archivado (INC-96)  
> **Pruebas Verificadas:** 2.253 unitarias en verde al 100% (2.263 totales con pruebas de integración)  
> **Archivos Principales:**  
> - `src/Ludeka.Web/Components/Pages/HomeDashboard.razor`  
> - `src/Ludeka.Web/Components/Home/RailHeader.razor`  
> - `src/Ludeka.Web/Components/Layout/MainLayout.razor`  
> - `src/Ludeka.Web/Components/Pages/Home.razor`  
> - `src/Ludeka.Web/Components/Shared/GameCard.razor`  
> - `src/Ludeka.Web/Components/Shared/GameListItem.razor`  
> - `src/Ludeka.Web/Components/Shared/QuickCollectionModal.razor`  
> - `src/Ludeka.Application/Contracts/IBggMassIngestionService.cs`  
> - `src/Ludeka.Application/Features/Bgg/BggMassIngestionService.cs`  
> - `src/Ludeka.Web/Components/Pages/CatalogQueueAdmin.razor`  
> - `src/Ludeka.Application/Features/Catalog/CachedCatalogService.cs`  

---

## 1. Propósito y Filosofía

Este módulo optimiza la experiencia visual y operativa de Ludeka en pantallas pequeñas (teléfonos móviles y tablets), eliminando elementos repetitivos que restaban inmediatez a la navegación, compactando cabeceras y listados, e integrando acciones de gestión de colección táctil en un solo toque. Adicionalmente, corrige el algoritmo de hash de caché en los criterios de ordenación del catálogo general y dota a la administración de un mecanismo desatendido y continuo para procesar por lotes en segundo plano miles de títulos en staging pendientes exclusivamente de síntesis de inteligencia artificial con Gemini Flash.

---

## 2. Decisiones Arquitectónicas y Flujos

### 2.1. Portada Móvil y Contraste de Lectura
- **Ocultación del Hero en Móvil:** Se aplica `hidden md:block` al contenedor del Hero en `HomeDashboard.razor`, de modo que en dispositivos móviles la portada arranque directamente con el Carril 1 de juegos.
- **Cabeceras de Carril en Fila Única:**
  - El Carril 1 presenta en una sola fila el título (`Catálogo`), el selector compacto (`Tendencia` / `Top`) y la flecha de navegación (`→`).
  - Los carriles secundarios (`Sorteos`, `Novedades`, `Eventos`) incorporan la propiedad `MobileTitle` en `RailHeader.razor` para reducir el tamaño del texto en móvil y un enlace `Ver todos →` con `whitespace-nowrap`.
- **Pie de Página Contrastado:** `MainLayout.razor` adopta un fondo carbón profundo (`#12161A`, `border-stone-800`) con tipografía nítida y botones de navegación integrados, diferenciando el pie de la hoja de lectura.

### 2.2. Catálogo Móvil y Ergonomía de Colección
- **Barra Superior Unificada:** En `Home.razor`, el buscador ocupa todo el ancho disponible (`flex-1`) junto a dos botones circulares compactos: Filtros (con contador de filtros activos) y Leyenda.
- **Leyenda Colapsable:** Se desacopla de la vista normal y se muestra únicamente de forma condicional (`@if (_showLegend)`) al pulsar el botón superior, cerrándose con un botón `×` o pulsando nuevamente el botón.
- **Modo Lista Acordeón Ultra-Denso (~44 px):** `GameListItem.razor` condensa cada título a una sola línea con miniatura, nombre, año, puntuación y flecha de expansión. Al pulsar la fila:
  - Se despliegan los metadatos completos (jugadores, duración, estilo y complejidad).
  - Se exponen 3 botones directos de colección: «Tengo», «Jugado» y «Deseado», vinculados reactivamente con `IUserLibraryService`.
  - Se ofrece el botón prominente «Ver ficha →».
- **Modo Cuadrícula con Botón Flotante `+`:** En `GameCard.razor`, pulsar la tarjeta conserva la navegación inmediata a la ficha del juego, mientras que un botón flotante `+` en la esquina inferior derecha de la carátula abre un *bottom sheet* accesible (`QuickCollectionModal.razor`) con las tres opciones de colección sin navegar fuera del catálogo.
- **Contratos Anti-CLS y WCAG 2.2 AA:** Se preservan las dimensiones intrínsecas `width="64"` y `height="64"`, `loading="lazy"`, `decoding="async"`, `aria-label` descriptivos y anillos de foco visibles `focus-visible:ring-2`.

### 2.3. Corrección de Hash en la Caché L1 de Catálogo
- En `CachedCatalogService.ComputeCriteriaHash`, se detectó que se omitía `criteria.SortBy`. Al cambiar de ordenación (ej. alfabético, año, dureza), la clave SHA-256 coincidía con la del orden por defecto (`Rank`), sirviendo datos cacheados erróneos. Se añadió `c.SortBy` al cálculo del hash asegurando invalidación inmediata.

### 2.4. Procesamiento Masivo Continuo de IA en Lotes para Staging
- **Contrato de Servicio:** En `IBggMassIngestionService` se agregan:
  - `RunContinuousAiDrainAsync(int maxItems = 4000, CancellationToken ct = default)`: Exige sesión y permiso `ModeratorPermission.CanEditGames`.
  - `RunScheduledContinuousAiDrainAsync(int maxItems = 4000, CancellationToken ct = default)`: Ruta de sistema para ejecutores en segundo plano.
- **Ejecución Continua:** `ExecuteContinuousAiDrainAsync` itera en bucle sobre `ProcessPendingAiBatchAsync` procesando lotes de Gemini Flash y ejecutando `PromoteReadyToCatalogBatchAsync` en cada ciclo para incorporar de inmediato al catálogo los títulos completados. Si la cuota diaria de Gemini se agota (`AiQuotaExceededCount > 0`), pausa ordenadamente el proceso.
- **Interfaz Administrativa (`CatalogQueueAdmin.razor`):**
  - Botón en la botonera de staging: «Lanzar Pendientes IA en Lotes (X)».
  - Acceso directo en la columna de métricas `Pend. IA Lote`.
  - Alerta de estado activo con animación de spinner, mensaje de avance y botón «Detener Lotes IA».

---

## 3. Verificación Automática

- Pruebas de contrato y miss/hit de caché: `CachedCatalogServiceTests`.
- Pruebas de contrato de marcado y accesibilidad: `CatalogPaginationContractTests`.
- Pruebas de guardas de seguridad y permisos: `BggMassIngestionWriteGuardTests`.
- Total de pruebas en verde: **2.253 pruebas unitarias (0 errores, 0 fallos)**.
