# header-account-gate Specification

> Capacidad NUEVA (INC-50). Puerta de entrada al área de cuenta en la cabecera global.

## Propósito

Ofrecer un único punto de entrada de cuenta en la cabecera que distinga sesión activa/anónima, enlace `/cuenta` o `/login` y cumpla WCAG 2.2 AA.

## Requirements

### Requirement: Botón de persona en la zona de utilidades de la cabecera

El sistema DEBE mostrar en la zona de utilidades de `MainLayout.razor` un botón de persona que, con sesión iniciada, navegue a `/cuenta` y muestre `UserName`; sin sesión, navegue a `/login` (sin `ReturnUrl`: no hay destino privado que preservar).

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

Los tests de contrato de fuente (precedente `AuthorizationPipelineContractTests.cs:139-176`, marcadores sin atar a posición) DEBEN afirmar: presencia del botón de persona en `MainLayout`, distinción con/sin sesión y existencia del enlace a `/login`.

#### Scenario: Marcadores de contrato en verde

- GIVEN la suite de tests
- WHEN se ejecuta `dotnet test Ludeka.sln`
- THEN los contratos de fuente de la puerta pasan.
