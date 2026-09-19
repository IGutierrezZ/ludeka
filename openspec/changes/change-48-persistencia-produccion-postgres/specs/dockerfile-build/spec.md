# Delta for dockerfile-build

> Cambio `change-48-persistencia-produccion-postgres` (INC-48), fase `sdd-spec`, 2026-09-19. Retira el valor por defecto de conexión SQLite embebido en la imagen final, para que la guarda de arranque en Production (ver [`../production-persistence-guard/spec.md`](../production-persistence-guard/spec.md)) sea alcanzable en un despliegue real: mientras la imagen siga fijando una conexión SQLite por defecto, esa guarda nunca se activaría.

> **Nota de convención:** la especificación viva actual (`openspec/specs/dockerfile-build/spec.md`) predata la convención canónica y agrupa 4 escenarios sueltos. Este delta modifica únicamente la porción del «Escenario 3: Ejecución segura bajo usuario no-root» relativa a las variables de entorno fijadas en la imagen; el resto de ese escenario (usuario `app`, puerto `8080`) no cambia. Los escenarios de compilación de Tailwind, de `dotnet publish` y de la sonda `HEALTHCHECK` (Escenarios 1, 2 y 4) no cambian con este incremento y no se repiten aquí.

## MODIFIED Requirements

### Requirement: Configuración de entorno embebida en la imagen final

La imagen de contenedor producida por la compilación NO DEBE fijar, en el bloque `ENV` de la etapa de runtime, ningún valor por defecto de cadena de conexión a base de datos. El resto de variables de entorno estándar del contenedor (puerto HTTP, entorno de ejecución declarado, diagnósticos) DEBE seguir fijándose igual que hoy.
(Previously: el bloque `ENV` de la etapa final fijaba `ConnectionStrings__DefaultConnection="Data Source=/app/data/ludeka.db"` junto al resto de variables — verificado en `Dockerfile:61-64`.)

*Verificación: inspección del `Dockerfile` (ausencia de `ConnectionStrings__DefaultConnection` en el bloque `ENV` de la etapa final). El comportamiento de arranque resultante en Production sin esa variable queda cubierto por `production-persistence-guard`, no se reprueba aquí.*

#### Scenario: Ejecución segura bajo usuario no-root, sin conexión embebida (modificado)

- GIVEN el contenedor en ejecución a partir de `mcr.microsoft.com/dotnet/aspnet:10.0`
- WHEN el proceso `dotnet Ludeka.Web.dll` arranca
- THEN se ejecuta bajo el usuario `app` y escucha en el puerto interno `8080` (`ASPNETCORE_HTTP_PORTS=8080`), igual que hoy
- AND la imagen no ha fijado ningún valor por defecto de `ConnectionStrings__DefaultConnection`
