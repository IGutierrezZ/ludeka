# Delta for docker-compose-orchestration

> Cambio `change-48-persistencia-produccion-postgres` (INC-48), fase `sdd-spec`, 2026-09-19. Declara `docker-compose.yml` y `docker-compose.staging.yml` explícitamente como entornos locales con SQLite, y corrige que `docker-compose.yml` no dispare, sin querer, la guarda de arranque en Production (ver [`../production-persistence-guard/spec.md`](../production-persistence-guard/spec.md)).

> **Nota de convención:** la especificación viva actual (`openspec/specs/docker-compose-orchestration/spec.md`) predata la convención canónica y agrupa 3 escenarios sueltos, ninguno relativo al entorno declarado (`ASPNETCORE_ENVIRONMENT`) de cada fichero. Este delta añade un requisito nuevo sobre esa dimensión; los escenarios existentes de persistencia por volumen, inyección de variables `.env` y política de reinicio (Escenarios 1, 2 y 3) no cambian con este incremento y no se repiten aquí.

## ADDED Requirements

### Requirement: Declaración explícita de entorno local en `docker-compose.yml` y `docker-compose.staging.yml`

`docker-compose.yml` y `docker-compose.staging.yml` DEBEN declararse, mediante su configuración de entorno y un comentario explicativo, como entornos locales de desarrollo o pruebas manuales que usan SQLite — nunca como una vía de despliegue a producción (esa vía es `docker-compose.prod.yml`, fuera de alcance de este cambio). En particular, `docker-compose.yml` NO DEBE tomar `Production` como valor por defecto de `ASPNETCORE_ENVIRONMENT`: hoy lo hace (`${ASPNETCORE_ENVIRONMENT:-Production}`, línea 11) combinado con una conexión SQLite fija (línea 12), lo que activaría la nueva guarda de arranque de `production-persistence-guard` y dejaría el entorno local inutilizable.
(Previously: `docker-compose.yml` no declaraba ningún entorno local explícito y su valor de `ASPNETCORE_ENVIRONMENT` por defecto era `Production`; `docker-compose.staging.yml` ya fija `ASPNETCORE_ENVIRONMENT=Staging` — línea 11 —, pero tampoco se declaraba explícitamente como entorno local.)

*Verificación: prueba que replica la configuración efectiva de cada fichero (variable de entorno + cadena de conexión) contra la guarda de arranque de `production-persistence-guard`, sin ejecutar Docker real.*

#### Scenario: La configuración efectiva de `docker-compose.yml` no activa la guarda de Production

- GIVEN la combinación de variables de `docker-compose.yml` sin sobrescribir `ASPNETCORE_ENVIRONMENT` (valor por defecto tras este cambio, ya no `Production`) y su conexión SQLite fija
- WHEN se evalúa la guarda de arranque de `production-persistence-guard` con esa combinación
- THEN la guarda no se activa y el proceso arranca con SQLite, igual que hoy

#### Scenario: `docker-compose.staging.yml` ya usa un entorno distinto de Production (sin cambio funcional)

- GIVEN que `docker-compose.staging.yml` fija `ASPNETCORE_ENVIRONMENT=Staging`
- WHEN se evalúa la guarda de arranque de `production-persistence-guard` con esa combinación
- THEN la guarda no se activa, sin necesidad de ningún cambio funcional en este fichero

#### Scenario: Ambos ficheros incluyen un comentario explícito de entorno local (verificación manual)

- GIVEN `docker-compose.yml` y `docker-compose.staging.yml`
- WHEN se revisa su cabecera o comentarios
- THEN ambos identifican explícitamente que son entornos locales/de pruebas con SQLite, no producción

*Nota: este último escenario es de revisión documental, no de comportamiento en tiempo de ejecución; se verifica manualmente en `sdd-verify`, no con `dotnet test`.*
