# Diseño Técnico: change-10-docker-deployment-staging (Incremento 10)

## 1. Arquitectura del Sistema de Despliegue y Empaquetado

```
[ Código Fuente / Git ]
         │
         ├── Stage 1: [ node:20-alpine ]
         │     └── Compila Tailwind CSS ➔ wwwroot/app.css
         │
         ├── Stage 2: [ mcr.microsoft.com/dotnet/sdk:10.0 ]
         │     ├── Restaura dependencias NuGet con caché (Ludeka.sln)
         │     ├── Copia app.css compilado
         │     └── dotnet publish -c Release -o /app/publish
         │
         └── Stage 3: [ mcr.microsoft.com/dotnet/aspnet:10.0 ]
               ├── Usuario: app (no-root, UID 1654)
               ├── Puerto: 8080 (ASPNETCORE_HTTP_PORTS=8080)
               ├── Volumen montado: /app/data (SQLite)
               ├── Endpoint Healthcheck: /healthz (liveness) y /ready (readiness)
               └── ENTRYPOINT ["dotnet", "Ludeka.Web.dll"]
```

---

## 2. Archivo de Solución (`Ludeka.sln`)

La raíz contendrá la solución estándar de .NET que agrupa:
- `src/Ludeka.Core/Ludeka.Core.csproj`
- `src/Ludeka.Application/Ludeka.Application.csproj`
- `src/Ludeka.Infrastructure/Ludeka.Infrastructure.csproj`
- `src/Ludeka.Web/Ludeka.Web.csproj`
- `tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj`

Esto permite:
1. `dotnet build` y `dotnet test` directos desde la raíz.
2. En Dockerfile, `COPY Ludeka.sln .` y `COPY src/*/*.csproj ...` seguido de `dotnet restore Ludeka.sln` para aprovechar al 100% la capa de caché de Docker ante cambios de código sin cambios en dependencias NuGet.

---

## 3. Clases y Sondas de Salud (Health Checks)

### 3.1 Ubicación: `src/Ludeka.Web/Health/`

#### 1. `SqliteDatabaseHealthCheck` (`IHealthCheck`)
- **Propósito:** Comprobar la disponibilidad real de la base de datos SQLite.
- **Implementación:**
  - Inyecta `LudekaDbContext`.
  - Invoca `await _dbContext.Database.CanConnectAsync(cancellationToken)`.
  - Ejecuta una consulta ligera si es posible (`await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1;", cancellationToken)`).
  - Devuelve `HealthCheckResult.Healthy("Base de datos SQLite operativa.")` o `HealthCheckResult.Unhealthy("Error de conexión a SQLite", exception)`.

#### 2. `StorageHealthCheck` (`IHealthCheck`)
- **Propósito:** Comprobar que el directorio donde reside la base de datos (`/app/data` o directorio local) existe y tiene permisos de escritura y lectura.
- **Implementación:**
  - Extrae la ruta del archivo de la cadena de conexión configurada (`DefaultConnection`).
  - Obtiene el directorio contenedor. Si no existe, intenta crearlo.
  - Genera un archivo temporal `.healthcheck_probe` y lo elimina inmediatamente para certificar permisos de escritura y lectura.
  - Devuelve `HealthCheckResult.Healthy("Almacenamiento accesible y con permisos de escritura.")` o `HealthCheckResult.Unhealthy("Fallo de permisos en el directorio de almacenamiento", exception)`.

#### 3. `NotificationQueueHealthCheck` (`IHealthCheck`)
- **Propósito:** Comprobar que la cola en memoria de notificaciones comunitarias (`ICommunityNotificationQueue`) no está cerrada o fallando.
- **Implementación:**
  - Inyecta `ICommunityNotificationQueue`.
  - Verifica que el canal esté disponible y operativo.
  - Devuelve `HealthCheckResult.Healthy("Cola de notificaciones operativa.")`.

### 3.2 Registro y Mapeo en `Program.cs`
- Servicios:
  ```csharp
  builder.Services.AddHealthChecks()
      .AddCheck<SqliteDatabaseHealthCheck>("sqlite_db", tags: ["ready"])
      .AddCheck<StorageHealthCheck>("storage", tags: ["ready"])
      .AddCheck<NotificationQueueHealthCheck>("notification_queue", tags: ["ready"]);
  ```
- Endpoints:
  - `/healthz`: Liveness check (filtra con predicado `_ => false`, solo confirma que el servidor responde con 200).
  - `/ready`: Readiness check (filtra con predicado `check => check.Tags.Contains("ready")`, evalúa todas las dependencias y retorna JSON estructurado con status, duración y componentes).

---

## 4. Resiliencia de la Base de Datos SQLite en Directorios

En `src/Ludeka.Web/Program.cs`, antes de `EnsureCreatedAsync()`:
```csharp
// Asegurar creación del directorio contenedor para SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=ludeka.db";
var dbPathMatch = System.Text.RegularExpressions.Regex.Match(connectionString, @"Data Source=([^;]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
if (dbPathMatch.Success)
{
    var rawPath = dbPathMatch.Groups[1].Value.Trim();
    var dir = Path.GetDirectoryName(rawPath);
    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
    {
        Directory.CreateDirectory(dir);
    }
}
```

---

## 5. Orquestación y Reverse Proxy Nginx

### 5.1 `docker-compose.yml`
```yaml
services:
  ludeka-web:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: ludeka-web
    restart: unless-stopped
    ports:
      - "5081:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Data Source=/app/data/ludeka.db
    env_file:
      - .env
    volumes:
      - ludeka_data:/app/data
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/healthz || exit 1"]
      interval: 30s
      timeout: 5s
      retries: 3
      start_period: 10s

volumes:
  ludeka_data:
    name: ludeka_data
```

### 5.2 Nginx Reverse Proxy (`deploy/nginx/default.conf`)
- Soporte para SignalR WebSockets:
  ```nginx
  location / {
      proxy_pass http://ludeka-web:8080;
      proxy_http_version 1.1;
      proxy_set_header Upgrade $http_upgrade;
      proxy_set_header Connection "upgrade";
      proxy_set_header Host $host;
      proxy_cache_bypass $http_upgrade;
      proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
      proxy_set_header X-Forwarded-Proto $scheme;
      proxy_buffering off;
  }
  ```

---

## 6. Scripts de Copia de Seguridad y Mantenimiento

### 6.1 `deploy/backup-sqlite.sh`
- Ejecuta:
  `sqlite3 /app/data/ludeka.db ".backup '/app/data/backups/ludeka_backup_${TIMESTAMP}.db'"`
- Si `sqlite3` CLI no está en el host, utiliza `docker exec ludeka-web ...` o copia segura.
- Purga:
  `find /app/data/backups -type f -name "ludeka_backup_*.db" -mtime +7 -delete`

### 6.2 `deploy/backup-sqlite.ps1`
- Equivalente para PowerShell en entornos Windows con rotación de 7 días.

### 6.3 `deploy/restore-sqlite.sh`
- Verificación previa con `PRAGMA integrity_check;`.
- Backup de salvaguarda antes de reemplazar el archivo activo.
