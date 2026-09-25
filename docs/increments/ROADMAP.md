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
| **INC-47** | Trabajos en Segundo Plano Correctos en Google Cloud Run (Jobs, Scheduler y Outbox Persistente) | ✅ Archivado | [inc-47-workers-cloud-run.md](archive/inc-47-workers-cloud-run.md) |
| **INC-48** | Persistencia de Producción en PostgreSQL, Medios en Cloudflare R2 con Fallback Local y Verdad Documental | ✅ Archivado | [inc-48-persistencia-produccion-postgres.md](archive/inc-48-persistencia-produccion-postgres.md) |
| **INC-49** | Vinculación de Cuentas entre Proveedores, Recuperación de Acceso y Política de Correo Ausente | ✅ Archivado | [inc-49-vinculacion-cuentas.md](archive/inc-49-vinculacion-cuentas.md) |
| **INC-50** | Área de Cuenta: Puerta de Acceso en la Cabecera y Hub del Usuario | ✅ Archivado | [inc-50-area-de-cuenta.md](archive/inc-50-area-de-cuenta.md) |
| **INC-51** | Huecos de Cobertura y Desviaciones Destapados al Archivar INC-40, INC-42 e INC-43 | ⏳ Planificado | [inc-51-huecos-cobertura-archivado.md](inc-51-huecos-cobertura-archivado.md) |
| **INC-52** | Autenticación y Acceso Administrativo en el Primer Despliegue de Producción | ✅ Archivado | [inc-52-autenticacion-en-el-despliegue.md](archive/inc-52-autenticacion-en-el-despliegue.md) |
| **INC-53** | Ingesta Masiva Autónoma de Catálogo BGG (~8.000 Juegos) sin Manipulación Manual | ✅ Archivado | [inc-53-ingesta-masiva-autonoma-bgg.md](archive/inc-53-ingesta-masiva-autonoma-bgg.md) |
| **INC-54** | Padrón Exhaustivo y Mecanismo de Carga del Directorio Lúdico Español | ✅ Archivado | [inc-54-directorio-exhaustivo.md](archive/inc-54-directorio-exhaustivo.md) |
| **INC-55** | Retirada del Tagline de Marca y Unificación de la Identidad | ✅ Archivado | [inc-55-tagline-identidad-marca.md](archive/inc-55-tagline-identidad-marca.md) |
| **INC-56** | Comunidad, Mecenazgo y Enlaces de Apoyo | ✅ Archivado | [inc-56-comunidad-mecenazgo.md](archive/inc-56-comunidad-mecenazgo.md) |
| **INC-57** | Diagnóstico y Optimización de Imágenes del Catálogo | ✅ Archivado | [inc-57-imagenes-catalogo.md](archive/inc-57-imagenes-catalogo.md) |
| **INC-58** | Paginación Real del Catálogo y Modos de Vista (Cuadrícula y Lista) | ✅ Archivado | [inc-58-paginacion-catalogo.md](archive/inc-58-paginacion-catalogo.md) |
| **INC-59** | Filtros del Catálogo y Tamaño en Mesa | ✅ Archivado | [inc-59-filtros-y-tamano-mesa.md](archive/inc-59-filtros-y-tamano-mesa.md) |
| **INC-60** | Navegación Móvil: Barra Inferior y Safe-Area | ✅ Archivado | [inc-60-navegacion-movil.md](archive/inc-60-navegacion-movil.md) |
| **INC-61** | Menú de Cuenta y Estado de Sesión en la Cabecera | ✅ Archivado | [inc-61-menu-de-cuenta.md](archive/inc-61-menu-de-cuenta.md) |
| **INC-62** | Preferencias de Usuario: Tema y País | ✅ Archivado | [inc-62-preferencias-usuario.md](archive/inc-62-preferencias-usuario.md) |
| **INC-63** | Conexiones OAuth: Múltiples Proveedores sin Cuentas Duplicadas | ✅ Archivado | [inc-63-conexiones-oauth.md](archive/inc-63-conexiones-oauth.md) |
| **INC-64** | Acceso por Correo con Verificación (Magic Link sin Contraseñas) | ✅ Archivado | [inc-64-acceso-por-correo.md](archive/inc-64-acceso-por-correo.md) |
| **INC-65** | «Me gusta» en Editoriales, Tiendas, Creadores y Vídeos | ✅ Archivado | [inc-65-likes-comunidad.md](archive/inc-65-likes-comunidad.md) |
| **INC-66** | Fundas de Cartas: Calidad de Datos y Enlaces de Compra | ✅ Archivado | [inc-66-fundas-cartas.md](archive/inc-66-fundas-cartas.md) |
| **INC-67** | Gamificación: Hitos y Logros del Jugador | ⏳ En progreso | [inc-67-hitos-y-logros.md](inc-67-hitos-y-logros.md) |
| **INC-68** | Gamificación: Clasificaciones Públicas y Anonimato | ⏳ Planificado | [inc-68-clasificaciones-y-anonimato.md](inc-68-clasificaciones-y-anonimato.md) |

