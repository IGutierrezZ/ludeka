# =====================================================================
# Stage 1: Compilación de Assets y Tailwind CSS con Node.js
# =====================================================================
FROM node:20-alpine AS css-build
WORKDIR /src

# Copiar archivos requeridos por Tailwind CLI para escaneo de clases
COPY src/Ludeka.Web/Styles/ ./Styles/
COPY src/Ludeka.Web/tailwind.config.js ./
COPY src/Ludeka.Web/Components/ ./Components/
COPY src/Ludeka.Web/wwwroot/ ./wwwroot/

# Generar app.css minificado en wwwroot/
RUN npx -y tailwindcss@3.4.17 -i ./Styles/input.css -o ./wwwroot/app.css --minify

# =====================================================================
# Stage 2: Compilación y Publicación de la Solución .NET 10
# =====================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar archivos de proyecto para aprovechar la caché de capas de Docker
COPY src/Ludeka.Core/Ludeka.Core.csproj src/Ludeka.Core/
COPY src/Ludeka.Application/Ludeka.Application.csproj src/Ludeka.Application/
COPY src/Ludeka.Infrastructure/Ludeka.Infrastructure.csproj src/Ludeka.Infrastructure/
COPY src/Ludeka.Web/Ludeka.Web.csproj src/Ludeka.Web/

# Restaurar paquetes NuGet de la aplicación web y sus dependencias
RUN dotnet restore src/Ludeka.Web/Ludeka.Web.csproj


# Copiar todo el código fuente del proyecto
COPY src/ ./src/

# Reemplazar app.css con la versión minificada generada en Stage 1
COPY --from=css-build /src/wwwroot/app.css ./src/Ludeka.Web/wwwroot/app.css

# Compilar y publicar en modo Release
WORKDIR /src/src/Ludeka.Web
RUN dotnet publish Ludeka.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

# =====================================================================
# Stage 3: Runtime Seguro de Producción (.NET 10 ASP.NET)
# =====================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Parámetros de entorno estándar para contenedores ASP.NET Core
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    ConnectionStrings__DefaultConnection="Data Source=/app/data/ludeka.db"

# Instalar cURL y sqlite3 para diagnósticos/backups y preparar directorios con permisos
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl sqlite3 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/data /app/logs \
    && chown -R app:app /app/data /app/logs

# Cambiar a usuario sin privilegios
USER app

# Copiar binarios publicados desde el Stage 2
COPY --from=build --chown=app:app /app/publish .

# Exponer el puerto estándar HTTP
EXPOSE 8080

# Definir volumen persistente para datos SQLite
VOLUME ["/app/data"]

# Sonda de salud integrada en el contenedor contra /healthz
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "Ludeka.Web.dll"]
