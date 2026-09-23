# Design: INC-50 — Área de Cuenta — Puerta de Acceso en la Cabecera y Hub del Usuario

> **Cambio:** `change-50-area-de-cuenta` · **Modo de artefactos:** hybrid · **Idioma:** español castellano
> **Rama:** `inc/area-de-cuenta` · HEAD de trabajo `b8b35fd` · worktree `C:\repos\ludeka-wt\area-de-cuenta`
> **strict_tdd:** true · **Test command:** `dotnet test Ludeka.sln` · **Línea base:** 1.622 unitarias + 10 de integración
> **Fuentes:** `proposal.md`, `spec.md`, unidades de capacidad en `specs/{header-account-gate,account-area-hub,policy-based-authorization,user-library-view}/spec.md`, `explore.md`, `docs/increments/inc-50-area-de-cuenta.md`

## Technical Approach (Enfoque técnico)

El cambio es **íntegramente aditivo en rutas y componentes Blazor**, anclado en tres precedentes ya testificados del repositorio:

1. **Página privada con `[Authorize]` plano** — precedente `AccountConnections.razor:1-2` (INC-49), que define el patrón de las rutas hijas `/cuenta/*`.
2. **Alias multi-`@page`** — precedente `Radar.razor`, `MediaModeration.razor`, testificado en `AuthorizationPipelineContractTests.AliasRoute_ShouldLiveInAPageThatIsAlreadyProtected` (líneas 63-74).
3. **Tests de contrato de fuente con `ReadSource(relativePath)`** — precedente `AuthorizationPipelineContractTests.cs:178-183` y `AccountConnectionsPageContractTests.cs:57-63` (el repo **duplica** deliberadamente estos helpers por fichero de test; decisión documentada en `WebStartupGuardsTests.cs:171`).

Sobre esa base, la puerta de cabecera consume la superficie **congelada** `ICurrentUserService.UserId`/`UserName` con el patrón `HasSession => !string.IsNullOrWhiteSpace(CurrentUserService.UserId)` ya usado en `MyLibrary.razor:963`; el `ReturnUrl` del camino interactivo se alinea reutilizando **la propia extensión `LoginRedirect.ToLogin`** (`LoginRedirect.cs:26-38`), sin duplicar semántica; y el hub `/cuenta` inventaría vialógicamente las cuatro entradas (Ludoteca, Conexiones, Tema, País) enlazando cada una a su superficie existente — una sola superficie de verdad por ajuste, cero UI duplicada.

## Threat Matrix (Applicability-Driven)

> El diseño **no** toca comandos shell, automatización VCS/PR, clasificación de ficheros ejecutables ni integración de procesos. Las rutas nuevas son navegación de aplicación Blazor (`@page`), cubierta por tests de contrato ordinarios, no por un borde de proceso.

| Boundary | Applicability | Reason |
|---|---|---|
| Documentation-like paths (`requirements.txt`, MD ejecutable, `README.sh`) | **N/A** | No se crean ni clasifican ficheros ejecutables; solo `.razor`, `.cs` de tests y documentación SDD. |
| Git repository selection (`git -C`, rutas relativas/absolutas) | **N/A** | El diseño no invoca git ni resuelve repositorios; los commits los hace apply bajo la política ordinaria. |
| Commit state (staged, `commit -a`, índice vacío) | **N/A** | Sin automatización de commits en el alcance. |
| Push state (tracking, first push, refspec) | **N/A** | `single-pr` bajo política ordinaria; sin comandos de push en el diseño. |
| PR commands (`--head`, prefijos de entorno, comandos compuestos) | **N/A** | Sin construcción de comandos PR; la estrategia de entrega la resuelve el orquestador con el `size:exception` pendiente. |

**Resultado:** matriz no aplicable — ninguna fila exige tests RED de borde de proceso. Los tests RED de este diseño son de contrato de fuente (ver *Testing Strategy*).

## Architecture Decisions (Decisiones de arquitectura)

### Decision D1 — Anclajes de las entradas «Tema» y «País» del hub

**Choice**: deep-link con parámetro de query a la pestaña Apariencia ya existente: ambas entradas enlazan a **`/cuenta/ludoteca?seccion=apariencia`**. `MyLibrary.razor` declara `[SupplyParameterFromQuery(Name = "seccion")]` (precedente: `AccountConnections.razor:120` `resultado`, `Login.razor:65` `aviso`) y en `OnInitialized` mapea el código cerrado `apariencia` → `_activeTab = TabType.Appearance`, que es la pestaña *Apariencia & País* (`MyLibrary.razor:193-197`) — único panel que ya contiene **ambas** superficies: el grid de temas (líneas 237-397) y la tarjeta de país (líneas 400-442). El hub NO muestra controles de tema ni de país: solo enlaces.

