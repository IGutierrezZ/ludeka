# 🗺️ Ludeka / Ludist — Hoja de Ruta de Incrementos SDD (Roadmap MVP & Post-MVP)

Este documento desglosa los bloques de la especificación funcional maestra (`LUDIST_SPEC_FUNCIONAL_MVP.md`) en **Vertical Slices (Incrementos Entregables)**. Los 6 primeros incrementos del MVP inicial se encuentran **100% completados, verificados y archivados**, dando paso a la fase de Consolidación, Comunidad y Operaciones. Cada incremento atraviesa todas las capas de la arquitectura (Dominio -> Casos de Uso -> Infraestructura -> UI Blazor -> Tests) bajo el ciclo formal **Spec-Driven Development (SDD)**.

---

## Incremento 1: Catálogo Base, Ficha Inteligente y Semáforo de Escalabilidad
- **Identificador SDD:** `change-01-core-catalog`
- **Puntos del MVP cubiertos:** 3.1 a 3.6, 9.1, 9.2 (Fichas, ADN Lúdico, Ratings y Semáforo).
- **Estado:** ✅ **Completado y Archivado** (Commit `a9314da`).
- **Alcance Funcional:**
  1. Modelo de datos del Juego: título original, nombre comercial en español, diseñador, editorial, año, URL carátula oficial BGG.
  2. Píldoras de ADN Lúdico (Badges): Confrontación (Cooperativo/Competitivo/Roles), Estilo (Euro/Ameritrash/Party/Filler), Modo Solitario.
  3. Semáforo Dinámico de Escalabilidad (1 a 7+ jugadores): cálculo de estado (🟢 Imprescindible | 🟡 Recomendado | 🔴 No recomendado) y etiqueta "Ideal a X jugadores".
  4. Edad de caja legal vs. Edad real comunitaria + indicador de dependencia del idioma (Nula/Baja/Alta).
  5. Duración estimada por persona y nivel de huella en mesa (Mesa pequeña, Comedor, Monstruo de mesa).
  6. Cliente de ingesta BGG XMLAPI2 para enriquecer fichas del Top 1.000.
  7. Componentes UI Blazor con Tailwind CSS: cabecera visual editorial, ficha con badges compactos de lectura en 3 segundos y buscador reactivo.

---

## Incremento 2: Ludoteca Personal, Colección en 4 Estados y Préstamos
- **Identificador SDD:** `change-02-library-loans`
- **Puntos del MVP cubiertos:** 7.1, 7.2, 7.3 (Colección, Préstamos y Formulario modular).
- **Estado:** ✅ **Completado y Archivado** (Commit `e4d777e`).
- **Alcance Funcional:**
   1. Barra de acción interactiva en la ficha del juego con 4 estados:
      - 🟢 *En mi ludoteca* (físico propio).
      - 🔵 *Jugado* (asociación, bar, amigos).
      - 🟡 *Deseado* (radar de interés).
      - 🔴 *Quiero comprar* (lista de seguimiento de ofertas).
   2. Módulo privado de préstamos ("¿A quién se lo dejé?"): registrar persona/asociación y fecha; listado en perfil con devolución en 1 clic.
   3. Formulario modular de valoración rápida en 45 segundos:
      - Puntuación 1 a 10 y micro-reseña de máximo 280 caracteres.
      - Chips de comensales (1J a 7J+) para votar el semáforo personal.
      - Experiencia infantil opcional (edad mínima sugerida y adaptación de reglas).
   4. Edición de valoración propia con tarjeta destacada encima de opiniones públicas.

---

## Incremento 3: Panel y Veredicto de la Mesa Fundadora
- **Identificador SDD:** `change-03-founding-verdict`
- **Puntos del MVP cubiertos:** 4.1, 4.2 (Ciclo de vida del veredicto y panel editorial).
- **Estado:** ✅ **Completado y Archivado** (Commit `4404e6b`).
- **Alcance Funcional:**
   1. Autenticación y roles de usuario: rol `FoundingTeam` / `Moderator`.
   2. Acceso directo desde la ficha: botón `[ 🛡️ Gestionar Veredicto Fundador ]`.
   3. Análisis oficial de la casa con foco en juego a 2 personas (parejas) y familias/niños.
   4. Galería fotográfica de mesa real: subida y visualización de 1 a 3 fotos tomadas en mesa propia.
   5. Sello de recomendación de la mesa fundadora (*Imprescindible*, *Recomendado con adaptaciones*, *Prescindible*).
   6. Algoritmo de reemplazo visual: el veredicto fundador tiene prioridad sobre la síntesis de IA inicial.

---

## Incremento 4: Hub Multimedia (YouTube e Instagram)
- **Identificador SDD:** `change-04-multimedia-hub`
- **Puntos del MVP cubiertos:** 5.1, 5.2, 5.3 (Hub multimedia segregado e ingesta).
- **Estado:** ✅ **Completado y Archivado** (Commit `eb03473`).
- **Alcance Funcional:**
   1. Pestañas horizontales limpias en la ficha sin mezclar formatos:
      - *Pestaña 1:* 🎬 Tutoriales de YouTube (16:9 con canal y duración).
      - *Pestaña 2:* 🎲 Partidas completas de YouTube (16:9 con badge obligatorio de número de jugadores, ej. "Partida a 2").
      - *Pestaña 3:* 💬 Opiniones y Redes (posts cuadrados de Instagram y Reels/Shorts 9:16).
   2. Pipeline de ingesta acotado: búsqueda quirúrgica para juegos del catálogo (máximo 2 mejores vídeos por juego).
   3. Panel de moderación rápida móvil: aprobar/descartar contenidos y asignación de vídeos huérfanos.

---

## Incremento 5: Importador BGG en 1 Clic y Auto-Catalogación
- **Identificador SDD:** `change-05-bgg-importer`
- **Puntos del MVP cubiertos:** 8.1, 8.2, 8.3 (Importación y cola nocturna).
- **Estado:** ✅ **Completado y Archivado** (Commit `95e656d`).
- **Alcance Funcional:**
  1. Integración con BGG: el usuario introduce su usuario de BGG y se importan sus listas (`Owned`, `Wishlist`).
  2. Cruce con el catálogo local: vinculación inmediata para juegos existentes; juegos no catalogados pasan a `⏳ En cola de catalogación`.
  3. Cola de auto-catalogación para enriquecer títulos pendientes ordenados por popularidad.
  4. Buscador asistido en vivo contra `/xmlapi2/search` de BGG para añadir juegos a mano con su `BggId` único sin duplicados.

---

## Incremento 6: Automatización Omnicanal, Radar de Sorteos y Comunidad
- **Identificador SDD:** `change-06-automation-community`
- **Puntos del MVP cubiertos:** 6.1 a 6.3, 10.1 a 10.5, 11.1, 11.2 (Generador de plantillas, Sorteos, Q&A).
- **Estado:** ✅ **Completado y Archivado** (Commit `f71e98f`, 146 tests en verde).
- **Alcance Funcional:**
  1. Formulario de creación rápida de sorteos externos y novedades de tiendas de los viernes.
  2. Motor de composición de imagen de marca (1:1 cuadrada) para Instagram con portada, píldora identificativa y pie de marca.
  3. Radar de Sorteos en la web con fecha límite de expiración automática.
  4. Consultorio de reglas Q&A estilo StackOverflow por juego (pregunta, respuestas, votos y respuesta aceptada).
  5. Manifiesto de Transparencia de Fondos en el pie de página de la plataforma.

---

## 🚀 Fase Post-MVP: Consolidación, Comunidad y Operaciones

---

## Incremento 7: Compilación de Producción, Optimización de Assets y Rendimiento Web
- **Identificador SDD:** `change-07-production-assets-perf`
- **Objetivo Principal:** Optimización extrema de rendimiento en carga móvil, purga de estilos y cumplimiento estricto de Core Web Vitals y accesibilidad.
- **Estado:** ✅ **Completado y Archivado** (154 tests en verde).
- **Alcance Funcional y Técnico:**
  1. **Pipeline de Assets y Tailwind CSS:** Configuración de compilación optimizada y purga de clases CSS mediante Tailwind CLI v3.4.17 (`app.css` minificado en 1.7s), eliminando dependencias CDN en producción.
  2. **Auditoría Core Web Vitals:** Optimización de Largest Contentful Paint (LCP < 1.2s mediante preconexiones de fuentes y carátula con `fetchpriority="high"`), Cumulative Layout Shift (CLS = 0 con contenedores rígidos `aspect-square`) e Interaction to Next Paint (INP con `@implements IDisposable` en búsquedas reactivas).
  3. **Estrategia de Caché Avanzada:** Caché de 2 niveles: Nivel 1 en aplicación con `CachedCatalogService` decorando `ICatalogService` con `IMemoryCache` (TTL 10m e invalidación por slug); Nivel 2 en HTTP con ASP.NET Core `Output Caching` con tags (`tag-catalog`, `tag-radar`, `tag-static`).
  4. **Optimización Multimedia:** Atributos `loading="lazy"`, `decoding="async"`, ratios fijos (`aspect-square`, `aspect-video`) y dimensiones explícitas en carátulas BGG, tutoriales, partidas, reseñas y fotografías de la comunidad.
  5. **Accesibilidad WCAG 2.2 Nivel AA:** Etiqueta `<html lang="es">`, enlace de salto accesible (*Skip Link*), estandarización de todos los modales con `role="dialog"` y `aria-labelledby`, semántica en pestañas (`role="tablist"`/`role="tab"`/`role="tabpanel"`), formularios accesibles con etiquetas asociadas y microtextos traducidos al español castellano.

---

