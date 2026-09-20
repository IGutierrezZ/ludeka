# Delta for social-login-authentication

## ADDED Requirements

### Requirement: Aviso de arranque cuando ningún proveedor social resulta utilizable

Además de los avisos existentes por proveedor habilitado sin credenciales, el sistema DEBE evaluar en el arranque si existe al menos un proveedor social utilizable (habilitado y con credenciales completas). Cuando ninguno lo sea, el sistema DEBE emitir una línea de registro distinguible de los avisos por proveedor individual, que indique inequívocamente que ningún proveedor de autenticación social está operativo. Esa línea DEBE emitirse con severidad de error cuando el entorno de ejecución es `Production`, y con severidad de aviso en cualquier otro entorno. Cuando exista al menos un proveedor utilizable, el sistema NO DEBE emitir esa línea.

*Verificación: prueba de la función de avisos existente, extendida con el parámetro de entorno; mismo patrón que `WebAuthenticationRegistrationTests` y que el requisito equivalente de avisos de almacenamiento de medios (`media-storage-precedence`).*

#### Scenario: Production con cero proveedores utilizables registra un error (caso negativo, defecto B4 sin corregir)

- GIVEN el entorno `Production` y los tres proveedores sociales no utilizables (deshabilitados, o habilitados sin credenciales completas) —el estado de la configuración versionada por defecto—
- WHEN la aplicación arranca
- THEN se registra una línea de severidad de error que indica que ningún proveedor de autenticación social está operativo

#### Scenario: Entorno distinto de Production con cero proveedores utilizables registra un aviso, no un error

- GIVEN un entorno distinto de `Production` (por ejemplo `Development`) con los tres proveedores sociales no utilizables
- WHEN la aplicación arranca
- THEN se registra una línea de severidad de aviso, no de error, que indica que ningún proveedor está operativo

#### Scenario: Al menos un proveedor utilizable no emite el aviso agregado

- GIVEN al menos un proveedor social habilitado y con credenciales completas
- WHEN la aplicación arranca
- THEN no se emite la línea de "ningún proveedor operativo"
- AND los avisos existentes por proveedor individual sin credenciales, si los hay, se siguen emitiendo sin cambios