**Alternatives considered**:
- *Invocar `LocationSelectorModal` desde el hub:* descartada — `_showLocationModal` es estado privado de `MainLayout.razor:319`; invocarlo desde `Account.razor` exige elevar estado (bus de eventos o cascada), acopla el hub a la cabecera y **no produce una URL**: el destino deja de ser enlazable, compartible y navegable en SSR. Además duplicaría la superficie de país (modal + tarjeta) como puerta del hub.
- *Enlazar solo a `/cuenta/ludoteca` sin parámetro:* descartada — cae en la pestaña por defecto `InCollection` (`MyLibrary.razor:942`) y el usuario no llega a la superficie de ajuste que promete la entrada.
- *Ancla `#fragment` a `panel-appearance`:* descartada — el panel solo existe en el DOM cuando la pestaña ya está activa; en carga inicial el fragmento no tiene elemento al que hacer scroll.
- *Mover la UI físicamente al hub:* fuera de alcance por decisión 5 de la propuesta (INC-61+).

**Rationale**: una sola superficie de verdad por ajuste (la UI de `MyLibrary` no se copia ni se mueve), el mecanismo es el mismo patrón de query de código cerrado que el repo ya usa y testifica, funciona en SSR estático (el parámetro llega en la petición), y es verificable por contrato de fuente sin atar posiciones de línea.

**Verificación por test de contrato de fuente** (`AccountAreaContractTests`):
- `MyLibrary.razor` contiene `@page "/cuenta/ludoteca"`, `@page "/mi-ludoteca"` y `SupplyParameterFromQuery(Name = "seccion")`.
- `Account.razor` contiene los marcadores `href="/cuenta/ludoteca?seccion=apariencia"` (entrada Tema) y el mismo destino (entrada País).
- El mapa `apariencia` → `TabType.Appearance` se afirma por marcador de código (p. ej. presencia de `TabType.Appearance` junto al manejo de `seccion`), sin atar a número de línea.

---

### Decision D2 — `ReturnUrl` exacta en `RedirectToLogin.razor`, guard anti-bucle y verificación del challenge SSR

**Choice**: la rama interactiva de `RedirectToLogin.razor:15` deja de construir la navegación a mano y pasa a llamar a **`Navigation.ToLogin()`** (la extensión ya existente en `LoginRedirect.cs:26-38`):

```razor
@code {
    protected override void OnInitialized()
    {
        if (RendererInfo.IsInteractive)
        {
            Navigation.ToLogin();   // antes: Navigation.NavigateTo(ExternalAuthenticationSchemes.LoginPath, forceLoad: true);
        }
    }
}
```

Con ello, el camino interactivo hereda **exactamente** la semántica de `LoginRedirect`:

- **Formato**: `"{LoginPath}?ReturnUrl={Uri.EscapeDataString(returnUrl)}"` con `returnUrl = absolute.PathAndQuery` (`LoginRedirect.cs:36,81`). El nombre `ReturnUrl` coincide con el parámetro por defecto del middleware de cookies de ASP.NET Core (`ReturnUrlParameter`), de modo que el desafío del middleware (SSR) y la navegación interactiva producen **el mismo formato intercambiable**.
- **Guard anti-bucle**: `ResolveReturnUrl` (`LoginRedirect.cs:69-82`) devuelve `null` — y `ToLogin` no navega — cuando la ruta actual ya es `/login` (o la URI no es absoluta). No hay bucle posible en el camino interactivo.
- **Sin duplicación**: no se copia ni se reescribe la construcción del query; `ResolveReturnUrl` sigue siendo `private` en `LoginRedirect` y **no se cambia su visibilidad** — el reutilizador es la extensión pública `ToLogin`, que ya encapsula escape + guard.

**Alternatives considered**:
- *Construir el query a mano en `RedirectToLogin` con `Uri.EscapeDataString` + lógica propia de guard:* descartada — sería copiar la semántica de `LoginRedirect`, recreando exactamente la bifurcación de dos caminos que este incremento existe para cerrar.
- *Hacer `ResolveReturnUrl` `internal`/`public` y llamarla desde el componente:* descartada — amplía la superficie de `LoginRedirect` sin necesidad; `ToLogin` ya es la frontera pública correcta.
- *No hacer nada en SSR y confiar solo en el middleware:* descartada por spec — la spec `policy-based-authorization` exige que `RedirectToLogin` preserve `ReturnUrl` en el camino interactivo.

