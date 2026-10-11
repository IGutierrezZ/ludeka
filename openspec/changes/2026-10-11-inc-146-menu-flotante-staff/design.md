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
│  - GameDetail.razor: 7 acciones (Editor, IA, BGG, etc.)│
│  - GiveawayDetail.razor: Editar sorteo                 │
│  - Eventos, Novedades, Catálogo, Directorios:          │
│    Limpieza de botones DOM y atajos centralizados      │
└────────────────────────────────────────────────────────┘
```

## 2. Decisiones de Diseño

### A. Componente `StaffFloatingMenu.razor`
- **Ubicación:** `src/Ludeka.Web/Components/Shared/StaffFloatingMenu.razor`.
- **Condición de renderizado:**
  ```csharp
  private bool IsStaff => CurrentUserService.IsFoundingTeam || CurrentUserService.IsInRole("Moderator");
  ```
  Si `!IsStaff`, el componente retorna inmediatamente sin renderizar ningún nodo DOM.
- **Disparador Flotante (FAB):**
  - Posicionamiento: `fixed bottom-20 right-4 sm:right-6 z-40 flex flex-col items-end`.
  - El offset `bottom-20` (5rem / 80px) asegura que en vista móvil el botón quede elevado por encima de la barra táctil fija `MobileBottomNav` (que ocupa `h-16` / 4rem con safe-area).
  - Estilo: Píldora con fondo `var(--inverse)` (`#22180F`), texto `var(--ticker)`, borde de 2px en `var(--mustard)`, sombra `shadow-2xl` y microinteracción `hover:scale-105 active:scale-95`.
  - Icono Lucide `zap` (14px) y texto "Staff Tools" en `sm:inline` / "Staff" en móvil.
- **Tarjeta Desplegable (`_isOpen`):**
  - Contenedor flotante `w-72 sm:w-80 rounded-3xl bg-[var(--card)] border-2 border-[var(--rule)] p-4 shadow-2xl space-y-3 animate-fade-in`.
  - Atributos accesibles: `role="dialog"`, `aria-label="Panel rápido de moderación y gestión"`.
  - Cabecera:
    - Badge con icono `zap` y texto `Herramientas Staff`.
    - Píldora con el rol: `Mesa Fundadora` o `Moderador`.
    - Botón de cierre `&times;` con `aria-label="Cerrar panel"`.
  - Cuerpo de Acciones:
    - **Caso 1:** Si `StaffActionService.CustomActions.Count > 0`, se listan las acciones publicadas activamente por la página en curso.
    - **Caso 2:** Si la lista está vacía, se evalúa la ruta (`IsCurrentPage(...)`) para ofrecer los atajos canónicos:
      - Catálogo (`/catalogo`): `+ Añadir juego desde BGG`, `Cola BGG`, `Reportes`.
      - Sorteos (`/sorteos`): `Ingesta Social`.
      - Eventos (`/eventos`): `Gestión de Eventos`.
      - Novedades (`/novedades`): `Ingesta Social`.
      - Tiendas (`/tiendas`): `Feeds & Afiliados`.
      - Directorios (`/creadores`, `/editoriales`): `Canales Monitorizados`.
  - Pie de Tarjeta:
    - Enlace destacado permanente a `/admin` («Panel de gestión») con flecha accesible.
- **Interacción y Accesibilidad:**
  - Cierre automático al hacer clic en cualquiera de las acciones.
  - Atajo de teclado: `onkeydown` para cerrar con `Escape`.
  - Cierre con backdrop transparente al hacer clic fuera del panel.

### B. Sustitución en `MainLayout.razor`
- Se retira `<StaffBar />` bajo el header.
- Se añade `<StaffFloatingMenu />` como componente global compartido en la raíz del layout, justo antes del cierre del contenedor principal.
- `StaffBar.razor` se retira o se redirige hacia `StaffFloatingMenu.razor`.

### C. Desacople y Consolidación de `GameDetail.razor`
- Se elimina el componente local redundante `GameStaffToolsPanel.razor`.
- `GameDetail.razor` inyecta `IStaffActionService` y registra sus acciones en un método privado `RegisterStaffActions()` invocado en el ciclo de vida:
  ```csharp
  private void RegisterStaffActions()
  {
      if (!IsStaff) return;

      var actions = new List<StaffActionItem>
      {
          new("Editar Metadatos", () => { _isEditorModalOpen = true; StateHasChanged(); return Task.CompletedTask; }, Icon: "pen-line"),
          new(_isGeneratingAiSummary ? "Sintetizando..." : (_aiSummary != null ? "Forzar Resíntesis IA" : "Generar con IA"), HandleRequestAiSummary, Icon: "bot"),
          new($"Asociar BGG ({(Game?.BggId ?? 0)})", () => { _isAssociateBggModalOpen = true; StateHasChanged(); return Task.CompletedTask; }, Icon: "globe")
      };

      if (Game?.BggId > 0)
      {
          actions.Add(new($"Sincronizar BGG ({Game.BggId})", HandleForceSyncBgg, Icon: "refresh-cw"));
      }

      if (CurrentUserService.IsFoundingTeam)
      {
          actions.Add(new("Veredicto Fundador", () => { _isFoundingModalOpen = true; StateHasChanged(); return Task.CompletedTask; }, Icon: "award"));
      }

      actions.Add(new("Cartel para Redes", () => { OpenSocialCardModal(); StateHasChanged(); return Task.CompletedTask; }, Icon: "palette"));
      actions.Add(new("Incidencias / Erratas", () => { _isReportModalOpen = true; StateHasChanged(); return Task.CompletedTask; }, Icon: "flag"));

      StaffActionService.SetCustomActions(actions);
  }
  ```
- `GameDetail.razor` implementa `IDisposable` y en `Dispose()` ejecuta `StaffActionService.ClearCustomActions()`.

### D. Ficha de Sorteos (`GiveawayDetail.razor`)
- Se inyecta `IStaffActionService`.
- Si el usuario tiene permisos de moderación (`CanModerate`), publica la acción contextual:
  - `Editar Sorteo` (icono `pen-line`), que activa `_isEditModalOpen = true`.
- Implementa `Dispose()` para limpiar las acciones registradas al salir.

### E. Limpieza de Interfaz Editorial en Listados y Tarjetas
- En `Events.razor`, `News.razor`, `Radar.razor`, etc., se retiran botones con `@if (IsModerator)` o enlaces directos a `/admin/...` incrustados en la vista, ya que ahora están permanentemente accesibles en el botón flotante contextual.

## 3. Plan de Pruebas y Validación (TDD)
1. **Pruebas de Contrato de Marcado (`WebMarkupContractTests.cs` o suite dedicada):**
   - Validar que `MainLayout` ya no renderiza la barra `StaffBar` (región `role="region"` con `aria-label="Barra de gestión staff"`).
   - Validar que `StaffFloatingMenu` se renderiza para usuarios con rol `Moderator` o `FoundingTeam`.
   - Validar que `StaffFloatingMenu` no se renderiza en absoluto para usuarios sin rol staff o anónimos.
2. **Pruebas de `IStaffActionService`:**
   - Registro reactivo, notificación por evento `OnActionsChanged` y limpieza con `ClearCustomActions()`.
3. **Pruebas Unitarias de `StaffFloatingMenu` (bUnit / ComponentTests):**
   - Apertura y cierre del diálogo al pulsar el botón FAB.
   - Presencia de acciones personalizadas cuando `CustomActions` está poblado.
   - Presencia de acciones canónicas por defecto según ruta cuando `CustomActions` está vacío.
   - Enlace canónico visible hacia `/admin`.
