# Especificación de Requerimientos: change-21-home-dashboard

## 1. Introducción y Propósito
El propósito de este incremento es dotar a Ludeka de una portada editorial viva (`/`) inspirada en aplicaciones modernas de referencia cultural, basada en cuatro carriles con deslizamiento horizontal táctil (*mobile-first*), desacoplando el catálogo completo con filtros facetados hacia la ruta `/catalogo`, limpiando el encabezado de navegación superior y vinculando cada ficha de juego con su página oficial en BoardGameGeek.

---

## 2. Requerimientos Funcionales (RF)

### RF-1: Desacople de Rutas y Navegación Principal
- **RF-1.1:** La ruta raíz (`/`) renderizará exclusivamente el componente `HomeDashboard.razor`.
- **RF-1.2:** La vista exhaustiva de catálogo con filtros de situación real (Parejas, Familiar, Solitario, Rápidas, Base, Expansiones) y paginación se ubicará en `/catalogo`.
- **RF-1.3:** El enlace "Catálogo" de la barra de navegación superior y menús móviles apuntará a `/catalogo`.
- **RF-1.4:** El logotipo del sitio en la barra superior mantendrá el enlace a la portada (`/`).
- **RF-1.5:** Los botones de retorno ("Volver al catálogo") en fichas de juego y páginas de error apuntarán a `/catalogo`.

### RF-2: Dashboard de Inicio Editorial (`HomeDashboard.razor`)
- **RF-2.1: Carril 1 — Top 20 Mejores Juegos:**
  - Mostrará los 20 juegos con mejor posición en el ranking BGG (o valoración global ponderada).
  - Cada tarjeta incluirá: carátula oficial con esquinas redondeadas, título en español, año de publicación, puntuación y semáforo de comensales.
  - Enlace directo a la ficha del juego (`/juegos/{Slug}`).
- **RF-2.2: Carril 2 — Sorteos Activos y Destacados:**
  - Mostrará hasta 20 sorteos vigentes (`DeadlineAt > Now`).
  - Ordenación: sorteos con `IsPromoted == true` en cabecera de carril; a continuación, sorteos ordenados por fecha límite más cercana (`DeadlineAt` ascendente).
  - Cada tarjeta incluirá: carátula/imagen, título del sorteo, organizador/colaborador, badge distintivo si es "⭐ Promocionado" y tiempo restante ("Finaliza en X días").
- **RF-2.3: Carril 3 — Novedades del Sector:**
  - Mostrará hasta 20 lanzamientos y novedades de editoriales y tiendas ordenadas cronológicamente (`ReleaseDate` descendente).
  - Cada tarjeta incluirá: imagen/portada, título del juego, editorial/tienda, etiqueta de "🆕 Novedad" o "🔄 Reimpresión", PVP estimado y fecha.
- **RF-2.4: Carril 4 — Próximos Eventos y Ferias Lúdicas:**
  - Mostrará las grandes citas del circuito de juegos de mesa (`StartDate >= Hoy` o en curso) ordenadas cronológicamente por fecha de inicio (`StartDate` ascendente).
  - Cada tarjeta incluirá: imagen/cartel oficial, título del evento, fechas formateadas (ej. "11-13 Oct 2026"), ciudad/recinto y botón con enlace oficial externo.
- **RF-2.5: Ergonomía Mobile-First y Scroll Snap:**
  - Los 4 carriles contarán con desplazamiento horizontal suave táctil (`overflow-x-auto snap-x snap-mandatory scrollbar-none`), permitiendo visualizar de 2 a 4 tarjetas en móvil con avance natural por gestos.

### RF-3: Limpieza del Navbar Superior
- **RF-3.1:** Eliminar el bloque de botones del selector de temas de colores (Papel, Madera, Mesa, Noche, Carbón) del encabezado (`MainLayout.razor`).
- **RF-3.2:** Mantener la inicialización y persistencia de las preferencias de tema del usuario en segundo plano a través de `IUserPreferenceService` y `localStorage` para que la configuración elegida en el perfil del usuario siga aplicándose en toda la aplicación.

### RF-4: Enlace Canónico a BoardGameGeek en la Ficha de Juego
- **RF-4.1:** En la cabecera editorial o sección de metadatos de `GameDetail.razor`, cuando `Game.BggId > 0`, se mostrará un botón claramente visible: `[ 🌐 Ver en BoardGameGeek ]`.
- **RF-4.2:** El enlace abrirá la URL oficial `https://boardgamegeek.com/boardgame/{BggId}` en una nueva pestaña del navegador con los atributos de seguridad `target="_blank" rel="noopener noreferrer"`.

---

## 3. Requerimientos No Funcionales (RNF)

- **RNF-1: Rendimiento y Caché (< 100ms):** Las consultas de los 4 carriles estarán agregadas y cacheadas en memoria mediante `CachedHomeDashboardService` con `IMemoryCache` (TTL de 10 minutos), garantizando tiempos de respuesta ultrarrápidos en SSR.
- **RNF-2: Accesibilidad WCAG 2.2 AA:** Todos los carriles horizontales dispondrán de nombres accesibles (`aria-label`), contraste cromático suficiente, soporte de foco por teclado y etiquetas descriptivas.
- **RNF-3: Compatibilidad de Esquema:** Las adiciones de base de datos se aplicarán de forma no destructiva a través de `SqliteSchemaMigrator`.

---

## 4. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Acceso a la página principal como Dashboard Editorial
  Dado que un usuario entra en la raíz de la web "/"
  Entonces visualiza el Dashboard de Inicio con sus cuatro carriles temáticos
  Y la barra de navegación contiene un enlace directo a "/catalogo"
  Y la cabecera superior no muestra el selector de paleta de colores

Escenario: Navegación al catálogo completo
  Dado que un usuario se encuentra en la portada "/"
  Cuando hace clic en el enlace "Catálogo" de la barra superior
  Entonces navega a "/catalogo"
  Y visualiza la barra de búsqueda y los filtros facetados de ADN lúdico

Escenario: Desplazamiento táctil en carril de mejores juegos en dispositivo móvil
  Dado un usuario que navega en un dispositivo móvil en "/"
  Cuando visualiza el carril "Top 20 Mejores Juegos"
  Entonces puede desplazar horizontalmente con el dedo el carrusel de 20 juegos
  Y las tarjetas muestran carátula, título, puntuación y semáforo de comensales

Escenario: Prioridad de sorteos promocionados en el Dashboard
  Dado un sorteo promocionado que finaliza dentro de 7 días
  Y un sorteo estándar que finaliza mañana
  Cuando el usuario visualiza el carril de Sorteos en la home
  Entonces el sorteo promocionado aparece en primera posición antes que el estándar

Escenario: Consulta de ferias y eventos lúdicos
  Dado que existen los eventos "Festival de Córdoba" (Octubre) e "InterOcio" (Marzo siguiente)
  Cuando el usuario visualiza el carril de Eventos Lúdicos en la home
  Entonces visualiza las tarjetas ordenadas cronológicamente por fecha de inicio
  Y cada tarjeta contiene el cartel, fechas, ciudad y enlace a la web oficial

Escenario: Navegación externa a la ficha de BGG
  Dado que un usuario visita la ficha del juego con BggId 13 ("Catan")
  Cuando hace clic en el botón "[ 🌐 Ver en BoardGameGeek ]"
  Entonces se abre una nueva pestaña del navegador en "https://boardgamegeek.com/boardgame/13"
```
