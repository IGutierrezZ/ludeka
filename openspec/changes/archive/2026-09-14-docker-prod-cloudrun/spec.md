# Especificación: INC-39 — Empaquetado Docker para Producción, Google Cloud Run y Pipeline CI/CD con Secretos

## 1. Resumen Ejecutivo
Para el despliegue final de Ludeka en producción sobre Google Cloud Run con base de datos en Supabase, se requiere:
1. Asegurar que la imagen Docker cumpla con el estándar de contenedores de Cloud Run (puerto dinámico `PORT`, sondas `/healthz`, permisos no-root).
2. Proporcionar un pipeline de Integración y Entrega Continua (CI/CD) en GitHub Actions para verificar pruebas unitarias y desplegar automáticamente en cada merge a `main`.
3. Ofrecer una guía operacional exhaustiva sobre cómo configurar los secretos de forma segura en Google Cloud y GitHub Actions sin exponerlos en el repositorio.

---

## 2. Requerimientos Técnicos

### REQ-1: Adaptabilidad Dinámica de Puerto en Docker (`PORT`)
- Google Cloud Run asigna dinámicamente el puerto del contenedor mediante la variable de entorno `PORT` (usualmente 8080).
- La aplicación ASP.NET Core 10 debe escuchar en el puerto indicado por `PORT` (o `8080` si no está definido).
- En `Program.cs`, se debe soportar `app.Urls` derivado de `PORT` o la configuración de entorno `ASPNETCORE_HTTP_PORTS`.

### REQ-2: Imagen Docker Optimizada y Segura
- Build multi-stage:
  - Etapa 1: Compilación de Tailwind CSS minificado con Node 20.
  - Etapa 2: Compilación y publicación Release en .NET 10 SDK.
  - Etapa 3: Runtime chiseled/ligero ASP.NET 10 sin dependencias innecesarias, ejecutando bajo el usuario no privilegiado `app`.
- Sonda de salud integrada mediante endpoint HTTP `/healthz`.

### REQ-3: Pipeline CI/CD en GitHub Actions (`.github/workflows/ci-cd.yml`)
- Job `ci-test`:
  - Se ejecuta en Pull Requests hacia `main` y en pushes a `main`.
  - Configura .NET 10 SDK.
  - Restaura y compila `Ludeka.sln`.
  - Ejecuta los 887 tests unitarios.
  - Construye la imagen Docker para verificar que no haya regresiones en el empaquetado.
- Job `deploy-cloudrun`:
  - Se ejecuta únicamente tras un merge exitoso a `main` si las credenciales de Google Cloud están configuradas en GitHub Secrets.
  - Autenticación con Google Cloud.
  - Compilación y push de la imagen Docker a Google Artifact Registry.
  - Despliegue de la revisión a Google Cloud Run con inyección de variables de entorno y referencias a secretos.

### REQ-4: Guía de Despliegue Operativo (`docs/deployment/google-cloud-run.md`)
- Manual claro en español que guíe al usuario en:
  1. Creación del proyecto en Google Cloud y habilitación de Cloud Run y Artifact Registry.
  2. Creación y asignación de secretos en Google Secret Manager / Cloud Run.
  3. Configuración de credenciales en GitHub Secrets (`GCP_PROJECT_ID`, `GCP_SA_KEY`, etc.).
  4. Despliegue inicial manual o desatendido vía CI/CD.
