# Plan de Tareas SDD / ODD — INC-60: Navegación Móvil — Barra Inferior y Safe-Area

- [x] **ODD-1 — Cimientos de Maquetación, Viewport y Safe-Area**
  - [x] 1.1 Configurar `viewport-fit=cover` en la etiqueta `<meta name="viewport">` de `App.razor`.
  - [x] 1.2 Definir variables CSS y clases utilitarias de soporte safe-area y espaciado de reserva inferior en `input.css`.
  - [x] 1.3 Adaptar `#blazor-error-ui` en `MainLayout.razor.css` para respetar la barra inferior en resoluciones móviles.

- [x] **ODD-2 — Componente de Barra de Navegación Móvil (`MobileBottomNav.razor`)**
  - [x] 2.1 Diseñar e implementar `src/Ludeka.Web/Components/Shared/MobileBottomNav.razor` (`fixed bottom-0 inset-x-0 z-40 lg:hidden`).
  - [x] 2.2 Configurar los 5 destinos principales con iconos Lucide (`house`, `dices`, `library`, `gift`, `user`) y microtextos.
  - [x] 2.3 Conectar la detección reactiva de ruta activa con `NavigationManager.LocationChanged` y emitir `aria-current="page"`.
  - [x] 2.4 Integrar `MobileBottomNav` dentro de `MainLayout.razor` asegurando el comportamiento adaptativo multi-tema.

- [x] **ODD-3 — Acciones de Colección al Alcance del Pulgar en Móvil**
  - [x] 3.1 Revisar y optimizar la ergonomía táctil en móvil de `CollectionActionBar.razor`.
  - [x] 3.2 Asegurar que en la ficha del juego (`GameDetail.razor`) las acciones de colección mantengan visibilidad y accesibilidad ergonómica.

- [x] **ODD-4 — Pruebas de Contrato y Verificación de No-Regresión**
  - [x] 4.1 Crear `tests/Ludeka.UnitTests/Web/MobileNavigationContractTests.cs` (verificación de marked landmark, safe-area, endpoints y accesibilidad).
  - [x] 4.2 Ejecutar suite completa `dotnet test Ludeka.sln` y validar el 100% en verde (1.695 pruebas superadas).

- [x] **ODD-5 — Especificación Viva, SDD y PR**
  - [x] 5.1 Actualizar especificaciones vivas en `docs/specs/sistema/` (`38-navegacion-movil-y-safe-area.md` y `README.md`).
  - [x] 5.2 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
  - [x] 5.3 Preparar PR hacia `main` (`scripts/sdd-worktree.ps1 pr navegacion-movil`).
