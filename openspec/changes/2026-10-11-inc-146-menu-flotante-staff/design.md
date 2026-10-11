# Diseño Técnico: INC-146 Menú Flotante Global de Moderación y Staff (Retirada de StaffBar y Limpieza Contextual)

## 1. Arquitectura y Componentes Afectados

```
┌────────────────────────────────────────────────────────┐
│  MainLayout.razor (Ludeka.Web)                         │
│  - Retirada de <StaffBar /> bajo la cabecera           │
│  - Inclusión de <StaffFloatingMenu /> global           │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│  StaffFloatingMenu.razor (Ludeka.Web/Components/Shared)│
│  - Botón flotante accesible (fixed bottom-20 right-4)   │
│  - Tarjeta desplegable con acciones contextuales       │
│  - Consumo reactivo de IStaffActionService             │
│  - Fallback a acciones canónicas por ruta              │
│  - Enlace permanente a /admin («Panel de gestión»)     │
└──────────────────────────▲─────────────────────────────┘
                           │ Publica acciones contextuales
┌──────────────────────────┴─────────────────────────────┐
│  Vistas y Fichas de Detalle                            │
│  - GameDetail.razor: 9 acciones (Editor, IA, YouTube,  │
│    Ingesta Flash, BGG, Veredicto, Cartel, Erratas)     │
│  - Radar.razor: Alta Exprés, Crear sorteo, Ingesta     │
│  - Events.razor / EventDetail.razor: Alta Exprés,      │
│    Editar evento, Eliminar, Gestión                    │
│  - News.razor / ReleaseDetail.razor: Alta Exprés,      │
│    Publicar, Sincronizar editoriales, Editar, IG       │
│  - Home.razor: Añadir BGG, Cola BGG, Reportes          │
│  - Directorios y Fichas: Nuevo, Sincronizar, Editar    │
│  - HomeDashboard.razor: Editar destacados              │
└────────────────────────────────────────────────────────┘
```

## 2. Decisiones de Diseño Detalladas

### A. Componente `StaffFloatingMenu.razor`
- **Ubicación:** `src/Ludeka.Web/Components/Shared/StaffFloatingMenu.razor`.
- **Condición de renderizado:**
  ```csharp
  private bool IsStaff => CurrentUserService.IsFoundingTeam || CurrentUserService.IsInRole("Moderator");
  ```
  Si `!IsStaff`, el componente retorna inmediatamente sin renderizar ningún nodo DOM.
- **Disparador Flotante (FAB):**
  - Posicionamiento: `fixed bottom-20 right-4 sm:right-6 z-40 flex flex-col items-end`.
  - El offset `bottom-20` (5rem / 80px) sitúa el botón flotante claramente por encima de `MobileBottomNav` (altura 4rem) en dispositivos móviles.
  - Estilo: Píldora con fondo `var(--inverse)` (`#22180F`), texto `var(--ticker)`, borde de 2px en `var(--mustard)`, sombra `shadow-2xl` y microinteracción `hover:scale-105 active:scale-95`.
  - Icono Lucide `zap` (14px) y texto "Staff Tools" en escritorio / "Staff" en móvil.
- **Tarjeta Desplegable (`_isOpen`):**
  - Contenedor flotante `w-72 sm:w-80 rounded-3xl bg-[var(--card)] border-2 border-[var(--rule)] p-4 shadow-2xl space-y-3 animate-fade-in`.
  - Atributos accesibles: `role="dialog"`, `aria-label="Panel rápido de moderación y gestión"`.
  - Cabecera:
    - Badge con icono `zap` y texto `Herramientas Staff`.
    - Píldora con el rol: `Mesa Fundadora` o `Moderador`.
    - Botón de cierre `&times;` con `aria-label="Cerrar panel"`.
  - Cuerpo de Acciones:
    - Si `StaffActionService.CustomActions.Count > 0`, renderiza las acciones registradas activamente por la página en curso.
    - Si la lista está vacía, evalúa `IsCurrentPage(...)` para proporcionar atajos canónicos por defecto (Catálogo, Sorteos, Eventos, Novedades, Tiendas, Directorios).
  - Pie de Tarjeta:
    - Enlace destacado permanente a `/admin` («Panel de gestión») con flecha accesible.

