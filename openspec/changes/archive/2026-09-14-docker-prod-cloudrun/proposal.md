# Propuesta: INC-39 — Empaquetado Docker para Producción, Google Cloud Run y Pipeline CI/CD con Secretos

## 1. Contexto y Justificación
Con la Fase 1 (APIs reales, atribución BGG, comunidad y motor privado de afiliados) y la Fase 2 (PostgreSQL en Supabase, migraciones oficiales de EF Core y AdminUserSeeder) completadas, el sistema está listo a nivel de código para su ejecución en producción.

El usuario ha elegido alojar la aplicación web en **Google Cloud** (aprovechando el tier gratuito de Google Cloud Run: 2 millones de peticiones/mes, escalado a cero cuando no hay tráfico para no consumir recursos) y la base de datos en **Supabase**.

Para que este despliegue sea robusto, repetible y seguro:
1. El contenedor Docker debe ser compatible con la especificación de Google Cloud Run (gestión dinámica de la variable de entorno `PORT`, arranque rápido y sondas de salud).
2. Los secretos de producción (cadenas de conexión a Supabase, API keys de Gemini/BGG/YouTube, tokens de Discord/Telegram) deben inyectarse de forma segura mediante GitHub Actions / Google Secret Manager sin viajar jamás dentro de la imagen Docker.
3. Se debe proporcionar un pipeline de CI/CD automatizado en GitHub Actions para validar tests en cada PR y desplegar de forma desatendida a Cloud Run al hacer merge en `main`.

---

## 2. Alcance Propuesto

### 2.1 Optimización del Dockerfile para Google Cloud Run
- Compatibilidad nativa con la variable `PORT` inyectada por Google Cloud Run:
  - En ASP.NET Core 10, enlazar `ASPNETCORE_HTTP_PORTS=${PORT:-8080}` o configurar `app.Urls.Add(...)` en `Program.cs` para respetar `PORT`.
- Minimizar el tamaño de la imagen final: optimizar etapas multi-stage (Node.js para Tailwind CSS + SDK .NET 10 + Runtime ASP.NET 10).
- Permisos no-root (`USER app`) para máxima seguridad conforme a las mejores prácticas de Cloud Native.
- Sondaje de salud `HEALTHCHECK` compatible con Cloud Run y Kubernetes.

### 2.2 Configuración Docker Compose de Producción (`docker-compose.prod.yml`)
- Manifiesto para pruebas locales de producción o para despliegue alternativo en VPS propio con Docker Compose consumiendo el archivo `.env`.

### 2.3 Pipeline CI/CD en GitHub Actions (`.github/workflows/ci-cd.yml`)
- **Fase de Integración Continua (CI):**
  - Se ejecuta en cada Pull Request o push a ramas de incremento.
  - Restaura, compila y ejecuta la suite completa de 887 pruebas unitarias.
  - Verifica la compilación de la imagen Docker.
- **Fase de Entrega Continua (CD):**
  - Se dispara tras el merge a `main`.
  - Autenticación segura con Google Cloud mediante Service Account Key o Workload Identity Federation (WIF).
  - Publicación de la imagen Docker en Google Artifact Registry (GAR).
  - Despliegue automático de la nueva revisión en Google Cloud Run inyectando las variables de configuración y secretos.

### 2.4 Guía Operativa de Despliegue en Google Cloud y Supabase (`docs/deployment/google-cloud-run.md`)
- Manual paso a paso en español con los comandos de la CLI `gcloud` y configuración en la consola web para aprovisionar el servicio y configurar los secretos.

---

## 3. Criterios de Aceptación
1. Compilación exitosa de la imagen Docker en entorno local.
2. El contenedor arranca correctamente y responde a peticiones HTTP en el puerto parametrizable (`PORT`).
3. La suite de 887 pruebas se ejecuta de forma exitosa en el workflow de CI de GitHub Actions.
4. El manifiesto de despliegue y la guía operativa cubren la totalidad del ciclo de vida en Google Cloud Run y Supabase.
