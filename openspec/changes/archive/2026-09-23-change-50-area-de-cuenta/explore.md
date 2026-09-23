# Exploración — change-50-area-de-cuenta (INC-50: Área de Cuenta)

> **Fase:** sdd-explore · **Rama:** `inc/area-de-cuenta` · **HEAD:** `b8b35fd` · **Fecha:** 2026-09-23
> **Documento de partida:** `docs/increments/inc-50-area-de-cuenta.md`
> **Idioma del artefacto:** español castellano (regla suprema de AGENTS.md del repositorio).

## Exploration: Área de Cuenta — puerta de cabecera y hub del usuario

### Current State (estado actual)

#### 1. Inventario de `/mi-ludoteca`

Ruta declarada en `src/Ludeka.Web/Components/Pages/MyLibrary.razor:1` (`@page "/mi-ludoteca"`). Referencias actuales (grep completo en `src/Ludeka.Web`):

| Quién la enlaza | Evidencia |
|---|---|
| Píldora de cabecera «Mi Ludoteca» | `src/Ludeka.Web/Components/Layout/MainLayout.razor:94` |
| Ficha de juego (acceso secundario) | `src/Ludeka.Web/Components/Pages/GameDetail.razor:45` |
| Perfil público (2 enlaces) | `src/Ludeka.Web/Components/Pages/PublicProfile.razor:32` y `:82` |
| Radar/sorteos | `src/Ludeka.Web/Components/Pages/Radar.razor:41` |
| Página offline (PWA) | `src/Ludeka.Web/wwwroot/offline.html:128` |
| **`start_url` del manifest PWA** | `src/Ludeka.Web/wwwroot/manifest.webmanifest:5` — punto crítico: reescribir la ruta sin alias rompería el arranque de la PWA instalada |

Son **7 referencias en 6 ficheros** más la propia página. Cualquier reescritura debe cubrir todas ellas o conservar la ruta viva.

**Páginas bajo `/cuenta` hoy:** solo existe una — `src/Ludeka.Web/Components/Pages/AccountConnections.razor:1` (`@page "/cuenta/conexiones"`) con `@attribute [Authorize]` en la línea 2 (patrón INC-49). No hay página `/cuenta` raíz, ni hub, ni secciones hijas. Las rutas cerradas de INC-49 viven en `src/Ludeka.Web/Authentication/AccountConnectionRoutes.cs:13` (`Page = "/cuenta/conexiones"`).

#### 2. Cabecera actual (`MainLayout.razor`)

- **Bloques condicionales por rol/permiso, no por sesión:** el menú de gestión se abre con `CurrentUserService.IsFoundingTeam || CurrentUserService.IsInRole("Moderator")` (línea 100) y sus ítems con `HasPermission(ModeratorPermission.*)` (líneas 128, 138, 148, 177, 197-198).
- **No consulta sesión en el markup:** grep de `HasSession|IsAuthenticated` en `MainLayout.razor` → **0 coincidencias en el marcado**; `CurrentUserService.UserId` solo aparece en `@code` (líneas 353, 356, 366, 375, 406, 407) para cargar tema/país. El patrón `HasSession => !string.IsNullOrWhiteSpace(CurrentUserService.UserId)` ya existe y está probado en `MyLibrary.razor:963`, `GameDetail.razor:524` y `GameReportModal.razor:184`.
- **Dónde insertar el botón de persona:** zona de utilidades `MainLayout.razor:54` (`<div class="flex items-center gap-2 sm:gap-2.5 shrink-0">`), junto a la píldora `/mi-ludoteca` actual (líneas 93-97) y al selector de país (líneas 77-91). La píldora actual es idéntica para todo el mundo (icono `library` + texto «Mi Ludoteca», sin distinción de sesión).
- **Hallazgo adicional:** `SwitchTheme` (`MainLayout.razor:401-417`) está definido pero **ningún control del markup lo invoca** (grep: 1 única coincidencia, la definición). El cambio de tema real vive en `MyLibrary.razor` (pestaña Apariencia).
- El aviso `AccountEmailNotice` se monta en la cabecera en `MainLayout.razor:230`.

#### 3. Estado de sesión expuesto (superficie congelada)

