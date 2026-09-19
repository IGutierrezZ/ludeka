# 🚀 Guía de Despliegue en Google Cloud Run y Supabase

Esta guía detalla el procedimiento paso a paso para desplegar Ludeka en **Google Cloud Run** conectado a **Supabase** aprovechando al máximo los niveles gratuitos (Free Tier).

---

## 1. Arquitectura de Despliegue

```mermaid
graph LR
    User([Usuarios]) -->|HTTPS| CloudRun[Google Cloud Run<br/>512 MB / 1 vCPU<br/>Escalado a 0]
    CloudRun -->|PostgreSQL SSL| Supabase[(Supabase PostgreSQL<br/>Managed Database)]
    CloudRun -->|HTTPS| BGG[BoardGameGeek API]
    CloudRun -->|HTTPS| Gemini[Google Gemini API]
    CloudRun -->|HTTPS| YouTube[YouTube Data API]
    GitHub[GitHub Repo] -->|Push a main| Actions[GitHub Actions CI/CD]
    Actions -->|Docker Push| ArtifactRegistry[Artifact Registry]
    ArtifactRegistry -->|Deploy| CloudRun
```

---

## 2. Beneficios del Tier Gratuito de Google Cloud Run
Google Cloud Run ofrece cada mes dentro de su capa gratuita:
- **2 millones de peticiones HTTP** al mes gratis.
- **180.000 vCPU-segundos** y **360.000 GiB-segundos** de memoria gratis.
- **Escalado a cero (`min-instances: 0`):** Cuando no hay tráfico en la web, el contenedor se apaga automáticamente y **no consume ni un solo céntimo**. En cuanto entra una petición, levanta en aproximadamente 1-2 segundos gracias a .NET 10 AOT/optimizado.

---

## 3. Paso a Paso: Configuración en Google Cloud

### Paso 3.1: Crear Proyecto y Habilitar Servicios
Desde la consola web de Google Cloud (o Google Cloud Shell):
```bash
# 1. Definir variables
export PROJECT_ID="ludeka-prod"
export REGION="europe-west1" # Madrid / Bélgica / Frankfurt

# 2. Configurar el proyecto
gcloud config set project $PROJECT_ID

# 3. Habilitar las APIs necesarias
gcloud services enable \
    run.googleapis.com \
    artifactregistry.googleapis.com \
    secretmanager.googleapis.com \
    cloudbuild.googleapis.com
```

### Paso 3.2: Crear Repositorio en Artifact Registry
```bash
gcloud artifacts repositories create ludeka \
    --repository-format=docker \
    --location=$REGION \
    --description="Imágenes Docker de Ludeka"
```

### Paso 3.3: Crear Cuenta de Servicio para GitHub Actions
```bash
# 1. Crear Service Account
gcloud iam service-accounts create github-deployer \
    --display-name="GitHub Actions Deployer"

# 2. Asignar roles necesarios
gcloud projects add-iam-policy-binding $PROJECT_ID \
    --member="serviceAccount:github-deployer@$PROJECT_ID.iam.gserviceaccount.com" \
    --role="roles/run.admin"

gcloud projects add-iam-policy-binding $PROJECT_ID \
    --member="serviceAccount:github-deployer@$PROJECT_ID.iam.gserviceaccount.com" \
    --role="roles/artifactregistry.writer"

gcloud projects add-iam-policy-binding $PROJECT_ID \
    --member="serviceAccount:github-deployer@$PROJECT_ID.iam.gserviceaccount.com" \
    --role="roles/iam.serviceAccountUser"

# 3. Generar clave JSON para GitHub Secrets
gcloud iam service-accounts keys create sa-key.json \
    --iam-account=github-deployer@$PROJECT_ID.iam.gserviceaccount.com
```

---

## 4. Configuración de Secretos en GitHub

En tu repositorio de GitHub (`https://github.com/IGutierrezZ/ludeka`), ve a:  
**Settings** ➔ **Secrets and variables** ➔ **Actions** ➔ **New repository secret**.

Añade los siguientes secretos:

