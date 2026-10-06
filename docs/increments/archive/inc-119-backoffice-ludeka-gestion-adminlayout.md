# INC-119: Backoffice «Ludeka Gestión» (`AdminLayout.razor` y 11 Módulos de Mesa) y Directorios/Clasificación

## Estado
⏳ Planificado (Fase 7 y Cierre del Rediseño Integral «Revista Lúdica»)

## Rama y Worktree Sugerido
- **Rama:** `inc/rediseño-revista-ludica` (o rama atómica `inc/backoffice-ludeka-gestion-adminlayout`)
- **Worktree:** `C:\repos\ludeka-wt\rediseño-revista-ludica`

## Contexto y Motivación
Para completar el rediseño sin comprometer la productividad ni la ergonomía del equipo de moderadores y de la Mesa Fundadora, se independiza el entorno de gestión profesional en un layout propio (`AdminLayout.razor`) basado en `Ludeka Gestion.dc.html`, y se unifican las páginas públicas restantes (Sorteos, Novedades, Eventos, Directorios, Clasificación, Perfil Público, Login y Transparencia):
1. **`AdminLayout.razor` (Backoffice de Gestión):**
   - Cabecera compacta de 56 px (`--inverse`) con estado de sincronización y red.
   - Barra lateral retráctil de 248 px en escritorio con contadores de pendientes y 6 grupos: *General, Moderación, Catálogo, Comercial, Comunicación y Administración*.
   - Barra inferior móvil para staff en dispositivos táctiles.
   - Verificación estricta de permisos granulares (`ModeratorPermission`) y pantalla elegante de «Acceso restringido» para usuarios no autorizados.
   - Adaptación de las 11 bandejas existentes a tablas densas de 14 px con acciones rápidas: *Bandeja Social con Alta Exprés*, *Reportes de Catálogo*, *Moderación Multimedia*, *Cola BGG*, *Eventos*, *Cuentas Monitorizadas*, *Feeds Comerciales EAN*, *Generador Instagram 1:1*, *Notificaciones Webhook*, *Gestión de Usuarios RBAC* y *Auditoría Inmutable*.
2. **Páginas Públicas Secundarias:**
   - **Sorteos (`Radar.razor` en `/sorteos`):** Banda mostaza, polaroids con cuenta atrás y segmentación por país/estado.
   - **Novedades (`News.razor` en `/novedades`):** Banda inversa con calendario mensual agrupado y botones de aviso.
   - **Eventos (`Events.razor` en `/eventos`):** Banda azul con tarjetas pastel y botón «Me interesa».
   - **Clasificación (`Leaderboards.razor` en `/clasificaciones`):** Banda ciruela, selector mensual, podio oro/plata/bronce y tabla completa con respeto estricto al anonimato.
   - **Perfil público (`PublicProfile.razor` en `/u/{id}`):** Avatar mostaza a $-6^\circ$, estadísticas lúdicas y bloques privados según RGPD.
   - **Directorios (`Publishers`, `Creators`, `Stores`):** Fichas con iniciales pastel a $-4^\circ$, filtros por país/tipo y botón ♥.
   - **Acceso (`Login.razor` en `/login`):** Dos tarjetas balanceadas (ventajas en terracota a la izquierda, enlace mágico y OAuth a la derecha).
   - **Transparencia (`Transparency.razor` en `/transparencia`):** 4 secciones numeradas sobre financiación, veredictos y BGG.
3. **Pie de Página Global (`MainLayout.razor`):**
   - Bloque `--inverse` con grid de 4 columnas, créditos BGG, aviso de afiliados y soporte de simulación offline para desarrollo.

## Alcance de la Solución
1. **`src/Ludeka.Web/Components/Layout/AdminLayout.razor` (Nuevo)**:
   - Layout dedicado con barra lateral, guardias de rol y adaptación móvil.
2. **Migración de Páginas Administrativas a `@layout AdminLayout`**:
   - `SocialInboxModeration.razor`, `GameReportsModeration.razor`, `MediaModeration.razor`, `CatalogQueueAdmin.razor`, `EventsManagement.razor`, `MonitoredAccountsDirectory.razor`, `AffiliatesAdmin.razor`, `InstagramModeration.razor`, `AdminNotifications.razor`, `UserManagement.razor`, `AuditLogViewer.razor`.
3. **Actualización de Páginas Públicas Restantes**:
   - `Radar.razor`, `News.razor`, `Events.razor`, `Leaderboards.razor`, `PublicProfile.razor`, `PublishersDirectory.razor`, `PublisherDetail.razor`, `CreatorsDirectory.razor`, `CreatorDetail.razor`, `StoresDirectory.razor`, `StoreDetail.razor`, `Login.razor`, `Transparency.razor`.
4. **Pruebas de Integración y Seguridad**:
   - Tests de autorización RBAC en `AdminLayoutAuthorizationContractTests.cs`.
   - Verificación de la suite completa de más de 2.300 pruebas unitarias en verde.

## Criterios de Aceptación
- El acceso al panel de gestión valida estrictamente permisos y no expone datos sensibles a usuarios estándar.
- Toda la web pública comparte el mismo lenguaje visual coherente (tipografía, botones, paleta y microfísica).
- La suite completa de pruebas unitarias y de integración finaliza al 100% en verde.
