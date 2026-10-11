# Especificación: INC-146 Menú Flotante Global de Moderación y Staff (Retirada de StaffBar y Limpieza Contextual)

## Requisitos Funcionales

- **RF-01 (Componente Global `StaffFloatingMenu.razor` en `MainLayout`):**
  - Debe renderizarse en `MainLayout.razor` exclusivamente cuando el usuario autenticado cuente con privilegios de staff (`CurrentUserService.IsFoundingTeam || CurrentUserService.IsInRole("Moderator")`).
  - Para usuarios anónimos o sin roles administrativos, el componente no debe emitir ningún elemento en el DOM.
  - Ubicación fija en la esquina inferior derecha (`fixed bottom-20 right-4 sm:right-6 z-40`), garantizando la separación de la barra de navegación móvil (`MobileBottomNav`) y el respeto de las áreas seguras (`safe-area-inset`).
  - Disparador FAB accesible con icono de staff (`zap`), etiqueta responsiva ("Staff Tools" en escritorio, "Staff" en móvil) y soporte de apertura/cierre.
  - Al abrirse, despliega una tarjeta flotante (`role="dialog"`, `aria-label="Panel rápido de moderación y gestión"`) con cabecera de rol, listado de acciones contextuales y enlace al panel central de gestión.
  - Debe cerrarse de forma automática al ejecutar cualquiera de las acciones, al pulsar la tecla `Escape` o al hacer clic fuera del panel.

- **RF-02 (Retirada de la Barra Superior Fija `StaffBar.razor`):**
  - La barra superior horizontal `StaffBar.razor` ubicada bajo la cabecera en `MainLayout.razor` queda eliminada de la maquetación.
  - La cabecera editorial y el flujo visual superior recuperan su altura limpia sin la franja fija mostaza de administración.

- **RF-03 (Acceso Canónico Permanente a Administración):**
  - El menú flotante debe incluir siempre, en la parte inferior de la tarjeta, un acceso directo a `/admin` («Panel de gestión» / Ludeka Gestión) con icono distintivo.
  - Debe mostrar con claridad el rol activo del usuario («Mesa Fundadora» o «Moderador»).

- **RF-04 (Consolidación de `GameStaffToolsPanel.razor` en la Ficha de Juego):**
  - En la ficha de juego (`GameDetail.razor` en `/juego/{slug}`):
    - Se retira el componente local `GameStaffToolsPanel.razor`.
    - `GameDetail.razor` publica sus acciones contextuales a través de `IStaffActionService`:
      1. `Editar Metadatos` -> abre modal `GameEditorModal`.
      2. `Generar con IA` / `Sintetizando...` / `Forzar Resíntesis IA` -> invoca generación/resíntesis IA.
      3. `Asociar BGG` -> abre modal `AssociateBggModal`.
      4. `Sincronizar BGG` (cuando BggId > 0) -> invoca sincronización forzada BGG.
      5. `Veredicto Fundador` (para Mesa Fundadora) -> abre modal `FoundingVerdictModal`.
      6. `Cartel para Redes` -> abre modal `SocialCardModal`.
      7. `Incidencias / Erratas` -> abre modal `GameReportModal`.
    - La vista implementa `IDisposable` para limpiar sus acciones al navegar a otra pantalla.

- **RF-05 (Matriz de Acciones Contextuales por Pantalla en Listados y Hubs):**
  - El menú flotante resuelve y muestra las acciones contextuales correspondientes a la ruta activa:
    - **Catálogo (`/catalogo`):**
      - `+ Añadir juego desde BGG` (abre `BggSearchModal`).
      - `Cola BGG` (enlace a `/admin/cola-catalogacion`).
      - `Reportes` (enlace a `/admin/reportes`).
    - **Sorteos (`/sorteos`):**
      - `Ingesta Social` (enlace a `/admin/ingesta-social`).
    - **Eventos (`/eventos`):**
      - `Gestión de Eventos` (enlace a `/admin/eventos`).
    - **Novedades (`/novedades`):**
      - `Ingesta Social` (enlace a `/admin/ingesta-social`).
    - **Tiendas (`/tiendas`):**
      - `Feeds & Afiliados` (enlace a `/admin/afiliados`).
    - **Creadores / Editoriales (`/creadores`, `/editoriales`):**
      - `Canales Monitorizados` (enlace a `/admin/canales-monitorizados`).

- **RF-06 (Matriz Contextual en Fichas de Detalle Individuales):**
  - **Ficha de Sorteo (`/sorteos/{id}`):**
    - Publica la acción `Editar Sorteo` (que abre el modal de edición existente en `GiveawayDetail.razor`).
  - **Ficha de Evento (`/eventos/{slug}` o modal):**
    - Publica la acción de edición / gestión de eventos.
  - **Fichas de Directorio (Creador, Editorial, Tienda):**
    - Publican acciones de edición y verificación cuando aplique.

- **RF-07 (Limpieza Sistemática de la Interfaz Editorial):**
  - Retirada de botones dispersos con `@if (IsStaff)` o `@if (CanModerate)` que saturaban las vistas públicas (como enlaces duplicados a `/admin/...` incrustados en tarjetas o encabezados).

## Requisitos No Funcionales

- **RNF-01 (Accesibilidad WCAG 2.2 AA):**
  - El botón flotante debe ofrecer un tamaño táctil mínimo de 44×44 px.
  - Diálogo con atributos accesibles `role="dialog"`, `aria-label="Panel rápido de moderación y gestión"` y `aria-expanded`.
  - Navegación por teclado completa, foco atrapable en el diálogo y cierre con tecla `Escape`.
- **RNF-02 (Responsive y Safe-Area Móvil):**
  - Ubicación calculada en `bottom-20` (80 px desde el fondo) para sobrevolar limpiamente la barra de navegación móvil (`MobileBottomNav`) sin bloquearla.
  - Adaptabilidad para pantallas estrechas (320 px a 420 px) manteniendo legibilidad sin desbordamientos horizontales.
- **RNF-03 (Cero Regresiones y Resiliencia de Circuito Blazor):**
  - Preservación del 100% de la suite de pruebas unitarias existente (2.795+ pruebas en verde).
  - Pruebas de contrato de marcado web y pruebas unitarias dedicadas en `tests/Ludeka.UnitTests/` para verificar:
    - Ausencia de `StaffBar` en el layout.
    - Presencia y comportamiento del `StaffFloatingMenu` para usuarios staff.
    - Ausencia total del componente para visitantes y usuarios no autorizados.
    - Correcto registro y desregistro de acciones en `IStaffActionService`.
