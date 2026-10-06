# INC-114: Layout Global, Cabecera Adaptativa, Barra Inferior Móvil con Hoja «Más» y Barra de Gestión Staff

## Estado
⏳ Planificado (Fase 2 del Rediseño Integral «Revista Lúdica»)

## Rama y Worktree Sugerido
- **Rama:** `inc/rediseño-revista-ludica` (o rama atómica `inc/layout-cabecera-barra-movil`)
- **Worktree:** `C:\repos\ludeka-wt\rediseño-revista-ludica`

## Contexto y Motivación
El layout global (`MainLayout.razor`) debe adoptar la estructura y comportamiento dinámico de la Revista Lúdica:
1. La cabecera (72 px) adopta automáticamente el color de fondo y texto de la banda contextual de la página activa (Terracota en Portada, Verde en Catálogo, Mostaza en Sorteos, Azul en Eventos, Ciruela en Clasificación, Papel en Ficha y Cuenta).
2. En móvil (<760 px), la navegación superior se simplifica y emerge la **barra inferior fija en píldora** (64 px, `--inverse`) con 5 destinos: Inicio, Catálogo, Sorteos, Entrar/Ludoteca y **Más**.
3. El botón **«Más»** despliega una hoja flotante accesible (`border: 2px solid var(--rule)`, radio 26 px) con la cuadrícula 2x4 de accesos pastel: Novedades, Eventos, Clasificación, Editoriales, Creadores, Tiendas, Transparencia y selector de tema.
4. **Preservación Crítica de Gestión:** Los usuarios con roles de staff (Mesa Fundadora / Moderador) deben contar con la **Barra de Gestión contextual** bajo la cabecera (`--paper-2`, borde discontinuo mostaza) con accesos de la página actual y enlace al panel central `Ludeka Gestión`.
5. **Preservación de Estado de Red:** El componente `OfflineIndicator` se estiliza como píldora accesible en cabecera junto con la franja de advertencia cuando no hay conexión.

## Alcance de la Solución
1. **`src/Ludeka.Web/Components/Layout/MainLayout.razor`**:
   - Detección de ruta y asignación reactiva de color de cabecera (`hdr.bg` y `hdr.fg`).
   - Montaje condicional del nuevo componente `<StaffActionBar />` para usuarios staff.
   - Montaje del banner de desconexión PWA cuando `navigator.onLine == false`.
   - Menú de navegación en escritorio con 4 destinos canónicos + desplegable «Más ▾».
2. **`src/Ludeka.Web/Components/Shared/MobileBottomNav.razor`**:
   - Reemplazo del diseño previo por la píldora fija de 64 px con objetivos táctiles $\ge 48$ px y acentos en mostaza.
   - Implementación de la hoja modal «Más de Ludeka» con soporte de teclado Escape, cierre exterior y roles ARIA.
3. **`src/Ludeka.Web/Components/Shared/StaffActionBar.razor` (Nuevo)**:
   - Barra contextual para roles de moderación y administración con badge de rol, acciones de página y enlace a `Ludeka Gestión`.
4. **`src/Ludeka.Web/Components/Shared/AccountMenu.razor`**:
   - Estilizado de píldora de cabecera con avatar mostaza (inicial) y desplegable de 290 px con borde `--rule` y 22 px de radio.
5. **Pruebas de Contrato y Accesibilidad**:
   - `MainLayoutContractTests.cs`, `MobileNavContractTests.cs` y pruebas de accesibilidad WCAG 2.2 AA.

## Criterios de Aceptación
- La cabecera tiñe su fondo según la sección activa sin parpadeos visuales (Zero-FOUC).
- En pantallas $<760$ px la barra móvil se muestra flotante respetando `safe-area-inset-bottom`.
- Los moderadores y administradores mantienen acceso íntegro a todas las funciones de gestión.