### B. Consolidación de la Ficha de Juego (`GameDetail.razor`)
1. **Retirada de `GameStaffToolsPanel.razor`:** Se elimina el componente local redundante.
2. **Retirada de elementos incrustados en la vista:**
   - Botón "Sincronizar" BGG al lado del contador de fotos hero.
   - Caja con borde mostaza y botones "Generar síntesis IA", "Editar veredicto", "Editar Ficha" en la sección de veredicto.
   - Botones "Hub Multimedia (Moderar)" y "Buscar en YouTube" en la sección de vídeos.
3. **Registro completo en `IStaffActionService`:**
   - `Editar Metadatos` -> `_isEditorModalOpen = true`
   - `Generar síntesis IA` / `Forzar Resíntesis IA` -> `HandleRequestAiSummary`
   - `Buscar en YouTube` -> abre `YouTubeSearchModal`
   - `Ingesta Flash de Vídeo` -> abre modal de ingesta de vídeo por URL
   - `Asociar BGG (@Game.BggId)` -> `_isAssociateBggModalOpen = true`
   - `Sincronizar BGG (@Game.BggId)` -> `HandleForceSyncBgg` (si BggId > 0)
   - `Veredicto Fundador` -> `_isFoundingModalOpen = true` (si `CurrentUserService.IsFoundingTeam`)
   - `Cartel para Redes` -> `OpenSocialCardModal()`
   - `Incidencias / Erratas` -> `_isReportModalOpen = true`
4. **Ciclo de vida:** Limpieza estricta en `Dispose()` mediante `StaffActionService.ClearCustomActions()`.

### C. Consolidación en Sorteos (`Radar.razor` y `GiveawayDetail.razor`)
1. **`Radar.razor`:**
   - Registra en `IStaffActionService`:
     - `+ Alta exprés de sorteo` (`_isExpressModalOpen = true`)
     - `+ Nuevo sorteo manual` (`OpenCreateGiveawayModal()`)
     - `Bandeja de ingesta social` (enlace a `/admin/ingesta-social`)
   - Se retiran los botones de lápiz `✎` en cada tarjeta de sorteo.
2. **`GiveawayDetail.razor`:**
   - Registra en `IStaffActionService`:
     - `Editar sorteo` (`OpenEditModal()`)
     - `Eliminar sorteo` (`_isDeleteConfirmOpen = true`, destructivo)
   - Se retira el botón incrustado "Cambiar o subir carátula" de la tarjeta.

### D. Consolidación en Eventos (`Events.razor` y `EventDetail.razor`)
1. **`Events.razor`:**
   - Registra en `IStaffActionService`:
     - `+ Alta exprés de evento` (`_isExpressModalOpen = true`)
     - `Gestión de eventos` (enlace a `/admin/eventos`)
   - Se retiran los botones administrativos incrustados de la cabecera y del estado vacío.
2. **`EventDetail.razor`:**
   - Registra en `IStaffActionService`:
     - `Editar evento` (`OpenEditModal()`)
     - `Eliminar evento` (`_isDeleteConfirmOpen = true`, destructivo)
     - `Panel de gestión` (enlace a `/admin/eventos`)
   - Se retiran los botones incrustados en la cabecera y en el cartel.

### E. Consolidación en Novedades (`News.razor` y `ReleaseDetail.razor`)
1. **`News.razor`:**
   - Registra en `IStaffActionService`:
     - `+ Alta exprés de novedad` (`_isExpressModalOpen = true`)
     - `+ Publicar novedad` (`_isCreateModalOpen = true`)
     - `Sincronizar editoriales` (`HandleSyncEditorialReleases`)
     - `Bandeja de pendientes` (`SetFilter("pending")`)
     - `Ingesta social` (enlace a `/admin/ingesta-social`)
   - Se retiran los botones administrativos de la cabecera.
