# account-area-hub Specification

> Capacidad NUEVA (INC-50). Área privada `/cuenta`: hub raíz, rutas hijas, cabecera compartida y alias de `/mi-ludoteca`.

## Propósito

Agrupar bajo `/cuenta` todo lo configurable a nivel de usuario, con secciones navegables y sin romper ninguna URL viva.

## Requirements

### Requirement: Hub raíz `/cuenta` con inventario de secciones

El sistema DEBE publicar una página `/cuenta` con `[Authorize]` que inventarie las secciones Ludoteca, Conexiones, Tema y País (decisión 5 vialógica), enlazando cada una a su superficie existente sin duplicar UI.

#### Scenario: Autenticado ve el inventario completo

- GIVEN sesión iniciada
- WHEN navega a `/cuenta`
- THEN ve las entradas Ludoteca, Conexiones, Tema y País.

#### Scenario: Anónimo redirigido a login con ReturnUrl

- GIVEN un visitante sin sesión
- WHEN solicita `/cuenta`
- THEN es redirigido a `/login` preservando `ReturnUrl`.

#### Scenario: Montaje del hub testificado

- GIVEN la suite de tests
- WHEN se ejecuta
- THEN el contrato de montaje del hub pasa.

### Requirement: Ruta hija `/cuenta/ludoteca` con alias permanente `/mi-ludoteca`

`MyLibrary.razor` DEBE declarar una segunda ruta `@page "/cuenta/ludoteca"` conservando `@page "/mi-ludoteca"` como alias permanente con idéntico comportamiento (criterio 6).

#### Scenario: Ambas rutas resuelven la misma vista

- GIVEN las dos rutas declaradas en `MyLibrary.razor`
- WHEN se solicita cualquiera de las dos
- THEN se renderiza la ludoteca con idéntico comportamiento.

#### Scenario: Alias vivo verificado por contrato

- GIVEN el contrato de fuente del alias
- WHEN se ejecuta la suite
- THEN afirma que `/mi-ludoteca` sigue declarada y resoluble.

### Requirement: `/cuenta/conexiones` alcanzable sin tocar la lógica de INC-49

`/cuenta/conexiones` DEBE ser alcanzable navegando desde el hub; la lógica de INC-49 NO se modifica (solo integración de navegación y cabecera de sección).

#### Scenario: Navegación desde el hub

- GIVEN sesión iniciada en `/cuenta`
- WHEN activa la entrada «Conexiones»
- THEN llega a `/cuenta/conexiones` sin escribir la URL ni depender del aviso de correo no verificado.

#### Scenario: INC-49 intacto

- GIVEN `AccountConnections.razor` y sus tests de contrato
- WHEN se ejecuta la suite
- THEN siguen en verde sin cambios de lógica de conexiones.

### Requirement: Cabecera de sección compartida entre rutas hijas

Las rutas hijas `/cuenta/*` DEBEN compartir un componente de cabecera de sección (`AccountSectionNav`) que marque la sección activa con marcado accesible (no solo color).

#### Scenario: Sección activa marcada

- GIVEN la ruta `/cuenta/ludoteca`
- WHEN se renderiza
- THEN la cabecera marca «Ludoteca» como sección activa mediante marcado accesible (p. ej. `aria-current`).

### Requirement: Reescritura selectiva de referencias a `/mi-ludoteca`

DEBEN reescribirse a `/cuenta/ludoteca`: la píldora de cabecera (`MainLayout.razor:94`), `GameDetail.razor:45` y `Radar.razor:41`. `PublicProfile.razor`, `manifest.webmanifest` y `offline.html` NO DEBEN modificarse.

#### Scenario: Referencias Razor reescritas al hub

- GIVEN los ficheros `MainLayout.razor`, `GameDetail.razor` y `Radar.razor`
- WHEN se inspecciona su fuente
- THEN enlazan `/cuenta/ludoteca`.

#### Scenario: Archivos fuera de alcance intactos

- GIVEN `PublicProfile.razor`, `manifest.webmanifest` y `offline.html`
- WHEN se inspecciona su fuente
- THEN conservan `/mi-ludoteca` y no fueron modificados.

### Requirement: Contrato de fuente del hub y del alias

Los tests de contrato de fuente DEBEN afirmar el montaje del hub `/cuenta`, la declaración de la ruta hija `/cuenta/ludoteca`, la vigencia del alias `/mi-ludoteca` y la ausencia de modificaciones en `PublicProfile`, `manifest.webmanifest` y `offline.html`.

#### Scenario: Contratos del área en verde

- GIVEN la suite de tests
- WHEN se ejecuta `dotnet test Ludeka.sln`
- THEN todos los contratos del área pasan.