## Incremento 8: Fichas de Expansión, Ecosistema y Compatibilidad Lúdica ("Mezclador de Mesa")
- **Identificador SDD:** `change-08-game-expansions`
- **Objetivo Principal:** Dotar a las expansiones de ficha propia, vídeos y valoraciones independientes, vinculación bidireccional con el juego base, tarjeta editorial de aportes, matriz de sinergia par-a-par y Mezclador interactivo de mesa con detección de sobrecarga.
- **Estado:** ✅ **Completado y Verificado** (170 tests en verde al 100%).
- **Alcance Funcional y Técnico:**
  1. **Modelo de Dominio Polimórfico (`GameType`, `ExpansionNecessity`, `ExpansionImpactTag`, `ExpansionSynergyLevel`):** Tipado formal en `Game` con relación reflexiva `BaseGameId`, deltas de duración/jugadores y etiquetas de impacto lúdico.
  2. **Matriz de Sinergias Par-a-Par (`ExpansionSynergy`) y Recetas de Mesa (`ExpansionRecipe`):** Relaciones conmutativas con explicaciones de compatibilidad y packs de expansión prediseñados para distintas configuraciones de mesa.
  3. **Motor de Evaluación en Tiempo Real (`IExpansionService`):** Lógica que analiza selecciones de expansiones en el "Mezclador de Mesa", detecta incompatibilidades, sobrecarga por duración (+45 min) o exceso de módulos (+2 módulos pesados) y calcula el tiempo total de la partida.
  4. **Persistencia e Índices en SQLite (`SqliteExpansionRepository`):** Consultas indexadas por juego base y pares de expansiones, con almacenamiento JSON de listas de etiquetas y colecciones de IDs.
  5. **Semillado Real de Alta Calidad:** Expansiones oficiales precargadas con imágenes, metadatos, valoraciones y tutoriales propios (Wingspan: Europa, Oceanía y Asia; Terraforming Mars: Preludio y Hellas & Elysium; Carcassonne: Posadas & Catedrales y Constructores & Comerciantes).
  6. **Componentes UI Editoriales Blazor:**
     - `ParentGameBanner.razor`: Banner de acceso al juego base desde la ficha de la expansión.
     - `ExpansionAporteCard.razor`: Tarjeta editorial con insignias de necesidad, etiquetas de impacto y resumen narrativo.
     - `ExpansionSisterList.razor`: Expansiones hermanas con badges de compatibilidad directa.
     - `ExpansionEcosystemSection.razor`: 3 pestañas dinámicas en el juego base (Catálogo, Mezclador interactivo y Recetas).
     - `GameCard.razor` & `Home.razor`: Badge `🧩 Expansión` y filtros de navegación segmentados.

---

## Incremento 9: Sistema de Notificaciones y Webhooks de Comunidad (Discord & Telegram)
- **Identificador SDD:** `change-09-notifications-webhooks`
- **Objetivo Principal:** Difusión multicanal automatizada para dinamizar la comunidad avisando de eventos clave en Discord y Telegram sin intervención manual.
- **Estado:** ✅ **Completado y Verificado** (189 tests en verde al 100%).
- **Alcance Funcional y Técnico:**
  1. **Motor de Webhooks Multicanal (`ICommunityNotificationService`):** Integración con Discord Webhooks y Telegram Bot API con plantillas enriquecidas (Embeds con color de marca, portada del juego, enlaces directos y botones de acción).
  2. **Cola de Despacho en Segundo Plano (Outbox Pattern / `Channel<T>`):** Desacoplamiento asíncrono mediante `BackgroundService` para no penalizar la latencia de las peticiones HTTP del usuario.
  3. **Eventos Automatizados del Sistema:**
     - ⚠️ **Alerta de Sorteo a punto de expirar:** Aviso automático 24 horas antes del cierre del plazo para maximizar participación comunitaria.
     - 🛍️ **Boletín de Lanzamientos de Viernes:** Resumen automatizado de novedades y reimpresiones en tiendas de juegos de mesa cada viernes por la mañana.
     - 🛡️ **Nuevo Veredicto Fundador publicado:** Notificación con el sello de recomendación (*Imprescindible* / *Recomendado*) y enlace a la ficha.
     - 💡 **Duda de Reglas resuelta:** Difusión de preguntas con solución aceptada para nutrir el conocimiento lúdico común.
  4. **Gestión de Configuración y Seguridad:** Parámetros configurables en `appsettings.json` (habilitar/deshabilitar canales individualmente, URLs de webhook seguras y modo simulado/dry-run para pruebas unitarias).

---

## Incremento 10: Despliegue, Empaquetado Docker y Configuración de Staging/Producción
- **Identificador SDD:** `change-10-docker-deployment-staging`
- **Objetivo Principal:** Empaquetado reproducible, seguro y listo para producción de toda la solución Ludeka para su despliegue en cualquier servidor VPS o entorno en la nube.
- **Estado:** ✅ **Completado y Verificado** (195 tests en verde al 100%).
- **Alcance Funcional y Técnico:**
  1. **Dockerfile Multi-Stage Optimizado:** Imagen de construcción .NET 10 SDK, compilación de frontend y runtime chiseled/alpine ultra-ligero y seguro ejecutándose con usuario no-root.
  2. **Orquestación con Docker Compose (`docker-compose.yml`):** Definición de servicios para el entorno **local** con volúmenes persistentes para la base de datos SQLite (`ludeka.db`), uploads de fotos de mesa y logs estructurados. En producción la persistencia es **PostgreSQL en Supabase** (`docker-compose.prod.yml`).
  3. **Endpoints de Health Checks (`/healthz` y `/ready`):** Diagnóstico en tiempo real del estado de la aplicación, conectividad con la base de datos configurada, permisos de escritura en el directorio de datos y disponibilidad del runtime.
  4. **Seguridad y Gestión de Secretos:** Configuración mediante variables de entorno (`ASPNETCORE_ENVIRONMENT`, cadenas de conexión, tokens y webhooks) sin credenciales en el repositorio.
  5. **Guía Operativa de Despliegue y Mantenimiento:** Documentación técnica paso a paso para despliegue en VPS Linux, procedimientos de backup/restore de **PostgreSQL en Supabase** (`scripts/supabase-backup.ps1`) y rotación de registros.

---

## Incremento 17: Sistema Comunitario de Reporte de Errores y Bandeja de Moderación de Fichas
- **Identificador SDD:** `change-17-community-error-reports`
- **Objetivo Principal:** Permitir a cualquier jugador reportar incidencias en fichas (imágenes incorrectas, datos erróneos de jugadores/duración/edad, enlaces caídos) y disponer de una bandeja de entrada en el panel de moderación para su gestión y resolución.
- **Estado:** ✅ **Completado y Archivado** (339 tests en verde al 100%).
- **Documento:** [`inc-17-community-error-reports.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-17-community-error-reports.md)

---

## Incremento 18: Editor Editorial de Fichas de Catálogo y Carga de Imágenes para Moderadores
- **Identificador SDD:** `change-18-moderator-game-editor`
- **Objetivo Principal:** Dotar al equipo fundador y moderadores de un editor integral de fichas de juego y soporte para subida directa de archivos de imagen (o enlace URL), resolviendo al instante reportes comunitarios y manteniendo la calidad canónica del catálogo.
- **Estado:** ✅ **Completado y Archivado** (371 tests en verde al 100%).
- **Documento:** [`inc-18-moderator-game-editor.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-18-moderator-game-editor.md)

---

## Incremento 19: Directorio Editorial y de Creadores: Fichas, Redes Sociales y Gestión para Moderadores
- **Identificador SDD:** `change-19-publishers-creators-directory`
- **Objetivo Principal:** Crear un directorio completo de editoriales y de creadores (autores y diseñadores), con fichas individuales que incluyan redes sociales, web oficial y sus juegos en Ludeka, junto con formularios de alta y edición directa para moderadores.
- **Estado:** ✅ **Completado y Archivado** (413 tests en verde al 100%).
- **Documento:** [`inc-19-publishers-creators-directory.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-19-publishers-creators-directory.md)

---

## Incremento 20: Gestión de Usuarios, Permisos Granulares de Moderación y Auditoría para la Mesa Fundadora
- **Identificador SDD:** `change-20-user-management-permissions-audit`
- **Objetivo Principal:** Dotar a la Mesa Fundadora de un panel de administración para gestionar usuarios, asignar roles y configurar permisos granulares de moderación (juegos, imágenes, editoriales, creadores, multimedia, reportes y tiendas), junto con un registro de auditoría cronológico para verificar quién modificó qué y cuándo.
- **Estado:** ✅ **Completado y Archivado** (433 tests en verde al 100%).
- **Documento:** [`inc-20-user-management-permissions-audit.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-20-user-management-permissions-audit.md)

---

## Incremento 21: Dashboard de Inicio Editorial, Desacople de Catálogo, Limpieza de Navbar y Enlace Canónico BGG
- **Identificador SDD:** `change-21-home-dashboard`
- **Objetivo Principal:** Reemplazar la página de inicio por un Dashboard editorial con 4 carriles en scroll horizontal mobile-first (Top 20 juegos BGG/Ludeka, sorteos destacados/próximos a finalizar, novedades recientes y próximos eventos). Mudar el catálogo completo a `/catalogo`, retirar el selector de temas de la barra superior y añadir en cada ficha de juego un enlace directo a su página oficial en BoardGameGeek.
- **Estado:** ✅ **Completado y Archivado** (453 tests en verde al 100%).
- **Documento:** [`inc-21-home-dashboard.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-21-home-dashboard.md)

---

## Incremento 22: Segregación de Sorteos, Novedades y Nuevo Módulo de Grandes Eventos Lúdicos
- **Identificador SDD:** `change-22-draws-news-events-split`
- **Objetivo Principal:** Disolver el módulo unificado de Radar para estructurar secciones y páginas independientes (`/sorteos`, `/novedades` y `/eventos`). Añadir la ingesta de imagen de Instagram y carga manual por moderadores, gestión de sorteos promocionados (`IsPromoted`) y calendario cronológico de grandes ferias y festivales de juegos de mesa.
- **Estado:** ✅ **Completado y Archivado (472 tests pasando al 100%)**
- **Documento:** [`inc-22-draws-news-events-split.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-22-draws-news-events-split.md)

---