- **`ICurrentUserService`** (`src/Ludeka.Application/Contracts/ICurrentUserService.cs:11-34`): propiedades `UserId` (línea 14), `UserName` (línea 17), `Roles` (línea 20), `IsFoundingTeam` (línea 23); métodos `IsInRole` (línea 26), `HasPermission` (línea 33). **Sin sesión: `UserId` y `UserName` vacíos** (documentado en las líneas 7-9).
- **Congelado por `tests/Ludeka.UnitTests/Application/CurrentUserContractTests.cs`:**
  - Línea 49: `Assert.Equal(["IsFoundingTeam", "Roles", "UserId", "UserName"], properties);`
  - Línea 58: `Assert.Equal(["HasPermission", "IsInRole"], methods);`
  - Líneas 24-25 y 36-37: prohíbe switchers e implementación por defecto de `HasPermission`.
- **Implementaciones declaradas (grep `class X : ICurrentUserService`): 21 clases** — 2 de producción (`AuthenticatedCurrentUserService` en `src/Ludeka.Web/Services/AuthenticatedCurrentUserService.cs:20`, `SystemCurrentUserService` en `src/Ludeka.Jobs/SystemCurrentUserService.cs:28`) + 19 dobles en tests. (El incremento decía «16»; el recuento actual en HEAD es 21 — mismo orden de magnitud, el argumento de riesgo se mantiene.)
- **`SessionIdentity`** (`src/Ludeka.Application Contracts/SessionIdentity.cs:12-41`): estático, frontera de anonimia — `Require(...)` lanza `UnauthorizedAccessException` si `UserId` vacío (líneas 32-40). Es el mecanismo que ya dispara `Navigation.TryRedirectToLogin(ex)` en toda la UI.
- **No existe ninguna interfaz `ICurrentUser`** en el repo (grep sin resultados); el nombre cercano es `ICurrentUserService` + el record privado `SessionIdentity` de `AuthenticatedCurrentUserService.cs:104-118`.
- **Implicación para la decisión 4:** `UserId` + `UserName` bastan para distinguir anónimo (`string.Empty`) de sesión activa y mostrar nombre reconocible **sin tocar el contrato congelado**. No hay URL de avatar en la superficie; `UserName` llega del claim `ClaimTypes.Name` (`AuthenticatedCurrentUserService.cs:130`) o del `AppUser` en circuito (líneas 77-85).

#### 4. Puertas existentes

- **`AccountEmailNotice.razor:21`** — único `href="/cuenta/conexiones"` de toda la interfaz (grep: 1 coincidencia). Solo renderiza con sesión sin correo verificado (`OnInitializedAsync` líneas 38-46 + `HasVerifiedProviderEmailAsync` línea 74).
- **`Login.razor:14-15`** — copy de promesa: «Entra con tu cuenta social: sin registro, sin contraseña y sin correo de confirmación.» Ruta `@page "/login"` en línea 1. Sin botón de vuelta al destino salvo el parámetro `ReturnUrl`.
- **`AuthorizeRouteView` → `RedirectToLogin`:** `src/Ludeka.Web/Components/Routes.razor:3-6`. El componente `src/Ludeka.Web/Components/Shared/RedirectToLogin.razor:15` hace `Navigation.NavigateTo(ExternalAuthenticationSchemes.LoginPath, forceLoad: true)` **sin añadir `ReturnUrl`** → **en el camino interactivo NO se conserva la intención de destino**. Contraste: el helper `LoginRedirect.ToLogin` (`src/Ludeka.Web/Services/LoginRedirect.cs:36-37`) sí preserva la ruta actual como `ReturnUrl` (comentario de propósito en líneas 9-13, con guard de no encadenar en líneas 69-82). En SSR estático la protección real la da el middleware (comentario `RedirectToLogin.razor:3-8`), cuyo challenge de cookie sí puede incluir `ReturnUrl` por convención de ASP.NET Core (no verificado en runtime en esta fase — ver *Limitations*).
- **`href="/login"` en la interfaz:** grep en `src/Ludeka.Web` → **cero resultados**. Confirmado el agujero 2.2 del incremento.

#### 5. Inventario de ajustes de usuario dispersos (candidatos a hub)

