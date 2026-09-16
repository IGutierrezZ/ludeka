# Especificación: anonymity-policy

> Capacidad nueva (INC-46). Frontera entre navegación pública y acciones con identidad.
> Fuente: `docs/increments/inc-46-autenticacion-real.md` §2.5 y §4.

## Propósito

Garantizar que la navegación pública permanezca intacta sin sesión y que toda escritura que dependa de una identidad exija sesión, sin `UserId` vacío ni usuario centinela.

## Requirements

### Requirement: Navegación pública sin sesión

Catálogo, fichas de juego, directorios, radar de ofertas, eventos, sorteos y novedades DEBEN permanecer accesibles sin sesión, sin error 500 y sin disparar escrituras con identidad.

#### Scenario: Anónimo recorre las rutas públicas

- GIVEN un visitante sin sesión
- WHEN solicita catálogo, ficha, editoriales, creadores, tiendas, radar/sorteos, eventos y novedades
- THEN todas responden correctamente
- AND ninguna provoca escrituras con `UserId` vacío.

### Requirement: Acciones con identidad exigen sesión

Mi Ludoteca, colección, préstamos, partidas, reseñas, preguntas de reglas, reportes de error y preferencias DEBEN exigir sesión iniciada. Un visitante sin sesión DEBE ser redirigido al inicio de sesión y NO DEBE ejecutarse ninguna escritura.

#### Scenario: Anónimo intenta una acción con identidad

- GIVEN un visitante sin sesión
- WHEN intenta registrar una partida, un préstamo o una preferencia
- THEN es redirigido al inicio de sesión
- AND no se crea ninguna fila.

#### Scenario: Sesión iniciada escribe con identidad real

- GIVEN una sesión iniciada
- WHEN ejecuta una acción con identidad
- THEN la fila se persiste con el `UserId` de la sesión.

### Requirement: Invariante de identidad no vacía

Las entidades `GamePlayLog`, `UserCollectionItem`, `UserGameReview`, `GameLoan`, `RuleQuestion`, `RuleAnswer`, `RuleVote`, `AuditLogEntry` y `UserPreference` DEBEN rechazar `UserId` vacío o nulo. El sistema NO DEBE crear ni usar un usuario centinela, y `MyLibrary.razor` NO DEBE construir enlaces `/u/{UserId}` con valor vacío.

#### Scenario: Constructores rechazan identidad vacía

- GIVEN cada una de las 9 entidades
- WHEN se instancian con `UserId` vacío o nulo
- THEN lanzan excepción de argumento y no se persisten.

#### Scenario: Sin usuario centinela

- GIVEN cualquier flujo anónimo
- WHEN se inspeccionan los datos escritos
- THEN no existe un usuario centinela ni filas atribuidas a él.

#### Scenario: Enlace de perfil solo con identidad real

- GIVEN la vista Mi Ludoteca sin sesión
- WHEN se renderiza
- THEN no emite `/u/` con `UserId` vacío e invita a iniciar sesión.

### Requirement: Auditoría solo con identidades reales

Todo `AuditLogEntry` DEBE registrarse con la identidad real de la sesión (`UserId` y `UserName`). NO DEBE existir auditoría generada por identidades simuladas.

#### Scenario: Auditoría de una operación autenticada

- GIVEN una sesión iniciada que ejecuta una mutación auditada
- WHEN se registra el evento
- THEN el actor corresponde a la identidad real de la sesión.

#### Scenario: Operación anónima sin auditoría

- GIVEN un intento de mutación sin sesión
- WHEN se rechaza
- THEN no se crea ningún `AuditLogEntry` con identidad simulada.
