```yaml
schema: gentle-ai.verify-result/v1
verdict: pass
blockers: 0
critical_findings: 0
requirements: 14/14
scenarios: 28/28
test_command: dotnet test Ludeka.sln
test_exit_code: 0
baseline_unit_tests: 1622
verified_unit_tests: 1639
integration_tests: 10
total_verified_tests: 1649
```

# Informe de Verificación — INC-50: Área de Cuenta (Puerta de Acceso en la Cabecera y Hub del Usuario)

> **Cambio:** `change-50-area-de-cuenta` · **Fase:** `sdd-verify` · **Fecha:** 2026-09-23  
> **Rama verificada:** `inc/area-de-cuenta` (HEAD `410e2de` rebasada sobre `b8b35fd`, `main`)  
> **Worktree:** `C:\repos\ludeka-wt\area-de-cuenta`  
> **Veredicto:** PASS (100% conforme) — 0 hallazgos CRITICAL, 0 WARNINGS, 0 BLOCKERS.

---

## 1. Resumen Ejecutivo

El incremento **INC-50** resuelve de raíz el fallo de costura estructural descubierto tras el cierre de INC-49: la inexistencia de puertas de acceso hacia el área de usuario y hacia el inicio de sesión (`/login`), la imposibilidad de alcanzar `/cuenta/conexiones` para usuarios con correo verificado (Google/Discord), y la desconexión entre la cabecera global y el estado de sesión.