2. **`ReleaseDetail.razor`:**
   - Registra en `IStaffActionService`:
     - `Editar novedad` (`OpenEditModal()`)
     - `Crear post Instagram` (enlace a `/admin/instagram...`)
     - `Eliminar novedad` (`_isDeleteConfirmOpen = true`, destructivo)
   - Se retiran los botones administrativos incrustados en la vista y en la sección de enlace oficial.

### F. Consolidación en Catálogo y Directorios
1. **`Home.razor` (`/catalogo`):**
   - Ofrece a través del menú flotante: `+ Añadir juego desde BGG`, `Cola BGG & Catalogación` y `Reportes de Catálogo`.
   - Se aloja o vincula `BggSearchModal` para activarse fluidamente.
2. **Directorios (`CreatorsDirectory`, `PublishersDirectory`, `StoresDirectory`):**
   - Registran en el menú flotante: `+ Nuevo [Creador/Editorial/Tienda]`, `Sincronizar padrón` (Mesa Fundadora) y enlaces de gestión.
   - Se retiran los botones incrustados en las cabeceras y en las tarjetas individuales.
3. **Fichas de Directorio (`CreatorDetail`, `PublisherDetail`, `StoreDetail`):**
   - Registran la acción `Editar` en `IStaffActionService` y retiran el botón incrustado en el hero.

## 3. Matriz Resumen de Acciones en Menú Flotante

| Vista / Pantalla | Acciones Contextuales en el Menú Flotante | Elementos Limpiados del DOM |
|---|---|---|
| **Ficha de Juego** (`/juego/{slug}`) | `Editar Metadatos`, `Síntesis IA`, `Buscar en YouTube`, `Ingesta Flash`, `Asociar BGG`, `Sincronizar BGG`, `Veredicto Fundador`, `Cartel Redes`, `Erratas` | `GameStaffToolsPanel`, botón BGG en foto hero, caja mostaza en veredicto, botones en multimedia |
| **Sorteos** (`/sorteos`) | `+ Alta exprés de sorteo`, `+ Nuevo sorteo manual`, `Bandeja de ingesta social` | Botones `✎` en tarjetas de listado |
| **Ficha Sorteo** (`/sorteos/{id}`) | `Editar sorteo`, `Eliminar sorteo` | Botón "Cambiar o subir carátula" |
| **Eventos** (`/eventos`) | `+ Alta exprés de evento`, `Gestión de eventos` | Botones de cabecera y estado vacío |
| **Ficha Evento** (`/eventos/{slug}`) | `Editar evento`, `Eliminar evento`, `Gestión de eventos` | Botones de cabecera y pie de cartel |
| **Novedades** (`/novedades`) | `+ Alta exprés de novedad`, `+ Publicar novedad`, `Sincronizar editoriales`, `Bandeja pendientes`, `Ingesta social` | Botones de cabecera |
| **Ficha Novedad** (`/novedades/{id}`) | `Editar novedad`, `Crear post Instagram`, `Eliminar novedad` | Botones de cabecera y enlace oficial |
| **Catálogo** (`/catalogo`) | `+ Añadir juego desde BGG`, `Cola BGG & Catalogación`, `Reportes de catálogo` | Sustituye la antigua `StaffBar` |
| **Directorios** (Creadores, Editoriales, Tiendas) | `+ Registrar nuevo`, `Sincronizar padrón`, `Panel de canales / feeds` | Botones de cabecera y lápices en tarjetas |
| **Fichas de Directorio** | `Editar datos de ficha`, `Ver canales / feeds` | Botón "Editar" en hero |
| **Portada** (`/`) | `Editar destacados de portada` | Sustituye la antigua `StaffBar` |