**Impacto en un test existente (parte del RED, no una ruptura accidental)**: `AuthorizationPipelineContractTests.Routes_ShouldUseAuthorizeRouteViewWithRedirectToLogin` (líneas 117-119) afirma hoy `Assert.Contains("ExternalAuthenticationSchemes.LoginPath", redirect)`. Tras el cambio, el componente ya no referencia `LoginPath` directamente (lo hace `ToLogin`). **La propia fase RED actualiza ese marcador** a `Assert.Contains("ToLogin(", redirect)`, conservando `RendererInfo.IsInteractive`. Actualizar el marcador es requisito de la tarea de implementación, no un fallo descubierto tarde.

**Verificación del challenge SSR que exploración no pudo comprobar en runtime** (hueco 1 de `explore.md`):

| Nivel | Qué verifica | Cómo |
|---|---|---|
| **Test de contrato de fuente** (unitario, automático) | El camino interactivo preserva `ReturnUrl` con la semántica de `LoginRedirect` | `AuthorizationPipelineContractTests`: `redirect` contiene `ToLogin(`; y **ausencia** de la navegación vieja sin retorno: `DoesNotContain("NavigateTo(ExternalAuthenticationSchemes.LoginPath")`. Se afirma presencia/ausencia del parámetro por la llamada a `ToLogin` (único productor de `?ReturnUrl=` en el componente). |
| **Smoke test manual en apply/verify** (runtime, documentado) | El challenge del middleware en SSR estático incluye `ReturnUrl` | Con la app arrancada según INC-50 §8 (`ASPNETCORE_URLS="https://localhost:7291;http://localhost:5081"`, sin `.claude/launch.json`), peticionar **con sesión borrada** `https://localhost:7291/cuenta` y observar redirección cuya `Location` es `/login?ReturnUrl=%2Fcuenta` (escape de `/cuenta`). Documentar la observación real en `verify-report.md`. |
| **Contingencia** | Si el middleware **no** desafía en SSR (Blazor resuelve `NotAuthorized` y el endpoint no lleva metadata de autorización) | El resultado del smoke queda registrado como hallazgo honesto; la protección en SSR seguiría siendo la de `AuthorizeRouteView → RedirectToLogin` que no navega en estático. Si se decide añadir desafío por endpoint, es **fuera de INC-50** (toca `Program.cs`/metadata del pipeline de INC-46) y se abre incremento propio. El diseño no asume el resultado. |

**Hallazgo de diseño descubierto al anclar el código (importante, honestidad de fase)**: preservar `ReturnUrl` en `/login` es hoy **condición necesaria pero no suficiente** para el retorno extremo a extremo, porque el consumo posterior no existe en HEAD:

- `Login.razor` **no lee** `ReturnUrl` (solo lee `?aviso=`, línea 65).
- `Program.cs:333` fija `RedirectUri = "/"` en el desafío de `/login/external`: tras el OAuth, el usuario siempre vuelve a `/`.

Es decir: el spec de `policy-based-authorization` se cumple con D2 (el redirect **preserve** `ReturnUrl`, sin bucles, verificable por contrato), pero «devolver al usuario de donde iba» extremo a extremo exige hacer que `Login.razor` reenvíe `ReturnUrl` al formulario y que `/login/external` lo valide (solo ruta local, anti open-redirect) y lo use como `RedirectUri`. Eso **toca el mecanismo de acceso de INC-46**, declarado *Out of Scope* en la propuesta («se construye la puerta, no se toca lo que hay detrás»). Decisión: **no se amplía el alcance en esta fase**; el gap queda como Open Question de maintainer con recomendación de incremento de seguimiento (ver *Open Questions*). tasks NO debe meter ese plumbing salvo decisión explícita del maintainer.

**Rationale de D2**: una sola semántica de `ReturnUrl` en toda la app (la ya probada de INC-46), cero duplicación, guard reutilizado, y verificación honesta en dos niveles (contrato automático + smoke documentado) sin inventar evidencia runtime que la fase de exploración no tuvo.

---

### Decision D3 — Accesibilidad: sección activa, nombre accesible del botón, foco y área objetivo

**Choice**, con marcadores concretos:

1. **Sección activa en `AccountSectionNav`** — patrón exacto **`aria-current`** ya usado en el repo (`HeroEditorial.razor:50`):

   ```razor
   <a href="/cuenta/ludoteca"
      aria-current="@(Active == "ludoteca" ? "page" : null)">Ludoteca</a>
   ```

   `aria-current="page"` (no solo color/clase) es el marcado accesible que exige el requisito «Cabecera de sección compartida» de `account-area-hub`. Las pestañas intra-ludoteca ya usan `aria-selected` (`MyLibrary.razor:115`) — se mantiene ese otro patrón donde corresponde; `aria-current` es para navegación entre rutas, no para `role="tablist"`.

