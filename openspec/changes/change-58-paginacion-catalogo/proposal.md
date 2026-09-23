# Propuesta SDD — INC-58: Paginación Real del Catálogo y Modos de Vista (Cuadrícula y Lista)

## 1. Motivación y Contexto

Tras la ingesta masiva de títulos de BGG en INC-53, el catálogo de Ludeka cuenta con aproximadamente 8.000 juegos registrados. Sin embargo, la interfaz de usuario en `/catalogo` (`Home.razor`) se encuentra limitada por una contradicción técnica declarada: el backend (`SqliteGameRepository.SearchAsync` y `CatalogResult`) ya soporta paginación con `Skip`/`Take` y cálculo de `TotalCount`, pero la página solicita de manera fija `page: 1, pageSize: 50` sin controles de avance. Como señaló el maintainer: *«solo se ven un puñado»*.

Adicionalmente, el maintainer ha solicitado la capacidad de conmutar el modo de visualización: *«Ademas agregaria para cambiar el modo de ver, si como esta como ahora o en modo lista»*.

Este incremento cierra la brecha entre backend y presentación, introduce paginación visible y usable con persistencia en URL, y habilita una vista conmutable entre la cuadrícula actual (`grid`) y una vista en lista (`list`) con métricas editoriales comparativas.

---

## 2. Alcance Propuesto

1. **Paginación Visible y Usable en `/catalogo`:**
   - Adopción del patrón probado en `AuditLogViewer.razor` (botones Anterior/Siguiente, indicador «Página X de Y»).
   - Contador visible y honesto del volumen de títulos encontrados (`_totalCount` de `CatalogResult`).
2. **Sincronización Bidireccional con la URL:**
   - Parámetros de consulta en URL: `page` (número de página), `view` (`grid` o `list`), `q` (búsqueda de texto) y presets.
   - Sincronización mediante `NavigationManager` sin recarga completa ni pérdida del historial de navegación (`popstate`).
3. **Modos de Vista Conmutables (Cuadrícula ↔ Lista):**
   - Selector visual accesible con iconos oficiales Lucide `layout-grid` y `list`.
   - Componente `GameListItem.razor` optimizado con miniatura WebP de 64x64px anti-CLS (INC-57), títulos (español y original), diseñador, editorial, badges de jugadores y duración, huella de mesa y rating.
   - Conmutación instantánea en memoria sin latencia de red.
4. **Ampliación de Iconografía Oficial:**
   - Añadir los paths oficiales de `layout-grid` y `list` a `IconCatalog.cs`.
5. **Batería de Pruebas Automatizadas:**
   - Pruebas unitarias y de contrato en `Ludeka.UnitTests/Web/CatalogPaginationContractTests.cs`.
   - Verificación de no-regresión sobre la suite completa.

---

## 3. Criterios de Aceptación

1. Desde `/catalogo` el usuario puede recorrer todas las páginas hasta la última sin truncamiento.
2. El total mostrado en pantalla coincide exactamente con `CatalogResult.TotalCount`.
3. Los parámetros `page`, `view` y filtros sobreviven a recargas del navegador y enlaces compartidos.
4. El usuario conmuta entre cuadrícula y lista sin recargar datos ni perder la página actual.
5. Los nuevos controles cumplen con las directrices de accesibilidad WCAG 2.2 AA (foco visible, roles ARIA, etiquetas semánticas).
6. Suite completa de pruebas en verde (`dotnet test`).