| Nombre del Secreto | Descripción | Ejemplo / Valor |
|---|---|---|
| `GCP_PROJECT_ID` | ID de tu proyecto en Google Cloud | `ludeka-prod` |
| `GCP_REGION` | Región de despliegue | `europe-west1` |
| `GCP_SA_KEY` | Contenido completo del archivo `sa-key.json` | `{"type": "service_account", ...}` |
| `SUPABASE_DB_CONNECTION` | Cadena de conexión PostgreSQL de Supabase | `Host=db.xxxx.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=...;SSL Mode=Require;Trust Server Certificate=true;` |
| `GEMINI_API_KEY` | Clave API de Google Gemini | `AIzaSy...` |
| `BGG_API_TOKEN` | Token de BoardGameGeek (si dispones de él) | *(opcional)* |
| `YOUTUBE_API_KEY` | Clave API de YouTube Data v3 | `AIzaSy...` |
| `DISCORD_WEBHOOK_URL` | Webhook del canal de Discord de Ludeka | `https://discord.com/api/webhooks/...` |
| `TELEGRAM_BOT_TOKEN` | Token del bot de Telegram | `123456:ABC-DEF...` |
| `TELEGRAM_CHAT_ID` | Chat ID o canal de Telegram | `@ludeka_comunidad` |

---

## 5. Configuración de la Base de Datos en Supabase

1. En tu panel de **Supabase**, ve a **Project Settings** ➔ **Database** ➔ **Connection parameters**.
2. Copia la cadena en formato **URI** o **Connection string (Node.js/ADO.NET)**:
   ```
   Host=aws-0-eu-central-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.tu-proyecto;Password=TU_PASSWORD;SSL Mode=Require;Trust Server Certificate=true;
   ```
   *(Nota: Puedes usar tanto el puerto directo `5432` como el Transaction Pooler `6543` de Supabase).*
