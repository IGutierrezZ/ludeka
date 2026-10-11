# Especificación: INC-146 Menú Flotante Global de Moderación y Staff (Retirada de StaffBar y Limpieza Contextual)

## Requisitos Funcionales

- **RF-01 (Componente Global `StaffFloatingMenu.razor` en `MainLayout`):**
  - Se renderiza en `MainLayout.razor` exclusivamente si el usuario cuenta con rol de staff (`CurrentUserService.IsFoundingTeam || CurrentUserService.IsInRole("Moderator")`).
  - No emite ningún nodo en el DOM para visitantes o usuarios sin rol staff.
  - Posicionamiento en esquina inferior derecha (`fixed bottom-20 right-4 sm:right-6 z-40`), elevado 80 px para sobrevolar la barra táctil fija móvil `MobileBottomNav` sin solapamiento ni bloqueo táctil.
  - Disparador FAB accesible con icono de staff (`zap`), etiqueta adaptativa ("Staff Tools" en escritorio, "Staff" en móvil) y área táctil >=44×44 px.
  - Tarjeta desplegable flotante con `role="dialog"`, cabecera de rol, lista de acciones contextuales activas y enlace fijo a `/admin`.
  - Cierre automático al ejecutar una acción, al pulsar la tecla `Escape` o al hacer clic fuera del panel.

- **RF-02 (Retirada de la Barra Superior `StaffBar.razor` y `StaffActionBar.razor`):**
  - Se retira `<StaffBar />` de `MainLayout.razor`. La cabecera superior y el inicio de página recuperan su altura editorial limpia sin la barra mostaza fija.

- **RF-03 (Acceso Canónico Permanente a Administración):**
  - El menú flotante incluye siempre en su pie un enlace a `/admin` («Panel de gestión» / Ludeka Gestión) con flecha accesible.
  - Muestra la insignia del rol activo: «Mesa Fundadora» o «Moderador».

- **RF-04 (Consolidación Exhaustiva de la Ficha de Juego — `GameDetail.razor`):**
  - Se retira el panel local `GameStaffToolsPanel.razor`.
  - Se retiran del DOM de `GameDetail.razor`:
    - El botón de sincronización BGG incrustado junto a la foto hero.
    - La caja con borde mostaza incrustada en la sección de veredicto con botones de edición duplicados.
    - Los botones incrustados en la pestaña multimedia ("Hub Multimedia (Moderar)", "Buscar en YouTube").
  - `GameDetail.razor` registra en `IStaffActionService` sus acciones contextuales completas:
    1. `Editar Metadatos` -> abre modal `GameEditorModal`.
    2. `Generar síntesis IA` / `Forzar Resíntesis IA` -> invoca síntesis Gemini.
    3. `Buscar en YouTube` -> abre modal `YouTubeSearchModal` con el título pre-cargado.
    4. `Ingesta Flash de Vídeo` -> abre modal de ingesta por URL.
    5. `Asociar BGG (@Game.BggId)` -> abre modal `AssociateBggModal`.
    6. `Sincronizar BGG (@Game.BggId)` (si BggId > 0) -> invoca sincronización forzada BGG.
    7. `Veredicto Fundador` (para Mesa Fundadora) -> abre modal `FoundingVerdictModal`.
    8. `Cartel para Redes` -> abre modal `SocialCardModal`.
    9. `Incidencias / Erratas` -> abre modal `GameReportModal`.

