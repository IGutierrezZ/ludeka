# Exploración: change-10-docker-deployment-staging (Incremento 10)

## 1. Estado Actual de la Solución

Ludeka cuenta con 9 incrementos completados, verificados con 189 tests unitarios al 100% en verde y archivados bajo la metodología Spec-Driven Development (SDD):
- **`Ludeka.Core`:** Entidades de dominio ricas (Juegos, Colecciones, Préstamos, Reseñas, Veredictos de la Mesa Fundadora, Hub Multimedia, Sorteos, Boletín de Viernes, Consultorio de Reglas Q&A, Expansiones y Sinergias, y Logs de Notificaciones Comunitarias).
- **`Ludeka.Application`:** Servicios de aplicación especializados, validaciones y contratos para todas las áreas funcionales.
- **`Ludeka.Infrastructure`:** Persistencia SQLite con EF Core 10, índices optimizados, decoradores de caché L1/L2, cola de notificaciones asíncronas con Channels y clientes HTTP tipados para BGG, Discord y Telegram.
- **`Ludeka.Web`:** Blazor Web App interactiva con SSR y componentes de servidor, Tailwind CSS 3.4 compilado y optimizado, 4 temas dinámicos e interfaz editorial responsive.
- **`Ludeka.UnitTests`:** 189 pruebas unitarias pasando al 100% en verde con xUnit y .NET 10.

---

## 2. Diagnóstico del Entorno y Oportunidad

Para que Ludeka pueda salir del entorno de desarrollo local y operar en producción en cualquier servidor VPS (Ubuntu/Debian) o plataforma en la nube (Hetzner, DigitalOcean, AWS, Azure):

1. **Falta de Empaquetado Docker:**
   - Actualmente no existe `Dockerfile` ni `.dockerignore`.
   - La compilación manual de Tailwind CSS y la ejecución directa con `dotnet run` no son reproducibles ni seguras en un servidor de producción.
2. **Ausencia de Solución (.sln) en la Raíz:**
   - No existe un archivo `Ludeka.sln` en la raíz del repositorio, lo que dificulta la restauración multi-proyecto eficiente con capas en Docker y la ejecución de `dotnet test` o `dotnet build` sin rutas explícitas.
3. **Persistencia de Datos SQLite en Contenedor:**
   - Por defecto, `Program.cs` utiliza `Data Source=ludeka.db` en el directorio de trabajo actual.
   - En un contenedor Docker, si el contenedor se reinicia o actualiza, la base de datos se perdería a menos que resida en una ruta persistente montada por volumen (ej. `/app/data/ludeka.db`) y la aplicación garantice de forma defensiva la creación del directorio antes de inicializar la base de datos.
4. **Ausencia de Endpoints de Health Checks (`/healthz` y `/ready`):**
   - No hay sondas de liveness ni readiness configuradas en ASP.NET Core.
   - Docker Compose, Kubernetes, cURL o los orquestadores no pueden verificar si el contenedor está vivo, si la base de datos SQLite responde o si el disco tiene permisos de escritura.
5. **Seguridad y Usuario No-Root:**
   - Las aplicaciones en producción deben ejecutarse bajo un usuario sin privilegios (usuario `app` con UID 1654 o chiseled/alpine) para evitar vulnerabilidades de escalada de privilegios en el host.
6. **Orquestación y Reverse Proxy:**
   - Blazor Server requiere comunicación bidireccional continua mediante WebSockets (`_blazor`).
   - Un reverse proxy como Nginx debe estar configurado con soporte explícito de cabeceras `Upgrade` y `Connection "upgrade"`, compresión gzip, cabeceras de seguridad HTTP y redirección HTTPS.
7. **Copias de Seguridad en Caliente de SQLite:**
   - Al ser SQLite una base de datos basada en un único archivo con modo WAL (Write-Ahead Logging), copiar el archivo `ludeka.db` directamente mientras la aplicación escribe puede provocar corrupción de datos. Se requiere un script de backup seguro mediante `.backup` o `VACUUM INTO`.

---

## 3. Estrategia Técnica Propuesta

1. **Creación del Archivo de Solución `Ludeka.sln`:**
   - Vincular los 4 proyectos de `src/` y el proyecto de `tests/` en la raíz para habilitar `dotnet test` y `dotnet restore` directos y optimizados.
2. **Dockerfile Multi-Stage Optimizado:**
   - *Stage 1 (Node / CSS Build):* Compilación de Tailwind CSS con `node:20-alpine` a partir de `input.css` y las plantillas Razor.
   - *Stage 2 (.NET SDK Build):* Restauración de paquetes con caché de capas sobre `Ludeka.sln`, copia de assets compilados y `dotnet publish -c Release -o /app/publish`.
   - *Stage 3 (Runtime Seguro):* Imagen `mcr.microsoft.com/dotnet/aspnet:10.0` ejecutando como usuario no-root `app`, exponiendo el puerto 8080 (`ASPNETCORE_HTTP_PORTS=8080`), con healthcheck integrado y volumen montado en `/app/data`.
3. **Endpoints de Health Checks (`/healthz` y `/ready`):**
   - Configuración en `Ludeka.Web` con `Microsoft.Extensions.Diagnostics.HealthChecks`:
     - `/healthz`: Liveness probe (devuelve 200 OK si el proceso ASP.NET Core está activo).
     - `/ready`: Readiness probe (comprueba conectividad SQLite con `CanConnectAsync`, permisos de escritura en la carpeta de datos y estado de la cola de notificaciones).
4. **Orquestación con Docker Compose (`docker-compose.yml` y perfiles):**
   - Definición del servicio `ludeka-web` con variables de entorno desacopladas (`.env`), límites de recursos, política de reinicio `unless-stopped` y volumen persistente `ludeka_data:/app/data`.
   - Configuración complementaria con Nginx (`nginx/nginx.conf`) para soporte de WebSockets Blazor, compresión y certificados SSL/TLS.
5. **Herramientas de Operación y Seguridad:**
   - Plantilla `.env.example` con variables de configuración para Staging y Producción.
   - `.dockerignore` quirúrgico para minimizar el contexto de build (< 10 MB).
   - Scripts de backup y restauración en caliente de SQLite (`deploy/backup-sqlite.sh` y `deploy/backup-sqlite.ps1`).
   - Guía Operativa de Despliegue en VPS Linux (`docs/deployment/DEPLOYMENT_GUIDE.md`).
