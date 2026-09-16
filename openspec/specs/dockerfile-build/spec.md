# Especificación: dockerfile-build (Compilación Multi-Stage y Runtime No-Root)

## 1. Contexto y Propósito
Empaqueta la solución Ludeka en una imagen de contenedor Docker reproducible, ligera y segura para .NET 10. La compilación incluye los assets CSS de Tailwind generados desde las plantillas Razor y el runtime se ejecuta bajo un usuario sin privilegios para mitigar vectores de escalada de permisos en el host.

---

## 2. Requerimientos y Criterios de Aceptación (Gherkin)

### Escenario 1: Compilación de estilos Tailwind en etapa Node.js
**Dado** el archivo de entrada `./Styles/input.css` y las plantillas Razor en `Components/`  
**Cuando** se ejecuta la etapa de compilación de CSS en Docker (`node:20-alpine`)  
**Entonces** Tailwind CLI v3.4.17 genera un archivo `app.css` minificado  
**Y** dicho archivo se transfiere a la etapa de compilación de .NET dentro de `wwwroot/`.

### Escenario 2: Restauración y compilación .NET 10 con caché de capas
**Dado** el archivo de solución `Ludeka.sln` y los proyectos `.csproj`  
**Cuando** se ejecuta `dotnet restore` en la etapa de construcción (`dotnet/sdk:10.0`)  
**Entonces** las capas de dependencias NuGet se almacenan en caché  
**Y** la compilación posterior `dotnet publish -c Release -o /app/publish` genera los binarios optimizados para producción.

### Escenario 3: Ejecución segura bajo usuario no-root
**Dado** el contenedor en ejecución a partir de `mcr.microsoft.com/dotnet/aspnet:10.0`  
**Cuando** el proceso `dotnet Ludeka.Web.dll` arranca  
**Entonces** se ejecuta bajo el usuario `app` (UID 1654)  
**Y** escucha en el puerto interno `8080` (`ASPNETCORE_HTTP_PORTS=8080`).

### Escenario 4: Sonda de salud integrada en el contenedor
**Dado** el contenedor en ejecución  
**Cuando** el daemon de Docker evalúa la instrucción `HEALTHCHECK`  
**Entonces** realiza una petición HTTP a `http://localhost:8080/healthz`  
**Y** reporta estado `healthy` si la respuesta es HTTP 200 dentro del intervalo de 30 segundos.