| Ajuste | Contrato/servicio | Superficie | Vistas que lo usan hoy |
|---|---|---|---|
| **Tema visual** | `IUserPreferenceService` (`src/Ludeka.Application/Contracts/IUserPreferenceService.cs:16-21`) | `GetUserThemeAsync` / `SetUserThemeAsync`; entidad `UserPreference.PreferredTheme` (`src/Ludeka.Core/Entities/UserPreference.cs:12`), 5 temas válidos (`NormalizeTheme`, líneas 49-59: editorial, wood, tabletop, midnight, charcoal) | UI principal: `MyLibrary.razor` pestaña `TabType.Appearance` («Apariencia & País», líneas 193-197, grid de temas 237, guard 1197); carga en cabecera `MainLayout.razor:353` |
| **País / ámbito territorial** | `IUserPreferenceService.GetUserCountryAsync/SetUserCountryAsync` (`IUserPreferenceService.cs:31-36`) + `IUserLocationService` (`MainLayout.razor:370`, efectivo) | `UserPreference.Country` (`UserPreference.cs:13`) | Modal de cabecera `LocationSelectorModal.razor:178` (invocado desde `MainLayout.razor:77-91/302-305`); tarjeta en `MyLibrary.razor:400-442` |
| **`GetUserPreferenceAsync` (DTO completo)** | `IUserPreferenceService.cs:26` | `UserPreferenceDto` | **Sin uso en `Ludeka.Web`** (grep: 0 coincidencias) — superficie muerta lista para el hub |
| **Nota:** `InstagramModeration.razor:362-365` «Tema Visual de Marca» es tema editorial, **no** es ajuste de usuario; no entra en el hub. |

#### 6. Precedente de prueba de contrato de fuente (INC-49)

`tests/Ludeka.UnitTests/Web/AuthorizationPipelineContractTests.cs` — patrón `ReadSource(relativePath)` (líneas 178-183) + `Assert.Contains`/`DoesNotContain` sobre marcadores:
- `MainLayout_ShouldMountTheAccountEmailNotice` (líneas 139-147): afirma presencia de `<AccountEmailNotice />` en el layout, sin atar posición.
- `AccountEmailNotice_ShouldSubscribeToConnectionsInvalidatedAndDisposeCleanly` (líneas 161-176): disciplina `IDisposable` + `Invalidated +=/-=` + `RendererInfo.IsInteractive`.
- `Routes_ShouldUseAuthorizeRouteViewWithRedirectToLogin` (líneas 108-120): ya testifica el pipeline de redirección al acceso.
- `SessionGuard_…` (líneas 122-136) y `PublicProfile_ShouldNeverReferenceTheUnverifiedEmailNotice` (149-159) completan el patrón.

**Este es el patrón exacto a reutilizar para testificar la cabecera** (presencia del botón de persona, distinción con/sin sesión, enlace a `/login`, ausencia en `PublicProfile`).

#### 7. Lía de base de pruebas real (recuento razonado, sin ejecutar la suite)

Recuento estático sobre `tests/` en HEAD `b8b35fd`:

| Fuente | Recuento |
|---|---|
| `[Fact]` en `Ludeka.UnitTests` | 994 |
| `[InlineData]` (casos de theories) | 471 |
| `MemberData` `ProtectedPages` | 10 filas (`AuthorizationPipelineContractTests.cs:15-27`) |
| `MemberData` `AllPolicies` × 4 theories | 11 políticas × 4 = 44 (`PolicyAuthorizationTests.cs:26-39`, usos en 99/106/129/138) |
| `MemberData` `GuardedServices` | 15 filas (`AdministrativeWriteGuardContractTests.cs:25-42`) |
| `MemberData` `MarkupContracts` | 88 filas (`WebMarkupContractTests.cs:39+`) |
| Theories sin casos | 0 (los 120 `[Theory]` tienen InlineData o MemberData) |
| **Total unitarias** | 994 + 471 + 10 + 44 + 15 + 88 = **1.622** ✅ |
| `Ludeka.IntegrationTests` | 10 `[Fact]`, 0 theories → **10** ✅ |

Coincide exactamente con el dato de `docs/increments/ROADMAP.md:87` (INC-54: «1.622 pruebas unitarias + 10 de integración»). **El criterio 8 de INC-50 cita 1.417 — está desactualizado**: la línea base vigente al abrir este incremento es **1.622 unitarias + 10 de integración**.

### Affected Areas (áreas afectadas)

