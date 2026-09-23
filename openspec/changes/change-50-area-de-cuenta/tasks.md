# Tasks: INC-50 — Área de Cuenta (Puerta de Acceso en la Cabecera y Hub del Usuario)

> **Cambio:** `change-50-area-de-cuenta` · modo híbrido · rama `inc/area-de-cuenta` (HEAD `b8b35fd`) · worktree `C:\repos\ludeka-wt\area-de-cuenta`
> **strict_tdd:** true · comando: `dotnet test Ludeka.sln` · línea base: **1.622 unitarias + 10 de integración**
> **Orden TDD:** design D4.3, 6 pasos · **Threat matrix:** todas las filas N/A — el RED de este cambio es de contrato de fuente, no de borde de proceso.

## Fase 1 — RED global (paso D4.3.1)

- [x] **1.1** Crear `tests/Ludeka.UnitTests/Web/AccountAreaContractTests.cs` con los 11 contratos de D4.4 (helpers `ReadSource`/`GetRepoRoot` propios; marcadores sin posiciones de línea; `RewrittenLinks_ShouldTargetTheAccountArea` lee `MainLayout`, `GameDetail` y `Radar`).
  - **Ficheros:** crear `tests/Ludeka.UnitTests/Web/AccountAreaContractTests.cs` · **Prereq:** ninguno (abre el ciclo).
  - **RED:** `dotnet test Ludeka.sln` → fallo observable: `No se encontró el archivo fuente: src/Ludeka.Web/Components/Pages/Account.razor` y marcadores ausentes en `MainLayout`/`MyLibrary`/`AccountConnections`/`GameDetail`/`Radar` (ancla: `Hub_ShouldDeclareItsRouteAndPlainAuthorize`).
  - **GREEN:** se cierra con 2.1-5.1; `OutOfScopeFiles_ShouldKeepTheAliveAlias` y `AccountConnections_ShouldKeepInc49LogicUntouched` nacen en verde como pines hacia adelante.
  - **Verificación:** RED observado y capturado · **Líneas:** 110-170.

- [x] **1.2** Modificar `tests/Ludeka.UnitTests/Web/AuthorizationPipelineContractTests.cs`: actualizar el marcador de `Routes_ShouldUseAuthorizeRouteViewWithRedirectToLogin` (`Contains("ExternalAuthenticationSchemes.LoginPath")` → `Contains("ToLogin(")`, conservando `RendererInfo.IsInteractive`) y añadir `RedirectToLogin_ShouldPreserveReturnUrlWithLoginRedirectSemantics` (`Contains("ToLogin(")` + `DoesNotContain("NavigateTo(ExternalAuthenticationSchemes.LoginPath")`).
  - **Ficheros:** modificar `tests/Ludeka.UnitTests/Web/AuthorizationPipelineContractTests.cs` · **Prereq:** 1.1.
  - **RED:** `dotnet test Ludeka.sln` → ambos tests en rojo sobre `RedirectToLogin.razor` actual (decisión D2: el marcador se actualiza dentro de la propia RED).
  - **GREEN:** se cierra con 4.1 · **Verificación:** RED capturado. El alias se afirma solo en 1.1 (`MyLibrary_ShouldDeclareCanonicalRouteAndAlias`); no se extiende `AliasRoute_ShouldLiveInAPage…` (un sitio u otro, no ambos). · **Líneas:** 15-35.

## Fase 2 — Alias + hub navegable (paso D4.3.2)

- [x] **2.1** Crear `src/Ludeka.Web/Components/Shared/AccountSectionNav.razor`: componente presentacional con `[Parameter] Active` (`"ludoteca" | "conexiones"`), vuelta al hub `href="/cuenta"`, enlaces Ludoteca/Conexiones y `aria-current="@(Active == … ? "page" : null)` (D3.1). Sin `ICurrentUserService` ni `NavigationManager`.
  - **Ficheros:** crear `AccountSectionNav.razor` · **Prereq:** 1.1.
  - **RED:** `dotnet test Ludeka.sln` → `SectionNav_ShouldMarkTheActiveSectionWithAriaCurrent` rojo (fichero inexistente).
  - **GREEN:** markup + `@code` con el único parámetro `Active` · **Verificación:** `dotnet test Ludeka.sln --filter "FullyQualifiedName~SectionNav_Should"` · **Líneas:** 40-65.