- **RF-05 (Matriz Contextual Completa en Listados y Hubs):**
  - **Sorteos (`/sorteos` — `Radar.razor`):**
    - Publica en `IStaffActionService`:
      1. `+ Alta exprés de sorteo` (abre modal `SocialExpressIngestModal`).
      2. `+ Proponer / Crear sorteo` (abre modal manual `EditorialModal`).
      3. `Bandeja de ingesta social` (enlace a `/admin/ingesta-social`).
    - Se retiran los botones `✎` incrustados en cada tarjeta del listado para dejar las tarjetas públicas limpias.
  - **Eventos (`/eventos` — `Events.razor`):**
    - Publica en `IStaffActionService`:
      1. `+ Alta exprés de evento` (abre modal `SocialExpressIngestModal`).
      2. `Gestión de eventos` (enlace a `/admin/eventos`).
    - Se retiran los botones de staff incrustados en la cabecera y en el estado vacío.
  - **Novedades (`/novedades` — `News.razor`):**
    - Publica en `IStaffActionService`:
      1. `+ Alta exprés de novedad` (abre modal `SocialExpressIngestModal`).
      2. `+ Publicar novedad` (abre modal manual `_isCreateModalOpen`).
      3. `Sincronizar editoriales` (invoca `HandleSyncEditorialReleases`).
      4. `Bandeja pendientes` (activa filtro de moderación pendiente).
      5. `Ingesta social` (enlace a `/admin/ingesta-social`).
    - Se retiran los botones de moderación incrustados en la cabecera de la sección.
  - **Catálogo (`/catalogo` — `Home.razor`):**
    - `+ Añadir juego desde BGG` (abre modal `BggSearchModal`).
    - `Cola de catalogación BGG` (enlace a `/admin/cola-catalogacion`).
    - `Reportes e incidencias` (enlace a `/admin/reportes`).
  - **Directorios (`/creadores`, `/editoriales`, `/tiendas`):**
    - `+ Nuevo Creador / Editorial / Tienda` (abre modal de creación respectivo).
    - `Sincronizar padrón` (para Mesa Fundadora).
    - `Canales monitorizados` (en creadores/editoriales) o `Feeds y afiliados` (en tiendas).
    - Se retiran los botones administrativos incrustados en cabecera y tarjetas de los directorios.
  - **Portada (`/` — `HomeDashboard.razor`):**
    - `Editar destacados de portada` (abre modal `_isFeaturedModalOpen`).

- **RF-06 (Matriz Contextual en Fichas de Detalle Individuales):**
  - **Ficha de Sorteo (`/sorteos/{id}` — `GiveawayDetail.razor`):**
    - `Editar sorteo` (abre modal de edición de sorteo y carátula R2).
    - `Eliminar sorteo` (destructivo con confirmación).
    - Se retira el botón incrustado "Cambiar o subir carátula" de la tarjeta.
  - **Ficha de Novedad (`/novedades/{id}` — `ReleaseDetail.razor`):**
    - `Editar novedad` (abre modal de edición de lanzamiento).
    - `Crear post Instagram` (enlace a `/admin/instagram`).
    - `Eliminar novedad` (destructivo con confirmación).
    - Se retiran los botones administrativos incrustados en la cabecera y en la sección de noticia.
  - **Ficha de Evento (`/eventos/{slug}` — `EventDetail.razor`):**
    - `Editar evento` (abre modal de edición de datos y cartel).
    - `Eliminar evento` (destructivo con confirmación).
    - `Panel de gestión` (enlace a `/admin/eventos`).
    - Se retiran los botones administrativos incrustados en la cabecera y tarjeta del cartel.
  - **Fichas de Directorio (`/creadores/{slug}`, `/editoriales/{slug}`, `/tiendas/{slug}`):**
    - `Editar ficha` (abre modal `CreatorEditModal`, `PublisherEditModal`, `StoreEditModal`).
    - Se retira el botón "Editar" incrustado en el hero de la ficha.

## Requisitos No Funcionales

- **RNF-01 (Accesibilidad WCAG 2.2 AA):**
  - Área táctil del botón flotante >=44×44 px.
  - Contenedor con `role="dialog"`, `aria-label="Panel rápido de moderación y gestión"` y `aria-expanded`.
  - Cierre accesible con tecla `Escape` y restablecimiento seguro del foco.
- **RNF-02 (Mobile Safe-Area y No Solapamiento):**
  - Offset `bottom-20` (80 px) para elevar el botón por encima de `MobileBottomNav` en móvil.
  - Respeto del safe-area-inset de iOS (`env(safe-area-inset-bottom)`).
- **RNF-03 (Cero Regresiones y Resiliencia en Blazor Circuit):**
  - Toda la suite de 2.795+ pruebas unitarias debe permanecer en verde.
  - Nuevas pruebas de contrato de marcado web y de servicio para certificar la presencia/ausencia del FAB y el registro de acciones.
