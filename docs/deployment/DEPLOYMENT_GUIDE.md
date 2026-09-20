# 🚀 Guía Operativa de Despliegue en Producción — Ludeka

Esta guía describe el procedimiento técnico para desplegar, operar y mantener la plataforma **Ludeka** (.NET 10 y Blazor Web App) en cualquier servidor VPS Linux (Ubuntu 22.04/24.04 LTS o Debian 12) utilizando Docker, Docker Compose y Nginx.

> [!IMPORTANT]
> **Producción usa PostgreSQL (Supabase) como base de datos y Cloudflare R2 como almacenamiento de medios.** SQLite queda reservado exclusivamente para las pruebas automatizadas y el desarrollo local; nunca es una opción de producción.

---

## 1. Requisitos de Infraestructura Recomendados

- **Servidor VPS:** Hetzner Cloud, DigitalOcean, OVH, AWS EC2, Azure VM.
- **CPU:** 2 vCPU (arquitectura x86_64 o ARM64).
- **Memoria RAM:** Mínimo 2 GB (4 GB recomendado para holgura de compilación y caché).
- **Almacenamiento:** 20 GB SSD / NVMe.
- **Sistema Operativo:** Ubuntu 24.04 LTS o Debian 12 con acceso `sudo`.
- **Puertos de Red Abiertos:** `80` (HTTP), `443` (HTTPS) y `22` (SSH).

---

## 2. Instalación de Docker y Docker Compose en el VPS

En una sesión SSH en el servidor:

```bash
# 1. Actualizar repositorios e instalar paquetes base
sudo apt update && sudo apt install -y curl git ufw ca-certificates gnupg

# 2. Instalar Docker Engine oficial
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt update && sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# 3. Permitir ejecución sin sudo para el usuario actual
sudo usermod -aG docker $USER
newgrp docker
```

Configurar el cortafuegos UFW:
```bash
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```

---

## 3. Clonación del Proyecto y Configuración de Secretos

```bash
# Clonar el repositorio oficial
git clone https://github.com/igutierrezz-hiberuscom/ludeka.git /opt/ludeka
cd /opt/ludeka

# Crear el archivo de entorno de producción
cp .env.example .env

# Editar secretos de producción
nano .env
```

Asegúrate de configurar:
- `PORT=5081` (puerto interno en el que escucha el contenedor frente al host).
- `ConnectionStrings__DefaultConnection` con la cadena de conexión PostgreSQL de Supabase (por ejemplo `Host=db.xxxx.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=...;SSL Mode=Require;Trust Server Certificate=true;`).
- `Database__Provider=PostgreSql` para seleccionar el proveedor Npgsql. La aplicación también lo autodetecta si la cadena contiene `Host=`, `Server=`, `Port=5432`, `supabase.co`, `postgres://` o `postgresql://`.
- `Database__SeedDemoData=false` para que la base de datos de producción arranque sin catálogo ni datos ficticios.
- Tokens de BGG, Discord y Telegram según corresponda.
- Establecer `CommunityNotifications__DryRun=false` para habilitar notificaciones reales a la comunidad.

---

## 4. Despliegue con Docker Compose (Producción)

El despliegue de producción utiliza `docker-compose.prod.yml`, que fija PostgreSQL como proveedor, desactiva el semillado de demostración y lee los secretos desde `.env`. El `docker-compose.yml` de la raíz se reserva para el entorno local con SQLite.

Para construir la imagen multi-stage (compilación de Tailwind CSS + .NET 10 SDK) y levantar el servicio en segundo plano:

```bash
# Construir y arrancar el contenedor de producción
docker compose -f docker-compose.prod.yml --env-file .env up -d --build

# Verificar estado y logs
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs -f ludeka
```

---

## 5. Endpoints de Diagnóstico y Salud (Health Checks)

El contenedor incluye dos sondas HTTP estándar de ASP.NET Core:

1. **Liveness Probe (`/healthz`):** confirma únicamente que el host web está activo; no evalúa ninguna dependencia.
   ```bash
   curl -i http://localhost:5081/healthz
   ```
   *Respuesta esperada:* HTTP `200 OK` con `{"status":"Healthy","mode":"liveness"}`.

2. **Readiness Probe (`/ready`):** evalúa las dependencias críticas: conectividad real con la base de datos configurada (`CanConnectAsync` + `SELECT 1` sobre el proveedor activo), permisos de lectura/escritura en el directorio de datos y disponibilidad de la cola de notificaciones en segundo plano.
   ```bash
   curl -i http://localhost:5081/ready
   ```
   *Respuesta esperada:* HTTP `200 OK` con `{"status":"Healthy","mode":"readiness"}`. Si alguna dependencia falla, responde `503 Service Unavailable`.

