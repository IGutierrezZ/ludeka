# 09. Arquitectura, Persistencia y Despliegue

## 1. Visión General y Estándares Técnicos
Ludeka opera bajo **.NET 10 (C# 13)** estructurado en Clean Architecture con capas estrictamente desacopladas, persistencia relacional en **PostgreSQL (Supabase)** en producción y **SQLite** únicamente para pruebas automatizadas y desarrollo local, empaquetado Docker multi-stage y endpoints de diagnóstico de salud.

---

## 2. Capas de la Solución

| Proyecto | Tipo | Responsabilidad | Dependencias Externas |
|---|---|---|---|
| [`src/Ludeka.Core`](file:///c:/repos/Ludeka/src/Ludeka.Core) | Class Library | Entidades de dominio, Enums, Value Objects | **Ninguna** (cero dependencias) |
| [`src/Ludeka.Application`](file:///c:/repos/Ludeka/src/Ludeka.Application) | Class Library | Casos de uso, interfaces, DTOs, validaciones | `Ludeka.Core` |
| [`src/Ludeka.Infrastructure`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure) | Class Library | EF Core con doble proveedor (PostgreSQL en producción, SQLite en pruebas), cliente BGG, webhooks, seeder | `Ludeka.Core`, `Ludeka.Application`, EF Core (Npgsql + Sqlite) |
| [`src/Ludeka.Web`](file:///c:/repos/Ludeka/src/Ludeka.Web) | Blazor Web App | UI Blazor SSR + Server interactivo, Tailwind CSS | Todas las capas |
| [`tests/Ludeka.UnitTests`](file:///c:/repos/Ludeka/tests/Ludeka.UnitTests) | xUnit Project | Pruebas unitarias y de integración | xUnit, Moq, FluentAssertions |

---

## 3. Persistencia y Estrategia Dual (SQLite Local / PostgreSQL Supabase en Producción)

- **Contexto Central:** `LudekaDbContext` con configuración fluent, colecciones `jsonb` nativas (`ToJson()`), colecciones primitivas (`PrimitiveCollection`) y fechas normalizadas en UTC (`DateTimeOffset.UtcNow`).
- **Soporte Oficial PostgreSQL / Supabase (INC-38):**
  - Paquete: `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3` con soporte oficial de .NET 10.
  - Conmutación automática: Si `Database:Provider` es `PostgreSql` o la cadena de conexión contiene parámetros de PostgreSQL (`Host=`, `Server=`, `supabase.co` o esquema `postgres://`), la aplicación configura `options.UseNpgsql(...)` con política de reintentos (`EnableRetryOnFailure`). Si apunta a un fichero `.db` o `Sqlite`, configura `options.UseSqlite(...)`.
- **Reconciliador de Esquema SQLite (`SqliteSchemaMigrator`):**
  - Ubicación: [`src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs)
  - Inspecciona `PRAGMA table_info` al arrancar la aplicación en SQLite y añade dinámicamente columnas faltantes sin borrar ni reiniciar bases de datos de desarrollo. Se desactiva automáticamente cuando el proveedor es PostgreSQL.
- **Esquema Real y Fuente de Verdad del Esquema:**
  - El modelo consta de **34 tablas** reales, creadas por **7 migraciones**. La única fuente de verdad son las migraciones de Entity Framework Core en `src/Ludeka.Infrastructure/Migrations/`, aplicadas automáticamente con `MigrateAsync()` al arrancar contra PostgreSQL.
  - El script [`docs/database/supabase_schema.sql`](file:///c:/repos/Ludeka/docs/database/supabase_schema.sql) es un **derivado regenerado** desde esas migraciones (INC-48), no la fuente de verdad ni un fichero editable a mano. Dos pruebas automáticas impiden que vuelva a quedarse atrás: `SupabaseSchemaFreshnessTests` comprueba sin PostgreSQL que cada tabla del modelo tiene su `CREATE TABLE`, y `PostgresSchemaVerificationTests` migra desde cero contra un PostgreSQL 17 real y verifica las 34 tablas y las 7 filas de `__EFMigrationsHistory`. El comando de regeneración está en la cabecera del propio script.
- **Usuario Administrador Fundador Garantizado (`AdminUserSeeder`):**
  - Ubicación: [`src/Ludeka.Infrastructure/Seeding/AdminUserSeeder.cs`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Seeding/AdminUserSeeder.cs)
  - En entornos limpios de producción o desarrollo, garantiza de forma idempotente la existencia de un usuario con rol `FoundingTeam` y permisos totales (`ModeratorPermission.All`), parametrizable mediante `AdminUserOptions`. Desde INC-46 la fila **no concede identidad ni sesión implícita** a ningún visitante.
- **Sesión autenticada (INC-46):**
  - Cookie propia `ludeka.session` (`HttpOnly`, `Secure`, `SameSite=Lax`, caducidad deslizante) con esquemas sociales Google/Discord/Facebook dirigidos por configuración (`Authentication__Providers__*`); `/healthz` y `/ready` quedan anónimos para no romper los probes de Cloud Run. El detalle vive en el [módulo 32](file:///c:/repos/Ludeka/docs/specs/sistema/32-autenticacion-y-autorizacion.md).
- **Cero Datos Ficticios en Producción (`Database:SeedDemoData`):**
  - Los sembradores demostrativos (`CatalogSeeder`, `DirectorySeeder`, etc.) únicamente se ejecutan si `SeedDemoData = true` en entorno de desarrollo. En producción, la base de datos arranca limpia sin catálogo mock.
- **Copias de Seguridad Automatizadas (`scripts/supabase-backup.ps1`):**
  - Script en PowerShell que ejecuta `pg_dump` con compresión `.sql.gz` y purga automática con retención rotativa (7 días por defecto).

---

## 4. Empaquetado Docker, Orquestación y Despliegue en Google Cloud Run (INC-39)

- **Dockerfile Multi-Stage ([`Dockerfile`](file:///c:/repos/Ludeka/Dockerfile)):**
  - *Build CSS:* Node.js 20 con Tailwind CSS CLI minificado (`input.css` ➔ `app.css`).
  - *Build .NET:* SDK `mcr.microsoft.com/dotnet/sdk:10.0` compilando en Release.
  - *Runtime Seguro:* Imagen `mcr.microsoft.com/dotnet/aspnet:10.0` ejecutando bajo usuario sin privilegios (`USER app`).
  - *Adaptabilidad de Puerto:* Soporte dinámico para la variable de entorno `PORT` inyectada por Google Cloud Run en `Program.cs`.
  - *Sonda de Salud:* Sonda nativa Docker `HEALTHCHECK` contra `/healthz`.
- **Despliegue en Google Cloud Run (Free Tier):**
  - Configuración óptima: 512 MB de memoria, 1 vCPU y escalado a cero (`min-instances: 0`) para garantizar coste cero cuando no hay tráfico.
  - Guía paso a paso: [`docs/deployment/google-cloud-run.md`](file:///c:/repos/Ludeka/docs/deployment/google-cloud-run.md).
- **Docker Compose:**
  - [`docker-compose.yml`](file:///c:/repos/Ludeka/docker-compose.yml): Entorno local con SQLite.
  - [`docker-compose.staging.yml`](file:///c:/repos/Ludeka/docker-compose.staging.yml): Entorno de pruebas.
  - [`docker-compose.prod.yml`](file:///c:/repos/Ludeka/docker-compose.prod.yml): Entorno de producción con mapeo de variables de entorno y soporte `.env`.
- **Pipeline de Integración y Entrega Continua (GitHub Actions):**
  - Archivo: [`.github/workflows/ci-cd.yml`](file:///c:/repos/Ludeka/.github/workflows/ci-cd.yml).
  - *CI:* Ejecución automática en cada PR y push a ramas de incremento de compilación, verificación de Docker y suite completa de 1.565 pruebas unitarias + 10 de integración.
  - *CD:* Despliegue desatendido a Google Cloud Run al hacer merge a `main` si los secretos están configurados.

---

## 5. Diagnóstico de Salud (Health Checks)

- Ubicación: `src/Ludeka.Web/Health/` (`DatabaseHealthCheck`, `StorageHealthCheck`, `NotificationQueueHealthCheck`).
- Endpoints expuestos:
  - `/healthz`: Liveness check del proceso web, sin evaluación de dependencias.
  - `/ready`: Readiness check (health checks con tag `ready`) que evalúa la conectividad real con la base de datos configurada (`CanConnectAsync` + `SELECT 1` y recuento de juegos, con el proveedor efectivo — `Npgsql` o `Sqlite` — leído de `_dbContext.Database.ProviderName`, nunca de un literal fijo), el almacén de medios realmente configurado (credenciales de Cloudflare R2 válidas, o una escritura/lectura real contra la ruta local configurada, o degradación a memoria si no hay ninguno) y la disponibilidad de la cola de notificaciones.

---

## 6. Arquitectura PWA, Modo Consulta Offline y Activos de Marca (Favicon e Iconografía)

- **Favicon Oficial y Metadatos de Navegador (INC-83):**
  - Declaración en `<head>` de [`src/Ludeka.Web/Components/App.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/App.razor):
    - `<link rel="icon" type="image/svg+xml" href="/favicon.svg" />` (vectorial moderno con escalabilidad infinita).
    - `<link rel="icon" type="image/png" sizes="48x48" href="/favicon-48x48.png" />`, `32x32` y `16x16`.
    - `<link rel="shortcut icon" href="/favicon.ico" />` (archivo ICO multi-resolución de 16, 32 y 48 px).
    - `<link rel="apple-touch-icon" sizes="180x180" href="/apple-touch-icon.png" />` (icono de contacto táctil de 180×180 px con fondo opaco para iOS/iPadOS).
  - *Diseño de Identidad Lúdica:* Squircle de fondo Charcoal (`#18181b`), orlado fino en ámbar de marca (`#d97706`) e isotipo oficial en alto contraste (hexágono cian `#0ea5e9` con siglas «LDK» en blanco y dado 3D isométrico central con puntos naranjas), garantizando visibilidad óptima sobre cualquier pestaña clara u oscura.
- **Manifiesto Web Estándar W3C:**
  - Archivo: [`src/Ludeka.Web/wwwroot/manifest.webmanifest`](file:///c:/repos/Ludeka/src/Ludeka.Web/wwwroot/manifest.webmanifest)
  - Configuración: `display: standalone`, orientación responsiva portrait/any, color temático `#d97706` y fondo `#18181b`.
  - Iconografía: Iconos vectoriales SVG y rasterizados PNG cuadrados (192×192, 512×512) y variante `maskable` con margen de seguridad del 15% para compatibilidad total con el recorte de iconos adaptativos en Android e iOS.
- **Service Worker con Estrategia Dual:**
  - Archivo: [`src/Ludeka.Web/wwwroot/service-worker.js`](file:///c:/repos/Ludeka/src/Ludeka.Web/wwwroot/service-worker.js)
  - Estrategia:
    - *Cache-First:* Para recursos estáticos versionados (`.css`, `.js`, fuentes, iconos, imágenes, favicons y manifiesto en `PRECACHE_ASSETS`).
    - *Network-First:* Para peticiones de navegación y páginas HTML, con degradación elegante a [`offline.html`](file:///c:/repos/Ludeka/src/Ludeka.Web/wwwroot/offline.html) cuando la red o el servidor están inaccesibles.
    - *Purga Automática:* En el evento `activate`, elimina cachés obsoletas asegurando consistencia entre versiones.
- **Instantánea Local de Ludoteca (`localStorage`):**
  - Módulo JS: [`src/Ludeka.Web/wwwroot/js/ludeka-offline.js`](file:///c:/repos/Ludeka/src/Ludeka.Web/wwwroot/js/ludeka-offline.js)
  - DTO: [`OfflineLibrarySnapshotDto`](file:///c:/repos/Ludeka/src/Ludeka.Application/DTOs/OfflineLibrarySnapshotDto.cs)
  - Comportamiento:
    - Cada vez que el usuario consulta su ludoteca online en [`MyLibrary.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Pages/MyLibrary.razor), se serializa una instantánea local compacta con timestamp.
    - Si el circuito SignalR o la conexión de red se interrumpe, el componente detecta el fallo, monta la instantánea de `localStorage` y muestra la colección con sus filtros y datos esenciales.
    - El modo offline está restringido a **consulta y lectura segura**, previniendo desincronizaciones o conflictos de concurrencia al no permitir mutaciones sin servidor.
- **Indicador de Conectividad Accesible:**
  - Componente: [`src/Ludeka.Web/Components/Shared/OfflineIndicator.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/OfflineIndicator.razor)
  - Integrado globalmente en [`MainLayout.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Layout/MainLayout.razor) con `role="status"` y `aria-live="polite"` notificando al usuario de desconexión o reconexión en tiempo real.

