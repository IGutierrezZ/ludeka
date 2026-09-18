# 🗺️ Catálogo de Incrementos de Ludeka (Roadmap SDD)

Este documento es el índice central de los paquetes de trabajo e incrementos (*Vertical Slices*) de Ludeka bajo la metodología **Spec-Driven Development (SDD)**.

> **Regla de Ciclo de Vida:**
> 1. Cada incremento dispone de su documento individual con su alcance técnico y criterios.
> 2. Los incrementos pendientes se ubican en `docs/increments/`.
> 3. Todo incremento se desarrolla en su propio worktree y rama (`inc/<slug>` desde `main`), y se integra a `main` exclusivamente vía Pull Request (ver sección "Incrementos en Curso").
> 4. Al completarse y verificarse, el incremento se traslada a `docs/increments/archive/` y nutre la **Especificación Viva del Sistema** (`docs/specs/sistema/`).

---

## 📋 Registro Central de Incrementos

| ID | Título | Estado | Documento |
|---|---|---|---|
| **INC-01** | Catálogo Base, Ficha Inteligente y Semáforo de Escalabilidad | ✅ Archivado | [inc-01-core-catalog.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-01-core-catalog.md) |
| **INC-02** | Ludoteca Personal, Colección en 4 Estados y Préstamos | ✅ Archivado | [inc-02-library-loans.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-02-library-loans.md) |
| **INC-03** | Panel y Veredicto de la Mesa Fundadora | ✅ Archivado | [inc-03-founding-verdict.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-03-founding-verdict.md) |
| **INC-04** | Hub Multimedia (YouTube e Instagram) | ✅ Archivado | [inc-04-multimedia-hub.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-04-multimedia-hub.md) |
| **INC-05** | Importador BGG en 1 Clic y Auto-Catalogación | ✅ Archivado | [inc-05-bgg-importer.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-05-bgg-importer.md) |
| **INC-06** | Automatización Omnicanal, Radar de Sorteos y Comunidad | ✅ Archivado | [inc-06-automation-community.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-06-automation-community.md) |
| **INC-07** | Compilación de Producción, Assets y Rendimiento Web | ✅ Archivado | [inc-07-production-assets-perf.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-07-production-assets-perf.md) |
| **INC-08** | Fichas de Expansión, Ecosistema y Mezclador de Mesa | ✅ Archivado | [inc-08-game-expansions.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-08-game-expansions.md) |
| **INC-09** | Notificaciones y Webhooks de Comunidad (Discord y Telegram) | ✅ Archivado | [inc-09-notifications-webhooks.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-09-notifications-webhooks.md) |
| **INC-10** | Despliegue, Empaquetado Docker y Staging/Producción | ✅ Archivado | [inc-10-docker-deployment-staging.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-10-docker-deployment-staging.md) |
| **INC-11** | Enlaces de Compra en Tiendas y Afiliados Contextuales | ✅ Archivado | [inc-11-store-affiliate-links.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-11-store-affiliate-links.md) |
| **INC-12** | Simulación y Mock de API BGG (30 Juegos Base + 10 Expansiones Reales) | ✅ Archivado | [inc-12-mock-bgg-simulation.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-12-mock-bgg-simulation.md) |
| **INC-13** | Módulo de Síntesis con IA (Google Gemini / Heurística) para Fichas | ✅ Archivado | [inc-13-ai-game-summary.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-13-ai-game-summary.md) |
| **INC-14** | Búsqueda Quirúrgica y Enlace de YouTube en Tiempo Real | ✅ Archivado | [inc-14-youtube-live-search.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-14-youtube-live-search.md) |
| **INC-15** | Estadísticas Avanzadas de Colección y ADN del Jugador | ✅ Archivado | [inc-15-player-profile-stats.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-15-player-profile-stats.md) |
| **INC-16** | PWA (Progressive Web App) y Modo Consulta Offline para Ludoteca | ✅ Archivado | [inc-16-pwa-offline-library.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-16-pwa-offline-library.md) |
| **INC-17** | Sistema Comunitario de Reporte de Errores y Bandeja de Moderación de Fichas | ✅ Archivado | [inc-17-community-error-reports.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-17-community-error-reports.md) |
| **INC-18** | Editor Editorial de Fichas de Catálogo y Carga de Imágenes para Moderadores | ✅ Archivado | [inc-18-moderator-game-editor.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-18-moderator-game-editor.md) |
| **INC-19** | Directorio de Editoriales, Creadores y Tiendas: Fichas, Redes Sociales y Foco Multimedia | ✅ Archivado | [inc-19-publishers-creators-directory.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-19-publishers-creators-directory.md) |
| **INC-20** | Gestión de Usuarios, Permisos Granulares de Moderación y Auditoría para la Mesa Fundadora | ✅ Archivado | [inc-20-user-management-permissions-audit.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-20-user-management-permissions-audit.md) |
| **INC-21** | Dashboard de Inicio Editorial, Desacople de Catálogo, Limpieza de Navbar y Enlace Canónico BGG | ✅ Archivado | [inc-21-home-dashboard.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-21-home-dashboard.md) |
| **INC-22** | Segregación de Sorteos, Novedades y Nuevo Módulo de Grandes Eventos Lúdicos | ✅ Archivado | [inc-22-draws-news-events-split.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-22-draws-news-events-split.md) |
| **INC-23** | Categorización y Gestión Editorial de Vídeos y Multimedia en Fichas de Juego | ✅ Archivado | [inc-23-multimedia-editorial-categorization.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-23-multimedia-editorial-categorization.md) |
| **INC-24** | Detección Automática de Juegos en Novedades y Cola Nocturna Inteligente BGG/Gemini | ✅ Archivado | [inc-24-nightly-game-discovery-cataloging.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-24-nightly-game-discovery-cataloging.md) |
| **INC-25** | Auditoría de Resiliencia, Rate Limiting y Estrategia de Token en el Importador de Ludotecas BGG | ✅ Archivado | [inc-25-bgg-collection-resilience-token.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-25-bgg-collection-resilience-token.md) |
| **INC-26** | Especificación de Fundas (Sleeves) por Juego y Enlaces de Compra Contextuales | ✅ Archivado | [inc-26-card-sleeves-spec-stores.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-26-card-sleeves-spec-stores.md) |
| **INC-27** | Monitorización y Verificación de Stock en Tiempo Real en Enlaces de Compra | ✅ Archivado | [inc-27-store-live-stock-check.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-27-store-live-stock-check.md) |
| **INC-28** | Generador y Publicador Directo de Posts para Instagram en Moderación | ✅ Archivado | [inc-28-instagram-direct-publisher.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-28-instagram-direct-publisher.md) |
| **INC-29** | Localización Geográfica por País, Filtrado Territorial y Detección de Ubicación | ✅ Archivado | [inc-29-country-location-filtering.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-29-country-location-filtering.md) |
| **INC-30** | Colección en 3 Estados, Estado 'Jugado' Independiente, Radar de Compra y Diario de Partidas | ✅ Archivado | [inc-30-played-independent-status.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-30-played-independent-status.md) |
| **INC-31** | Portada Minimalista y Reorientación Autores → Creadores de Contenido | ✅ Archivado | [portada-minimalista-creadores (archivo SDD)](file:///c:/repos/Ludeka/openspec/changes/archive/2026-09-08-portada-minimalista-creadores/proposal.md) |
| **INC-32** | Fix HTTP 500 en Fichas de Juego (ORDER BY DateTimeOffset en SQLite) | ✅ Archivado | Fix directo (bugfix Strict TDD; ver `docs/specs/ROADMAP_MVP_SLICES.md`, Incremento 32) |
| **INC-33** | Fix 500 Latente en Mi Ludoteca (ORDER BY DateTimeOffset en GetByUserIdAsync) | ✅ Archivado | Fix directo (bugfix Strict TDD; ver `docs/specs/ROADMAP_MVP_SLICES.md`, Incremento 33) |
| **INC-34** | Barrido Sistémico de ORDER BY DateTimeOffset en Repositorios SQLite | ✅ Archivado | Fix directo (bugfix Strict TDD; ver `docs/specs/ROADMAP_MVP_SLICES.md`, Incremento 34) |
| **INC-35** | Portada Editorial: Narrativa Hogareña, Microinteracciones e Imágenes por Defecto | ✅ Archivado | [inc-35-portada-editorial.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-35-portada-editorial.md) |
| **INC-36** | Rediseño Editorial del Resto de Páginas (Catálogo, Fichas, Eventos, Sorteos, Novedades) + Fix Responsive del Hero | ✅ Archivado | [inc-36-rediseno-paginas-editoriales.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-36-rediseno-paginas-editoriales.md) |
| **INC-37** | Modo Producción: APIs Reales, Atribución BGG, Comunidad y Motor Privado de Afiliados | ✅ Archivado | [inc-37-apis-produccion-afiliados.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-37-apis-produccion-afiliados.md) |
| **INC-38** | Persistencia PostgreSQL en Supabase, Estrategia Dual y Herramientas de Migración y Backup | ✅ Archivado | [inc-38-postgresql-supabase.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-38-postgresql-supabase.md) |
| **INC-39** | Empaquetado Docker para Producción, Google Cloud Run y Pipeline CI/CD con Secretos | ✅ Archivado | [inc-39-docker-prod-cloudrun.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-39-docker-prod-cloudrun.md) |
| **INC-40** | Pipeline de Almacenamiento y Optimización de Medios (Cloudflare R2 + SkiaSharp + WebP) | ✅ Archivado | [inc-40-medios-r2-skiasharp.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-40-medios-r2-skiasharp.md) |
| **INC-41** | Ingesta Masiva de Catálogo BGG (~8.000 juegos), Fotos GeekDo y Síntesis IA en Lotes | ✅ Archivado | [inc-41-ingesta-bgg-catalogo.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-41-ingesta-bgg-catalogo.md) |
| **INC-42** | Hub de Ingesta Social y Multimedia (Bandeja de Moderación Editable + Alta Exprés + Canales) | ✅ Archivado | [inc-42-ingesta-social-moderacion.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-42-ingesta-social-moderacion.md) |
| **INC-43** | Ingesta Continua y Auto-Descubrimiento de Novedades BGG en el Lote Nocturno | ✅ Archivado | [inc-43-ingesta-continua-bgg.md](file:///c:/repos/Ludeka/docs/increments/archive/inc-43-ingesta-continua-bgg.md) |
| **INC-44** | Worker de Recolección Automática de Canales Sociales Monitorizados (YouTube RSS / Instagram) | ✅ Archivado | [inc-44-social-collector-worker.md](archive/inc-44-social-collector-worker.md) |
| **INC-45** | Radar de Bajadas de Precios, Mínimos Históricos y Alertas de Ofertas para 'Quiero comprar' | ✅ Archivado | [inc-45-price-radar-discounts.md](archive/inc-45-price-radar-discounts.md) |
| **INC-46** | Autenticación Real, Autorización por Roles y Retirada de la Identidad Simulada | ✅ Archivado | [inc-46-autenticacion-real.md](archive/inc-46-autenticacion-real.md) |
| **INC-47** | Trabajos en Segundo Plano Correctos en Google Cloud Run (Jobs, Scheduler y Outbox Persistente) | ⏳ Planificado | [inc-47-workers-cloud-run.md](inc-47-workers-cloud-run.md) |
| **INC-48** | Persistencia de Producción en PostgreSQL, Medios en Cloudflare R2 con Fallback Local y Verdad Documental | ⏳ En progreso (migración entregada) | [inc-48-persistencia-produccion-postgres.md](inc-48-persistencia-produccion-postgres.md) |
| **INC-49** | Vinculación de Cuentas entre Proveedores, Recuperación de Acceso y Política de Correo Ausente | ✅ Archivado | [inc-49-vinculacion-cuentas.md](archive/inc-49-vinculacion-cuentas.md) |
| **INC-50** | Área de Cuenta: Puerta de Acceso en la Cabecera y Hub del Usuario | ⏳ Planificado | [inc-50-area-de-cuenta.md](inc-50-area-de-cuenta.md) |

## 🚨 Bloqueo de Salida a Producción

Los incrementos **INC-47** e **INC-48** son **prerrequisitos de la salida a producción**. Hasta que ambos estén archivados, Ludeka no debe exponerse públicamente:

1. **INC-47** — sin él, los trabajos se duplican al escalar y las notificaciones se pierden al escalar a cero.
2. **INC-48** — sin él, el esquema de producción arranca incompleto y las imágenes no persisten.

**INC-46 quedó archivado el 2026-09-16**: la identidad simulada está retirada, cada ruta administrativa exige sesión y permiso y toda escritura revalida la sesión.

## 🌿 Incrementos en Curso (Worktrees / PRs)

Un incremento activo = un worktree en `C:\repos\ludeka-wt\<slug>` + rama `inc/<slug>` + PR a `main`. Crear con `scripts/sdd-worktree.ps1 new <slug>`, cerrar con `pr <slug>` y limpiar con `done <slug>` tras el merge. Un solo escritor por worktree; los artefactos del incremento (specs, roadmap) viven en su rama y entran al PR.

*(INC-49 completó su cadena de 7 PRs, mergeó a `main` y quedó archivado el 2026-09-18; ya no figura aquí.)*

- **INC-50 Área de Cuenta** — worktree `C:\repos\ludeka-wt\area-de-cuenta`, rama `inc/area-de-cuenta`. Nace de un agujero destapado en el smoke test de INC-49: se puede iniciar sesión, pero **no hay ninguna puerta a la cuenta**. El único enlace a `/cuenta/conexiones` vive dentro del aviso que solo se muestra a cuentas sin correo verificado, no existe ningún enlace a `/login` en toda la interfaz, y la cabecera no consulta la sesión. El incremento añade el botón de persona en la cabecera y agrupa la ludoteca y los ajustes de usuario en un área de cuenta. **Arrastra pendiente el smoke test de INC-49 (tarea 4.5), que quedó en el paso 2 de 7.**





