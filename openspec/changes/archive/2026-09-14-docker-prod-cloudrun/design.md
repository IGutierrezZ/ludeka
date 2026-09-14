# Diseño Técnico: INC-39 — Empaquetado Docker para Producción, Google Cloud Run y Pipeline CI/CD con Secretos

## 1. Soporte de Puerto y Configuración Cloud Run en ASP.NET Core 10

### 1.1 Puerto Dinámico en `Program.cs`
Google Cloud Run inyecta la variable de entorno `PORT`. En `Program.cs`:
```csharp
var customPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(customPort) && int.TryParse(customPort, out var port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}
```
Esto asegura compatibilidad bidireccional:
- Si se ejecuta en local/Docker Compose: escucha en `8080` (o el puerto configurado).
- Si se ejecuta en Cloud Run: escucha exactamente en el puerto asignado por Google Cloud.

### 1.2 Dockerfile Multi-Stage
```dockerfile
# Stage 1: Tailwind CSS minificado
FROM node:20-alpine AS css-build
WORKDIR /src
COPY src/Ludeka.Web/Styles/ ./Styles/
COPY src/Ludeka.Web/tailwind.config.js ./
COPY src/Ludeka.Web/Components/ ./Components/
COPY src/Ludeka.Web/wwwroot/ ./wwwroot/
RUN npx -y tailwindcss@3.4.17 -i ./Styles/input.css -o ./wwwroot/app.css --minify

# Stage 2: Compilación Release .NET 10
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Ludeka.sln ./
COPY src/Ludeka.Core/Ludeka.Core.csproj src/Ludeka.Core/
COPY src/Ludeka.Application/Ludeka.Application.csproj src/Ludeka.Application/
COPY src/Ludeka.Infrastructure/Ludeka.Infrastructure.csproj src/Ludeka.Infrastructure/
COPY src/Ludeka.Web/Ludeka.Web.csproj src/Ludeka.Web/
COPY tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj tests/Ludeka.UnitTests/
RUN dotnet restore Ludeka.sln
COPY src/ ./src/
COPY --from=css-build /src/wwwroot/app.css ./src/Ludeka.Web/wwwroot/app.css
WORKDIR /src/src/Ludeka.Web
RUN dotnet publish Ludeka.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime no privilegiado
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0
USER app
COPY --from=build --chown=app:app /app/publish .
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/healthz || exit 1
ENTRYPOINT ["dotnet", "Ludeka.Web.dll"]
```

---

## 2. Pipeline de CI/CD en GitHub Actions (`.github/workflows/ci-cd.yml`)

### 2.1 Estructura del Workflow
- **Disparadores:**
  - `push` a `main`.
  - `pull_request` a `main`.
- **Job `test` (CI):**
  - Runner: `ubuntu-latest`.
  - Pasos:
    1. Checkout de código.
    2. Setup .NET 10 SDK (`actions/setup-dotnet@v4`).
    3. Restauración de dependencias: `dotnet restore Ludeka.sln`.
    4. Compilación de solución: `dotnet build Ludeka.sln --configuration Release --no-restore`.
    5. Ejecución de tests: `dotnet test Ludeka.sln --configuration Release --no-build --verbosity normal`.
    6. Verificación de construcción Docker: `docker build -t ludeka:ci .`.
- **Job `deploy` (CD):**
  - Condición: `github.ref == 'refs/heads/main' && github.event_name == 'push' && env.HAS_GCP_SECRET == 'true'`.
  - Autenticación con Google Cloud vía Service Account (`google-github-actions/auth@v2`).
  - Publicación en Artifact Registry y despliegue a Cloud Run (`google-github-actions/deploy-cloudrun@v2`).

---

## 3. Despliegue con Docker Compose (`docker-compose.prod.yml`)

Permite ejecutar la versión de producción localmente o en un VPS con un solo comando:
```bash
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```
Mapea los puertos externos al `8080` del contenedor y carga los secretos de entorno.