- `src/Ludeka.Web/Components/Layout/MainLayout.razor` — insertar botón de persona; el fichero transversal de mayor alcance (riesgo ya previsto en INC-50 §7).
- `src/Ludeka.Web/Components/Pages/MyLibrary.razor` — reubicación/alias de `/mi-ludoteca`; ya contiene pestaña de Apariencia y País candidata al hub.
- `src/Ludeka.Web/Components/Pages/AccountConnections.razor` — integración como sección del hub (solo enlace/rutas, sin tocar lógica).
- `src/Ludeka.Web/Components/Shared/RedirectToLogin.razor` — posible preservación de `ReturnUrl` (decisión 2).
- `src/Ludeka.Web/wwwroot/manifest.webmanifest`, `wwwroot/offline.html` — referencias de `/mi-ludoteca` fuera de Razor.
- `tests/Ludeka.UnitTests/Web/AuthorizationPipelineContractTests.cs` — extender el precedente de contrato de fuente a la cabecera.
- `src/Ludeka.Application.Contracts/ICurrentUserService.cs` — **NO se toca** (congelado).

### Las 5 decisiones de la sección 4 y la evidencia que las informa

#### Decisión 1 — Topología de la navegación
- A favor de **rutas hijas `/cuenta/*` con sección compartida:** ya existe el patrón de página con `[Authorize]` estrenado por INC-49 (`AccountConnections.razor:1-2`), rutas cerradas centralizadas (`AccountConnectionRoutes.cs`), y alias multi-`@page` es práctica usada en el repo (`Radar.razor:1-2`, `MediaModeration.razor:1-4`, `CreatorsDirectory.razor:1-2`, `GameReportsModeration.razor:1-2`) y testificada (`AuthorizationPipelineContractTests.cs:63-74`).
- A favor de **pestañas en una única página:** `MyLibrary.razor` ya opera con `TabType` + `aria-selected` (líneas 115-210, enum 930-942), pero esa pestaña es intra-página, no navegación entre áreas privadas.
- Restricción: el hub necesita `[Authorize]` y `Routes.razor:3-6` ya enruta denegaciones a `RedirectToLogin`.

#### Decisión 2 — Destino del anónimo
- `RedirectToLogin.razor:15` **pierde la intención de destino** (navega a `LoginPath` sin `ReturnUrl`); `LoginRedirect.cs:30-37` **la conserva** (`?ReturnUrl=` escapada). Hay dos mecanismos incoherentes hoy: el de servicios sí devuelve al usuario de donde iba, el de ruta no.
- `LoginRedirect.ResolveReturnUrl` (líneas 69-82) ya resuelve y auto-protege contra bucles en `/login`.
- Escribir `/cuenta` sin sesión hoy caería en `NotAuthorized` → `RedirectToLogin` → `/login` sin retorno garantizado en modo interactivo.

#### Decisión 3 — Compatibilidad de `/mi-ludoteca`
- **7 referencias en 6 ficheros** (tabla §1), incluido `manifest.webmanifest:5` (`start_url`) y `offline.html:128` — fuera del alcance de un grep Razor simple.
- Precedente de alias en la misma página: multi-`@page` testificado en `AuthorizationPipelineContractTests.cs:63-74`.
- Criterio 6 del incremento: ninguna URL viva puede romperse. La opción de esfuerzo más bajo y riesgo más bajo es conservar `/mi-ludoteca` como alias (doble `@page` o redirect) **y** actualizar las referencias internas al hub.

#### Decisión 4 — Identidad visible en la cabecera
- Sin tocar el contrato congelado: `UserId != string.Empty` distingue sesión (patrón ya usado: `AccountEmailNotice.razor:40`, `MyLibrary.razor:963`) y `UserName` (`ICurrentUserService.cs:17`) da nombre reconocible, con vacío cuando no hay sesión.
- **No existe hoy ninguna URL de avatar** en la superficie de sesión (`AuthenticatedCurrentUserService.cs` solo proyecta UserId/UserName/Role/Permissions). Elegir avatar del proveedor implica: claim nuevo o lectura de `AppUser`, almacenamiento/privacidad de la foto — alcance nuevo, como advierte el incremento §7.
- WCAG 2.2 AA (criterio 7): el estado con/sin sesión no puede comunicarse solo por color/icono — el botón debe incluir texto o nombre accesible (skill `accessibility`: iconos con `aria-label`, objetivo ≥24px, foco visible).

#### Decisión 5 — Inventario de ajustes de usuario existentes
- **Temas:** servicio completo + UI dispersa en `MyLibrary` (pestaña Apariencia) con carga de preferencia también en `MainLayout.OnAfterRenderAsync:352-367`.
- **País:** doble superficie — modal de cabecera (`LocationSelectorModal`) y tarjeta en `MyLibrary:400-442`, más el efectivo `IUserLocationService`.
- **`GetUserPreferenceAsync`/`UserPreferenceDto`:** sin consumidores en Web — oportunidad de consolidar en el hub.
- `SwitchTheme` muerto en `MainLayout:401` (candidato a eliminar o reutilizar al tocar la cabecera).