2. **Nombre accesible del botón de persona** — **«Entrar» sin sesión / `UserName` con sesión**, como texto real (no solo `aria-label` sobre un icono mudo):

   ```razor
   @if (HasSession)
   {
       <a href="/cuenta" ...>
           <Icon Name="user" Size="14" />
           <span class="sr-only sm:not-sr-only ...">@CurrentUserService.UserName</span>
       </a>
   }
   else
   {
       <a href="/login" ...>
           <Icon Name="user" Size="14" />
           <span class="sr-only sm:not-sr-only">Entrar</span>
       </a>
   }
   ```

   Justificación de la elección: la propuesta (decisión 4) fija icono + `UserName` sin avatar; el spec de `header-account-gate` exige que el lector de pantalla exprese el estado «mediante texto (nombre del usuario o «Entrar»)». La clase **`sr-only sm:not-sr-only`** es la clave técnica: los otros pills de cabecera usan `hidden sm:inline` (p. ej. `MainLayout.razor:96`), y `hidden` = `display:none` **elimina el texto del árbol de accesibilidad en móvil**, dejando el enlace sin nombre accesible donde solo cabe el icono. Con `sr-only` el nombre («Entrar»/`UserName`) existe en todos los tamaños y el texto visible en ≥sm cumple WCAG 2.5.3 (el texto visible está contenido en el nombre accesible). No se añade `aria-label` superpuesto: el contenido del texto **es** el nombre accesible, evitando divergencia label/visible. Si `UserName` es largo, `truncate` recorta el visual pero el texto completo sigue disponible al árbol de accesibilidad.

3. **Indicador de foco** — clase Tailwind concreta, precedente literal de los botones de `AccountConnections.razor:94,106`:

   ```
   focus:outline-none focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)] focus-visible:ring-offset-2
   ```

   `focus-visible` (no `focus`) evita anillo al clic de ratón y lo muestra con teclado — exactamente el escenario «alcanzable con Tab, indicador de foco visible».

4. **Área objetivo ≥ 24×24 px CSS** — el pill hereda `px-2.5 py-1.5` + icono 14 + bordes (≈28 px de alto), pero se hacen **explícitos y testificables** con `min-h-[24px] min-w-[24px]` en el enlace, para que el contrato de fuente pueda afirmar la restricción WCAG 2.2 AA (2.5.8) sin medir el render.

**Alternatives considered**: nombre accesible solo por `aria-label` sobre icono (descartada: el texto visible desaparece en móvil y el nombre del árbol diverge del visible); estado por color/borde del pill (descartada: el spec prohíbe comunicar estado solo por color o icono); `hidden sm:inline` como el resto de la cabecera (descartada: rompe el nombre accesible en móvil, ver arriba).

**Contrato de fuente asociado** (`AccountAreaContractTests`): `MainLayout.razor` contiene `href="/cuenta"`, `href="/login"`, `Entrar`, `sr-only sm:not-sr-only`, `focus-visible:ring-2`, `min-h-[24px]` y el patrón `HasSession`; `AccountSectionNav.razor` contiene `aria-current=` y `?page?`/`"page"`.

---

### Decision D4 — Arquitectura de implementación

#### D4.1 Estructura de ficheros

