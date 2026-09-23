# Spec: change-50-area-de-cuenta (INC-50: Área de Cuenta)

> Modo híbrido · Español castellano · Fuentes: `proposal.md`, `explore.md`, `inc-50-area-de-cuenta.md`, `config.yaml`.
> Unidades de enrutado por capacidad: `specs/header-account-gate/spec.md`, `specs/account-area-hub/spec.md`, `specs/policy-based-authorization/spec.md`, `specs/user-library-view/spec.md`.

## ADDED Requirements — `header-account-gate`

### Requirement: Botón de persona en la zona de utilidades de la cabecera

El sistema DEBE mostrar en la zona de utilidades de `MainLayout.razor` un botón de persona que, con sesión iniciada, navegue a `/cuenta` y muestre `UserName`; sin sesión, navegue a `/login` (sin `ReturnUrl`).

#### Scenario: Con sesión navega al área de cuenta

- GIVEN una sesión activa (`UserId` no vacío)
- WHEN el usuario activa el botón de persona
- THEN navega a `/cuenta`
- AND la identidad visible muestra `UserName`.

#### Scenario: Sin sesión navega al inicio de sesión

- GIVEN un visitante sin sesión (`UserId` vacío)
- WHEN activa el botón de persona
- THEN navega a `/login`.

#### Scenario: De cero a al menos un enlace a /login

- GIVEN la interfaz actual sin ningún `href="/login"`
- WHEN se despliega el cambio
- THEN existe al menos un enlace a `/login` en la interfaz.

### Requirement: Accesibilidad WCAG 2.2 AA de la puerta

El control DEBE ser operable por teclado con foco visible, DEBE comunicar el estado con/sin sesión con texto (no solo icono ni color), DEBE exponer nombre accesible y DEBE tener área objetivo ≥ 24×24 px CSS.

#### Scenario: Operación por teclado con foco visible

- GIVEN la cabecera renderizada
- WHEN se navega por teclado hasta el botón
- THEN es alcanzable con Tab, activable con Enter o Espacio y muestra indicador de foco visible.

#### Scenario: Estado comunicado con texto, no solo icono o color

- GIVEN los estados con y sin sesión
- WHEN un lector de pantalla lee el control
- THEN expresa el estado mediante texto (nombre del usuario o «Entrar»), nunca solo por icono o color.

### Requirement: Contrato de fuente de la puerta

Los tests de contrato de fuente (precedente `AuthorizationPipelineContractTests.cs:139-176`, marcadores sin atar a posición) DEBEN afirmar: presencia del botón de persona, distinción con/sin sesión y existencia del enlace a `/login`.

#### Scenario: Marcadores de contrato en verde

- GIVEN la suite de tests
- WHEN se ejecuta `dotnet test Ludeka.sln`
- THEN los contratos de fuente de la puerta pasan.

## ADDED Requirements — `account-area-hub`

### Requirement: Hub raíz `/cuenta` con inventario de secciones

El sistema DEBE publicar una página `/cuenta` con `[Authorize]` que inventarie Ludoteca, Conexiones, Tema y País (decisión 5 vialógica), enlazando cada una a su superficie existente sin duplicar UI.

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

DEBEN reescribirse a `/cuenta/ludoteca`: la píldora (`MainLayout.razor:94`), `GameDetail.razor:45` y `Radar.razor:41`. `PublicProfile.razor`, `manifest.webmanifest` y `offline.html` NO DEBEN modificarse.

#### Scenario: Referencias Razor reescritas al hub

- GIVEN los ficheros `MainLayout.razor`, `GameDetail.razor` y `Radar.razor`
- WHEN se inspecciona su fuente
- THEN enlazan `/cuenta/ludoteca`.

#### Scenario: Archivos fuera de alcance intactos

- GIVEN `PublicProfile.razor`, `manifest.webmanifest` y `offline.html`
- WHEN se inspecciona su fuente
- THEN conservan `/mi-ludoteca` y no fueron modificados.

### Requirement: Contrato de fuente del hub y del alias

Los tests de contrato de fuente DEBEN afirmar el montaje del hub, la ruta hija `/cuenta/ludoteca`, la vigencia del alias `/mi-ludoteca` y la ausencia de modificaciones en `PublicProfile`, `manifest.webmanifest` y `offline.html`.

#### Scenario: Contratos del área en verde

- GIVEN la suite de tests
- WHEN se ejecuta `dotnet test Ludeka.sln`
- THEN todos los contratos del área pasan.

## MODIFIED Requirements — `policy-based-authorization`

### Requirement: Pipeline de autenticación y autorización en Blazor SSR interactivo

`Program.cs` DEBE llamar a `UseAuthentication()` y `UseAuthorization()` antes de `UseAntiforgery()`, DEBE registrar `AddCascadingAuthenticationState()` y `Routes.razor` DEBE usar `AuthorizeRouteView` con vistas `NotAuthorized` y `RedirectToLogin`. La redirección interactiva (`RedirectToLogin`) DEBE preservar `ReturnUrl` con la misma semántica que `LoginRedirect.ToLogin` (URL escapada + guard anti-bucle `ResolveReturnUrl`), sin bucles de redirección.

(Previously: `RedirectToLogin.razor:15` navegaba a `/login` sin `ReturnUrl`, perdiendo la intención de destino que `LoginRedirect.cs:36` sí conserva.)

#### Scenario: Anónimo en ruta protegida es redirigido a login