- [x] **2.2** Crear `src/Ludeka.Web/Components/Pages/Account.razor`: `@page "/cuenta"` + `@attribute [Authorize]` plano (patrón `AccountConnections.razor:1-2`, sin política) e inventario de 4 tarjetas-enlace: Ludoteca → `/cuenta/ludoteca`, Conexiones → `/cuenta/conexiones`, Tema y País → `/cuenta/ludoteca?seccion=apariencia` (D1). Razor puro, sin `@code`, SSR-friendly.
  - **Ficheros:** crear `Account.razor` · **Prereq:** 1.1.
  - **RED:** `dotnet test Ludeka.sln` → `Hub_ShouldDeclareItsRouteAndPlainAuthorize` y `Hub_ShouldInventoryTheFourAccountSections` rojos.
  - **GREEN:** solo markup de enlaces con los marcadores `href` y textos `Ludoteca`/`Conexiones`/`Tema`/`País` · **Verificación:** `dotnet test Ludeka.sln --filter "FullyQualifiedName~Hub_Should"` · **Líneas:** 90-140.

- [x] **2.3** Modificar `src/Ludeka.Web/Components/Pages/MyLibrary.razor`: (a) segunda ruta `@page "/cuenta/ludoteca"` conservando `@page "/mi-ludoteca"`; (b) `[SupplyParameterFromQuery(Name = "seccion")]` + mapeo `apariencia` → `TabType.Appearance` en `OnInitialized` (D1); (c) montar `<AccountSectionNav Active="ludoteca" />`. **Sin `@attribute [Authorize]`** (preserva el comportamiento anónimo actual del alias).
  - **Ficheros:** modificar `MyLibrary.razor` · **Prereq:** 2.1.
  - **RED:** `dotnet test Ludeka.sln` → `MyLibrary_ShouldDeclareCanonicalRouteAndAlias`, `MyLibrary_ShouldAcceptTheAppearanceDeepLink` y `ChildPages_ShouldMountTheSharedSectionHeader` (parte ludoteca) rojos.
  - **GREEN:** atributos de ruta/query añadidos; el `@code` de 390 líneas no se reestructura · **Verificación:** `dotnet test Ludeka.sln --filter "FullyQualifiedName~MyLibrary_Should|FullyQualifiedName~ChildPages_Should"` · **Líneas:** 15-30.

- [x] **2.4** Modificar `src/Ludeka.Web/Components/Pages/AccountConnections.razor`: **solo markup de navegación** — montar `<AccountSectionNav Active="conexiones" />` antes del `<header>` existente. Cero cambios en `@code`, servicios o flujos de INC-49.
  - **Ficheros:** modificar `AccountConnections.razor` · **Prereq:** 2.1.
  - **RED:** `dotnet test Ludeka.sln` → `ChildPages_ShouldMountTheSharedSectionHeader` (parte conexiones) rojo.
  - **GREEN:** una línea de montaje · **Verificación:** `dotnet test Ludeka.sln --filter "FullyQualifiedName~ChildPages_Should"` + `AccountConnectionsPageContractTests` existentes siguen en verde · **Líneas:** 3-10.

## Fase 3 — Puerta en cabecera (paso D4.3.3)

- [ ] **3.1** Modificar `src/Ludeka.Web/Components/Layout/MainLayout.razor`: (a) botón de persona en la zona de utilidades (líneas 54-97): con sesión `href="/cuenta"` + texto `UserName`, sin sesión `href="/login"` (sin `ReturnUrl`) + texto «Entrar»; clases WCAG `sr-only sm:not-sr-only`, `focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)] focus-visible:ring-offset-2`, `min-h-[24px] min-w-[24px]` (D3.2-D3.4); (b) `private bool HasSession => !string.IsNullOrWhiteSpace(CurrentUserService.UserId);` en el `@code` existente; (c) píldora línea 94: `href="/mi-ludoteca"` → `href="/cuenta/ludoteca"`.
  - **Ficheros:** modificar `MainLayout.razor` · **Prereq:** 2.2 (el destino `/cuenta` debe existir antes que la puerta).
  - **RED:** `dotnet test Ludeka.sln` → `HeaderGate_ShouldDistinguishSessionStatesAndLinkLogin` y `HeaderGate_ShouldMeetWcagFocusAndTargetSizeMarkers` rojos.
  - **GREEN:** markup condicional sobre la inyección ya presente `@inject ICurrentUserService CurrentUserService`; **sin tocar `ICurrentUserService`** · **Verificación:** `dotnet test Ludeka.sln --filter "FullyQualifiedName~HeaderGate_Should"` + regresión de cabecera · **Líneas:** 40-70.

