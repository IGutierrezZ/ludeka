# Especificación de Requerimientos: change-22-draws-news-events-split

## 1. Introducción y Propósito
El propósito del Incremento 22 es disolver el concepto monolítico de "Radar" (`/radar`) en la arquitectura y experiencia de usuario de Ludeka, segregando sus responsabilidades en tres verticales independientes y especializadas:
1. **Sorteos (`/sorteos`):** Radar de sorteos activos y finalizados con gobernanza de sorteos destacados/patrocinados (`IsPromoted`), expiración automática y soporte de imagen.
2. **Novedades (`/novedades`):** Línea temporal y muro de anuncios, lanzamientos editoriales y primicias comerciales de tiendas (`WeeklyRelease`).
3. **Eventos Lúdicos (`/eventos` y `/admin/eventos`):** Gran calendario y directorio de las grandes citas, ferias y festivales del sector de juegos de mesa en España y el mundo (Festival de Córdoba, InterOcio, Essen SPIEL, Gen Con, DAU Barcelona), con panel editorial de administración.
4. **Reestructuración de la Navegación:** Retirar el acceso a `/radar` del menú principal y footer de `MainLayout.razor`, incorporando accesos limpios a `/sorteos`, `/novedades` y `/eventos`, preservando `/radar` como alias retrocompatible.

---

## 2. Requerimientos Funcionales (RF)

### RF-1: Reestructuración de Rutas y Menú de Navegación
- **RF-1.1:** En la barra superior (`MainLayout.razor`), se sustituye el enlace monolítico `Radar` (`/radar`) por enlaces diferenciados:
  - `🎁 Sorteos` apuntando a `/sorteos`.
  - `📰 Novedades` apuntando a `/novedades`.
  - `🎪 Eventos` apuntando a `/eventos`.
- **RF-1.2:** En el pie de página (`footer` de `MainLayout.razor`), se sustituye `📡 Radar` por los tres enlaces individuales (`/sorteos`, `/novedades`, `/eventos`).
- **RF-1.3:** La ruta `/radar` se mantendrá operativa como alias que renderiza la vista de sorteos e incluye enlaces contextuales directos a novedades y eventos para preservar enlaces externos o marcadores de usuarios.

### RF-2: Vertical Especializada de Sorteos (`/sorteos`)
- **RF-2.1:** La página `/sorteos` renderiza exclusivamente sorteos comunitarios y promocionados.
- **RF-2.2:** Filtro por estado: permite alternar entre "Vigentes" (`DeadlineAt >= Ahora`) y "Todos / Finalizados".
- **RF-2.3: Gobernanza de Sorteos Promocionados (`IsPromoted`):**
  - Los sorteos con `IsPromoted == true` se muestran con un badge distintivo "⭐ Promocionado" y fondo sutilmente destacado.
  - Orden de presentación: los sorteos promocionados se sitúan en cabecera de lista ordenados por proximidad de fin; seguidos de los sorteos estándar ordenados por proximidad de fin.
  - Los usuarios con roles `FoundingTeam` o `Moderator` disponen de un botón/control en cada tarjeta para conmutar `IsPromoted` en 1 clic.
  - En el modal de proponer/crear sorteo, los usuarios con roles `FoundingTeam` o `Moderator` pueden marcar la casilla "Promocionado".
- **RF-2.4: Soporte de Imagen:** Se muestra la carátula/imagen original de Instagram o la imagen subida/alojada, con fallback a imagen por defecto si no existe.

### RF-3: Vertical Especializada de Novedades (`/novedades`)
- **RF-3.1:** La página `/novedades` muestra el catálogo de lanzamientos y primicias editoriales (`WeeklyRelease`) ordenadas cronológicamente (`ReleaseDate` descendente).
- **RF-3.2:** Cada tarjeta editorial incluye: carátula visual con esquinas redondeadas, título del juego, editorial/distribuidor, fecha de lanzamiento, PVP estimado (si está disponible) e indicador de "🆕 Novedad" o "🔄 Reimpresión".
- **RF-3.3: Filtro por Editorial:** Permite filtrar por editorial emisora o buscar por título de lanzamiento.
- **RF-3.4: Alta Manual de Novedades (`CreateWeeklyRelease`):**
  - Botón visible para `FoundingTeam` y `Moderator`: `[ ➕ Añadir Novedad ]`.
  - Modal accesible para ingresar título, editorial, fecha, PVP estimado, reedición, notas y URL de carátula o archivo subido.