- GIVEN un visitante sin sesión
- WHEN solicita `/admin/auditoria`
- THEN es redirigido al inicio de sesión, sin renderizar contenido administrativo.

#### Scenario: Orden del pipeline

- GIVEN el pipeline HTTP compilado
- WHEN se inspecciona su orden
- THEN `UseAuthentication` y `UseAuthorization` preceden a `UseAntiforgery`.

#### Scenario: Estado de autenticación en cascada en SSR interactivo

- GIVEN un componente con `AuthorizeView` dentro del circuito
- WHEN la sesión cambia entre anónima y autenticada
- THEN la vista refleja el estado en cascada.

#### Scenario: ReturnUrl preservado en la redirección interactiva

- GIVEN un anónimo que solicita `/cuenta` en modo interactivo
- WHEN `AuthorizeRouteView` invoca `RedirectToLogin`
- THEN la navegación a `/login` incluye `ReturnUrl` escapada con la ruta de origen.

#### Scenario: Sin bucles de redirección

- GIVEN una `ReturnUrl` que apunta ya a `/login`
- WHEN se resuelve antes de redirigir
- THEN el guard anti-bucle (`ResolveReturnUrl`) impide el encadenamiento.

#### Scenario: Contrato de fuente de ReturnUrl

- GIVEN la suite de tests
- WHEN se ejecuta
- THEN el contrato afirma que `RedirectToLogin` preserva `ReturnUrl` con la semántica de `LoginRedirect`.

## MODIFIED Requirements — `user-library-view`

### Requirement: Navegación por Pestañas de Estado

La página DEBE organizar los juegos del usuario en pestañas reactivas con ruta canónica `/cuenta/ludoteca` (sección del hub) y alias permanente `/mi-ludoteca` de idéntico comportamiento:
1. *En mi ludoteca (N)*
2. *Jugados (N)*
3. *Deseados (N)*
4. *Quiero comprar (N)*
5. *Préstamos activos (N)*
6. *Cola comunitaria (N)* (visible para moderadores o equipo fundador).

(Previously: la ruta única era `/mi-ludoteca`; ahora la canónica es `/cuenta/ludoteca` y `/mi-ludoteca` queda como alias.)

#### Escenario: Renderizado de contadores por pestaña

- DADO un usuario con 3 `InCollection`, 5 `Played`, 2 `Wishlist`, 1 `WantToBuy` y 1 préstamo activo
- CUANDO navega a `/cuenta/ludoteca`
- ENTONCES las pestañas muestran `(3)`, `(5)`, `(2)`, `(1)` y `(1)`.

#### Escenario: Cambio de pestaña reactivo

- DADO el usuario en `/cuenta/ludoteca`
- CUANDO pulsa la pestaña *Deseados*
- ENTONCES la vista muestra exclusivamente los juegos `Wishlist`.

#### Escenario: Alias `/mi-ludoteca` con idéntico comportamiento

- DADO el alias `/mi-ludoteca` vivo
- CUANDO el usuario navega a `/mi-ludoteca`
- ENTONCES recibe la misma vista, pestañas y contadores que en `/cuenta/ludoteca`.

El resto de requisitos de la capacidad (préstamos, importación BGG, badge de cola, panel de moderación) permanecen INTACTOS.

## Requisitos transversales

### Requirement: Tests de contrato de fuente

La suite DEBE incluir tests de contrato de fuente (marcadores sin atar a posición) para: botón de persona, distinción con/sin sesión, enlace `/login`, alias vivo, montaje del hub y preservación de `ReturnUrl`.

#### Scenario: Suite completa en verde

- GIVEN la suite de pruebas
- WHEN se ejecuta `dotnet test Ludeka.sln`
- THEN todas las pruebas pasan, sin bajar de la línea base **1.622 unitarias + 10 de integración**.

### Requirement: Contrato congelado de `ICurrentUserService`

`ICurrentUserService` NO DEBE modificarse (21 implementaciones; superficie congelada por `CurrentUserContractTests.cs:41-59`); la puerta usa solo `UserId` y `UserName` existentes.

#### Scenario: Superficie intacta

- GIVEN el contrato y sus tests congelados
- WHEN se ejecuta la suite
- THEN `CurrentUserContractTests` siguen en verde sin cambios en la interfaz.

### Requirement: Sin migraciones y rollback por git revert

El cambio DEBE ser íntegramente aditivo en código y rutas: SIN migraciones de esquema; el rollback SE HACE con `git revert` del PR.

#### Scenario: Rollback sin datos

- GIVEN el PR único de `inc/area-de-cuenta`
- WHEN se revierte
- THEN cabecera, rutas, `RedirectToLogin` y enlaces vuelven al estado previo sin tocar base de datos.

## Fuera de alcance (EXCLUIDO)

- **Avatar del proveedor** (decisión 4: alcance de datos/privacidad; incremento propio).
- **Página física `/cuenta/ajustes`** y consolidación de `GetUserPreferenceAsync`/`UserPreferenceDto`/`SwitchTheme` (INC-61+).
- **Herramientas de moderación y administración** (ya gobernadas por permisos en cabecera).
- **Perfil público** (`PublicProfile.razor` sin modificar; resuelve por alias).
- **`ICurrentUserService`** (contrato congelado, no modificado).
- **`manifest.webmanifest` y `offline.html`** (conservan `/mi-ludoteca`).