### 🧭 Orden lógico sugerido para INC-55…INC-68

Backlog del 2026-09-22 agrupado en seis fases. El orden intra-fase es el de numeración; entre fases, respetar dependencias documentadas en cada `inc-NN-*.md`:

1. **Fase A — Marca y Comunidad:** INC-55 (tagline/identidad) y INC-56 (comunidad y mecenazgo). Sin dependencias; hacerlos primero porque fijan el tono y la transparencia del resto.
2. **Fase B — Catálogo:** INC-57 (imágenes, con diagnóstico antes de tocar nada), luego INC-58 (paginación y modos de vista) y después INC-59 (filtros avanzados y tamaño en mesa), que se apoya en la paginación y comparte el estado de URL.
3. **Fase C — Móvil:** INC-60 (barra inferior y safe-area). Requiere decidir destinos con INC-61 en el horizonte.
4. **Fase D — Cuenta (tras INC-50):** INC-61 (menú de cabecera), INC-62 (preferencias), INC-63 (conexiones OAuth) e INC-64 (acceso por correo). INC-61 cuelga del hub de INC-50; INC-63/64 deben resolverse juntas para fijar la frontera correo vs OAuth.
5. **Fase E — Comunidad y Compra:** INC-65 («me gusta») y INC-66 (fundas). INC-66 alinea su afiliación con lo decidido en INC-56.
6. **Fase F — Gamificación:** INC-67 (hitos) y, al final del backlog, INC-68 (clasificaciones y anonimato). INC-68 consume las señales de INC-65/67 y debe ser el último por su riesgo de privacidad.

## 🚨 Bloqueo de Salida a Producción

