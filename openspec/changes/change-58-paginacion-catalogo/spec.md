# Especificación de Requisitos SDD — INC-58: Paginación Real del Catálogo y Modos de Vista

## 1. Requisitos Funcionales (RF)

- **RF-01 (Controles de Paginación):**  
  En `/catalogo`, si `TotalPages > 1`, se deben mostrar controles de paginación que incluyan botón «Anterior», texto indicativo «Página X de Y» y botón «Siguiente». El botón «Anterior» se deshabilitará en la página 1; el botón «Siguiente» se deshabilitará en la página `TotalPages`.

- **RF-02 (Tamaño de Página Estándar):**  
  El tamaño de página por defecto para el catálogo será de 24 elementos (múltiplo común de 2, 3, 4 y 6 columnas del grid responsivo, garantizando filas completas sin huecos visuales).

- **RF-03 (Indicador de Total de Títulos):**  
  La interfaz debe mostrar el recuento total de títulos disponibles bajo los criterios de filtrado actuales con formato claro (ej. «Mostrando X de Y juegos»).

- **RF-04 (Persistencia de Estado en URL):**  
  Cualquier cambio de página, modo de vista, término de búsqueda o preset debe reflejarse en la query string de la URL (`page`, `view`, `q`, etc.) mediante `NavigationManager.NavigateTo(..., replace: true)`. Al cargar la URL directamente, estos parámetros deben inicializar el estado del componente.

- **RF-05 (Reinicio de Página ante Nuevos Filtros):**  
  Al escribir un nuevo término de búsqueda o seleccionar un nuevo preset de catálogo, la página activa debe resetearse automáticamente a `page = 1`.

- **RF-06 (Conmutador de Modos de Vista):**  
  La interfaz debe ofrecer un selector con dos estados mutuamente excluyentes:
  - `grid` (Cuadrícula, valor por defecto): presenta las tarjetas [`GameCard.razor`](file:///C:/repos/ludeka-wt/paginacion-catalogo/src/Ludeka.Web/Components/Shared/GameCard.razor).
  - `list` (Lista): presenta las filas [`GameListItem.razor`](file:///C:/repos/ludeka-wt/paginacion-catalogo/src/Ludeka.Web/Components/Shared/GameListItem.razor).
  La conmutación debe operar en memoria con la colección ya cargada en la página actual.

- **RF-07 (Fila de Lista Editorial `GameListItem`):**  
  Cada elemento en modo lista debe mostrar:
  1. Carátula en miniatura (64x64px) usando `ThumbnailUrl` (WebP 400px de INC-57) con fallback a placeholder SVG.
  2. Título principal en español y título original si difiere.
  3. Diseñador y editorial principal.
  4. Metadatos comparativos: número de jugadores recomendados/ideales, duración estimada, huella en mesa (`TableFootprint`), estilo de juego y puntuación BGG/Ludeka.
  5. Enlace accesible y navegable hacia la ficha individual `/juegos/{slug}`.

---

## 2. Requisitos No Funcionales (RNF)

- **RNF-01 (Accesibilidad WCAG 2.2 AA):**  
  - Botones de paginación y modo de vista con áreas táctiles mínimas de 44x44px o espaciado suficiente.
  - Anillos de foco visibles mediante `focus-visible:ring-2`.
  - Roles ARIA semánticos (`role="group"`, `aria-label="Modo de visualización"`, `aria-pressed="true|false"`).
- **RNF-02 (Estabilidad Visual Anti-CLS):**  
  Las imágenes de `GameListItem` deben declarar explícitamente `width="64"`, `height="64"`, `loading="lazy"` y `decoding="async"`.
- **RNF-03 (Eficiencia de Rendimiento):**  
  No realizar peticiones repetitivas a la base de datos al cambiar exclusivamente el modo de vista entre cuadrícula y lista.
- **RNF-04 (Compatibilidad C# 13 / .NET 10):**  
  Uso de tipos inmutables, `IReadOnlyList`, `ValueTask` donde proceda y convenciones idiomáticas del proyecto.