---

## 5-bis. Trabajos en Segundo Plano: Modelo Externalizado (INC-47)

> [!IMPORTANT]
> **Gate de producción.** El host web (`Ludeka.Web`) ya no ejecuta ningún trabajo de negocio en proceso (catalogación nocturna, radar de precios, recolector social, despachador de notificaciones comunitarias): la misma imagen de contenedor publica también `Ludeka.Jobs`, un ejecutable de vida corta (una unidad de trabajo por invocación, sin servidor HTTP) disparado por un planificador externo. En un VPS con Docker Compose, ese disparo externo hay que aportarlo tú mismo (por ejemplo, `cron` invocando `docker compose run --rm ludeka dotnet Ludeka.Jobs.dll <nombre-del-trabajo>` para cada uno de los cuatro trabajos, con la misma cadencia que la tabla de la sección siguiente). Los `IHostedService` en proceso se retiraron el 2026-09-19, así que **cualquier despliegue necesita un disparador externo desde el primer día**: sin él no corre ningún trabajo. En Google Cloud Run ese disparador es Cloud Scheduler — ver [`google-cloud-run.md`, sección 9](google-cloud-run.md#9-trabajos-en-segundo-plano-cloud-run-jobs--cloud-scheduler-inc-47), cuya **§9.0 da el orden de puesta en marcha correcto**, y el resto de la sección el detalle completo (los cuatro Jobs, sus cadencias, la cuenta de servicio con `roles/run.invoker`, la retención de `JobExecutionLeases` y un hueco funcional declarado sobre el boletín semanal de notificaciones).
>
> Nombres de trabajo válidos: `nightly-cataloging`, `price-radar`, `social-collector`, `notification-outbox`. Código de salida: `0` en éxito (incluida una ventana ya completada por otra ejecución), distinto de cero ante fallo observable — ver `src/Ludeka.Jobs/JobHostRunner.cs`.
>
> La sonda `HEALTHCHECK` del `Dockerfile` (contra `/healthz`) no aplica en modo trabajo: `Ludeka.Jobs` no expone ningún puerto HTTP.

---

## 6. Configuración de Nginx Frontal y Certificado SSL (HTTPS)

### 6.1 Instalar Nginx y Certbot en el Host
```bash
sudo apt install -y nginx certbot python3-certbot-nginx
```

### 6.2 Copiar Configuración del Servidor
```bash
# Copiar archivo de configuración virtual
sudo cp /opt/ludeka/deploy/nginx/default.conf /etc/nginx/sites-available/ludeka.conf
sudo ln -s /etc/nginx/sites-available/ludeka.conf /etc/nginx/sites-enabled/

# Ajustar el upstream a localhost:5081 si Nginx se ejecuta en el host
sudo sed -i 's/ludeka-web:8080/127.0.0.1:5081/g' /etc/nginx/sites-available/ludeka.conf

# Verificar sintaxis y recargar
sudo nginx -t
sudo systemctl reload nginx
```

### 6.3 Obtener Certificado SSL Gratuito con Let's Encrypt
```bash
sudo certbot --nginx -d ludeka.es -d www.ludeka.es
```

> [!IMPORTANT]
> Blazor Server requiere comunicación continua mediante WebSockets (`_blazor`). La configuración provista en `deploy/nginx/default.conf` incluye las directivas `Upgrade $http_upgrade`, `Connection $connection_upgrade` y `proxy_buffering off` necesarias para evitar desconexiones de interfaz.

---

## 7. Copias de Seguridad de la Base de Datos PostgreSQL (Supabase)

La base de datos de producción reside en **Supabase (PostgreSQL)**, por lo que el respaldo se realiza con `pg_dump` contra esa instancia mediante el script oficial `scripts/supabase-backup.ps1`. El script genera volcados comprimidos `.sql.gz` y aplica retención rotativa (7 días por defecto).

### 7.1 Requisitos
- Tener instaladas las utilidades cliente de PostgreSQL (`pg_dump`) en el host que ejecute el respaldo.
- Definir la cadena de conexión en la variable de entorno `SUPABASE_DB_URL` (o pasarla con `-ConnectionString`).

### 7.2 Ejecución Manual
```powershell
pwsh ./scripts/supabase-backup.ps1 -OutputDir /var/backups/ludeka -RetentionDays 7
```

Si `pg_dump` no está disponible localmente, el propio script propone el equivalente directo:
```bash
pg_dump "$SUPABASE_DB_URL" | gzip > /var/backups/ludeka/ludeka_prod_$(date +%F_%H%M%S).sql.gz
```

### 7.3 Programar Backup Diario con Cron
Editar el crontab del sistema:
```bash
sudo crontab -e
```
Añadir la siguiente línea para ejecutar el respaldo todas las noches a las 04:00 AM:
```cron
0 4 * * * cd /opt/ludeka && SUPABASE_DB_URL="$SUPABASE_DB_URL" pwsh ./scripts/supabase-backup.ps1 -OutputDir /var/backups/ludeka >> /var/log/ludeka_backup.log 2>&1
```

El script conserva automáticamente los últimos 7 días de respaldos (configurable con `-RetentionDays`) y purga copias más antiguas.

---

## 8. Actualización de Versiones (Zero-Downtime Rollout)

Para desplegar una nueva versión publicada en git:

```bash
cd /opt/ludeka

# 1. Obtener últimos cambios
git pull origin main

# 2. Reconstruir imagen y recrear el contenedor de producción
docker compose -f docker-compose.prod.yml --env-file .env up -d --build

# 3. Comprobar salud tras el despliegue
curl -f http://localhost:5081/ready || echo "ALERTA: Despliegue fallido"
```

Los datos de aplicación residen en **Supabase (PostgreSQL)**, por lo que reconstruir la imagen no los borra. El volumen `ludeka_data` pertenece al entorno local (`docker-compose.yml`) y no se usa en producción.

---

## 9. Resolución de Incidencias Comunes

### A. La interfaz parpadea o se desconecta ("Attempting to reconnect...")
- **Causa:** El proxy inverso no está reenviando los WebSockets o tiene buffering activo.
- **Solución:** Revisa que `proxy_buffering off;` y las cabeceras `Upgrade` y `Connection` estén presentes en la configuración de Nginx.

### B. Error `503 Service Unavailable` en `/ready`
- **Causa:** La base de datos configurada no responde (conectividad PostgreSQL/Supabase, credenciales o cadena de conexión incorrectas) o el directorio de datos carece de permisos de escritura para el usuario `app` (UID 1654).
- **Solución:** Ejecuta `docker compose -f docker-compose.prod.yml logs ludeka` para identificar la excepción y verifica la conectividad con Supabase y los permisos del volumen con `docker exec -it ludeka_app_prod ls -la /app/data`.

---

## 10. Persistencia de Medios: Cloudflare R2 en Producción y Fallback Local

Ludeka guarda los medios en **Cloudflare R2** (API compatible con S3) mediante `CloudflareR2StorageService`. Desde el INC-48, la selección se decide en `LudekaServiceCollectionExtensions` con una precedencia de tres vías:

1. **Credenciales R2 válidas** (`CloudflareR2Options.HasValidCredentials`) → `CloudflareR2StorageService`. Es el camino de producción.
2. **Sin R2, pero con `Media__LocalStoragePath` configurada** → `PhysicalFileImageStorageService`, que escribe en disco y se sirve por HTTP bajo `/images`.
3. **Sin ninguna de las dos** → `SimulatedImageStorageService`, en memoria.

> [!WARNING]
> `HasValidCredentials` exige además que **`Cloudflare__Simulate` sea `false`**, y `appsettings.json` lo trae en `true`. Inyectar las tres credenciales sin poner esa variable a `false` deja el almacenamiento **en memoria sin ningún síntoma visible**: el despliegue parece correcto y las imágenes se pierden al reiniciar. Arrancar en `Production` sin R2 válido emite un aviso explícito en el log y `/ready` lo refleja en el componente `storage`.

### 10.1 Producción (Cloudflare R2)

1. Crear el bucket `ludeka-media` en Cloudflare R2.
2. Crear un token de API R2 con permiso *Object Read & Write* limitado a ese bucket.
3. Anotar el *Account ID* de Cloudflare.
4. Configurar un dominio público o subdominio `r2.dev` para el bucket y usarlo como `Cloudflare__PublicCdnBaseUrl`.
5. Guardar en Google Secret Manager: `R2_ACCOUNT_ID`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`, `R2_BUCKET_NAME`.
6. Inyectar en el runtime: `Cloudflare__AccountId`, `Cloudflare__AccessKeyId`, `Cloudflare__SecretAccessKey`, `Cloudflare__BucketName=ludeka-media`, `Cloudflare__PublicCdnBaseUrl=<dominio>` y `Cloudflare__Simulate=false`.
7. Verificar con una subida real y comprobar el objeto en el bucket.

### 10.2 Local y Pruebas (Fallback en Disco)

1. Dejar `Cloudflare__Simulate=true` o las credenciales vacías.
2. Definir la ruta local de medios.
3. Las pruebas automatizadas usan almacenamiento en memoria o un directorio temporal.