3. Al arrancar Ludeka por primera vez contra Supabase, **las migraciones oficiales de Entity Framework Core se ejecutarán automáticamente**, creando todas las tablas, índices nativos `jsonb` y el **usuario Administrador Fundador inicial permanente** (`admin-fundador` / `admin@ludeka.es`).
4. No ejecutes [`docs/database/supabase_schema.sql`](file:///c:/repos/Ludeka/docs/database/supabase_schema.sql) a mano en el **SQL Editor de Supabase**: está desactualizado y produce un esquema incompatible con EF Core. Las migraciones son la única fuente de verdad (su regeneración o retirada está planificada en el INC-48).

---

## 6. Despliegue Manual con gcloud CLI (Alternativa a GitHub Actions)

Si deseas desplegar directamente desde tu terminal local con Docker:

```bash
# 1. Compilar y publicar imagen Docker
gcloud builds submit --tag europe-west1-docker.pkg.dev/$PROJECT_ID/ludeka/ludeka-web:latest

# 2. Desplegar en Cloud Run
gcloud run deploy ludeka-web \
    --image=europe-west1-docker.pkg.dev/$PROJECT_ID/ludeka/ludeka-web:latest \
    --region=europe-west1 \
    --platform=managed \
    --allow-unauthenticated \
    --port=8080 \
    --memory=512Mi \
    --cpu=1 \
    --min-instances=0 \
    --max-instances=5 \
    --set-env-vars="ASPNETCORE_ENVIRONMENT=Production,Database__Provider=PostgreSql,Database__SeedDemoData=false,Bgg__SimulateApi=false,Gemini__Simulate=false,YouTube__Simulate=false,CommunityNotifications__DryRun=false" \
    --set-env-vars="ConnectionStrings__DefaultConnection=Host=db.xxxx.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=TU_PASSWORD;SSL Mode=Require;Trust Server Certificate=true;"
```

---

## 7. Despliegue Alternativo en VPS con Docker Compose

Si prefieres alojar el contenedor en un servidor VPS propio (ej. Hetzner, OVH, DigitalOcean) en lugar de Google Cloud:

```bash
# 1. Clonar el repositorio y copiar .env
cp .env.example .env
nano .env # Completar variables de Supabase y APIs

# 2. Levantar con Docker Compose de producción
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

---

## 8. Notas Operativas (PostgreSQL en Producción)

- **Acceso a secretos:** la cuenta de servicio que ejecuta el runtime necesita `roles/secretmanager.secretAccessor` para leer los secretos almacenados en Secret Manager.
- **Medios en Cloudflare R2:** hoy las variables `Cloudflare__*` no se inyectan en el despliegue de Cloud Run, por lo que la aplicación cae en el almacenamiento simulado y las imágenes quedan **en memoria** (se pierden al reiniciar la instancia). Es una carencia conocida pendiente del INC-48.
- **Readiness probe:** configura `/ready` como *readiness probe* en Cloud Run para que el tráfico no llegue a instancias con dependencias (base de datos, almacenamiento o cola) no disponibles.

---

## 9. Trabajos en Segundo Plano: Cloud Run Jobs + Cloud Scheduler (INC-47)

> [!IMPORTANT]
> **Gate de producción.** El PR que retira los cuatro `IHostedService` en proceso del host web (`Ludeka.Web`) se abre pero **no se mergea** hasta confirmar que esta sección está provisionada y **disparando de verdad** contra el proyecto de Google Cloud real. Mergear antes deja producción sin ningún ejecutor de los cuatro trabajos de negocio: ni catalogación nocturna, ni radar de precios, ni recolector social, ni drenaje del outbox de notificaciones.

Desde este incremento, la misma imagen de contenedor que publica `Ludeka.Web` (servicio HTTP) sirve también `Ludeka.Jobs` (ejecutable de vida corta, una unidad de trabajo por invocación, sin servidor HTTP ni sondas). El modo se selecciona **sobrescribiendo el `ENTRYPOINT`** del contenedor en el recurso Cloud Run Job — la imagen no cambia, solo el comando de arranque.

### 9.1. Los cuatro Cloud Run Jobs

| Job de Cloud Run | Nombre de trabajo (`--args`) | Comando |
|---|---|---|
| `ludeka-job-nightly-cataloging` | `nightly-cataloging` | `dotnet Ludeka.Jobs.dll nightly-cataloging` |
| `ludeka-job-price-radar` | `price-radar` | `dotnet Ludeka.Jobs.dll price-radar` |
| `ludeka-job-social-collector` | `social-collector` | `dotnet Ludeka.Jobs.dll social-collector` |
| `ludeka-job-notification-outbox` | `notification-outbox` | `dotnet Ludeka.Jobs.dll notification-outbox` |

Los cuatro se despliegan sobre la **misma imagen** que el servicio web (`gcloud run jobs deploy`), con las mismas variables de entorno y secretos que el servicio (`ConnectionStrings__DefaultConnection`, `Database__Provider=PostgreSql`, tokens de BGG/Discord/Telegram, etc. — ver §4). El pipeline (`.github/workflows/ci-cd.yml`) publica una revisión nueva de los cuatro Jobs en cada despliegue, además de la revisión del servicio web.

> [!WARNING]
> **Hueco de evidencia heredado del diseño, no verificado contra GCP real en este ciclo:** la ortografía exacta de las banderas de `gcloud run jobs deploy` (en particular `--command`/`--args` para sobrescribir el `ENTRYPOINT`) y la disponibilidad de un modo *job* explícito en `google-github-actions/deploy-cloudrun@v2` no se han confirmado con acceso real a Google Cloud. Confirmar antes de dar por buena esta sección.

### 9.2. Los cuatro Cloud Scheduler

Cada Job tiene su propio Cloud Scheduler, con cadencia coherente con la ventana de idempotencia del trabajo (`JobExecutionLeases`, `UNIQUE (JobName, WindowKey)`):

| Trabajo | Cron (UTC) | Coherencia con la ventana |
|---|---|---|
| `nightly-cataloging` | `0 3 * * *` | Ventana diaria; una sola ejecución al día |
| `price-radar` | `0 */6 * * *` | Debe ser ≥ `PriceRadar:CheckIntervalHours` (6 h) |
| `social-collector` | `0 */2 * * *` | Debe ser ≥ `SocialCollector:IntervalMinutes` (120 min) |
| `notification-outbox` | `*/5 * * * *` | Ventana de un segundo: cada disparo drena el outbox pendiente |

Cada Cloud Scheduler invoca `:run` sobre su Job correspondiente con cuerpo vacío — el nombre del trabajo va fijado en el propio recurso Job (`--args`), no en la programación, para que una sobrescritura mal escrita en Scheduler nunca pueda ejecutar en silencio el trabajo equivocado.

**Nota operativa — cadencia frente a ventana:** si la cadencia del Scheduler es más frecuente que el tamaño de la ventana del trabajo (por ejemplo, un reintento manual fuera de calendario), los disparos sobrantes terminan con código de salida **0** sin hacer nada, por diseño (`SkippedAlreadyCompleted`). No es un fallo: es la misma restricción `UNIQUE (JobName, WindowKey)` que impide el trabajo duplicado. Quien revise los registros de Cloud Run debe saberlo para no perseguir un fantasma.

### 9.3. Cuenta de servicio invocadora (IAM)

Cloud Scheduler necesita una identidad autorizada para invocar cada Job:

```bash
# 1. Crear la cuenta de servicio invocadora (puede ser una sola, compartida por los 4 Scheduler)
gcloud iam service-accounts create ludeka-scheduler-invoker \
    --display-name="Ludeka Cloud Scheduler Invoker"

# 2. Autorizarla a invocar cada uno de los cuatro Jobs
for JOB in nightly-cataloging price-radar social-collector notification-outbox; do
  gcloud run jobs add-iam-policy-binding "ludeka-job-${JOB}" \
    --region="$REGION" \
    --member="serviceAccount:ludeka-scheduler-invoker@${PROJECT_ID}.iam.gserviceaccount.com" \
    --role="roles/run.invoker"
done

# 3. Configurar cada Cloud Scheduler para autenticar con esa cuenta de servicio (OIDC) al invocar
#    "https://<region>-run.googleapis.com/apis/run.googleapis.com/v1/namespaces/<project>/jobs/ludeka-job-<nombre>:run"
```

Sin el rol `roles/run.invoker` concedido, Cloud Scheduler recibe `403 Forbidden` al intentar disparar el Job — el escenario "Cloud Scheduler dispara el Job y la invocación es autorizada" (verificación manual) exige confirmar esto contra el proyecto real, no solo que el binding se creó sin error.

### 9.4. Retención de `JobExecutionLeases`

A razón de un drenaje del outbox cada 5 minutos, la tabla `JobExecutionLeases` crece aproximadamente 288 filas al día solo por ese trabajo (más las filas de los otros tres, mucho menos frecuentes). **No existe política de purga automática en el alcance de este incremento.** Se recomienda una retención operativa de **90 días** (por ejemplo, un `DELETE` programado fuera de banda sobre filas con `CompletedAt` anterior al umbral) para evitar crecimiento indefinido; queda como deuda operativa declarada, no como tarea de este incremento.

### 9.5. Hueco funcional descubierto durante `sdd-apply` (Fase 11) — sin trabajo programado de reemplazo

> [!WARNING]
> **Esto NO es una advertencia decorativa: es una pérdida de funcionalidad real a partir del merge de este PR, y ningún Cloud Scheduler de los cuatro anteriores la cubre.**
>
> `CommunityNotificationDispatcherHostedService.RunPeriodicScanAsync` —hoy con sondeo interno cada 60 minutos, todavía presente en el código pero ya sin ningún `AddHostedService` que lo arranque tras este PR— hacía DOS cosas que **ningún `IJobRunner` de `Ludeka.Jobs` reemplaza**:
>
> 1. **El boletín semanal de novedades** (`ICommunityNotificationService.RunFridayReleasesBulletinAsync`, disparado los viernes, con concesión de ventana `JobName = "community-weekly-bulletin"`).
> 2. **El escaneo de sorteos próximos a expirar** (`RunExpiringGiveawaysScanAsync`, sin concesión de ventana, ejecutado en cada vuelta del sondeo).
>
> El diseño (§7.2, tabla de trabajos, y §8.9, tabla de Cloud Scheduler) fija **exactamente cuatro** trabajos externalizados, y `community-weekly-bulletin` no es uno de ellos — nunca tuvo Cloud Run Job ni Cloud Scheduler propios. Al retirar el `AddHostedService` que lo arrancaba (requisito "El host web no ejecuta ningún trabajo de negocio en proceso", que nombra explícitamente al despachador de notificaciones comunitarias entre los cuatro tipos a retirar), **el boletín semanal y el escaneo de sorteos dejan de dispararse por completo**, de forma indefinida, hasta que alguien decida cómo reemplazarlos.
>
> Esto **no lo puede resolver `sdd-apply` dentro del alcance de las tareas 11.1-11.10**: crear un quinto Cloud Run Job y un quinto Cloud Scheduler es infraestructura nueva no aprobada por el diseño ni por el maintainer, y con el mismo gate de provisión manual que los otros cuatro. Queda como **decisión pendiente del maintainer**, con estas salidas razonables:
>
> - **(a)** Aceptar la pausa temporal del boletín semanal y del escaneo de sorteos hasta un incremento futuro que les dé un quinto trabajo programado.
> - **(b)** Añadir un quinto `IJobRunner`/Cloud Run Job/Cloud Scheduler para `community-weekly-bulletin` (la lógica de dominio ya existe en `CommunityNotificationService`; falta el *runner* fino y la infraestructura de disparo).
> - **(c)** Cualquier otro mecanismo de disparo que el maintainer prefiera (por ejemplo, ampliar el `notification-outbox` existente para que también revise el boletín semanal en cada drenaje, aunque eso mezclaría dos responsabilidades con cadencias muy distintas en un mismo trabajo).
>
> Este hueco es independiente del gate de producción del §3/§9 (que exige confirmar que los CUATRO Cloud Scheduler existentes disparan de verdad): aunque ese gate se cumpla al 100 %, el boletín semanal y el escaneo de sorteos seguirán sin dispararse hasta que se resuelva este punto.