| Fichero | Acción | Descripción |
|---|---|---|
| `src/Ludeka.Web/Components/Pages/Account.razor` | **Crear** | Hub raíz: `@page "/cuenta"` + `@attribute [Authorize]` (patrón plano de `AccountConnections.razor:1-2`, sin política). Inventario de 4 tarjetas-enlace: Ludoteca → `/cuenta/ludoteca`, Conexiones → `/cuenta/conexiones`, Tema → `/cuenta/ludoteca?seccion=apariencia`, País → `/cuenta/ludoteca?seccion=apariencia`. |
| `src/Ludeka.Web/Components/Shared/AccountSectionNav.razor` | **Crear** | Cabecera de sección compartida: enlace de vuelta al hub (`/cuenta`) + enlaces Ludoteca/Conexiones con `aria-current` (D3.1). |
| `src/Ludeka.Web/Components/Layout/MainLayout.razor` | **Modificar** | (a) Botón de persona en la zona de utilidades (junto a las líneas 77-97), con `HasSession` en `@code` (patrón `MyLibrary.razor:963`); (b) reescritura de la píldora línea 94: `href="/mi-ludoteca"` → `href="/cuenta/ludoteca"`. |
| `src/Ludeka.Web/Components/Pages/MyLibrary.razor` | **Modificar** | (a) Segunda ruta `@page "/cuenta/ludoteca"` conservando `@page "/mi-ludoteca"` (línea 1); (b) `[SupplyParameterFromQuery(Name = "seccion")]` + mapeo `apariencia` → `TabType.Appearance` en `OnInitialized`; (c) montar `<AccountSectionNav Active="ludoteca" />`. **Sin `@attribute [Authorize]`**: añadirlo cambiaría el comportamiento anónimo actual de `/mi-ludoteca` (banner de invitación, líneas 61-77) y rompería el criterio «alias con idéntico comportamiento». |
| `src/Ludeka.Web/Components/Pages/AccountConnections.razor` | **Modificar** | **Solo markup de navegación**: montar `<AccountSectionNav Active="conexiones" />` dentro del contenedor (antes del `<header>` existente). Cero cambios en `@code`, servicios o flujos de INC-49. |
| `src/Ludeka.Web/Components/Shared/RedirectToLogin.razor` | **Modificar** | Línea 15 → `Navigation.ToLogin();` (D2) + comentario de propósito actualizado. |
| `src/Ludeka.Web/Components/Pages/GameDetail.razor` (línea 45), `Radar.razor` (línea 41) | **Modificar** | `href="/mi-ludoteca"` → `href="/cuenta/ludoteca"`. |
| `src/Ludeka.Web/Components/Pages/PublicProfile.razor` (32, 82) | **No modificar** | Fuera de alcance §5; el alias mantiene vivos sus enlaces. |
| `src/Ludeka.Web/wwwroot/manifest.webmanifest` (5), `wwwroot/offline.html` (128) | **No modificar** | Conservan `/mi-ludoteca`; criterio 6 garantizado por el alias. |
| `src/Ludeka.Application/Contracts/ICurrentUserService.cs` | **No modificar** | Contrato congelado (`CurrentUserContractTests.cs:41-59`, 21 implementaciones). |
| `tests/Ludeka.UnitTests/Web/AccountAreaContractTests.cs` | **Crear** | Contratos de la puerta, el hub, el alias, las reescrituras y la accesibilidad (ver D4.4). Helpers `ReadSource`/`GetRepoRoot` **propios** — precedente: el repo duplica estos helpers en 8+ ficheros de test (misma decisión documentada en `WebStartupGuardsTests.cs:171`). |
| `tests/Ludeka.UnitTests/Web/AuthorizationPipelineContractTests.cs` | **Modificar** | (a) Actualizar el marcador `LoginPath` → `ToLogin(` en `Routes_ShouldUseAuthorizeRouteViewWithRedirectToLogin`; (b) nuevo contrato `ReturnUrl` (presencia de `ToLogin`, ausencia de la navegación vieja). |

#### D4.2 Patrón de componente e inyección

- **`Account.razor`**: razor puro — sin `@code` (salvo nada; `PageTitle` es markup). Sin inyecciones: el inventario son enlaces estáticos y `[Authorize]` lo resuelve el pipeline. SSR-friendly de fábrica.
- **`AccountSectionNav.razor`**: componente **presentacional** con un único parámetro de entrada:

  ```razor
  @code {
      /// <summary>Sección activa: "ludoteca" | "conexiones". Cadena de conjunto cerrado,
      /// no texto libre; el componente solo la compara, nunca la pinta como contenido.</summary>
      [Parameter] public string Active { get; set; } = string.Empty;
  }
  ```

  Sin `ICurrentUserService`, sin `NavigationManager`: los enlaces son `<a href>` normales que resuelve el router. El padre (página) le pasa la sección — patrón container-presentational, testeable por contrato de fuente (quién monta con qué `Active`).
- **Puerta en `MainLayout.razor`**: markup condicional + propiedad computada en el `@code` existente, consumiendo la **inyección ya presente** `@inject ICurrentUserService CurrentUserService` (línea 6):

  ```csharp
  private bool HasSession => !string.IsNullOrWhiteSpace(CurrentUserService.UserId);
  ```

  Patrón literal de `MyLibrary.razor:963`. **Sin tocar `ICurrentUserService`**: `UserId`/`UserName` bastan (superficie congelada). La cabecera ya consulta `CurrentUserService` en markup SSR (roles en línea 100), así que el patrón está probado en este mismo fichero.
- **`MyLibrary.razor`**: solo se le añaden atributos de ruta/query y el montaje del nav; su `@code` de 390 líneas no se reestructura.

#### D4.3 Orden de implementación seguro (TDD)

Se confirma el candidato de la propuesta, con la razón de cada paso:

1. **RED global** — escribir `AccountAreaContractTests.cs` completo + actualizar/añadir los contratos de `AuthorizationPipelineContractTests.cs`. La suite debe **fallar de forma observable** (p. ej. `No se encontró el archivo fuente: src/Ludeka.Web/Components/Pages/Account.razor`, marcadores ausentes en `MainLayout`/`MyLibrary`/`RedirectToLogin`). Capturar el RED.
2. **Alias + hub navegable** — segunda `@page` en `MyLibrary`, crear `Account.razor`, `AccountSectionNav`, montaje en `MyLibrary` y `AccountConnections`, parámetro `seccion`. Motivo primero: el destino `/cuenta` y `/cuenta/ludoteca` debe existir **antes** de que la puerta y las reescrituras apunten a él (ningún enlace nuevo queda colgando ni se rompe `/mi-ludoteca` en ningún instante).
3. **Puerta en cabecera** — botón de persona + `HasSession` + reescritura de la píldora en `MainLayout`. Transversal: se hace con los contratos ya escritos fallando sobre este fichero y la suite de regresión disponible.
4. **`ReturnUrl`** — `RedirectToLogin` → `Navigation.ToLogin()` (el marcador del test existente se actualizó en el paso 1: hasta aquí ese test está en RED).
5. **Reescrituras selectivas** — `GameDetail.razor:45`, `Radar.razor:41` (los «no tocar» — `PublicProfile`, manifest, offline — se afirman por contrato con `Contains("/mi-ludoteca")`, que ya pasa y actúa de pin de regresión).
6. **GREEN + refactor** — `dotnet test Ludeka.sln` completo: objetivo **≥ 1.622 unitarias + 10 de integración** (nunca por debajo de la línea base). **Detener antes el servidor** si quedó arrancado (`MSB3027` bloquea `bin/Debug`, nota INC-50 §8). El smoke SSR de D2 se ejecuta en apply/verify con la app levantada según §8 (HTTPS 7291/5081, sin `.claude/launch.json`).

#### D4.4 Dónde viven los tests y qué marcadores afirman (sin atar a líneas)

**`tests/Ludeka.UnitTests/Web/AccountAreaContractTests.cs` (nuevo)** — estilo `AccountConnectionsPageContractTests` (lectura de texto, sin bUnit, que el repo no tiene):

| Test (nombre orientativo) | Fichero leído | Marcadores (Contains/DoesNotContain) |
|---|---|---|
| `Hub_ShouldDeclareItsRouteAndPlainAuthorize` | `Pages/Account.razor` | `@page "/cuenta"`, `@attribute [Authorize]`, `DoesNotContain("[Authorize(Policy")` |
| `Hub_ShouldInventoryTheFourAccountSections` | `Pages/Account.razor` | `href="/cuenta/ludoteca"`, `href="/cuenta/conexiones"`, `href="/cuenta/ludoteca?seccion=apariencia"` (Tema y País), textos `Ludoteca`/`Conexiones`/`Tema`/`País` |
| `HeaderGate_ShouldDistinguishSessionStatesAndLinkLogin` | `Layout/MainLayout.razor` | `href="/cuenta"`, `href="/login"`, `Entrar`, `UserName` (`@CurrentUserService.UserName`), `HasSession`, `!string.IsNullOrWhiteSpace(CurrentUserService.UserId)` |
| `HeaderGate_ShouldMeetWcagFocusAndTargetSizeMarkers` | `Layout/MainLayout.razor` | `sr-only sm:not-sr-only`, `focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)]`, `min-h-[24px]`, `min-w-[24px]` |
| `SectionNav_ShouldMarkTheActiveSectionWithAriaCurrent` | `Shared/AccountSectionNav.razor` | `aria-current=`, `"page"`, `href="/cuenta"` (vuelta al hub), `Active` |
| `MyLibrary_ShouldDeclareCanonicalRouteAndAlias` | `Pages/MyLibrary.razor` | `@page "/cuenta/ludoteca"`, `@page "/mi-ludoteca"` |
| `MyLibrary_ShouldAcceptTheAppearanceDeepLink` | `Pages/MyLibrary.razor` | `SupplyParameterFromQuery(Name = "seccion")`, `TabType.Appearance` |
| `ChildPages_ShouldMountTheSharedSectionHeader` | `MyLibrary.razor` + `AccountConnections.razor` | `AccountSectionNav Active="ludoteca"` / `Active="conexiones"` |
| `RewrittenLinks_ShouldTargetTheAccountArea` | `GameDetail.razor`, `Radar.razor` | `href="/cuenta/ludoteca"` |
| `OutOfScopeFiles_ShouldKeepTheAliveAlias` | `PublicProfile.razor`, `manifest.webmanifest`, `offline.html` | `Contains("/mi-ludoteca")` en cada uno |
| `AccountConnections_ShouldKeepInc49LogicUntouched` | `Pages/AccountConnections.razor` | mantiene los marcadores INC-49 ya testificados (`Connections.InvalidateCache()`, `method="post"`…) — los tests de `AccountConnectionsPageContractTests` **existente** siguen en verde sin editarse |

**`AuthorizationPipelineContractTests.cs` (existente, modificado)**:

| Test | Marcador |
|---|---|
| `Routes_ShouldUseAuthorizeRouteViewWithRedirectToLogin` (**marcador actualizado en RED**) | `Contains("ToLogin(")` en `RedirectToLogin.razor` en lugar de `Contains("ExternalAuthenticationSchemes.LoginPath")`; conserva `RendererInfo.IsInteractive`. |
| `RedirectToLogin_ShouldPreserveReturnUrlWithLoginRedirectSemantics` (**nuevo**) | `Contains("ToLogin(")`, `DoesNotContain("NavigateTo(ExternalAuthenticationSchemes.LoginPath")` — afirma la presencia/ausencia esperada del parámetro vía su único productor. |
| `AliasRoute_ShouldLiveInAPage…` (teoría existente) | Extender su `MemberData`/`InlineData` con `{ "MyLibrary.razor", "/mi-ludoteca" }` para que el alias quede cubierto por el precedente ya en verde (o cubrirlo en `AccountAreaContractTests` si se prefiere no mezclar tablas — decisión de apply, un sitio u otro, no ambos). |

Ningún marcador usa números de línea. Los tests «no tocar» de archivos fuera de alcance son `Contains` que ya pasan hoy y actúan como pin hacia adelante.

## Data Flow

```
                        ┌───────────────────────────────┐
                        │  MainLayout.razor (SSR+circuito)│
                        │  HasSession = UserId != vacío   │
                        └──────┬────────────────┬─────────┘
                   con sesión   │                │  sin sesión
                   href=/cuenta │                │ href=/login  (sin ReturnUrl:
                        │       │                │  no hay destino privado)
                        ▼       │                ▼
              ┌──────────────┐  │      ┌──────────────────────┐
              │ Account.razor│  │      │ Login.razor          │
              │ /cuenta      │  │      │ (INC-46 — no se toca)│
              │ [Authorize]  │  │      └──────────────────────┘
              └──┬────┬───┬──┘  │
     Ludoteca    │    │   │ Tema/País (?seccion=apariencia)
                 │    │   └────────────────────────────┐
   Conexiones    │    ▼                                ▼
                 │  ┌─────────────────────┐   ┌──────────────────────────┐
                 │  │ MyLibrary.razor     │   │ MyLibrary (misma vista)  │
                 │  │ /cuenta/ludoteca    │   │ pestaña Appearance activa│
                 │  │ + alias /mi-ludoteca│◄──│ (grid temas + tarjeta país)│
                 │  └─────────────────────┘   └──────────────────────────┘
                 ▼
   ┌───────────────────────────┐
   │ AccountConnections.razor  │  (lógica INC-49 intacta;
   │ /cuenta/conexiones        │   solo +AccountSectionNav)
   └───────────────────────────┘

  Anónimo en /cuenta (interactivo):
  AuthorizeRouteView ──NotAuthorized──► RedirectToLogin
                                            │ RendererInfo.IsInteractive
                                            ▼
                                     Navigation.ToLogin()  ◄── LoginRedirect.cs:26-38
                                            │                 (EscapeDataString +
                                            │                  guard ResolveReturnUrl)
                                            ▼
                              /login?ReturnUrl=%2Fcuenta
```

## Interfaces / Contracts

```csharp
// Sin cambios: superficie congelada — src/Ludeka.Application/Contracts/ICurrentUserService.cs
// (CurrentUserContractTests.cs:41-59 la fija con Assert.Equal exacto; 21 implementaciones)
public interface ICurrentUserService
{
    string UserId { get; }     // vacío ⇒ anónimo (patrón ya usado en MyLibrary/GameDetail)
    string UserName { get; }   // identidad visible de la puerta
    // … resto sin tocar
}
```

```razor
@* Nuevo componente presentacional — Shared/AccountSectionNav.razor *@
[Parameter] public string Active { get; set; } = string.Empty;  // "ludoteca" | "conexiones"
```

```csharp
// Nuevo en MyLibrary.razor (query de código cerrado, patrón Login.razor:65)
[SupplyParameterFromQuery(Name = "seccion")]
public string? Seccion { get; set; }   // "apariencia" ⇒ TabType.Appearance; otro/ausente ⇒ comportamiento actual
```

```csharp
// Contrato de URL reutilizado (ya existe, NO se modifica) — LoginRedirect.cs
public static void ToLogin(this NavigationManager navigation);
// → $"{LoginPath}?ReturnUrl={Uri.EscapeDataString(PathAndQuery)}"  con guard en ResolveReturnUrl
```

## Testing Strategy (strict_tdd: true)

