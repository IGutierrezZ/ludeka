# Tareas de Implementación: change-10-docker-deployment-staging (Incremento 10)

## Fase 1: Solución Raíz e Infraestructura de Diagnóstico
- [x] **1.1** Crear el archivo de solución `Ludeka.sln` en la raíz del repositorio y vincular los 5 proyectos (`Core`, `Application`, `Infrastructure`, `Web`, `UnitTests`).
- [x] **1.2** Implementar `SqliteDatabaseHealthCheck.cs` en `src/Ludeka.Web/Health/` para validar la conectividad de la base de datos.
- [x] **1.3** Implementar `StorageHealthCheck.cs` en `src/Ludeka.Web/Health/` para certificar permisos de escritura en la carpeta de datos.
- [x] **1.4** Implementar `NotificationQueueHealthCheck.cs` en `src/Ludeka.Web/Health/` para monitorizar la cola de notificaciones en memoria.
- [x] **1.5** Configurar `Program.cs` en `Ludeka.Web`:
  - Registrar los health checks y mapear `/healthz` y `/ready` con formato JSON estructurado.
  - Añadir la resiliencia defensiva que crea el directorio padre de la base de datos SQLite si no existe antes de `EnsureCreatedAsync()`.

## Fase 2: Empaquetado Docker y Orquestación
- [x] **2.1** Crear `.dockerignore` quirúrgico para minimizar el contexto de construcción.
- [x] **2.2** Crear `Dockerfile` multi-stage optimizado (.NET 10 + Node.js 20 Alpine para Tailwind CSS + ASP.NET 10 runtime con usuario no-root `app` y HEALTHCHECK integrado).
- [x] **2.3** Crear `docker-compose.yml` para despliegue de producción con volumen persistente `ludeka_data:/app/data`, variables de entorno y política de reinicio.
- [x] **2.4** Crear `docker-compose.staging.yml` para entornos de preproducción.
- [x] **2.5** Crear `.env.example` documentando todas las claves de configuración de Staging y Producción.

## Fase 3: Reverse Proxy Frontal y Operaciones de Mantenimiento
- [x] **3.1** Crear `deploy/nginx/nginx.conf` con configuración de compresión y buffering.
- [x] **3.2** Crear `deploy/nginx/default.conf` con soporte específico para WebSockets de Blazor Server (`_blazor`), cabeceras de seguridad y SSL.
- [x] **3.3** Crear scripts de copia de seguridad en caliente para SQLite:
  - `deploy/backup-sqlite.sh` (Bash para Linux VPS).
  - `deploy/backup-sqlite.ps1` (PowerShell para Windows).
- [x] **3.4** Crear `deploy/restore-sqlite.sh` con validación previa de integridad (`PRAGMA integrity_check`).
- [x] **3.5** Elaborar la Guía Operativa de Despliegue en VPS Linux en `docs/deployment/DEPLOYMENT_GUIDE.md`.

## Fase 4: Pruebas Unitarias, Publicación y Verificación
- [x] **4.1** Crear `tests/Ludeka.UnitTests/Health/HealthChecksTests.cs` con pruebas unitarias para las tres sondas de salud.
- [x] **4.2** Ejecutar la suite completa de pruebas unitarias (`dotnet test Ludeka.sln`) y verificar 100% en verde (alcanzando >= 195 tests).
- [x] **4.3** Probar la publicación en modo Release (`dotnet publish src/Ludeka.Web/Ludeka.Web.csproj -c Release`).
- [x] **4.4** Elaborar `verify-report.md` en `.openspec/changes/change-10-docker-deployment-staging/`.
- [x] **4.5** Registrar la decisión en Engram Persistent Memory (`mem_save`).