## Fase 4 — ReturnUrl (paso D4.3.4)

- [ ] **4.1** Modificar `src/Ludeka.Web/Components/Shared/RedirectToLogin.razor`: línea 15 → `Navigation.ToLogin();` (dentro de `if (RendererInfo.IsInteractive)`) + comentario de propósito actualizado. Reutiliza `LoginRedirect.ToLogin` (escape + guard `ResolveReturnUrl`); no se cambia la visibilidad de `ResolveReturnUrl`.
  - **Ficheros:** modificar `RedirectToLogin.razor` · **Prereq:** 1.2.
  - **RED:** `dotnet test Ludeka.sln` → `Routes_ShouldUseAuthorizeRouteViewWithRedirectToLogin` (marcador ya actualizado en 1.2) y `RedirectToLogin_ShouldPreserveReturnUrlWithLoginRedirectSemantics` en rojo.
  - **GREEN:** sustituir `NavigateTo(ExternalAuthenticationSchemes.LoginPath, forceLoad: true)` por `Navigation.ToLogin()` · **Verificación:** `dotnet test Ludeka.sln --filter "FullyQualifiedName~Routes_ShouldUseAuthorizeRouteView|FullyQualifiedName~RedirectToLogin_Should"` · **Líneas:** 5-12. **Excluido:** plumbing de `ReturnUrl` en `Login.razor`/`Program.cs` (Open Question, fuera de alcance).

## Fase 5 — Reescrituras selectivas (paso D4.3.5)

- [ ] **5.1** Reescribir `href="/mi-ludoteca"` → `href="/cuenta/ludoteca"` en `src/Ludeka.Web/Components/Pages/GameDetail.razor:45` y `Radar.razor:41`. **No modificar** `PublicProfile.razor`, `manifest.webmanifest` ni `offline.html` (pines `Contains("/mi-ludoteca")` ya en verde).
  - **Ficheros:** modificar `GameDetail.razor`, `Radar.razor` · **Prereq:** 2.3.
  - **RED:** `dotnet test Ludeka.sln` → `RewrittenLinks_ShouldTargetTheAccountArea` rojo.
  - **GREEN:** dos `href` · **Verificación:** `dotnet test Ludeka.sln --filter "FullyQualifiedName~RewrittenLinks_Should|FullyQualifiedName~OutOfScopeFiles_Should"` · **Líneas:** 2-4.

## Fase 6 — GREEN global + verificación (paso D4.3.6)

- [ ] **6.1** GREEN global: `dotnet test Ludeka.sln` completo ≥ **1.622 unitarias + 10 de integración** (nunca por debajo de la línea base); parar antes el servidor si quedó arrancado (`MSB3027` bloquea `bin/Debug`, INC-50 §8). Refactor solo si mantiene la suite en verde. **Prereq:** 1.1-5.1 · **Verificación:** suite completa · **Líneas:** 0.

- [ ] **6.2** Corregir el criterio 8 en `docs/increments/inc-50-area-de-cuenta.md`: `1.417 pruebas` → `1.622 pruebas unitarias + 10 de integración`. **Prereq:** 6.1 · **RED:** no aplica (documento); verificación: `Select-String -Pattern "1.622"` en el fichero sin resto de `1.417` · **GREEN:** criterio corregido · **Líneas:** 1-2.

- [ ] **6.3** Smoke SSR de `ReturnUrl`: arrancar la app según INC-50 §8 (`ASPNETCORE_URLS="https://localhost:7291;http://localhost:5081"`, `--no-launch-profile`, sin `.claude/launch.json`); con sesión borrada pedir `https://localhost:7291/cuenta` y observar `Location: /login?ReturnUrl=%2Fcuenta`. Documentar la observación real (o la contingencia honesta si el middleware no emite challenge en SSR) en `verify-report.md`. **Prereq:** 4.1 · **Verificación:** evidencia manual documentada · **Líneas:** 0.

- [ ] **6.4** Smoke manual INC-49 tarea 4.5 (continuar del paso 2/7 al 7/7) con OAuth de `ludeka-web-user-secrets-2026`: login Google/Discord; navegar **desde la puerta** a `/cuenta` y **desde el hub** a `/cuenta/conexiones` sin escribir la URL; probar Tab/foco visible y estado con texto en la puerta; pestañas y alias `/mi-ludoteca` de la ludoteca. Parar el servidor después. **Prereq:** 2.2, 3.1 · **Verificación:** checklist manual completado · **Líneas:** 0.