**INC-47** quedó archivado el 2026-09-19 (26 PRs mergeadas). **INC-48** quedó archivado el 2026-09-20 (12 PRs mergeadas, #63–#74: nueve de entrega y tres de archivado). Ambos **prerrequisitos de salida a producción están ahora cerrados**.

🚨 **Antes del primer despliegue real, sea cual sea el incremento que lo dispare:** el host web ya no ejecuta ningún trabajo de negocio en proceso (INC-47), así que el único ejecutor son los Cloud Run Jobs. Configurar `GCP_PROJECT_ID` y `GCP_SA_KEY` en los secretos de GitHub es lo que arma el despliegue automático. **Sigue estrictamente el orden de puesta en marcha de [`docs/deployment/google-cloud-run.md` §9.0](file:///c:/repos/Ludeka/docs/deployment/google-cloud-run.md)** o tendrás un servicio web en producción sin nadie que ejecute la catalogación nocturna, el radar de precios, el recolector social ni el drenaje del outbox.

- **INC-47** — archivado el 2026-09-19. Resuelve la duplicación de trabajos al escalar y la pérdida de notificaciones al escalar a cero. **Nota:** el PR #60 (retirada de `AddHostedService`) está **fusionado desde el 2026-09-19**, no abierto. El gate real no es de *merge*, sino de **despliegue** (ver el aviso de arriba): antes de configurar `GCP_PROJECT_ID` y `GCP_SA_KEY`, sigue el orden de puesta en marcha de `docs/deployment/google-cloud-run.md` §9.0.
- **INC-48** — archivado el 2026-09-20. Resuelve: (1) imágenes en memoria, (2) fallback local sin HTTP, (3) arranque silencioso a SQLite en Production, (4) sondas de salud que mienten. Verificación `pass_with_warnings`, 1.565 unitarias + 10 de integración verdes.

**INC-46 quedó archivado el 2026-09-16**: la identidad simulada está retirada, cada ruta administrativa exige sesión y permiso, y toda escritura revalida la sesión.

## 🌿 Incrementos en Curso (Worktrees / PRs)

Un incremento activo = un worktree en `C:\repos\ludeka-wt\<slug>` + rama `inc/<slug>` + PR a `main`. Crear con `scripts/sdd-worktree.ps1 new <slug>`, cerrar con `pr <slug>` y limpiar con `done <slug>` tras el merge. Un solo escritor por worktree; los artefactos del incremento (specs, roadmap) viven en su rama y entran al PR.

- *(Ningún incremento en curso activo; INC-67 listo para PR y merge a `main`).*

*(INC-67 entregó su verificación con 1.843 pruebas unitarias en verde, y quedó archivado el 2026-09-26. Implementa el sistema de hitos y logros no invasivos del jugador con catálogo canónico de 10 hitos [Colección, Partidas, Comunidad], entidad inmutable UserMilestone, persistencia dual SQLite/PostgreSQL con clave natural [UserId, Type] y migración defensiva, servicio IMilestoneService con evaluación idempotente fechada preservando UnlockedAt original, convivencia armónica con el rango/rasgo ComputePlayerBadge de INC-15, componente editorial accesible MilestonesCard y vitrina en PublicProfile).*

*(INC-66 entregó su verificación con 1.817 pruebas unitarias en verde, y quedó archivado el 2026-09-25. Acredita la calidad y fiabilidad de los datos de fundas y enlaces de compra con validaciones de dominio en SleeveItem [30-250 mm], normalización de orientación y alias en StandardSleeveCatalog, erradicación de la inferencia silenciosa de 50 cartas en BggSleeveParser con soporte de conversión de pulgadas a mm, soporte oficial de Amazon con tag ludeka-21 e inclusión de Cuarto de Juegos y Tablerum con parámetros específicos en SleeveStoreUrlResolver, curación de Catán y Dixit en seed-games.json y presentación honesta sin datos en SleeveGuideCard).*

*(INC-65 entregó su verificación con 1.781 pruebas unitarias en verde, y quedó archivado el 2026-09-25. Implementa el sistema de «me gusta» de usuario para Editoriales, Tiendas, Creadores y Vídeos multimedia con LikeTargetType, voto idempotente por usuario autenticado, redirección interactiva de invitados a login preservando ReturnUrl, conteo agregado y ordenación determinista de listados y vídeos por me gusta comunitarios, distinción explícita frente al LikesCount externo de Instagram y componente accesible LikeButton integrado en directorios y multimedia).*

*(INC-64 entregó su verificación con 1.756 pruebas unitarias en verde, y quedó archivado el 2026-09-25. Implementa el acceso mediante Magic Link sin contraseñas con tokens de 32 bytes de alta entropía, hash SHA-256 en BD y caducidad de 15 minutos, persistencia dual SQLite/PostgreSQL, emisor simulado DevelopmentEmailSender para desarrollo, endpoints HTTP seguros /login/magic-link y /login/magic-link/request, protección contra redirecciones abiertas, erradicación de la contradicción "sin correo de confirmación" en Login.razor y conservación de proveedores sociales).*

*(INC-63 entregó su PR #113, verificada con 1.711 pruebas unitarias + 10 de integración en verde (1.721 en total), y quedó archivado el 2026-09-25. Garantiza de extremo a extremo la vinculación multi-proveedor sin duplicación de cuentas (Google + Discord), enriquece AccountConnectionDto y AccountConnectionsService con ProviderEmail y ProviderEmailVerifiedAt, deshabilita preventivamente el botón de desvinculación cuando solo resta un método de acceso único con indicativo visual accesible WCAG 2.2 AA y formaliza la suite de ciclo de vida MultiProviderLifecycleTests).*

*(INC-62 entregó su verificación con 1.703 pruebas unitarias en verde, y quedó archivado el 2026-09-25. Implementa la limpieza de cabecera en MainLayout, el menú desplegable AccountMenu con 7 destinos directos sin opción intermedia redundante, las pantallas dedicadas de Apariencia, País y Privacidad, y el control de visibilidad pública de perfil con migración SQLite DDL).*

*(INC-61 entregó su PR #111, verificada con 1.691 pruebas unitarias + 10 de integración en verde (1.701 en total), y quedó archivado el 2026-09-25. Implementa el menú desplegable de cuenta y estado de sesión en la cabecera superior de la aplicación para usuarios identificados, manteniendo una puerta clara de acceso para visitantes invitados).*

*(INC-60 entregó su verificación con 1.685 pruebas unitarias + 10 de integración en verde (1.695 en total), y quedó archivado el 2026-09-24. Implementa la barra de navegación inferior móvil fija al alcance del pulgar con 5 destinos canónicos, soporte integral de safe-area-inset con viewport-fit=cover, espaciador de reserva anti-solape, elevación de alertas del sistema, optimización táctil de CollectionActionBar y cumplimiento estricto WCAG 2.2 AA).*

*(INC-58 entregó su PR #106, verificada con 1.668 pruebas unitarias + 10 de integración en verde (1.678 en total), y quedó archivado el 2026-09-24. Implementa la paginación real del catálogo a 24 títulos por página, modos conmutables en memoria cuadrícula ↔ lista con GameListItem.razor, iconografía oficial Lucide, sincronización bidireccional en URL y preservación del historial popstate).*

*(INC-54 entregó su PR #98, verificada con 1.622 pruebas unitarias + 10 de integración en verde, y quedó archivado el 2026-09-22. Incorpora el padrón exhaustivo nacional de 46 editoriales de España, 37 tiendas y 35 creadores de contenido en seed-directory.json, con motor de siembra aditivo e idempotente, runner seed-directory en Ludeka.Jobs y panel de sincronización interactiva en administración y directorios).*

*(INC-53 entregó su cadena de 3 PRs apilados, verificada con 1.604 pruebas unitarias + 10 de integración en verde, y quedó archivado el 2026-09-21. Automatiza la ingesta de ~8.000 títulos de BGG en staging mediante streaming continuo HTTP desde mirror público diario con fallback resiliente de 5 días, botón en admin con permisos CanEditGames, runner seed-staging en Ludeka.Jobs y auto-siembra nocturna en Fase 3 de nightly-cataloging).*

*(INC-52 entregó su cadena de 8 PRs (#77–#84), toda mergeada a `main` el 2026-09-21, y quedó archivado ese mismo día. **Puerta del primer despliegue real.** Resolvió cinco defectos bloqueantes: `UseForwardedHeaders` no existía (B1, `redirect_uri` OAuth en `http` tras proxy), el pipeline no inyectaba credenciales de autenticación (B2, cero botones de acceso), el correo del administrador fundador se fijaba irreversiblemente en el primer arranque (B3, maintainer fuera de su panel), ningún registro advertía cuando cero proveedores estaban operativos (B4, logs silenciosos), y el ROADMAP mentía sobre el PR #60 abierto (B5, falsedad documental). Verificación: `pass_with_warnings`, 1.593 pruebas unitarias + 10 de integración verdes. **Nota:** la advertencia sobre el orden de puesta en marcha de `docs/deployment/google-cloud-run.md` §9.0 sigue vigente: antes de configurar `GCP_PROJECT_ID` y `GCP_SA_KEY`, sigue ese orden o el primer despliegue dejará producción sin ejecutor de trabajos.*

*(INC-49 completó su cadena de 7 PRs, mergeó a `main` el 2026-09-18 y quedó archivado el 2026-09-18; ya no figura aquí.)*

*(INC-47 entregó su cadena completa de 26 PRs (#35–#61), toda mergeada a `main` el 2026-09-19, y quedó archivado ese mismo día. 🚨 **Su puerta de seguridad sigue viva, pero es de DESPLIEGUE, no de merge:** el host web ya no ejecuta ningún trabajo en proceso, así que el único ejecutor son los Cloud Run Jobs. Se pudo mergear sin riesgo porque aún no existe entorno de producción —sin los secretos `GCP_PROJECT_ID` y `GCP_SA_KEY`, el flujo omite todo despliegue—. **Antes de configurar esos secretos, sigue el orden de puesta en marcha de `docs/deployment/google-cloud-run.md` §9.0**, o el primer despliegue dejará producción sin ningún ejecutor de trabajos.)*

*(INC-48 entregó su cadena de nueve PRs (#63–#71), toda mergeada a `main` el 2026-09-20, y quedó archivado ese mismo día con tres PRs apilados más (#72–#74), partidos porque el cierre sumaba 793 líneas. Resolvió cuatro defectos de producción: (1) imágenes en memoria en lugar de R2/disco, (2) fallback en disco con 404 por falta de middleware y desajuste de rutas, (3) arranque silencioso a SQLite efímera en Production, (4) sondas de salud que reportan valores codificados en lugar de estado real. Verificación: `pass_with_warnings`, 1.565 pruebas unitarias + 10 de integración verdes. Hueco abierto: no hay entorno de producción real para acreditar sondas de Cloud Run y despliegue.)*

*(El 2026-09-18, PR #32, se cerró el residuo de archivado SDD: `openspec/changes/` conservaba INC-40 a INC-45 sin archivar aunque ya estaban entregados y mergeados, así que el almacén canónico había dejado de ser fuente de verdad sobre qué está en curso. Los seis quedaron archivados y `gentle-ai sdd-status` devuelve `archived` para todos. La verificación contra el código real destapó tres huecos de trabajo que nunca se hizo, registrados en **INC-51**.)*

*(INC-50 entregó la puerta de acceso en la cabecera en MainLayout.razor, el hub centralizado /cuenta con inventario de secciones, la ruta canónica /cuenta/ludoteca con alias permanente /mi-ludoteca, la navegación compartida AccountSectionNav y la preservación interactiva de ReturnUrl con 1.639 pruebas unitarias + 10 de integración en verde, y quedó archivado el 2026-09-23.)*

*(INC-55 entregó la retirada del claim ajeno y la adopción del lema oficial «Juegos, sorteos, eventos y opiniones de verdad. Bienvenido a tu mesa.» en UI, PWA, prompts, User-Agent y especificaciones activas con 1.639 pruebas unitarias + 10 de integración en verde, y quedó archivado el 2026-09-23 en el PR #102).*

*(INC-56 entregó la visibilidad de los canales de comunidad Discord y Telegram y la vía de mecenazgo voluntario en Ko-fi en MainLayout.razor y Transparency.razor, junto con el soporte oficial de Amazon en AffiliateOptions y appsettings.json, verificado con 1.646 pruebas unitarias + 10 de integración en verde, y quedó archivado el 2026-09-23).*

*(INC-57 entregó el diagnóstico empírico de imágenes del catálogo (docs/specs/diagnostico-imagenes-catalogo.md), optimización masiva de miniaturas WebP a 400px en seed-games.json (reducción del payload de 4,8 MB a ~908 KB), dimensiones intrínsecas width/height en tarjetas de carril anti-CLS, decoding="async" y loading="lazy", verificado con 1.653 pruebas unitarias + 10 de integración en verde, y quedó archivado el 2026-09-23 en el PR #104).*