| Layer | Qué probar | Enfoque |
|---|---|---|
| **Unit — contrato de fuente (RED→GREEN)** | Puerta (presencia, distinción de sesión, `href="/login"`, marcadores WCAG), hub (`/cuenta` + inventario), alias vivo, `seccion` deep-link, montajes de `AccountSectionNav` con `aria-current`, reescrituras, archivos fuera de alcance intactos, `ReturnUrl` en `RedirectToLogin` | `AccountAreaContractTests` (nuevo) + extensión de `AuthorizationPipelineContractTests`; `ReadSource` propio; marcadores sin posiciones de línea. **RED observado** = suite falla antes de crear/editar producción; **GREEN** = `dotnet test Ludeka.sln` en verde. |
| **Unit — regresión existente** | INC-49 (`AccountConnectionsPageContractTests`), INC-46 (pipeline, `SessionDenialUiContractTests`, `PolicyAuthorizationTests`), `CurrentUserContractTests`, `WebMarkupContractTests` (88 casos) | Sin editarlos salvo el único marcador `LoginPath`→`ToLogin` declarado en D2. Si alguno rojo al final ⇒ la tarea no está completa. |
| **Integración** | PostgreSQL vía Testcontainers (`Ludeka.IntegrationTests`) | Sin cambios previstos; debe seguir en 10/10. |
| **E2E / manual (apply/verify)** | Smoke SSR del challenge (`/cuenta` anónima → `/login?ReturnUrl=%2Fcuenta`), tablas de aceptación (puerta con/sin sesión, inventario, alias), herencia del smoke pendiente de INC-49 §8 | Documentado en D2 y en INC-50 §8 (arranque HTTPS 7291/5081, arranque lento por `PriceRadarHostedService`, parar servidor antes de `dotnet test`). |

**Comando exacto:** `dotnet test Ludeka.sln` · **Línea base observada a respetar:** **1.622 unitarias + 10 de integración** (criterio 8 corregido; el 1.417 del documento de incremento está desactualizado — corregirlo en apply).

## Migration / Rollout

**No migration required.** Sin datos persistidos nuevos, sin esquema, sin feature flags. El cambio es aditivo en rutas: `/mi-ludoteca` sigue viva (alias), manifest/offline intactos. Rollback: `git revert` del PR único de `inc/area-de-cuenta` restaura cabecera, rutas, `RedirectToLogin` y enlaces. Despliegue: ordinario, sin fases.

## Size Forecast (Review Workload Guard — `single-pr`)

```
Decision needed before apply: Yes
Chained PRs recommended: No
400-line budget risk: High
```

Pronóstico **actualizado** respecto a los 450-650 de la propuesta (la propuesta era conservadora: D2 reutiliza `ToLogin` en vez de reconstruir la lógica, y la integración de `AccountConnections` es un solo montaje):

| Fichero | Adiciones+deletions estimados |
|---|---|
| `Account.razor` (nuevo) | 90-140 |
| `AccountSectionNav.razor` (nuevo) | 40-65 |
| `AccountAreaContractTests.cs` (nuevo) | 110-170 |
| `MainLayout.razor` (puerta + píldora + `HasSession`) | 40-70 |
| `MyLibrary.razor` (doble `@page` + `seccion` + montaje nav) | 15-30 |
| `AuthorizationPipelineContractTests.cs` (marcador + contrato ReturnUrl) | 15-35 |
| `AccountConnections.razor` (montaje nav) | 3-10 |
| `RedirectToLogin.razor` (`ToLogin` + comentario) | 5-12 |
| `GameDetail.razor` + `Radar.razor` (reescrituras) | 2-4 |
| **Total autoría previsto** | **~320-530** |

- Sigue **por encima de las 400 líneas** del guard en el escenario central → `400-line budget risk: High`, `Decision needed before apply: Yes`.
- **`size:exception` pendiente de aprobación del maintainer — NO aprobado por esta fase.** Si se rechaza: cadena `header-account-gate` → `account-area-hub` (para eso `Chained PRs recommended: No` solo mientras la excepción se plantea como vía prevista; tasks debe recalcular sobre su desglose real).
- tasks agrupa en unidades de trabajo < ~400 líneas autoría cada una (heurística de planificación, no tope).

## Open Questions

- [ ] **Consumo extremo a extremo del `ReturnUrl`** (hallazgo D2): `Login.razor` no lo lee y `Program.cs:333` fija `RedirectUri = "/"`. ¿El maintainer quiere cerrarlo en INC-50 (tocaría `Login.razor` + `/login/external` con validación anti open-redirect — modifica el mecanismo INC-46 hoy *Out of Scope*) o abrir incremento de seguimiento? **Recomendación:** incremento de seguimiento; INC-50 cumple su spec (preserve + sin bucles + contrato) sin tocar INC-46.
- [ ] **Resultado real del smoke SSR** (D2): si el middleware no emite challenge con `ReturnUrl` en SSR estático, ¿se acepta el hallazgo documentado o se abre trabajo de metadata de endpoint fuera de INC-50? El diseño no presupone el resultado.
- [ ] **`size:exception`** sobre el presupuesto de 400 líneas — decisión del maintenedor, no de esta fase (tasks lo transporta como `Decision needed before apply: Yes`).