## Cobertura: mapeo requisito → tarea → test

| Requisito / escenario (spec) | Tarea | Test verificador |
|---|---|---|
| Puerta: con sesión → `/cuenta` + `UserName` | 3.1 | `HeaderGate_ShouldDistinguishSessionStatesAndLinkLogin` (RED en 1.1) |
| Puerta: sin sesión → `/login` | 3.1 | ídem |
| Puerta: ≥1 enlace `/login` en la interfaz | 3.1 | ídem |
| Puerta: teclado + foco visible (WCAG) | 3.1 | `HeaderGate_ShouldMeetWcagFocusAndTargetSizeMarkers` + smoke 6.4 |
| Puerta: estado con texto, no solo icono/color | 3.1 | ídem (`sr-only sm:not-sr-only`, «Entrar»/`UserName`) |
| Puerta: área objetivo ≥24×24 (WCAG 2.5.8) | 3.1 | ídem (`min-h-[24px] min-w-[24px]`) |
| Puerta: contrato de fuente en verde | 1.1 | `dotnet test Ludeka.sln` |
| Hub: `/cuenta` con `[Authorize]` plano | 2.2 | `Hub_ShouldDeclareItsRouteAndPlainAuthorize` |
| Hub: inventario Ludoteca/Conexiones/Tema/País | 2.2 | `Hub_ShouldInventoryTheFourAccountSections` |
| Hub: Tema/País → `?seccion=apariencia` (D1) | 2.2 + 2.3 | `Hub_ShouldInventory…` + `MyLibrary_ShouldAcceptTheAppearanceDeepLink` |
| Hub: anónimo redirigido con `ReturnUrl` | 4.1 (+2.2) | `RedirectToLogin_ShouldPreserveReturnUrlWithLoginRedirectSemantics` + smoke 6.3 |
| Hub: montaje del hub testificado | 2.2 | `Hub_ShouldDeclareItsRouteAndPlainAuthorize` |
| Ludoteca: ruta hija + alias `/mi-ludoteca` | 2.3 | `MyLibrary_ShouldDeclareCanonicalRouteAndAlias` |
| Ludoteca: alias vivo por contrato | 1.1 + 2.3 | ídem (teoría `AliasRoute_…` preexistente cubre el precedente) |
| Conexiones: alcanzable desde el hub | 2.2 | `Hub_ShouldInventoryTheFourAccountSections` |
| Conexiones: INC-49 intacto | 2.4 | `AccountConnections_ShouldKeepInc49LogicUntouched` + `AccountConnectionsPageContractTests` (sin editar) |
| Cabecera de sección: `aria-current` activo | 2.1 | `SectionNav_ShouldMarkTheActiveSectionWithAriaCurrent` |
| Cabecera de sección: montada en hijas | 2.3 + 2.4 | `ChildPages_ShouldMountTheSharedSectionHeader` |
| Reescritura: píldora, `GameDetail`, `Radar` | 3.1 + 5.1 | `RewrittenLinks_ShouldTargetTheAccountArea` |
| Fuera de alcance: `PublicProfile`/manifest/offline intactos | 5.1 (no tocar) | `OutOfScopeFiles_ShouldKeepTheAliveAlias` |
| Contratos del hub y del alias en verde | 1.1 | `dotnet test Ludeka.sln` |
| PBA: `ReturnUrl` preservada vía `LoginRedirect.ToLogin()` | 4.1 | `Routes_ShouldUseAuthorizeRouteViewWithRedirectToLogin` + `RedirectToLogin_ShouldPreserveReturnUrlWithLoginRedirectSemantics` (1.2) |
| PBA: guard anti-bucle (`ResolveReturnUrl`) | 4.1 (reutiliza `ToLogin`) | `RedirectToLogin_Should…` (`Contains("ToLogin(")`) + `LoginRedirect` ya testificado |
| PBA: orden de pipeline / cascada / anónimo en ruta protegida | sin cambios | tests INC-46 existentes vía 6.1 (regresión) |
| ULV: ruta canónica + alias idéntico | 2.3 | `MyLibrary_ShouldDeclareCanonicalRouteAndAlias` |
| ULV: contadores, cambio de pestaña, cola de moderación | sin cambios | regresión `user-library-view` existente vía 6.1 |
| Transversal: contratos de fuente + línea base 1.622+10 | 1.1, 1.2, 6.1 | `dotnet test Ludeka.sln` |
| Transversal: `ICurrentUserService` intacto | excluido (no se toca) | `CurrentUserContractTests` vía 6.1 |
| Transversal: sin migraciones, rollback `git revert` | inherente (aditivo) | 6.1 (suite sin migraciones nuevas) |
| Criterio 8 corregido en `inc-50-area-de-cuenta.md` | 6.2 | revisión documental |
| Smoke INC-49 tarea 4.5 (2/7 → 7/7) | 6.4 | manual documentado |
| Smoke SSR de `ReturnUrl` | 6.3 | manual documentado en `verify-report.md` |

