# Delta for policy-based-authorization

## MODIFIED Requirements

### Requirement: Pipeline de autenticación y autorización en Blazor SSR interactivo

`Program.cs` DEBE llamar a `UseAuthentication()` y `UseAuthorization()` antes de `UseAntiforgery()`, DEBE registrar `AddCascadingAuthenticationState()` y `Routes.razor` DEBE usar `AuthorizeRouteView` con vistas `NotAuthorized` y `RedirectToLogin`. La redirección interactiva al inicio de sesión (`RedirectToLogin`) DEBE preservar `ReturnUrl` con la misma semántica que `LoginRedirect.ToLogin` (URL escapada + guard anti-bucle `ResolveReturnUrl`), sin bucles de redirección.

(Previously: `RedirectToLogin.razor:15` navegaba a `/login` con `forceLoad` sin añadir `ReturnUrl`, perdiendo la intención de destino que `LoginRedirect.cs:36` sí conserva.)

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
- THEN el guard anti-bucle (`ResolveReturnUrl`) impide el encadenamiento y no se produce bucle.

#### Scenario: Contrato de fuente de ReturnUrl

- GIVEN la suite de tests
- WHEN se ejecuta
- THEN el contrato de fuente afirma que `RedirectToLogin` preserva `ReturnUrl` con la semántica de `LoginRedirect`.