### RF-4: Vertical de Grandes Citas y Festivales (`/eventos`)
- **RF-4.1:** La página `/eventos` muestra el calendario oficial de ferias, festivales y macro-eventos lúdicos (`BoardGameEvent`), excluyendo torneos locales menores.
- **RF-4.2: Pestañas de Navegación Temporal:**
  - **Pestaña "Próximas Citas":** Eventos vigentes o futuros (`EndDate >= Hoy`), ordenados por fecha de inicio más cercana (`StartDate` ascendente).
  - **Pestaña "Histórico de Eventos":** Eventos ya concluidos (`EndDate < Hoy`), ordenados por fecha de fin más reciente (`EndDate` descendente).
- **RF-4.3: Tarjeta Visual Editorial:**
  - Cartel panorámico/vertical con esquinas redondeadas.
  - Badge dinámico de situación: "🔴 En curso", "🟢 En X días" o "Concluido".
  - Fechas formateadas en español (ej. "11-13 Oct 2026").
  - Ciudad y recinto (ej. "Córdoba — Palacio de la Merced").
  - Organizador y distintivo de evento oficial.
  - Botón saliente directo hacia la web oficial del evento (`WebsiteUrl`) con `target="_blank" rel="noopener noreferrer"`.
- **RF-4.4: Enlace de Moderación:** Si el usuario es moderador o de la Mesa Fundadora, se muestra un botón para acceder a la gestión de eventos (`/admin/eventos`).

### RF-5: Panel Editorial de Administración de Eventos (`/admin/eventos`)
- **RF-5.1:** Ruta restringida a usuarios con rol `FoundingTeam` o `Moderator`.
- **RF-5.2:** Listado de eventos con acciones de Crear, Editar, Eliminar y Subir Cartel.
- **RF-5.3:** Formulario modal para ingresar y actualizar título, descripción, ubicación, fechas (inicio y fin con validación `EndDate >= StartDate`), organizador, web oficial y subida o enlace de imagen.

### RF-6: Almacenamiento Físico de Imágenes (`IImageStorageService`)
- **RF-6.1:** Extender `IImageStorageService` para persistir ficheros en carpetas organizadas (`wwwroot/images/events/`, `wwwroot/images/draws/`, `wwwroot/images/releases/`).
- **RF-6.2:** Validaciones estrictas: extensiones admitidas (.jpg, .jpeg, .png, .webp), tamaño máximo <= 5 MB y saneamiento de nombres de archivo.

---

## 3. Requerimientos No Funcionales (RNF)
- **RNF-1: Rendimiento:** Carga instantánea con SSR y consultas indexadas por fecha.
- **RNF-2: Accesibilidad WCAG 2.2 AA:** Semántica estricta (`aria-selected`, `role="tab"`, contrastes altos, soporte de teclado en modales y tarjetas).
- **RNF-3: Resiliencia de Esquema:** Compatibilidad defensiva sin romper bases de datos SQLite preexistentes.

---

## 4. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Segregación de menús en la navegación principal
  Dado un usuario que navega por Ludeka
  Cuando inspecciona el menú principal y el pie de página
  Entonces no visualiza la sección genérica "Radar"
  Y visualiza accesos independientes para "Sorteos", "Novedades" y "Eventos"

Escenario: Consulta del calendario de eventos lúdicos
  Dado que existen los eventos "Festival de Córdoba" (Octubre) e "InterOcio" (Marzo siguiente)
  Cuando el usuario accede a "/eventos"
  Entonces los eventos aparecen ordenados por fecha de inicio más cercana
  Y cada tarjeta muestra el cartel, las fechas de celebración y el enlace oficial externo

Escenario: Marcado de sorteo promocionado por un moderador
  Dado un moderador autenticado en "/sorteos"
  Cuando pulsa el botón para marcar como promocionado un sorteo
  Entonces el sorteo adquiere "IsPromoted = true"
  Y pasa a figurar en la cabecera de la lista con el distintivo "⭐ Promocionado"
  Pero un usuario regular no dispone del botón de moderación para conmutar "IsPromoted"

Escenario: Creación de novedad editorial manual
  Dado un moderador en la sección "/novedades"
  Cuando completa el formulario de nueva novedad con título "Terraforming Mars: Preludio 2", editorial "Maldito Games" y fecha
  Entonces la novedad queda registrada y se muestra en el listado cronológico de novedades
```