### Approaches (síntesis para la propuesta)

1. **Rutas hijas `/cuenta/*` + alias `/mi-ludoteca` + botón con `UserId/UserName`** — reutiliza todo el patrón INC-49 (`[Authorize]`, contrato de fuente, rutas cerradas); esfuerzo Medio; riesgo de cabecera contenido con test de contrato.
2. **Hub monolítico con pestañas tipo `MyLibrary`** — menos rutas, pero rompe el precedente de `AccountConnections` como página propia o exige reescritura grande de INC-49; esfuerzo Medio-Alto; más regresión sobre código testificado.
3. **Solo puerta en cabecera, hub después** — cierra de inmediato los criterios 1-4, deja criterio 5 a medias; esfuerzo Bajo; no cumple el enunciado del maintainer («dentro de mi cuenta tendré mi ludoteca»).

### Recommendation (recomendación)

**Enfoque 1:** rutas hijas bajo `/cuenta` con cabecera de sección compartida, `AccountConnections` integrada sin tocar su lógica, `/mi-ludoteca` conservada como alias que resuelve a la sección equivalente (y referencias internas reescritas al hub), botón de persona en `MainLayout.razor:54-97` basado en `UserId`/`UserName` (sin ampliar `ICurrentUserService`), y `ReturnUrl` preservado alineando `RedirectToLogin.razor` con el comportamiento ya probado de `LoginRedirect`. Todo se testifica con el patrón de contrato de fuente de INC-49.

### Risks (riesgos)

- **`MainLayout.razor` es transversal:** un error de render afecta a todas las páginas; mitigación: test de contrato de fuente + suite 1.622 en verde.
- **Contrato congelado de `ICurrentUserService` (21 implementaciones):** cualquier ampliación rompe `CurrentUserContractTests.cs:41-59`; mitigación: usar `UserId`/`UserName` existentes o crear contrato aparte (precedente INC-49).
- **Rota `/mi-ludoteca` viva con referencias externas** (manifest PWA, offline): romperla silencia el criterio 6; alias obligatorio salvo decisión explícita.
- **Incoherencia `ReturnUrl` entre `RedirectToLogin` y `LoginRedirect`:** corregir solo un camino deja destinos perdidos según cómo se llegue al acceso.
- **Avatar del proveedor = alcance de datos/privacidad:** si se elige, tratar como alcance propio.
- **Criterio 8 desactualizado (1.417):** la propuesta debe fijar la línea base real 1.622 + 10.

### Huecos abiertos (open gaps)

1. No se verificó en runtime si el challenge HTTP del middleware (SSR) incluye `ReturnUrl` al `/login`; solo se leyó el código. Limitación honesta de fase solo-lectura.
2. No se inspeccionó la entidad `AppUser`/claims de proveedor para confirmar si existe algún dato de avatar ya persistido en BD (no buscado en esta fase más allá de la superficie de sesión).
3. No se ejecutó `dotnet test`: el recuento 1.622+10 es estático razonado, no observado en ejecución.
4. Definir si el hub mueve física o solo vialógicamente la pestaña Apariencia de `MyLibrary` (depende de la decisión 5 en propuesta).
5. `PublicProfile.razor` enlaza `/mi-ludoteca` con lógica propia (líneas 32 y 82): comprobar en propuesta si debe apuntar al hub o conservar el alias.

### Ready for Proposal

**Sí.** Hay evidencia completa para las 5 decisiones, precedentes de implementación y de testificación, y línea base de pruebas verificada. El orquestador puede informar al usuario de que la exploración confirma el agujero descrito en INC-50 y aporta los datos para decidir; ninguna pregunta de producto queda bloqueante (la elección entre enfoques es de propuesta).

### Limitations (limitaciones de esta fase)

- Solo lectura: no se editó código, no se hizo commit, no se ejecutó la app ni la suite de tests.
- El recuento de pruebas es un conteo estático de atributos con desglose; coincide exactamente con el dato publicado en ROADMAP, pero no es un `dotnet test` observado.
- `MemberData` programático (`AllPolicies`) se resolvió por su fuente congelada (11 políticas), no por ejecución.