## Incremento 23: Categorización y Gestión Editorial de Vídeos y Multimedia en Fichas de Juego
- **Identificador SDD:** `change-23-multimedia-editorial-categorization`
- **Objetivo Principal:** Establecer la taxonomía formal de vídeos (`QuickOverview` ["Cómo Funciona"], `Tutorial`, `Gameplay`, `ReviewOpinion`) asistida por heurística semántica en el panel de moderación e ingesta, y habilitar a administradores/moderadores para reclasificar, reasignar a otro juego con autocompletado asistido o eliminar vídeos directamente desde la ficha pública del juego con auditoría estricta de cambios.
- **Estado:** ✅ **Completado y Archivado (531 tests pasando al 100%)**
- **Documento:** [`inc-23-multimedia-editorial-categorization.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-23-multimedia-editorial-categorization.md)

---

## Incremento 24: Detección Automática de Juegos en Novedades y Cola Nocturna Inteligente BGG/Gemini
- **Identificador SDD:** `change-24-nightly-game-discovery-cataloging`
- **Objetivo Principal:** Extraer el título del juego mencionado en cada novedad capturada por el batch nocturno, verificar si existe en Ludeka o en BGG y encolarlo. El proceso nocturno ingesta los juegos de la cola y completa el cupo diario hasta 20 títulos con los mejores del Top de BGG no catalogados, respetando límites de API de BGG y Gemini.
- **Estado:** ✅ **Completado y Archivado (600 tests pasando al 100%)**
- **Documento:** [`inc-24-nightly-game-discovery-cataloging.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-24-nightly-game-discovery-cataloging.md)
- **Módulo del Sistema:** [`18-deteccion-novedades-y-cola-nocturna.md`](file:///c:/repos/Ludeka/docs/specs/sistema/18-deteccion-novedades-y-cola-nocturna.md)

---

## Incremento 25: Auditoría de Resiliencia, Rate Limiting y Estrategia de Token en el Importador de Ludotecas BGG
- **Identificador SDD:** `change-25-bgg-collection-resilience-token`
- **Objetivo Principal:** Auditar y blindar el cliente de importación de colecciones BGG frente a respuestas `202 Accepted` de BGG mediante polling con backoff exponencial, mitigar errores de rate limit (429/503), añadir soporte para cabeceras y tokens/claves API de BGG y proporcionar feedback visual en tiempo real al usuario.
- **Estado:** ✅ **Completado y Archivado (609 tests pasando al 100%)**
- **Documento:** [`inc-25-bgg-collection-resilience-token.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-25-bgg-collection-resilience-token.md)
- **Módulo del Sistema:** [`05-integracion-bgg.md`](file:///c:/repos/Ludeka/docs/specs/sistema/05-integracion-bgg.md)

---

## Incremento 26: Especificación de Fundas (Sleeves) por Juego y Enlaces de Compra Contextuales
- **Identificador SDD:** `change-26-card-sleeves-spec-stores`
- **Objetivo Principal:** Incorporar en la ficha de cada juego la sección "Protege tu juego", extrayendo medidas exactas de cartas (ancho x alto en mm), número de cartas y paquetes de fundas recomendados (vía BGG / comunidad), conectándolos con enlaces de compra directos al tamaño de funda exacto en tiendas colaboradoras como Zacatrus.
- **Estado:** ✅ **Completado y Archivado (636 tests pasando al 100%)**
- **Documento:** [`inc-26-card-sleeves-spec-stores.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-26-card-sleeves-spec-stores.md)
- **Módulo del Sistema:** [`19-especificacion-fundas-y-enlaces-tiendas.md`](file:///c:/repos/Ludeka/docs/specs/sistema/19-especificacion-fundas-y-enlaces-tiendas.md)

---

## Incremento 27: Monitorización y Verificación de Stock en Tiempo Real en Enlaces de Compra
- **Identificador SDD:** `change-27-store-live-stock-check`
- **Objetivo Principal:** Detección de disponibilidad y stock en tiempo real en tiendas comerciales asociadas sin penalizar la velocidad de carga de la ficha de juego (renderizado progresivo no bloqueante, timeout de 1.5s, caché en memoria con TTL y badges visuales claros de disponibilidad).
- **Estado:** ✅ **Completado y Archivado** (656 tests en verde al 100%).
- **Documento:** [`inc-27-store-live-stock-check.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-27-store-live-stock-check.md)
- **Módulo del Sistema:** [`20-verificacion-stock-tiempo-real-tiendas.md`](file:///c:/repos/Ludeka/docs/specs/sistema/20-verificacion-stock-tiempo-real-tiendas.md)

---

## Incremento 28: Generador y Publicador Directo de Posts para Instagram en Moderación
- **Identificador SDD:** `change-28-instagram-direct-publisher`
- **Objetivo Principal:** Permitir a los moderadores generar publicaciones automáticas para la cuenta oficial de Instagram de Ludeka a partir de sorteos o novedades aprobados, disponiendo de un previsualizador 1:1, compositor de imagen de marca, editor de copy y botón de publicación directa vía Meta Graph API.
- **Estado:** ✅ **Completado y Archivado** (681 tests en verde al 100%).
- **Documento:** [`inc-28-instagram-direct-publisher.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-28-instagram-direct-publisher.md)
- **Módulo del Sistema:** [`21-publicador-directo-instagram.md`](file:///c:/repos/Ludeka/docs/specs/sistema/21-publicador-directo-instagram.md)

---

## Incremento 29: Localización Geográfica por País, Filtrado Territorial y Detección de Ubicación
- **Identificador SDD:** `change-29-country-location-filtering`
- **Objetivo Principal:** Dotar a la plataforma de filtrado y contextualización geográfica por país para tiendas físicas y online, sorteos y eventos lúdicos. Incluye la selección voluntaria de país en el perfil de usuario con advertencia explícita sobre el filtrado territorial, marcado visual de país en listados y fichas de compras (con soporte para juegos y fundas de cartas), estado vacío cuando no existen tiendas vinculadas en el país seleccionado, y detección de ubicación (móvil/ordenador) para ordenar y priorizar contenidos locales.
- **Estado:** ✅ **Completado y Archivado** (573 tests en verde al 100%).
- **Documento:** [`inc-29-country-location-filtering.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-29-country-location-filtering.md)
- **Módulo del Sistema:** [`17-localizacion-territorial-pais.md`](file:///c:/repos/Ludeka/docs/specs/sistema/17-localizacion-territorial-pais.md)

---

## Incremento 30: Colección en 3 Estados, Estado 'Jugado' Independiente, Radar de Compra y Diario de Partidas
- **Identificador SDD:** `change-30-played-independent-status`
- **Objetivo Principal:** Reestructuración de la colección personal eliminando el estado redundante 'Deseado' en favor de 3 estados potentes ('En mi ludoteca', 'Jugado', 'Comprar'). El estado 'Jugado' es ortogonal e independiente de la posesión o compra. Solo se permite valorar títulos que hayan sido marcados como jugados. Se introduce el nuevo subsistema de Diario de Partidas (registro de qué juego, fecha, lugar, comensales y comentarios) con actualización automática a 'Jugado' y analíticas de sesiones lúdicas.
- **Estado:** ✅ **Completado y Archivado** (705 tests en verde al 100%).
- **Documento:** [`inc-30-played-independent-status.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-30-played-independent-status.md)
- **Módulo del Sistema:** [`02-ludoteca-y-prestamos.md`](file:///c:/repos/Ludeka/docs/specs/sistema/02-ludoteca-y-prestamos.md)

---

## Incremento 31: Portada Minimalista y Reorientación Autores → Creadores de Contenido
- **Identificador SDD:** `portada-minimalista-creadores`
- **Objetivo Principal:** Minimalizar el hero de la portada (sin badge ni titular visible; párrafo, buscador rápido y 4 píldoras de acceso — Catálogo Completo, Sorteos, Novedades y Eventos — con h1 accesible visualmente oculto conforme a WCAG 2.2 AA), corregir el enlace 'Ver todas las novedades' hacia /novedades y retirar el banner '¡Radar renovado!' manteniendo /radar como alias silencioso de /sorteos. Completa la reorientación del directorio Autores → Creadores de Contenido: sembrado con purga de los 6 diseñadores retirados y re-siembra aditiva desde el padrón estático de canales (incluido Análisis Parálisis), fichas con redes sociales sin sección 'Obras' ni cruce por Game.Designer, diseñador de juego como texto plano en fichas de juego (sin enlaces rotos), alias /autores operativo y reetiquetado transversal a 'Creadores'.
- **Estado:** ✅ **Completado y Archivado** (723 tests en verde al 100%).
- **Documento:** [`portada-minimalista-creadores/proposal.md` (archivo SDD)](file:///c:/repos/Ludeka/openspec/changes/archive/2026-09-08-portada-minimalista-creadores/proposal.md)
- **Módulo del Sistema:** [`22-portada-y-directorio-creadores.md`](file:///c:/repos/Ludeka/docs/specs/sistema/22-portada-y-directorio-creadores.md)

---

## Incremento 32: Fix HTTP 500 en Fichas de Juego (ORDER BY DateTimeOffset en SQLite)
- **Identificador:** `fix-32-playlog-orderby-datetimeoffset` (fix directo, sin ciclo SDD)
- **Objetivo Principal:** Corregir el error HTTP 500 en todas las fichas de juego (`/juegos/{slug}`), causado por `SqliteGamePlayLogRepository.GetByUserAndGameAsync` que aplicaba `OrderByDescending` sobre propiedades `DateTimeOffset` (`PlayDate`, `CreatedAt`) en LINQ-to-EF. EF Core SQLite no traduce ORDER BY sobre `DateTimeOffset` (se persiste como TEXT), lanzando `NotSupportedException` en cada render de la ficha vía `GameDetail.RefreshPlaysCountAsync`. El fix materializa el query y ordena en memoria (LINQ to Objects) preservando la semántica `PlayDate DESC, CreatedAt DESC`.
- **Estado:** 🏆. **Completado y Archivado** (725 tests en verde al 100%; defecto introducido en INC-30, commit fc90de0).
- **Documento:** [`SqliteGamePlayLogRepositoryTests.cs` (test rojo→verde del fix)](file:///c:/repos/Ludeka/tests/Ludeka.UnitTests/Infrastructure/SqliteGamePlayLogRepositoryTests.cs)
- **Módulo del Sistema:** [`02-ludoteca-y-prestamos.md`](file:///c:/repos/Ludeka/docs/specs/sistema/02-ludoteca-y-prestamos.md)

---

## Incremento 33: Fix 500 Latente en Mi Ludoteca (ORDER BY DateTimeOffset en GetByUserIdAsync)
- **Identificador:** `fix-33-playlog-orderby-getbyuserid` (fix directo, sin ciclo SDD; segunda instancia del mismo defecto que INC-32)
- **Objetivo Principal:** Corregir el error HTTP 500 latente en `/mi-ludoteca` (sección Diario de Partidas vía `GamePlayLogService.GetUserPlaysAsync`/`GetUserPlaysStatsAsync`), causado por `SqliteGamePlayLogRepository.GetByUserIdAsync`, que aplicaba `OrderByDescending` sobre `DateTimeOffset` (`PlayDate`, `CreatedAt`) en LINQ-to-EF. Mismo root cause que INC-32: EF Core SQLite no traduce ORDER BY sobre `DateTimeOffset`. El fix replica el patrón de INC-32 (`d6005cc`): materializar el query y ordenar en memoria preservando la semántica `PlayDate DESC, CreatedAt DESC`.
- **Estado:** 🏆. **Completado y Archivado** (727 tests en verde al 100%: 725 previos + 2 nuevos por triangulación Strict TDD; test RED confirmado con `NotSupportedException`).
- **Documento:** [`SqliteGamePlayLogRepositoryTests.cs` (tests rojo→verde del fix)](file:///c:/repos/Ludeka/tests/Ludeka.UnitTests/Infrastructure/SqliteGamePlayLogRepositoryTests.cs)
- **Módulo del Sistema:** [`02-ludoteca-y-prestamos.md`](file:///c:/repos/Ludeka/docs/specs/sistema/02-ludoteca-y-prestamos.md)

---

## Incremento 34: Barrido Sistémico de ORDER BY DateTimeOffset en Repositorios SQLite
- **Identificador:** `fix-34-orderby-datetimeoffset-sweep` (fix directo, sin ciclo SDD; barrido final de la clase de defecto de INC-32/33)
- **Objetivo Principal:** Matar de raíz la clase de bug «ORDER BY DateTimeOffset no traducible por SQLite → `NotSupportedException` → HTTP 500 latente» en los 5 repositorios restantes: `SqliteInstagramPostDraftRepository.GetDraftsAsync` (CreatedAt), `SqliteAuditLogRepository.GetLogsAsync` (Timestamp), `SqliteGameEditLogRepository.GetByGameIdAsync` (EditedAt), `SqliteNightlyCatalogingLogRepository.GetRecentLogsAsync`/`GetLatestLogAsync` (StartedAt, 2 sitios) y `SqliteUserCollectionRepository.GetPlayedByUserIdAsync` (AddedAt). El fix replica el patrón de INC-32/33: materializar con `ToListAsync` y ordenar en memoria (LINQ to Objects) con desempate determinista por `Id` (desc). La fase RED descubrió además el defecto hermano en el mismo repo de auditoría: los filtros de rango de fechas (`Timestamp >= fromDate` / `<= toDate`) tampoco son traducibles por SQLite, afectando a `GetLogsAsync` y `CountLogsAsync` (usados por `AuditService.GetAuditLogsAsync` con `FromDate`/`ToDate`); el fix mueve esos filtros a memoria. Se verificó que `SqliteCommunityNotificationRepository.GetRecentLogsAsync` ya ordena sobre lista materializada (seguro, sin tocar) y que los ORDER BY restantes sobre `StartDate`/`EndDate`/`ReleaseDate` son campos `DateOnly` traducibles.
- **Estado:** 🏆. **Completado y Archivado** (739 tests en verde al 100%: 727 previos + 12 nuevos por triangulación Strict TDD; RED confirmado con `NotSupportedException` en los ORDER BY e `InvalidOperationException` en los WHERE de fechas; grep final sin sitios pendientes).
- **Documento:** tests [`SqliteInstagramPostDraftRepositoryTests.cs`](file:///c:/repos/Ludeka/tests/Ludeka.UnitTests/Infrastructure/SqliteInstagramPostDraftRepositoryTests.cs), [`SqliteAuditLogRepositoryTests.cs`](file:///c:/repos/Ludeka/tests/Ludeka.UnitTests/Infrastructure/SqliteAuditLogRepositoryTests.cs), [`SqliteGameEditLogRepositoryTests.cs`](file:///c:/repos/Ludeka/tests/Ludeka.UnitTests/Infrastructure/SqliteGameEditLogRepositoryTests.cs), [`SqliteNightlyCatalogingLogRepositoryTests.cs`](file:///c:/repos/Ludeka/tests/Ludeka.UnitTests/Infrastructure/SqliteNightlyCatalogingLogRepositoryTests.cs), [`SqliteUserCollectionRepositoryTests.cs`](file:///c:/repos/Ludeka/tests/Ludeka.UnitTests/Infrastructure/SqliteUserCollectionRepositoryTests.cs) (rojo→verde del fix)
- **Módulo del Sistema:** [`02-ludoteca-y-prestamos.md`](file:///c:/repos/Ludeka/docs/specs/sistema/02-ludoteca-y-prestamos.md)

---

## Incremento 35: Portada Editorial — Narrativa Hogareña, Microinteracciones e Imágenes por Defecto
- **Identificador SDD:** `portada-editorial`
- **Objetivo Principal:** Transformar la portada `/` en una portada editorial con narrativa hogareña: hero con titular visible «La mesa está servida» en serif display Fraunces (delta RENAMED+MODIFIED+ADDED sobre `home-landing-hero`, aprobado por el usuario), 5 variantes de fondo intercambiables in situ (fotos de ambiente < 200 KB con `<picture>` AVIF/WebP/JPEG y `fetchpriority="high"`, escena CSS de serie e ilustración futura), scrim por variables de tema; extracción del markup duplicado de los 4 carriles a componentes dedicados (`Components/Home/`) con render de imagen y fallback por dominio (inline `DefaultImage.razor` temable + `onerror` a `/images/defaults/`), tokens de microinteracción `.rail-card` con equivalencia de foco (`:has(:focus-visible)`), `prefers-reduced-motion` y scroll sin scrollbar; iconografía Lucide global vía `Icon.razor` con catálogo whitelist de 94 iconos (cero emojis de interfaz en toda la web); corrección de clases muertas (`.scrollbar-none`, `sm:w-68`).
- **Estado:** ✅ **Completado y Archivado** (suite 847/847 en verde al 100%; +108 sobre el baseline real 739 de INC-34; verificación PASS WITH WARNINGS 19/19 requerimientos y 30/30 escenarios; WARNING-1 corregido en 44e5ae4).
- **Documento:** [`inc-35-portada-editorial.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-35-portada-editorial.md) (cadena de 4 PRs apilados; cambio SDD archivado en `openspec/changes/archive/2026-09-10-portada-editorial/`)
- **Módulo del Sistema:** [`23-portada-editorial.md`](file:///c:/repos/Ludeka/docs/specs/sistema/23-portada-editorial.md)

---

## Incremento 36: Rediseño Editorial del Resto de Páginas + Fix Responsive del Hero
- **Identificador SDD:** `rediseno-paginas-editoriales`
- **Objetivo Principal:** Extender el lenguaje editorial de la portada (INC-35) a las 5 páginas restantes (Catálogo, Fichas, Eventos, Sorteos, Novedades) mediante tokens/clases compartidos (`PageHeaderEditorial`, shell `EditorialModal`, lenguaje `.rail-card` en tarjetas de página) y corregir el bug responsive del hero (causa raíz: `min-height` fijo + hijos absolutos + recorte destructivo del 16:9; estrategia A: ratio responsiva + `object-position` focal + cap `clamp()` + suelo acotado). Incluye el token `--on-brand` para resolver el fallo WCAG 2.2 AA de los botones de marca en 3 temas.
- **Estado:** ✅ **Completado y Archivado** (suite 855/855 en verde al 100%; baseline 854 + el Fact acotado del ancho del hero; verificación **PASS** 13/13 requerimientos y 34/34 escenarios, 0 blockers y 0 hallazgos críticos). Cadena de 6 PRs apilados (#8–#13) pendiente del merge ordenado por el maintainer.
- **Documento:** [`inc-36-rediseno-paginas-editoriales.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-36-rediseno-paginas-editoriales.md) (cambio SDD archivado en `openspec/changes/archive/2026-09-10-rediseno-paginas-editoriales/`)
- **Módulos del Sistema:** [`24-fundaciones-editoriales-y-componentes.md`](file:///c:/repos/Ludeka/docs/specs/sistema/24-fundaciones-editoriales-y-componentes.md) (nuevo), [`23-portada-editorial.md`](file:///c:/repos/Ludeka/docs/specs/sistema/23-portada-editorial.md), [`01-catalogo-y-fichas.md`](file:///c:/repos/Ludeka/docs/specs/sistema/01-catalogo-y-fichas.md), [`15-dashboard-inicio-editorial.md`](file:///c:/repos/Ludeka/docs/specs/sistema/15-dashboard-inicio-editorial.md) y [`16-sorteos-novedades-y-eventos.md`](file:///c:/repos/Ludeka/docs/specs/sistema/16-sorteos-novedades-y-eventos.md)

---

## Incremento 37: Modo Producción — APIs Reales, Atribución BGG, Comunidad y Motor Privado de Afiliados
- **Identificador SDD:** `apis-produccion-afiliados`
- **Objetivo Principal:** Preparar el núcleo de Ludeka para su despliegue en producción sobre Docker/Google Cloud con base de datos en Supabase:
  1. Desacople estricto de datos simulados/mocks en BGG, Gemini y YouTube cuando `Simulate = false`. En caso de fallo o cuota agotada, fallar de forma controlada y registrable en la bandeja de incidencias sin inventar texto predefinido ni vídeos mock.
  2. Cumplimiento legal con BGG mediante la incorporación en el pie de página global de la insignia oficial *"Powered by BoardGameGeek"* con enlace canónico externo.
  3. Enlaces directos a las comunidades oficiales de Discord y Telegram en el footer configurables vía opciones.
  4. Motor centralizado y privado de afiliación de tiendas (`IAffiliateUrlResolver`) que inyecta parámetros query de colaboradores (Zacatrus, Mathom, Dungeon Marvels, Cuarto de Juegos, Tablerum) de forma invisible en la BD y en formularios, con etiquetado seguro `rel="noopener noreferrer sponsored"`.
  5. Corrección del cálculo de calendario de domingos en `CommunityNotificationService`.
- **Estado:** ✅ **Completado y Archivado** (suite 866/866 en verde al 100%; +11 pruebas de motor de afiliados y resiliencia de producción).
- **Documento:** [`inc-37-apis-produccion-afiliados.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-37-apis-produccion-afiliados.md) (cambio SDD archivado en `openspec/changes/archive/2026-09-14-apis-produccion-afiliados/`).
- **Módulos del Sistema:** [`25-motor-afiliados-y-atribucion-comunitaria.md`](file:///c:/repos/Ludeka/docs/specs/sistema/25-motor-afiliados-y-atribucion-comunitaria.md) (nuevo), [`05-integracion-bgg.md`](file:///c:/repos/Ludeka/docs/specs/sistema/05-integracion-bgg.md), [`10-sintesis-ia.md`](file:///c:/repos/Ludeka/docs/specs/sistema/10-sintesis-ia.md), [`04-hub-multimedia.md`](file:///c:/repos/Ludeka/docs/specs/sistema/04-hub-multimedia.md) y [`08-notificaciones-y-webhooks.md`](file:///c:/repos/Ludeka/docs/specs/sistema/08-notificaciones-y-webhooks.md).

---

## Incremento 38: Persistencia PostgreSQL en Supabase, Estrategia Dual y Usuario Admin Inicial
- **Identificador SDD:** `postgresql-supabase`
- **Objetivo Principal:** Dotar a Ludeka de persistencia en PostgreSQL gestionado en Supabase manteniendo SQLite en local y tests:
  1. Integración del paquete oficial `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3` con soporte para .NET 10.
  2. Conmutación inteligente y automática de proveedor (`UseNpgsql` vs `UseSqlite`) mediante `Database:Provider` o detección de cadena de conexión.
  3. Desacople estricto de datos de prueba en producción mediante la bandera `Database:SeedDemoData` (por defecto `false` en producción). En producción, la base de datos arranca limpia sin catálogo ni eventos mock.
  4. Creación garantizada y permanente del usuario Administrador Fundador inicial (`AdminUserSeeder`) con rol `FoundingTeam` y permisos completos de moderación para permitir la administración inmediata del sistema tras el arranque.
  5. Script SQL canónico e idempotente (`docs/database/supabase_schema.sql`) para crear o auditar la totalidad de las tablas y tipos `jsonb` en Supabase con 1 solo clic.
  6. Script automatizado de copias de seguridad (`scripts/supabase-backup.ps1`) con compresión `.sql.gz` y política de retención rotativa (7 días).
- **Estado:** ✅ **Completado y Archivado** (suite 887/887 en verde al 100%; +21 pruebas unitarias cubriendo detección de proveedor y sembrador de admin).
- **Documento:** [`inc-38-postgresql-supabase.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-38-postgresql-supabase.md) (cambio SDD archivado en `openspec/changes/archive/2026-09-14-postgresql-supabase/`).
- **Módulos del Sistema:** [`09-arquitectura-y-despliegue.md`](file:///c:/repos/Ludeka/docs/specs/sistema/09-arquitectura-y-despliegue.md).

---

## Incremento 39: Empaquetado Docker para Producción, Google Cloud Run y Pipeline CI/CD con Secretos
- **Identificador SDD:** `docker-prod-cloudrun`
- **Objetivo Principal:** Dotar a Ludeka de la infraestructura completa y automatizada para su despliegue continuo en producción sobre Google Cloud Run:
  1. Adaptación dinámica de ASP.NET Core a la variable de entorno `PORT` inyectada por Google Cloud Run en `src/Ludeka.Web/Program.cs` (`builder.WebHost.UseUrls($"http://0.0.0.0:{parsedPort}")`).
  2. Manifiesto `docker-compose.prod.yml` para despliegues autónomos o pruebas de producción local con soporte de variables de entorno `.env` y sondas de salud.
  3. Workflow oficial de GitHub Actions (`.github/workflows/ci-cd.yml`) con integración continua (CI) para compilación .NET 10 y ejecución de 887 tests, y despliegue continuo (CD) desatendido a Google Cloud Run mediante autenticación segura por Workload Identity Federation o Service Account Key tras merge a `main`.
  4. Guía operacional exhaustiva en español (`docs/deployment/google-cloud-run.md`) con comandos `gcloud`, roles IAM, catálogo de secretos en GitHub y optimizaciones de costes con escalado a cero.
  5. Verificación y robustecimiento de la suite de tests (`BggImportServiceProgressTests`) asegurando 100% de estabilidad en entornos multihilo.
- **Estado:** ✅ **Completado y Archivado** (suite 887/887 en verde al 100%; verificación de build de imagen Docker y workflow CI/CD).
- **Documento:** [`inc-39-docker-prod-cloudrun.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-39-docker-prod-cloudrun.md) (cambio SDD archivado en `openspec/changes/archive/2026-09-14-docker-prod-cloudrun/`).
- **Módulos del Sistema:** [`09-arquitectura-y-despliegue.md`](file:///c:/repos/Ludeka/docs/specs/sistema/09-arquitectura-y-despliegue.md).

---

## Incremento 40: Pipeline de Almacenamiento y Optimización de Medios (Cloudflare R2 + SkiaSharp + WebP)
- **Identificador SDD:** `change-40-medios-r2-skiasharp`
- **Objetivo Principal:** Almacenamiento y optimización de medios con 0 € en costes salientes (*zero egress fees*) mediante Cloudflare R2 y SkiaSharp en memoria (WebP a 82% de calidad, variantes deterministas cover/back/table).
- **Estado:** ✅ **Completado y Archivado** (suite 906/906 en verde al 100%; +19 pruebas unitarias de procesamiento gráfico y nombrado).
- **Documento:** [`inc-40-medios-r2-skiasharp.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-40-medios-r2-skiasharp.md).
- **Módulos del Sistema:** [`26-almacenamiento-medios-r2-skiasharp.md`](file:///c:/repos/Ludeka/docs/specs/sistema/26-almacenamiento-medios-r2-skiasharp.md).

---

## Incremento 41: Ingesta Masiva de Catálogo BGG (~8.000 títulos), Fotos GeekDo y Síntesis IA en Lotes
- **Identificador SDD:** `change-41-ingesta-bgg-catalogo`
- **Objetivo Principal:** Carga y enriquecimiento a escala del catálogo lúdico base:
  1. Filtro streaming CSV `bg_ranks.csv` con umbral `usersrated >= 30` (~8.000 juegos comunitariamente relevantes).
  2. Tabla de staging aislada `BggCatalogStaging` con tracking desacoplado de etapas (`Fetch`, `Images`, `Ai`, `Promotion`).
  3. Extracción de las 3 imágenes más votadas de GeekDo (portada, trasera y mesa) convertidas a WebP y subidas a R2 vía `IImageStorageService`.
  4. Síntesis editorial con Google Gemini Flash agrupando de 5 a 10 juegos por llamada estructurada, con pausa limpia ante cuota agotada (HTTP 429).
  5. Reingeniería del orquestador nocturno `NightlyCatalogingService` para drenar staging progresivamente sin bloquear novedades ni solicitudes prioritarias.
  6. Panel administrativo en `/admin/cola-catalogacion` y galería comunitaria en ficha de juego.
- **Estado:** ✅ **Completado y Archivado** (suite 930/930 en verde al 100%; +24 pruebas unitarias sin dependencias externas de mock).
- **Documento:** [`inc-41-ingesta-bgg-catalogo.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-41-ingesta-bgg-catalogo.md).
- **Módulos del Sistema:** [`27-ingesta-masiva-bgg-galeria-geekdo-ia-lotes.md`](file:///c:/repos/Ludeka/docs/specs/sistema/27-ingesta-masiva-bgg-galeria-geekdo-ia-lotes.md).

---

## Incremento 42: Hub de Ingesta Social y Multimedia (Bandeja de Moderación Editable + Alta Exprés + Directorio de Cuentas Monitorizadas)
- **Identificador SDD:** `change-42-ingesta-social-moderacion`
- **Objetivo Principal:** Pipeline integral de captura y curación comunitaria de contenidos lúdicos en redes:
  1. Alta exprés ("copiar, pegar y listo") por URL con extracción OpenGraph y oEmbed de YouTube sin APIs de pago (descartando Apify).
  2. Asistencia IA inteligente (Gemini Flash + heurística en español) para detectar sorteos, novedades, eventos y vídeos.
  3. Modo manual avanzado para reels/vídeos sin descripción, con buscador predictivo de catálogo y subida de miniatura WebP a Cloudflare R2 vía `IImageStorageService`.
  4. Bandeja de moderación 100% editable (`/admin/ingesta-social`) para corregir títulos, fechas, recinto o juego antes de aprobar atómicamente a `Giveaway`, `WeeklyRelease`, `BoardGameEvent` o `MediaItem`.
  5. Directorio de fuentes y canales monitorizados (`/admin/canales-monitorizados`) con sincronización en 1 clic desde entidades de `Publisher`, `Creator` y `Store`.
  6. Puntos de entrada transversales en `MainLayout.razor`, `Radar.razor`, `News.razor` y `Events.razor`, cumpliendo el contrato de maquetación libre de emojis.
- **Estado:** ✅ **Completado y Archivado** (suite 960/960 en verde al 100%; +30 pruebas unitarias nuevas sin dependencias de mock).
- **Documento:** [`inc-42-ingesta-social-moderacion.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-42-ingesta-social-moderacion.md).
- **Módulos del Sistema:** [`28-hub-ingesta-social-moderacion.md`](file:///c:/repos/Ludeka/docs/specs/sistema/28-hub-ingesta-social-moderacion.md).

---

## Incremento 43: Ingesta Continua y Auto-Descubrimiento de Novedades BGG en el Lote Nocturno
- **Identificador SDD:** `change-43-ingesta-continua-bgg`
- **Objetivo Principal:** Auto-descubrimiento y catalogación continua de los lanzamientos y tendencias mundiales de BoardGameGeek:
  1. Servicio de descubrimiento `IBggDiscoveryService`: escaneo del Hotness (`/xmlapi2/hot?type=boardgame`) y filtrado de lanzamientos recientes del año.
  2. Deduplicación estricta contra `Games`, `PendingBggImports` y `BggCatalogStaging`.
  3. Nuevos orígenes de cola: `CatalogQueueOrigin.BggNewReleases` y `CatalogQueueOrigin.BggHotness`.
  4. Fase 1.5 en el orquestador nocturno `NightlyCatalogingService` con trazabilidad en bitácora.
  5. UI de administración en `/admin/cola-catalogacion` con botón de escaneo bajo demanda, filtrado por origen y badges editoriales.
- **Estado:** ✅ **Completado y Archivado** (suite 968/968 en verde al 100%; +8 pruebas unitarias nuevas sin dependencias de mock).
- **Documento:** [`inc-43-ingesta-continua-bgg.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-43-ingesta-continua-bgg.md).
- **Módulos del Sistema:** [`29-ingesta-continua-novedades-bgg.md`](file:///c:/repos/Ludeka/docs/specs/sistema/29-ingesta-continua-novedades-bgg.md).

---

## Incremento 44: Worker de Recolección Multicanal Automática (YouTube RSS, Telegram, Feeds de Editoriales e Instagram)
- **Identificador SDD:** `change-44-social-collector-worker`
- **Objetivo Principal:** Sondeo desatendido y bajo demanda de fuentes y canales monitorizados en `MonitoredSocialAccount` sin APIs de pago ni servicios externos de scraping (cero Apify):
  1. Feeds Atom oficiales de canales de YouTube (`/feeds/videos.xml?channel_id=...`) con resolución de `@handle`.
  2. Extracción limpia de la vista web pública oficial `https://t.me/s/{canal}` de canales de difusión abiertos de Telegram.
  3. Soporte universal de feeds RSS 2.0 y Atom de blogs y webs de editoriales y tiendas.
  4. Estrategia híbrida para Instagram con plantillas de RSS-Bridge, crawler no invasivo de cortesía y modo simulado para desarrollo/CI.
  5. Orquestador `ISocialCollectorService` con deduplicación estricta contra `SocialInboxItems` y depósito en estado `PendingReview` con análisis de IA y miniaturas WebP en Cloudflare R2.
  6. Servicio en segundo plano `SocialCollectorHostedService` y controles interactivos en `/admin/canales-monitorizados` e `/admin/ingesta-social`.
- **Estado:** ✅ **Completado y Archivado** (suite 988/988 en verde al 100%; +20 pruebas unitarias nuevas sin dependencias de mock).
- **Documento:** [`inc-44-social-collector-worker.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-44-social-collector-worker.md).
- **Módulos del Sistema:** [`30-recolector-canales-sociales.md`](file:///c:/repos/Ludeka/docs/specs/sistema/30-recolector-canales-sociales.md) y [`28-hub-ingesta-social-moderacion.md`](file:///c:/repos/Ludeka/docs/specs/sistema/28-hub-ingesta-social-moderacion.md).

---

## Incremento 45: Radar de Bajadas de Precios, Mínimos Históricos y Alertas de Ofertas para 'Quiero comprar'
- **Identificador SDD:** `change-45-price-radar-discounts`
- **Objetivo Principal:** Registro histórico de precios de tiendas, detección de ofertas destacadas y notificaciones automáticas a usuarios que tengan el título en su radar de compra.
- **Estado:** ✅ **Completado y Archivado** (suite 1.005/1.005 en verde al 100%; +17 pruebas unitarias y de marcado nuevas sin dependencias de mock).
- **Documento:** [`inc-45-price-radar-discounts.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-45-price-radar-discounts.md).
- **Módulos del Sistema:** [`31-radar-precios-minimos-historicos.md`](file:///c:/repos/Ludeka/docs/specs/sistema/31-radar-precios-minimos-historicos.md).

---

## Incremento 46: Autenticación Real, Autorización por Permisos y Retirada de la Identidad Simulada
- **Identificador SDD:** `change-46-autenticacion-real`
- **Objetivo Principal:** Sustituir la identidad simulada (singleton mutable que concedía `FoundingTeam` a cualquier visitante) por una sesión autenticada y verificable:
  1. Acceso social de un solo clic con Google, Discord y Facebook opcional, dirigido por configuración (`Authentication__Providers__*`), sin cuentas con correo/contraseña ni infraestructura de correo.
  2. Entidad `ExternalLogin` con índice único `(Provider, ProviderKey)` y vinculación en cascada: par reincidente → correo verificado → alta `CommunityUser`/`None`; nunca `FoundingTeam` ni fusión por correo sin verificar.
  3. Cookie de sesión propia `ludeka.session` (`HttpOnly`, `SecurePolicy.Always`, `SameSite=Lax`, caducidad deslizante), `AddCascadingAuthenticationState` y `AuthorizeRouteView` con `RedirectToLogin`.
  4. 11 políticas de permiso sobre 12 banderas (`All = 4095`), `[Authorize(Policy)]` en 10 páginas (14 rutas con alias) y `PermissionAuthorizationHandler` que relee el `AppUser` y deniega a `Suspended`.
  5. Revalidación de sesión y permiso en los 15 servicios administrativos de escritura (`SessionIdentity` e `ISessionPermissionGuard`, relectura sin rastreo) con rutas de sistema para los ciclos programados.
  6. Retirada de `DefaultCurrentUserService`, `SwitchRole`/`SwitchUser` y sus cinco puntos de interfaz; invalidación de circuito (`IUserSessionInvalidator` + `SessionGuard` con recarga forzada) al suspender una cuenta o cambiar permisos.
  7. Política de anonimia: navegación pública intacta sin sesión, escrituras con identidad solo con sesión real, sin `UserId` vacío ni usuario centinela.
- **Estado:** ✅ **Completado y Archivado** (suite 1.345/1.345 en verde al 100%; +340 pruebas sobre la base de 1.005; verificación independiente `pass_with_warnings` con 17/17 requisitos y 42/42 escenarios conformes y 0 hallazgos CRITICAL).
- **Documento:** [`inc-46-autenticacion-real.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-46-autenticacion-real.md).
- **Módulos del Sistema:** [`32-autenticacion-y-autorizacion.md`](file:///c:/repos/Ludeka/docs/specs/sistema/32-autenticacion-y-autorizacion.md), [`14-gestion-usuarios-permisos-y-auditoria.md`](file:///c:/repos/Ludeka/docs/specs/sistema/14-gestion-usuarios-permisos-y-auditoria.md) y [`09-arquitectura-y-despliegue.md`](file:///c:/repos/Ludeka/docs/specs/sistema/09-arquitectura-y-despliegue.md).

---

## Incremento 47: Trabajos en Segundo Plano Correctos en Google Cloud Run (Jobs, Scheduler y Outbox Persistente)
- **Identificador SDD:** `change-47-workers-cloud-run`
- **Objetivo Principal:** Corregir los cuatro trabajos en segundo plano, que hoy se ejecutan dentro del proceso web con estado en memoria y sin coordinación entre instancias, algo incorrecto por construcción con `--min-instances=0 --max-instances=5`:
  1. **Modelo de ejecución elegido por el maintainer (Rama A):** externalizar los cuatro trabajos a *Cloud Run Jobs* disparados por *Cloud Scheduler*, retirando los cuatro `AddHostedService` del host web (`Program.cs:166,215,244,319`). Elimina la ejecución duplicada por construcción, no por bloqueo. Descartada la alternativa de conservarlos en proceso con `pg_advisory_lock`.
  2. **Habilitador de pruebas de integración contra PostgreSQL real**, primero en el orden porque nada del outbox es probable sin él: los 40 ficheros de `tests/` usan `UseSqlite` y ninguno `UseNpgsql`, Testcontainers ni Respawn, y SQLite no implementa `FOR UPDATE SKIP LOCKED`. El paso `docker build` que ya convive con `dotnet test` en el mismo job de CI acredita demonio Docker disponible en el *runner*.
  3. **Outbox persistente real:** hoy la fila de `CommunityNotificationLog` se crea *después* de leer el mensaje del `Channel` en memoria (`CommunityNotificationService.cs:108` y `:157`), así que no protege nada. Pasa a escribirse en `EnqueueAsync`, con reclamación por `SELECT ... FOR UPDATE SKIP LOCKED`, contador de intentos y reintento. `ICommunityNotificationQueue` conserva `EnqueueAsync`, de modo que los dos productores no se tocan.
  4. **Idempotencia por ventana temporal**, construida entera: `NightlyCatalogingExecutionLog` solo tiene hoy un `HasIndex(l => l.StartedAt)` no único (`LudekaDbContext.cs:403`), sin restricción por ventana ni clave de periodo.
  5. **Observabilidad y código de salida** distinto de cero ante fallo, con métricas por ejecución persistidas en la bitácora, más la corrección de `NotificationQueueHealthCheck`, que hoy devuelve `Healthy` reportando solo el nombre del tipo de la cola y no detectaría la pérdida que aparenta vigilar.
  6. **Superficie de despliegue e IAM:** recurso(s) Cloud Run Job, cuatro Cloud Scheduler configurables (1×/día lote, 6 h radar, 120 min social, 5-15 min outbox), cuenta de servicio con `roles/run.invoker` y paso nuevo de pipeline, con `docs/deployment/` actualizado.
- **Estado:** ✅ **Archivado** el 2026-09-19. Ciclo SDD completo y entrega en cadena `stacked-to-main` de **26 PRs (#35–#61), todos mergeados**; cinco rebanadas hubo que partirlas contra el techo de 400 líneas, con una sola `size:exception` en todo el incremento. Suite al cierre: 1.537 pruebas unitarias y 9 de integración contra PostgreSQL real. Especificación viva en [`34-trabajos-en-segundo-plano-cloud-run.md`](file:///c:/repos/Ludeka/docs/specs/sistema/34-trabajos-en-segundo-plano-cloud-run.md). 🚨 **Su puerta de seguridad es de DESPLIEGUE, no de merge:** el host web ya no ejecuta trabajos en proceso, así que antes del primer despliegue real hay que seguir el orden de puesta en marcha de `docs/deployment/google-cloud-run.md` §9.0.
- **Documento:** [`inc-47-workers-cloud-run.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-47-workers-cloud-run.md).
- **Módulos del Sistema:** [`09-arquitectura-y-despliegue.md`](file:///c:/repos/Ludeka/docs/specs/sistema/09-arquitectura-y-despliegue.md), [`08-notificaciones-y-webhooks.md`](file:///c:/repos/Ludeka/docs/specs/sistema/08-notificaciones-y-webhooks.md) y [`18-deteccion-novedades-y-cola-nocturna.md`](file:///c:/repos/Ludeka/docs/specs/sistema/18-deteccion-novedades-y-cola-nocturna.md).

---

## Incremento 53: Ingesta Masiva Autónoma de Catálogo BGG (~8.000 Juegos) sin Manipulación Manual
- **Identificador SDD:** `change-53-ingesta-masiva-autonoma-bgg`
- **Objetivo Principal:** Descarga y poblado 100% autónomo del dataset de clasificación BGG en la tabla de Staging sin manipulación de archivos locales por parte del usuario:
  1. Descarga en streaming HTTP directo desde el dataset público y actualizado diariamente (`beefsack/bgg-ranking-historicals`), con filtrado comunitario (`usersrated >= 30`), resiliencia con fallback de 5 días y descompresión al vuelo sin sobrepasar 30 MB de memoria RAM.
  2. Botón de acción interactivo en `/admin/cola-catalogacion` con indicadores reactivos de progreso y guarda de permisos `CanEditGames`.
  3. Ejecución desatendida en `Ludeka.Jobs` con el runner `SeedStagingJobRunner` (`seed-staging`) y auto-siembra inteligente en Fase 3 de `nightly-cataloging` si staging está vacío.
- **Estado:** ✅ **Completado y Archivado** el 2026-09-21 (cadena de 3 PRs apilados, suite completa 1.604 unitarias + 10 de integración en verde al 100%, 0 fallos).
- **Documento:** [`inc-53-ingesta-masiva-autonoma-bgg.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-53-ingesta-masiva-autonoma-bgg.md).
- **Módulos del Sistema:** [`27-ingesta-masiva-bgg-galeria-geekdo-ia-lotes.md`](file:///c:/repos/Ludeka/docs/specs/sistema/27-ingesta-masiva-bgg-galeria-geekdo-ia-lotes.md) y [`34-trabajos-en-segundo-plano-cloud-run.md`](file:///c:/repos/Ludeka/docs/specs/sistema/34-trabajos-en-segundo-plano-cloud-run.md).

---

## Incremento 54: Padrón Exhaustivo y Mecanismo de Carga del Directorio Lúdico Español
- **Identificador SDD:** `change-54-directorio-exhaustivo`
- **Objetivo Principal:** Incorporar el padrón nacional exhaustivo de la industria de juegos de mesa en España y mecanismos autónomos de siembra e idempotencia sin requerir inserción manual:
  1. **Dataset Canónico Embebido (`seed-directory.json`):** 46 editoriales de España, 37 tiendas especializadas (35 en España + 2 internacionales) y 35 creadores de contenido audiovisual en español, con webs oficiales, logos en alta resolución y redes sociales vinculadas (`SocialNetworkLink`).
  2. **Motor de Siembra Aditiva e Idempotencia (`DirectorySeeder` / `DirectorySeederService`):** Deserialización del recurso embebido con fallback a disco, enriquecimiento de campos nulos/vacíos sin sobrescribir ediciones manuales previas y preservación de la purga de diseñadores legados (INC-31). Desacoplado con `IDbContextFactory` para concurrencia segura en Blazor Server.
  3. **Mecanismo de Ejecución CLI (`Ludeka.Jobs`):** Runner `SeedDirectoryJobRunner` registrado como `seed-directory`, gobernado por `IJobExecutionCoordinator` y con inicialización defensiva de SQLite en entornos locales.
  4. **Mecanismo de Ejecución Web:** Botón y panel interactivo "Sincronizar Padrón Completo" en `/admin/cola-catalogacion` (`CatalogQueueAdmin.razor`) y botones directos en `/editoriales`, `/tiendas` y `/creadores` con control de permisos fundadores.
- **Estado:** ✅ **Completado y Archivado** el 2026-09-22 (PR #98 mergeado, suite completa 1.622 unitarias + 10 de integración en verde al 100%, 0 fallos).
- **Documento:** [`inc-54-directorio-exhaustivo.md`](archive/inc-54-directorio-exhaustivo.md).
- **Módulos del Sistema:** [`13-directorio-editoriales-creadores-tiendas.md`](sistema/13-directorio-editoriales-creadores-tiendas.md) y [`34-trabajos-en-segundo-plano-cloud-run.md`](sistema/34-trabajos-en-segundo-plano-cloud-run.md).

---

## 📦 Backlog 2026-09-22: Marca, Catálogo, Móvil, Cuenta y Gamificación

Backlog de catorce incrementos (INC-55…INC-68) agrupado en seis fases, con orden lógico sugerido y dependencias documentadas en cada `inc-NN-*.md` de [`docs/increments/`](file:///c:/repos/Ludeka/docs/increments/):

1. **Fase A — Marca y Comunidad:** INC-55, INC-56.
2. **Fase B — Catálogo:** INC-57, INC-59, INC-58.
3. **Fase C — Móvil:** INC-60.
4. **Fase D — Cuenta (tras INC-50):** INC-61, INC-62, INC-63, INC-64.
5. **Fase E — Comunidad y Compra:** INC-65, INC-66.
6. **Fase F — Gamificación:** INC-67, INC-68.

---

## Incremento 55: Retirada del Tagline de Marca y Unificación de la Identidad
- **Identificador SDD:** `change-55-tagline-identidad-marca`
- **Objetivo Principal:** Retirar de todas las superficies vivas el claim de marca disperso en 44 menciones (config, docs, manifest, CSS, UI y prompt de Gemini) y adoptar el nuevo lema oficial: «Juegos, sorteos, eventos y opiniones de verdad. Bienvenido a tu mesa.».
- **Estado:** ✅ **Completado y Archivado** (PR #102, 2026-09-23; 1.639 pruebas unitarias + 10 de integración en verde al 100%).
- **Documento:** [`inc-55-tagline-identidad-marca.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-55-tagline-identidad-marca.md).
- **Módulos del Sistema:** [`15-dashboard-inicio-editorial.md`](file:///c:/repos/Ludeka/docs/specs/sistema/15-dashboard-inicio-editorial.md) y [`22-portada-y-directorio-creadores.md`](file:///c:/repos/Ludeka/docs/specs/sistema/22-portada-y-directorio-creadores.md).

---

## Incremento 56: Comunidad, Mecenazgo y Enlaces de Apoyo
- **Identificador SDD:** `change-56-comunidad-mecenazgo`
- **Objetivo Principal:** Visibilidad de canales de comunidad, vías de mecenazgo y consolidación del motor de afiliados:
  1. **Vía de Mecenazgo Ko-fi:** Propiedad tipada `KofiUrl` en `CommunityNotificationOptions` (`https://ko-fi.com/ludeka` por defecto, configurable por variable de entorno `CommunityNotifications__KofiUrl`).
  2. **Interfaz Accesible:** Botón accesible de Ko-fi en el pie de página de `MainLayout.razor` junto a Discord y Telegram; consumo reactivo de URLs dinámicas en `Transparency.razor` eliminando enlaces genéricos hardcodeados.
  3. **Afiliación Amazon Oficial:** Soporte nativo de `Amazon` en `AffiliateOptions.CreateDefaultRules()` (`tag=ludeka-21`, dominio `amazon.es`) y en `appsettings.json` / `docker-compose.prod.yml`.
- **Estado:** ✅ **Completado y Archivado** el 2026-09-23 (suite completa 1.646 unitarias + 10 de integración en verde al 100%, 0 fallos).
- **Documento:** [`inc-56-comunidad-mecenazgo.md`](../increments/archive/inc-56-comunidad-mecenazgo.md).
- **Módulos del Sistema:** [`08-notificaciones-y-webhooks.md`](sistema/08-notificaciones-y-webhooks.md) y [`25-motor-afiliados-y-atribucion-comunitaria.md`](sistema/25-motor-afiliados-y-atribucion-comunitaria.md).

---

## Incremento 57: Diagnóstico y Optimización de Imágenes del Catálogo
- **Identificador SDD:** `change-57-imagenes-catalogo`
- **Objetivo Principal:** Medir antes de cortar: diagnóstico fino de pesos y tiempos del pipeline R2 + SkiaSharp + WebP (ya operativo desde INC-40), y solo después aplicar `<picture>` sistemático, dimensiones intrínsecas y revisión de la caché `immutable`.
- **Estado:** ✅ **Completado y Archivado** el 2026-09-23 (PR #104, suite completa 1.653 unitarias + 10 de integración en verde al 100%, 0 fallos).
- **Documento:** [`inc-57-imagenes-catalogo.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-57-imagenes-catalogo.md).
- **Módulos del Sistema:** [`26-almacenamiento-medios-r2-skiasharp.md`](file:///c:/repos/Ludeka/docs/specs/sistema/26-almacenamiento-medios-r2-skiasharp.md).

---

## Incremento 58: Paginación Real del Catálogo y Modos de Vista (Cuadrícula y Lista)
- **Identificador SDD:** `change-58-paginacion-catalogo`
- **Objetivo Principal:** Cerrar la contradicción entre el backend (que ya pagina con `Skip`/`Take` y devuelve `TotalCount`) y la UI (que muestra «solo un puñado»): paginación visible, filtros+página en URL, patrón reutilizado de `AuditLogViewer.razor` y modo de vista conmutable cuadrícula ↔ lista.
- **Estado:** ✅ **Completado y Archivado** el 2026-09-24 (PR #106, suite completa 1.668 unitarias + 10 de integración en verde al 100%, 0 fallos).
- **Documento:** [`inc-58-paginacion-catalogo.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-58-paginacion-catalogo.md).
- **Módulos del Sistema:** [`01-catalogo-y-fichas.md`](file:///c:/repos/Ludeka/docs/specs/sistema/01-catalogo-y-fichas.md).

---

## Incremento 59: Filtros del Catálogo y Tamaño en Mesa
- **Identificador SDD:** `change-59-filtros-y-tamano-mesa`
- **Objetivo Principal:** Auditar filtro a filtro `GameFilterCriteria` (conectado en UI vs roto) y exponer `TableFootprint` (ya en el dominio) junto a jugadores y `PlayingTimeMinutes`, para responder «¿me cabe en la mesa?».
- **Estado:** ✅ Archivado (entregado el 2026-09-24, PR #108, 1.687 pruebas).
- **Documento:** [`inc-59-filtros-y-tamano-mesa.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-59-filtros-y-tamano-mesa.md).
- **Módulos del Sistema:** [`01-catalogo-y-fichas.md`](file:///c:/repos/Ludeka/docs/specs/sistema/01-catalogo-y-fichas.md).

---

## Incremento 60: Navegación Móvil — Barra Inferior y Safe-Area
- **Identificador SDD:** `change-60-navegacion-movil`
- **Objetivo Principal:** Construir la navegación móvil que hoy no existe (cero matches de `BottomNav`/`safe-area`): barra inferior al alcance del pulgar con acceso a las acciones de colección, respetando `env(safe-area-inset-*)` y sin duplicar el header en escritorio.
- **Estado:** ✅ Archivado (entregado el 2026-09-24, 1.695 pruebas al 100% en verde).
- **Documento:** [`inc-60-navegacion-movil.md`](file:///c:/repos/Ludeka/docs/increments/archive/inc-60-navegacion-movil.md).
- **Módulos del Sistema:** [`38-navegacion-movil-y-safe-area.md`](file:///c:/repos/Ludeka/docs/specs/sistema/38-navegacion-movil-y-safe-area.md).

---

## Incremento 61: Menú de Cuenta y Estado de Sesión en la Cabecera
- **Identificador SDD:** `change-61-menu-de-cuenta`
- **Objetivo Principal:** Que la cabecera reconozca al usuario: menú de cuenta (perfil, preferencias, conexiones, ludoteca, salir) colgando del hub de INC-50, con puerta clara a `/login` para invitados (hoy solo existe `AccountEmailNotice` como componente consciente de sesión).
- **Estado:** ✅ **Completado y Archivado** el 2026-09-25 (PR #111, 1.701 pruebas en verde).
- **Documento:** [`inc-61-menu-de-cuenta.md`](archive/inc-61-menu-de-cuenta.md).
- **Módulos del Sistema:** [`32-autenticacion-y-autorizacion.md`](sistema/32-autenticacion-y-autorizacion.md) y [`37-area-de-cuenta-y-puerta-de-acceso.md`](sistema/37-area-de-cuenta-y-puerta-de-acceso.md).

---

## Incremento 62: Preferencias de Usuario — Apariencia, País y Privacidad
- **Identificador SDD:** `change-62-preferencias-usuario`
- **Objetivo Principal:** Exponer preferencias de usuario (tema vía `NormalizeTheme`, país vía `CountryCatalog` y visibilidad pública de perfil en `UserPreference.HidePublicProfile`) en pantallas estables del área de cuenta (`/cuenta/apariencia`, `/cuenta/pais`, `/cuenta/privacidad`), con limpieza de utilidades en cabecera (`MainLayout.razor`) y menú de cuenta con 7 accesos canónicos sin salto intermedio a `/cuenta`.
- **Estado:** ✅ **Completado y Archivado** el 2026-09-25 (1.703 pruebas unitarias al 100% en verde).
- **Documento:** [`inc-62-preferencias-usuario.md`](archive/inc-62-preferencias-usuario.md).
- **Módulos del Sistema:** [`39-preferencias-de-usuario-privacidad-y-navegacion.md`](sistema/39-preferencias-de-usuario-privacidad-y-navegacion.md).

---

## Incremento 63: Conexiones OAuth — Múltiples Proveedores sin Cuentas Duplicadas
- **Identificador SDD:** `change-63-conexiones-oauth`
- **Objetivo Principal:** Garantizar multi-proveedor → misma cuenta (Google + Discord, entrar con cualquiera sin duplicar cuentas) y endurecer la vinculación heredada de INC-49: política de última cuenta en `UnlinkAsync`, destino real de `ProviderEmailVerifiedAt` (hoy campo huérfano), tests de `ExternalLoginCollisionException` y alcance de la pantalla de conexiones desde el menú.
- **Estado:** ⏳ Planificado (backlog 2026-09-22, Fase D).
- **Documento:** [`inc-63-conexiones-oauth.md`](file:///c:/repos/Ludeka/docs/increments/inc-63-conexiones-oauth.md).

---

## Incremento 64: Acceso por Correo con Verificación (Evaluar e Implantar si se Aprueba)
- **Identificador SDD:** `change-64-acceso-por-correo`
- **Objetivo Principal:** Valorar (puerta de decisión del maintainer) el login por email con verificación de buzón y, si se aprueba, implantarlo —con la bifurcación abierta contraseña vs magic link— cerrando la contradicción con la promesa de `Login.razor:15`, sin dejar ninguna combinación de estado sin puerta de acceso.
- **Estado:** ⏳ Planificado (backlog 2026-09-22, Fase D).
- **Documento:** [`inc-64-acceso-por-correo.md`](file:///c:/repos/Ludeka/docs/increments/inc-64-acceso-por-correo.md).

---

## Incremento 65: «Me gusta» en Editoriales, Tiendas, Creadores y Vídeos
- **Identificador SDD:** `change-65-likes-comunidad`
- **Objetivo Principal:** Añadir el «me gusta» de usuario (solo logueados) sobre editoriales, tiendas, creadores y vídeos multimedia —el único `LikesCount` actual es de posts Instagram—, con like idempotente, conteo, ordenación de listados y vídeos por me gusta y frontera nominal explícita con `MediaItem.LikesCount`.
- **Estado:** ⏳ Planificado (backlog 2026-09-22, Fase E).
- **Documento:** [`inc-65-likes-comunidad.md`](file:///c:/repos/Ludeka/docs/increments/inc-65-likes-comunidad.md).

---

## Incremento 66: Fundas de Cartas — Calidad de Datos y Enlaces de Compra
- **Identificador SDD:** `change-66-fundas-cartas`
- **Objetivo Principal:** Acreditar la calidad del dato `boardgamecardsleeve` de BGG sobre el catálogo ingestado, validar medidas en `SleeveItem`, testear `SleeveStoreUrlResolver` por tienda y decidir Amazon con tag de afiliado (alineado con INC-56), sin fabricar medidas cuando no haya dato.
- **Estado:** ⏳ Planificado (backlog 2026-09-22, Fase E).
- **Documento:** [`inc-66-fundas-cartas.md`](file:///c:/repos/Ludeka/docs/increments/inc-66-fundas-cartas.md).

---

## Incremento 67: Gamificación — Hitos y Logros del Jugador
- **Identificador SDD:** `change-67-hitos-y-logros`
- **Objetivo Principal:** Sistema de hitos del jugador (colección, diario, comunidad) con desbloqueo idempotente y fechado, en coherencia declarada con `ComputePlayerBadge` de INC-15 y sin comparación entre usuarios (frontera con INC-68).
- **Estado:** ⏳ Planificado (backlog 2026-09-22, Fase F).
- **Documento:** [`inc-67-hitos-y-logros.md`](file:///c:/repos/Ludeka/docs/increments/inc-67-hitos-y-logros.md).

---

## Incremento 68: Gamificación — Clasificaciones Públicas y Anonimato
- **Identificador SDD:** `change-68-clasificaciones-y-anonimato`
- **Objetivo Principal:** Clasificaciones de jugadores con opt-in explícito, seudónimo no derivado de la cuenta y ventana temporal, separadas del ranking de juegos (§3.2 del spec), con anti-trampas básico. Último del backlog por su riesgo de privacidad.
- **Estado:** ⏳ Planificado (backlog 2026-09-22, Fase F).
- **Documento:** [`inc-68-clasificaciones-y-anonimato.md`](file:///c:/repos/Ludeka/docs/increments/inc-68-clasificaciones-y-anonimato.md).

---

## Convención de Trabajo para Cada Incremento (Ciclo SDD)

Cada incremento se ejecutará siguiendo estrictamente las 7 fases de Spec-Driven Development:
1. `sdd-explore`: Análisis del estado actual del código y requerimientos del slice.
2. `sdd-propose`: Propuesta de arquitectura y alcance con aprobación previa.
3. `sdd-spec`: Especificación de requerimientos técnicos y criterios de aceptación Gherkin.
4. `sdd-design`: Diseño de clases, interfaces, endpoints y componentes Razor.
5. `sdd-tasks`: Checklist de tareas secuenciadas.
6. `sdd-apply`: Implementación rigurosa con pruebas unitarias en verde.
7. `sdd-verify`: Verificación independiente contra requerimientos antes de cerrar el ciclo.
