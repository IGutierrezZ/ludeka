# 09. Arquitectura, Persistencia y Despliegue

## 1. Visión General y Estándares Técnicos
Ludeka opera bajo **.NET 10 (C# 13)** estructurado en Clean Architecture con capas estrictamente desacopladas, persistencia relacional en SQLite con auto-migración no destructiva, empaquetado Docker multi-stage y endpoints de diagnóstico de salud.

---

## 2. Capas de la Solución

| Proyecto | Tipo | Responsabilidad | Dependencias Externas |
|---|---|---|---|
| [`src/Ludeka.Core`](file:///c:/repos/Ludeka/src/Ludeka.Core) | Class Library | Entidades de dominio, Enums, Value Objects | **Ninguna** (cero dependencias) |
| [`src/Ludeka.Application`](file:///c:/repos/Ludeka/src/Ludeka.Application) | Class Library | Casos de uso, interfaces, DTOs, validaciones | `Ludeka.Core` |
| [`src/Ludeka.Infrastructure`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure) | Class Library | SQLite EF Core, cliente BGG, webhooks, seeder | `Ludeka.Core`, `Ludeka.Application`, EF Core SQLite |
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
- **Script SQL Canónico de Supabase (`docs/database/supabase_schema.sql`):**
  - DDL completo, determinista e idempotente para crear o auditar la totalidad de las 24 tablas, tipos `jsonb`, índices y claves foráneas en Supabase con 1 clic desde el SQL Editor.
- **Usuario Administrador Fundador Garantizado (`AdminUserSeeder`):**
  - Ubicación: [`src/Ludeka.Infrastructure/Seeding/AdminUserSeeder.cs`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Seeding/AdminUserSeeder.cs)
  - En entornos limpios de producción o desarrollo, garantiza de forma idempotente la existencia de un usuario con rol `FoundingTeam` y permisos totales (`ModeratorPermission.All`), parametrizable mediante `AdminUserOptions`.
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
  - *CI:* Ejecución automática en cada PR y push a ramas de incremento de compilación, verificación de Docker y suite completa de 887 tests.
  - *CD:* Despliegue desatendido a Google Cloud Run al hacer merge a `main` si los secretos están configurados.

---

## 5. Diagnóstico de Salud (Health Checks)

- Ubicación: [`src/Ludeka.Web/Health/LudekaHealthCheck.cs`](file:///c:/repos/Ludeka/src/Ludeka.Web/Health/LudekaHealthCheck.cs)
- Endpoints expuestos:
  - `/healthz`: Liveness check del proceso web.
  - `/ready`: Readiness check que comprueba conectividad real con SQLite (`SELECT 1`) y espacio libre en disco (>50 MB).

---

## 6. Arquitectura PWA y Modo Consulta Offline

- **Manifiesto Web Estándar W3C:**
  - Archivo: [`src/Ludeka.Web/wwwroot/manifest.webmanifest`](file:///c:/repos/Ludeka/src/Ludeka.Web/wwwroot/manifest.webmanifest)
  - Configuración: `display: standalone`, orientación responsiva portrait/any, color temático `#d97706` y fondo `#0f172a`.
  - Iconografía: Iconos vectoriales SVG y rasterizados PNG (192x192, 512x512) y variante `maskable` con margen de seguridad del 15% para compatibilidad total con el recorte de iconos adaptativos en Android e iOS.
- **Service Worker con Estrategia Dual:**
  - Archivo: [`src/Ludeka.Web/wwwroot/service-worker.js`](file:///c:/repos/Ludeka/src/Ludeka.Web/wwwroot/service-worker.js)
  - Estrategia:
    - *Cache-First:* Para recursos estáticos versionados (`.css`, `.js`, fuentes, iconos, imágenes).
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

