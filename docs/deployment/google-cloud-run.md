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
