# Tareas: INC-39 — Empaquetado Docker para Producción, Google Cloud Run y Pipeline CI/CD con Secretos

- [x] **1. Adaptación de Program.cs y Dockerfile para Google Cloud Run**
  - [x] 1.1 Configurar el soporte dinámico para la variable de entorno `PORT` en `Program.cs`.
  - [x] 1.2 Ajustar `Dockerfile` multi-stage optimizando capas y asegurando compatibilidad no-root.
  - [x] 1.3 Crear `docker-compose.prod.yml` para despliegues locales de producción o VPS.

- [x] **2. Pipeline de Integración y Entrega Continua (CI/CD)**
  - [x] 2.1 Crear `.github/workflows/ci-cd.yml` con jobs de validación de pruebas (`test`) y despliegue a Google Cloud Run (`deploy`).

- [x] **3. Guía Operativa de Despliegue en Google Cloud y Supabase**
  - [x] 3.1 Crear `docs/deployment/google-cloud-run.md` documentando la configuración de secretos, comandos `gcloud` y activación del tier gratuito de Cloud Run.

- [x] **4. Verificación y Pruebas**
  - [x] 4.1 Ejecutar `dotnet build Ludeka.sln` y verificar compilación limpia.
  - [x] 4.2 Ejecutar `dotnet test Ludeka.sln` y verificar que los 887 tests pasen al 100%.

- [ ] **5. Cierre, Documentación Viva y Pull Request**
  - [ ] 5.1 Actualizar la Especificación Viva del Sistema (`docs/specs/sistema/`).
  - [ ] 5.2 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
  - [ ] 5.3 Realizar commit convencional y abrir Pull Request con `scripts/sdd-worktree.ps1 pr docker-prod-cloudrun`.