## Fuera de alcance (excluido de estas tareas)

Avatar del proveedor · página física `/cuenta/ajustes` (INC-61+) · plumbing extremo de `ReturnUrl` en `Login.razor`/`Program.cs` (Open Question) · moderación/administración · perfil público (`PublicProfile.razor`) · cualquier edición de `ICurrentUserService` · `manifest.webmanifest` y `offline.html` (solo pines).

## Review Workload Forecast

```
Review Workload Forecast
Chained PRs recommended: No
400-line budget risk: Alto
Estimated changed lines: 320-540 (rango)
Decision needed before apply: Sí
```

Guard contract (líneas literales para guards inferiores):

```text
Decision needed before apply: Yes
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: High
```

| Campo | Valor |
|-------|-------|
| Estimated changed lines | 320-540 (adiciones+borrados, autoría; alinea con el design 320-530 + corrección del criterio 8) |
| 400-line budget risk | Alto |
| Chained PRs recommended | No |
| Suggested split | PR único; fallback si el maintainer rechaza la excepción: PR1 `header-account-gate` → PR2 `account-area-hub` |
| Delivery strategy | single-pr |
| Chain strategy | size-exception (**pendiente de aprobación del maintainer — NO aprobado por esta fase**) |

**Decision needed before apply: Yes** — con `single-pr`, apply exige `size:exception` aprobado antes de empezar. Cada tarea individual queda por debajo de ~400 líneas (heurística, no tope). Commits de unidad de trabajo tras el GREEN global (6.1); los tests viajan con su comportamiento y la suite completa valida la cabeza del PR.

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| U1 | Hub, sección compartida, rutas hijas/alias + contratos del área (1.1, 2.1-2.4) ≈258-415 líneas | PR único | `dotnet test Ludeka.sln --filter "FullyQualifiedName~Hub_Should\|FullyQualifiedName~MyLibrary_Should\|FullyQualifiedName~SectionNav_Should\|FullyQualifiedName~ChildPages_Should\|FullyQualifiedName~OutOfScopeFiles\|FullyQualifiedName~AccountConnections_ShouldKeep"` | N/A — la verificación runtime es la suite; el smoke es U4 | revert de `Account.razor`, `AccountSectionNav.razor`, `MyLibrary.razor`, `AccountConnections.razor` y `AccountAreaContractTests.cs` |
| U2 | Puerta de cabecera, `ReturnUrl` y reescrituras (1.2, 3.1, 4.1, 5.1) ≈62-121 líneas | PR único | `dotnet test Ludeka.sln --filter "FullyQualifiedName~HeaderGate_Should\|FullyQualifiedName~Routes_ShouldUseAuthorizeRouteView\|FullyQualifiedName~RedirectToLogin_Should\|FullyQualifiedName~RewrittenLinks_Should"` | smoke SSR (T6.3) | revert de `MainLayout.razor`, `RedirectToLogin.razor`, `GameDetail.razor`, `Radar.razor` y marcadores de `AuthorizationPipelineContractTests.cs` |
| U3 | Corrección del criterio 8 (6.2) ≈1-2 líneas | PR único | `Select-String -Pattern "1.622" docs\increments\inc-50-area-de-cuenta.md` | N/A (documento) | revert del diff documental |
| U4 | Smokes runtime: SSR `ReturnUrl` + INC-49 tarea 4.5 (6.3, 6.4) | PR único (sin commit propio) | — | app INC-50 §8 (HTTPS 7291/5081, sin `.claude/launch.json`) + navegador | N/A — la evidencia vive en `verify-report.md`, fuera del diff del PR |

Nota U1: el fichero de contratos es compartido; su subconjunto enfocado valida U1, el resto (`HeaderGate_*`, `RewrittenLinks_*`) se valida en U2 y en la cabeza del PR con la suite completa (paso 6.1), porque el diseño exige RED global y GREEN global.
