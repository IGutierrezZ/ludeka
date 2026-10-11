# Propuesta: INC-146 — Menú Flotante Global de Moderación y Staff (Retirada de StaffBar y Limpieza Contextual)

## Intención
Unificar toda la experiencia de moderación y administración en Ludeka a través de un único botón flotante interactivo (FAB) global en escritorio y móvil, retirando la barra superior fija `StaffBar` y eliminando la contaminación visual de botones administrativos dispersos en las pantallas editoriales. Todas las opciones de moderación y accesos directos de gestión se agrupan en este menú contextual no intrusivo y accesible (WCAG 2.2 AA).

## Alcance

### Dentro del Alcance
- **Componente Global `StaffFloatingMenu.razor`:**
  - Sustituir `StaffBar.razor` en `MainLayout.razor` por un componente flotante en la esquina inferior derecha (`fixed bottom-20 right-4 sm:right-6 z-40`).
  - Visibilidad condicionada a roles de staff (`IsFoundingTeam` o `Moderator`).
  - Adaptabilidad responsive móvil/escritorio sin solapar la barra fija inferior táctil (`MobileBottomNav`) ni el botón de ayuda/scroll.
  - Cumplimiento estricto de WCAG 2.2 AA: foco accesible, tecla `Escape`, atributos `aria-haspopup="dialog"`, `aria-expanded`, etiquetas accesibles.
- **Evolución del Servicio Mediador `IStaffActionService`:**
  - Soporte para acciones contextuales dinámicas publicadas por la pantalla activa.
  - Soporte para metadatos enriquecidos de acción (icono, etiqueta, variante destructiva, manejador asíncrono, categoría o prioridad).
  - Acciones por defecto contextuales basadas en la ruta si la página no publica acciones personalizadas.
  - Acceso directo permanente al panel central de administración (`/admin` — Ludeka Gestión).
- **Matriz de Acciones Contextuales por Pantalla:**
  - **Catálogo (`/catalogo`):** `+ Añadir desde BGG`, `Cola de catalogación`, `Incidencias y reportes`.
  - **Sorteos (`/sorteos`):** `+ Alta exprés de sorteo`, `Bandeja de ingesta social`.
  - **Eventos (`/eventos`):** `+ Crear evento`, `Panel de gestión de eventos`.
  - **Novedades (`/novedades`):** `+ Registrar lanzamiento`, `Ingesta social`.
  - **Directorios (`/creadores`, `/editoriales`, `/tiendas`):** `+ Registrar entidad`, `Sincronizar directorio`.
  - **Ficha de Juego (`/juego/{slug}`):** Integrar las opciones de `GameStaffToolsPanel` (`Editar metadatos`, `Síntesis IA`, `Asociar BGG`, `Sincronizar BGG`, `Veredicto fundador`, `Cartel redes`, `Reportes`).
  - **Ficha de Sorteo (`/sorteos/{id}`):** `Editar sorteo`, `Subir carátula R2`, `Moderar estado`.
  - **Ficha de Evento (`/eventos/{slug}`):** `Editar evento`, `Cambiar visibilidad/fechas`.
  - **Fichas de Directorio:** `Editar ficha`, `Verificar enlaces/redes`, `Feeds y afiliados`.
- **Limpieza de Interfaz Editorial:**
  - Retirar la barra superior `StaffBar.razor`.
  - Retirar `GameStaffToolsPanel.razor` como componente local redundante en `GameDetail.razor`, delegando en el menú flotante global.
  - Eliminar botones de staff incrustados en tarjetas y cabeceras que ensuciaban la experiencia de usuario general.

### Fuera del Alcance
- Modificar el sistema de permisos de usuario (`ModeratorPermission`) o esquemas de base de datos.
- Modificar la lógica interna de los modales de edición (los modales existentes se reutilizan activándolos desde el menú flotante).
- Rediseñar el panel interno `/admin` (Ludeka Gestión).

## Capacidades

### Nuevas Capacidades
- `staff-floating-menu`: Componente Blazor reactivo global para invocar herramientas de moderación y administración desde cualquier punto de la aplicación en escritorio y móvil.
- `staff-contextual-actions`: Matriz estandarizada de registro y consumo de acciones contextuales por pantalla vía `IStaffActionService`.

### Capacidades Modificadas
- `main-layout`: Sustitución de `StaffBar` por `StaffFloatingMenu`.
- `game-detail`: Retirada de `GameStaffToolsPanel` y publicación de acciones al servicio global.
- `events`, `news`, `radar`: Supresión de botones administrativos dispersos en favor de acciones registradas en el botón flotante.

## Enfoque Arquitectónico
- **Capa de Aplicación (`Ludeka.Application`)**:
  - `IStaffActionService` y `StaffActionItem`: Ampliar si es necesario para dar soporte a agrupaciones, iconos Lucide y estados reactivos limpios.
- **Capa Web / Interfaz (`Ludeka.Web`)**:
  - Nuevo componente `StaffFloatingMenu.razor` alojado en `MainLayout.razor`.
  - Renderizado condicional basado en `ICurrentUserService.IsFoundingTeam || ICurrentUserService.IsInRole("Moderator")`.
  - Tarjeta desplegable flotante con transiciones suaves, cabecera de rol, listado de acciones contextuales ordenadas y enlace a `/admin`.
  - Coordinación de modales contextuales en páginas de detalle mediante `EventCallback` y delegados en `StaffActionItem`.

## Áreas Afectadas
| Área | Impacto | Descripción |
|---|---|---|
| `Ludeka.Application/Contracts` | Bajo | Extensión de `IStaffActionService` / `StaffActionItem` |
| `Ludeka.Web/Components/Layout/MainLayout.razor` | Medio | Sustitución de `StaffBar` por `StaffFloatingMenu` |
| `Ludeka.Web/Components/Shared/` | Alto | Creación de `StaffFloatingMenu.razor`, retirada de `StaffBar.razor` y `GameStaffToolsPanel.razor` |
| `Ludeka.Web/Components/Pages/` | Alto | Publicación de acciones en `GameDetail`, `Events`, `News`, `Radar`, `GiveawayDetail`, etc., y limpieza de botones DOM |
| `tests/Ludeka.UnitTests/` | Medio | Pruebas de contrato de marcado web, visibilidad para staff/visitante y eventos de `IStaffActionService` |

## Riesgos y Mitigaciones
| Riesgo | Severidad | Mitigación |
|---|---|---|
| Solapamiento del botón flotante en móvil con la barra `MobileBottomNav` | Media | Posicionar con offset explícito (`bottom-20`) y respetar safe-area en iOS (`env(safe-area-inset-bottom)`) |
| Fuga de memoria o desincronización de eventos en Blazor Circuit | Baja | Implementar `IDisposable` en páginas que registren acciones en `IStaffActionService` para limpiar al cambiar de ruta |
| Acciones no disponibles en páginas con SSR estático | Baja | El layout y las páginas interactivas ya operan con InteractiveServer / Streaming rendering; `IStaffActionService` es Scoped por circuito |

## Plan de Rollback
- Revertir la rama `inc/menu-flotante-staff`.
- Restaurar `StaffBar.razor` y `GameStaffToolsPanel.razor` desde `main`.
