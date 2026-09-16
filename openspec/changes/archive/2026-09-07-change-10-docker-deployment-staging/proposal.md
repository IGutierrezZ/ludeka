# Propuesta: change-10-docker-deployment-staging (Incremento 10: Despliegue, Empaquetado Docker y Configuración de Staging/Producción)

## 1. Resumen Ejecutivo y Motivación

Con los 9 incrementos funcionales completados (Catálogo, Ludoteca, Veredicto Fundador, Hub Multimedia, Importador BGG, Automatización Comunitaria, Rendimiento Web, Expansiones y Notificaciones Multicanal), **Ludeka** está lista para dar el salto al entorno real de producción.

El **Incremento 10** proporciona una infraestructura de despliegue reproducible, segura y lista para operar en cualquier VPS Linux (Ubuntu/Debian) o proveedor cloud (Hetzner, DigitalOcean, AWS, Azure), garantizando:
- **Reproducibilidad Total ("Funciona en todas partes"):** Empaquetado en contenedores Docker mediante un `Dockerfile` multi-stage que compila los assets de Tailwind CSS y el código .NET 10 desde el código fuente sin dependencias manuales del host.
- **Seguridad y Usuario No-Root:** Ejecución bajo el usuario sin privilegios `app` (UID 1654), superficie de ataque mínima y sin credenciales ni secretos en el repositorio.
- **Persistencia Confiable para SQLite:** Configuración defensiva de volúmenes persistentes (`ludeka_data:/app/data`), asegurando la creación del directorio padre y eliminando cualquier riesgo de pérdida de datos en reinicios de contenedores.
- **Observabilidad con Health Checks Oficiales:** Endpoints de diagnóstico `/healthz` (liveness) y `/ready` (readiness) que evalúan la disponibilidad del runtime, conectividad con SQLite, permisos de escritura en disco y la cola en segundo plano.
- **Soporte Nativo de WebSockets para Blazor Server:** Configuración de reverse proxy Nginx con actualización de protocolos (`Upgrade`, `Connection "upgrade"`), compresión gzip y cabeceras de seguridad estrictas (HSTS, CSP, X-Frame-Options).
- **Herramientas de Operación y Backup en Caliente:** Scripts automatizados para copias de seguridad consistentes de SQLite en modo WAL sin detener el servicio, y guía técnica completa de mantenimiento.

---

## 2. Decisiones de Arquitectura y Especificación de Componentes

### 2.1 Archivo de Solución (`Ludeka.sln`)
- **Propósito:** Unificar los proyectos `src/Ludeka.Core`, `src/Ludeka.Application`, `src/Ludeka.Infrastructure`, `src/Ludeka.Web` y `tests/Ludeka.UnitTests`.
- **Beneficio:** Permite que `dotnet test` y `dotnet build` se ejecuten desde la raíz sin parámetros adicionales, y permite a Docker restaurar dependencias con máxima eficiencia de caché (`dotnet restore Ludeka.sln`).

---

### 2.2 Dockerfile Multi-Stage Optimizado

El `Dockerfile` constará de 3 etapas optimizadas:

```
[ Stage 1: node:20-alpine ] ➔ Compila input.css con Tailwind CLI v3.4.17 ➔ app.css minificado
          │
[ Stage 2: dotnet/sdk:10.0 ] ➔ Restaura Ludeka.sln (con caché) + Compila código + Publica en Release
          │
[ Stage 3: dotnet/aspnet:10.0 ] ➔ Runtime ligero, usuario app (no-root), volumen /app/data, Healthcheck integrado
```

- **Características Clave:**
  - Base runtime: `mcr.microsoft.com/dotnet/aspnet:10.0`.
  - Puerto HTTP: `8080` (estándar ASP.NET Core 8/10 en contenedores, variable `ASPNETCORE_HTTP_PORTS=8080`).
  - Usuario: `USER app` (UID 1654) para ejecución sin privilegios de root.
  - Directorio de datos: `/app/data` con permisos asignados al usuario `app`.
  - `HEALTHCHECK`: Comando nativo cURL o invocación HTTP contra `http://localhost:8080/healthz` con intervalo de 30s, timeout de 5s y 3 reintentos.

---

### 2.3 `.dockerignore` Quirúrgico
Exclusión explícita de artefactos temporales, binarios y herramientas de desarrollo para garantizar que el contexto de compilación enviado al daemon de Docker sea de apenas unos pocos megabytes:
- Excluye: `**/bin/`, `**/obj/`, `.git/`, `.openspec/`, `docs/`, `tests/`, `*.db`, `*.db-wal`, `*.db-shm`, `.vscode/`, `.gemini/`.

---

### 2.4 Endpoints de Health Checks en `Ludeka.Web`

Integración en `src/Ludeka.Web/Program.cs` mediante el paquete estándar de ASP.NET Core:
1. **Endpoint `/healthz` (Liveness):**
   - Verifica que el host web esté activo y respondiendo a peticiones HTTP.
   - Devuelve `200 OK` con payload JSON descriptivo (`{"status":"Healthy","timestamp":"..."}`).
2. **Endpoint `/ready` (Readiness):**
   - Evalúa los subsistemas críticos antes de recibir tráfico de usuarios:
     - `SqliteDatabaseHealthCheck`: Ejecuta `db.Database.CanConnectAsync()` y valida la consulta base.
     - `StorageHealthCheck`: Comprueba que el directorio que aloja la base de datos SQLite tiene permisos de lectura y escritura.
     - `NotificationQueueHealthCheck`: Comprueba que la cola en memoria de notificaciones comunitarias esté operativa.
   - Devuelve `200 OK` si todos los checks son saludables o `503 Service Unavailable` si alguno falla.