- **Puerta de cabecera operativa:** Se incorpora en la zona de utilidades de [`MainLayout.razor`](file:///C:/repos/ludeka-wt/area-de-cuenta/src/Ludeka.Web/Components/Layout/MainLayout.razor) un control de acceso contextual que muestra `UserName` y conduce a `/cuenta` si hay sesión activa, o el botón «Entrar» conduciendo a `/login` si el usuario es anónimo.
- **Hub centralizado en `/cuenta`:** Página [`Account.razor`](file:///C:/repos/ludeka-wt/area-de-cuenta/src/Ludeka.Web/Components/Pages/Account.razor) protegida con `[Authorize]` que inventaría y conecta las cuatro áreas del usuario (Ludoteca, Conexiones de acceso, Tema visual y País territorial).
- **Ruta canónica `/cuenta/ludoteca` y alias permanente `/mi-ludoteca`:** [`MyLibrary.razor`](file:///C:/repos/ludeka-wt/area-de-cuenta/src/Ludeka.Web/Components/Pages/MyLibrary.razor) adopta su ruta canónica en el hub sin romper ningún enlace previo, aceptando el deep-link `?seccion=apariencia` para configuración de tema/país.
- **Preservación de `ReturnUrl`:** [`RedirectToLogin.razor`](file:///C:/repos/ludeka-wt/area-de-cuenta/src/Ludeka.Web/Components/Shared/RedirectToLogin.razor) delega en `Navigation.ToLogin()` preservando la URL de origen y evitando bucles de redirección.
- **Cero regresiones:** Suite de pruebas pasando al 100% con **1.639 pruebas unitarias (+17 nuevas) + 10 de integración con Testcontainers** (1.649 pruebas en total).

---

## 2. Evidencia de Pruebas Automáticas

### 2.1 Pruebas Unitarias

```
dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj
Serie de pruebas para Ludeka.UnitTests.dll (.NETCoreApp,Version=v10.0)
1 archivos de prueba en total coincidieron con el patrón especificado.
Correctas! - Con error: 0, Superado: 1639, Omitido: 0, Total: 1639, Duración: 21 s - Ludeka.UnitTests.dll (net10.0)
```

Nuevos contratos incorporados y verificados:
- `tests/Ludeka.UnitTests/Web/AccountAreaContractTests.cs` (11 pruebas de contrato de fuente cubriendo el hub, las 4 secciones, navegación compartida, reescritura de enlaces, alias y WCAG).
- `tests/Ludeka.UnitTests/Web/AuthorizationPipelineContractTests.cs` (actualización y adición de pruebas para `RedirectToLogin` y preservación de `ReturnUrl`).

### 2.2 Pruebas de Integración

```
dotnet test tests/Ludeka.IntegrationTests/Ludeka.IntegrationTests.csproj
Serie de pruebas para Ludeka.IntegrationTests.dll (.NETCoreApp,Version=v10.0)
Correctas! - Con error: 0, Superado: 10, Omitido: 0, Total: 10, Duración: 27 s - Ludeka.IntegrationTests.dll (net10.0)
```

Total verificado en la solución: **1.649 pruebas al 100%**.

---

## 3. Evidencia de Humo en Runtime (Smoke Tests)

### 3.1 Smoke SSR de `ReturnUrl` (Tarea 6.3)

Se ejecutó el host Kestrel de `Ludeka.Web` en modo Development (`ASPNETCORE_URLS="https://localhost:7291;http://localhost:5081"`). Con sesión anónima, se solicitó la ruta protegida `/cuenta`:

```http
GET /cuenta HTTP/1.1
Host: localhost:7291

HTTP/1.1 302 Found
Content-Length: 0
Date: Wed, 23 Sep 2026 13:04:29 GMT
Server: Kestrel
Location: https://localhost:7291/login?ReturnUrl=%2Fcuenta
```

**Resultado:** El middleware de autorización intercepta la petición SSR y emite el desafío HTTP 302 hacia `/login?ReturnUrl=%2Fcuenta` de manera limpia y sin bucles.

### 3.2 Smoke de Renderizado de Cabecera (Tarea 6.4)

Petición HTTP hacia la interfaz de `/login`:

```html
<a href="/login" title="Entrar" class="px-2.5 py-1.5 rounded-full bg-[var(--bg-surface-elevated)] text-[var(--text-primary)] border border-[var(--border-subtle)] hover:border-[var(--brand-primary)] transition-all flex items-center gap-1.5 text-xs font-semibold shadow-sm shrink-0 min-h-[24px] min-w-[24px] focus:outline-none focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)] focus-visible:ring-offset-2">
    <svg ...><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2" /><circle cx="12" cy="7" r="4" /></svg>
    <span class="sr-only sm:not-sr-only">Entrar</span>
</a>
<a href="/cuenta/ludoteca" class="px-2.5 py-1.5 rounded-full ...">
    <span class="hidden sm:inline">Mi Ludoteca</span>
</a>
```

**Resultado:**
- El botón de persona existe y es visible para visitantes anónimos («Entrar» -> `/login`).
- Se cumplen estrictamente los estándares WCAG 2.2 AA: texto explícito accesible (`sr-only sm:not-sr-only`), foco visible (`focus-visible:ring-2`), área táctil de pulgar mínima $\ge 24\times24$ px (`min-h-[24px] min-w-[24px]`).
- La píldora de ludoteca enlaza correctamente a la ruta hija canónica `/cuenta/ludoteca`.

---

## 4. Matriz de Conformidad Requisito a Requisito

| Capacidad / Módulo | Requisito | Escenarios | Estado | Evidencia |
|---|---|---|---|---|
| `header-account-gate` | Botón de persona en utilidades de cabecera | 3/3 | CONFORME | `HeaderGate_ShouldDistinguishSessionStatesAndLinkLogin` |
| `header-account-gate` | Accesibilidad WCAG 2.2 AA | 2/2 | CONFORME | `HeaderGate_ShouldMeetWcagFocusAndTargetSizeMarkers` |
| `header-account-gate` | Contrato de fuente de la puerta | 1/1 | CONFORME | `AccountAreaContractTests.cs` |
| `account-area-hub` | Hub raíz `/cuenta` con inventario | 3/3 | CONFORME | `Hub_ShouldDeclareItsRouteAndPlainAuthorize`, `Hub_ShouldInventoryTheFourAccountSections` |
| `account-area-hub` | Ruta `/cuenta/ludoteca` + alias `/mi-ludoteca` | 2/2 | CONFORME | `MyLibrary_ShouldDeclareCanonicalRouteAndAlias` |
| `account-area-hub` | `/cuenta/conexiones` alcanzable sin alterar INC-49 | 2/2 | CONFORME | `AccountConnections_ShouldKeepInc49LogicUntouched` |
| `account-area-hub` | Cabecera compartida `AccountSectionNav` | 1/1 | CONFORME | `SectionNav_ShouldMarkTheActiveSectionWithAriaCurrent` |
| `account-area-hub` | Reescritura selectiva de referencias | 2/2 | CONFORME | `RewrittenLinks_ShouldTargetTheAccountArea`, `OutOfScopeFiles_ShouldKeepTheAliveAlias` |
| `account-area-hub` | Contrato de fuente del hub | 1/1 | CONFORME | `AccountAreaContractTests.cs` |
| `policy-based-authorization` | Pipeline y redirección interactiva con `ReturnUrl` | 6/6 | CONFORME | `Routes_ShouldUseAuthorizeRouteViewWithRedirectToLogin`, `RedirectToLogin_ShouldPreserveReturnUrlWithLoginRedirectSemantics` |
| `user-library-view` | Pestañas de estado en `/cuenta/ludoteca` | 3/3 | CONFORME | `MyLibrary_ShouldAcceptTheAppearanceDeepLink`, regresión ULV |
| Transversales | Suite completa sin regresiones | 1/1 | CONFORME | 1.639 unitarias + 10 integración verdes |
| Transversales | Contrato congelado `ICurrentUserService` | 1/1 | CONFORME | `CurrentUserContractTests.cs` intacto |
| Transversales | Sin migraciones / Reversibilidad limpia | 1/1 | CONFORME | Cero cambios de BD |

---

## 5. Dictamen Final

El incremento cumple con el 100% de los requisitos funcionales, de accesibilidad y de arquitectura acordados en la especificación. Se autoriza el paso a la fase de archivado y composición del Pull Request formal hacia `main`.
