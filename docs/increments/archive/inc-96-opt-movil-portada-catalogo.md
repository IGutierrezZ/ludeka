# INC-96: Optimización Visual Móvil de Portada y Catálogo, Acciones Rápidas de Colección y Procesamiento Continuo de IA en Lotes

> **Estado:** ✅ Archivado  
> **Rama:** `inc/opt-movil-portada-catalogo`  
> **Worktree:** `F:\repos\ludeka-wt\opt-movil-portada-catalogo`  
> **Fecha:** 2026-10-02  
> **Pruebas Verificadas:** 2.253 unitarias en verde al 100% (2.263 totales con integración)  

---

## 1. Contexto y Justificación

Al utilizar Ludeka en dispositivos móviles, se identificaron varios problemas de densidad de información, ergonomía táctil y comportamiento funcional:
1. **Pérdida de foco en la Portada (`/`):** El bloque Hero ocupaba más de media pantalla en móvil con contenido estático repetitivo antes de que el usuario pudiera ver el catálogo o los carriles de actividad.
2. **Apilamiento vertical en cabeceras de carril:** La cabecera del carril de juegos y los carriles secundarios (Sorteos, Novedades y Eventos) rompían en 2 o 3 niveles ocupando un valioso espacio vertical.
3. **Pie de página poco diferenciado:** El footer se confundía con la propia hoja de lectura blanca/grisácea, careciendo de contraste de final de página.
4. **Catálogo disperso en móvil (`/catalogo`):** El buscador, botón de filtros y la leyenda de iconografía estaban dispuestos verticalmente o consumían demasiado espacio.
5. **Modo lista poco denso:** Cada elemento ocupaba un alto excesivo, impidiendo ojear muchos juegos rápidamente, y carecía de acciones directas sobre la ludoteca del usuario.
6. **Fallo en filtros de ordenación:** Al cambiar el criterio de ordenación en `/catalogo`, la vista no alteraba el orden de los juegos debido a la omisión de `criteria.SortBy` en el hash SHA256 de la caché L1 de `CachedCatalogService`.
7. **Procesamiento de IA masivo en segundo plano para staging:** Con miles de títulos en staging con datos BGG e imágenes ya completadas pero pendientes de síntesis de IA Gemini Flash (`PendingAiCount`), se requería un botón administrativo para lanzar todos los lotes de IA en segundo plano de manera continua sin bloquear la interfaz.

---

## 2. Objetivos y Alcance

1. **Optimización Móvil de Portada (`HomeDashboard.razor`, `RailHeader.razor` y `MainLayout.razor`):**
   - Ocultación del Hero en móviles mediante `hidden md:block` para arrancar inmediatamente con contenido vivo.
   - Cabecera del carril de juegos en una sola fila compacta: título («Catálogo»), selector («Tendencia» / «Top») y enlace integrado con flecha (`→`).
   - Carriles de Sorteos, Novedades y Eventos condensados en una sola fila con títulos breves en móvil (`MobileTitle`) y enlace «Ver todos →» con `whitespace-nowrap`.
   - Footer con fondo carbón profundo (`#12161A`, `border-stone-800`), tipografía nítida contrastada y botones integrados.

2. **Catálogo y Colección en Móvil (`Home.razor`, `GameCard.razor`, `GameListItem.razor` y `QuickCollectionModal.razor`):**
   - Fila superior unificada: Buscador (`flex-1`) + botón Filtros + botón Leyenda.
   - Modal colapsable de leyenda controlado mediante el botón superior sin ocupar espacio predeterminado.
   - Modo lista ultra-compacto (~44 px) con expansión en acordeón al pulsar, mostrando metadatos completos, botón «Ver ficha» y tres acciones directas de colección («Tengo», «Jugado», «Deseado»).
   - Modo cuadrícula con navegación táctil directa a la ficha al pulsar la carátula, acompañada de un botón flotante `+` ergonómico en la esquina inferior derecha que despliega un *bottom sheet* accesible (`QuickCollectionModal.razor`) con las acciones de colección.
   - Cumplimiento de contratos anti-CLS (`width="64" height="64" loading="lazy" decoding="async"`) y accesibilidad WCAG 2.2 AA.

3. **Corrección de la Caché de Ordenación (`CachedCatalogService.cs`):**
   - Incorporación obligatoria de `criteria.SortBy` en `ComputeCriteriaHash`, invalidando adecuadamente el hash al conmutar entre valoración, año, dureza, duración o alfabético.

4. **Procesamiento Continuo de IA en Segundo Plano para Staging (`IBggMassIngestionService`, `BggMassIngestionService` y `CatalogQueueAdmin.razor`):**
   - Nuevos métodos `RunContinuousAiDrainAsync` y `RunScheduledContinuousAiDrainAsync` en `IBggMassIngestionService` con comprobación de permisos granulares (`ModeratorPermission.CanEditGames`).
   - Bucle continuo que itera lotes de Gemini Flash (`ProcessPendingAiBatchAsync`) y promueve automáticamente los títulos listos al catálogo (`PromoteReadyToCatalogBatchAsync`) hasta agotar los pendientes o la cuota diaria.
   - Botón interactivo en `CatalogQueueAdmin.razor` («Lanzar Pendientes IA en Lotes»), acceso rápido en la columna de métricas `Pend. IA Lote` y panel de alerta con indicador de progreso activo y botón de cancelación.

---

## 3. Verificación Automática

- `CachedCatalogServiceTests`: Pruebas de miss y hit de caché al variar el orden de ordenación superadas.
- `CatalogPaginationContractTests`: 15 de 15 pruebas superadas (contratos anti-CLS y accesibilidad de `GameListItem`).
- `BggMassIngestionWriteGuardTests`: 7 de 7 pruebas superadas (rutas sin sesión, con permiso y sistema).
- **Suite completa:** 2.253 pruebas unitarias superadas en verde (0 errores, 0 fallos).