3. **Mapeo y Formateo:**
   - Soporta respuesta JSON limpia tanto para orquestadores (Docker, Kubernetes) como para monitoreo humano.

---

### 2.5 Resiliencia de Inicialización SQLite en Contenedores

En `src/Ludeka.Web/Program.cs`, antes de invocar `Database.EnsureCreatedAsync()`:
- Inspeccionar la cadena de conexión `DefaultConnection`.
- Si apunta a una ruta con subcarpetas (ej. `/app/data/ludeka.db` o `data/ludeka.db`), comprobar si el directorio contenedor existe y, en caso contrario, crearlo con `Directory.CreateDirectory(...)`.
- Esto previene el error común `DirectoryNotFoundException` en montajes de volúmenes Docker nuevos.

---

### 2.6 Orquestación con Docker Compose

1. **`docker-compose.yml` (Base / Producción):**
   - Servicio `ludeka-web`:
     - `build: .`
     - `container_name: ludeka-web`
     - `restart: unless-stopped`
     - `ports: ["5081:8080"]` (o `["8080:8080"]`)
     - `env_file: .env`
     - `volumes:`
       - `ludeka_data:/app/data`
     - `healthcheck:` comprobando `http://localhost:8080/healthz`.
     - `networks: [ludeka-net]`
2. **`docker-compose.staging.yml` / Perfil Staging:**
   - Configuración para entorno de pruebas y preproducción con puertos y variables independientes.
3. **Configuración de Nginx Reverse Proxy (`deploy/nginx/`):**
   - Configuración lista para usar con soporte de WebSockets:
     ```nginx
     proxy_http_version 1.1;
     proxy_set_header Upgrade $http_upgrade;
     proxy_set_header Connection "upgrade";
     ```
   - Buffering optimizado para Blazor Server.
   - Cabeceras de seguridad: `X-Frame-Options SAMEORIGIN`, `X-Content-Type-Options nosniff`, `Referrer-Policy strict-origin-when-cross-origin`.
   - Soporte para certificados SSL de Let's Encrypt / Certbot.

---

### 2.7 Plantillas de Variables de Entorno (`.env.example`)

Documentación clara de todas las variables de entorno configurables en producción y staging:
- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__DefaultConnection=Data Source=/app/data/ludeka.db`
- `Bgg__ApiToken=tu_token_aqui`
- `CommunityNotifications__Enabled=true`
- `CommunityNotifications__DryRun=false`
- `CommunityNotifications__DiscordWebhookUrl=...`
- `CommunityNotifications__TelegramBotToken=...`
- `CommunityNotifications__TelegramChatId=...`

---

### 2.8 Scripts de Copia de Seguridad y Restauración

- **`deploy/backup-sqlite.sh` (Linux / Bash):**
  - Ejecuta un respaldo en caliente mediante SQLite CLI con `sqlite3 /app/data/ludeka.db ".backup '/app/data/backups/ludeka_backup_$(date +%Y%m%d_%H%M%S).db'"` o `VACUUM INTO`.
  - Política de retención: elimina automáticamente copias con más de 7 días.
- **`deploy/backup-sqlite.ps1` (PowerShell para entornos Windows/desarrollo):**
  - Equivalente en PowerShell para copias locales seguras.
- **`deploy/restore-sqlite.sh`:**
  - Script de restauración con confirmación de seguridad y validación de integridad (`PRAGMA integrity_check`).

---

### 2.9 Guía Operativa de Despliegue (`docs/deployment/DEPLOYMENT_GUIDE.md`)

Manual de operaciones en español con instrucciones paso a paso:
1. Requisitos del servidor VPS (2 vCPU, 2 GB RAM, Ubuntu 24.04 LTS / Debian 12).
2. Instalación de Docker y Docker Compose.
3. Clonación del repositorio y configuración del archivo `.env`.
4. Levantamiento del servicio con `docker compose up -d`.
5. Configuración de Nginx con HTTPS automático mediante Certbot.
6. Automatización de backups diarios con cron.
7. Procedimiento de actualización de versiones sin pérdida de datos.
8. Diagnóstico y resolución de incidencias comunes.

---

## 3. Plan de Pruebas y Validación

1. **Pruebas Unitarias de Health Checks (`HealthChecksTests.cs`):**
   - Verificar que `SqliteDatabaseHealthCheck` responde `Healthy` con base de datos en memoria o SQLite funcional.
   - Verificar que `StorageHealthCheck` valida correctamente rutas válidas y detecta rutas no escribibles.
   - Verificar que `NotificationQueueHealthCheck` reporta el estado operativo de la cola.
2. **Pruebas de Compilación y Publicación .NET 10:**
   - Comprobar que `dotnet publish -c Release` compila limpiamente sin errores ni advertencias bloqueantes.
   - Comprobar que la solución `Ludeka.sln` compila todos los proyectos y pasa el 100% de los tests unitarios.
3. **Validación Sintáctica de Docker y Nginx:**
   - Validación de sintaxis de `Dockerfile`, `.dockerignore`, `docker-compose.yml` y configuraciones de Nginx.
4. **Meta de Pruebas:**
   - Mantener el 100% de los 189 tests existentes en verde y añadir tests específicos para la infraestructura de salud, alcanzando >= 195 tests en verde.
