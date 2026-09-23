# Proposal: Área de Cuenta — Puerta de Acceso en la Cabecera y Hub del Usuario

> **Cambio:** `change-50-area-de-cuenta` · **Incremento:** INC-50 · **Rama:** `inc/area-de-cuenta` (HEAD `b8b35fd`)
> **Modo de artefactos:** hybrid · **Idioma:** español castellano (regla suprema de AGENTS.md) · **strict_tdd:** true
> **Fuentes:** `openspec/changes/change-50-area-de-cuenta/explore.md`, `docs/increments/inc-50-area-de-cuenta.md`, `openspec/config.yaml`

## Intent

INC-46 entregó el mecanismo de acceso y INC-49 la pantalla de conexiones, pero ninguno construyó la puerta: con sesión iniciada no existe ningún punto de la interfaz desde el que llegar a `/cuenta/conexiones` (su único enlace vive en `AccountEmailNotice.razor:21`, que solo se renderiza sin correo verificado) ni a ninguna otra vista de cuenta, y en todo `src/Ludeka.Web` hay **cero** enlaces a `/login`. `MainLayout.razor` no consulta sesión en el markup (0 coincidencias de `HasSession|IsAuthenticated`): solo distingue roles de moderación y muestra la píldora `/mi-ludoteca` idéntica a todo el mundo. Esto deja sin respuesta el pedido textual del maintainer: un botón de persona que lleve a la cuenta con sesión y al login sin ella, y un área de cuenta que agrupe ludoteca y demás ajustes de usuario. Además, el camino interactivo de redirección (`RedirectToLogin.razor:15`) pierde la intención de destino mientras que el de servicios (`LoginRedirect.cs:36`) la conserva: hay una incoherencia real que este incremento debe cerrar.

## Scope

### In Scope

- **Puerta de cabecera:** botón de persona en `MainLayout.razor` (zona de utilidades, líneas 54-97): con sesión → `/cuenta`; sin sesión → `/login`. Identidad reconocible con `UserName` (sin avatar). Accesible por teclado, foco visible, estado con/sin sesión comunicado con texto o nombre accesible (WCAG 2.2 AA).
- **Hub `/cuenta`:** página raíz privada (`[Authorize]`) con inventario de secciones del usuario: Ludoteca, Conexiones y Ajustes (tema visual y país), según la decisión 5.
- **Rutas hijas `/cuenta/*`:** `/cuenta/ludoteca` (segunda ruta de `MyLibrary.razor` conservando `/mi-ludoteca`) y `/cuenta/conexiones` (ya entregada por INC-49: solo se integra en la navegación del hub, **sin tocar su lógica**). Cabecera de sección compartida entre rutas hijas.
- **Alias `/mi-ludoteca`:** obligatorio y permanente (criterio 6); se reescriben al hub las referencias internas Razor decididas en la decisión 3.
- **`ReturnUrl` alineado:** `RedirectToLogin.razor` preserva la intención de destino con la misma semántica (escapada + guard anti-bucle) que `LoginRedirect.ToLogin`/`ResolveReturnUrl`.
- **Tests de contrato de fuente** al estilo de `AuthorizationPipelineContractTests.cs:139-176` para: presencia del botón de persona, distinción con/sin sesión, enlace a `/login`, ruta alias viva, montaje del hub y preservación de `ReturnUrl`.
- **Corrección del criterio 8:** línea base real **1.622 unitarias + 10 de integración** (no 1.417).

### Out of Scope

- **Perfil público** (`PublicProfile.razor`): sus enlaces a `/mi-ludoteca` (líneas 32 y 82) **no se tocan**; siguen resolviendo por el alias. No se mezcla el área privada con lo que ven los demás (§5 del incremento).
- **Herramientas de moderación y administración:** ya tienen su sitio en la cabecera, gobernado por permisos (§5).
- **Mecanismos de INC-46 y lógica de conexiones de INC-49:** se construye la puerta, no se toca lo que hay detrás (§5). `ICurrentUserService` **no se toca** (contrato congelado).
- **Avatar del proveedor:** alcance nuevo de datos/privacidad — fuera de INC-50 (decisión 4).
- **Página física `/cuenta/ajustes` con UI mudada desde MyLibrary, consolidación de `GetUserPreferenceAsync`/`UserPreferenceDto` y limpieza de `SwitchTheme` muerto:** dejado para INC-61+ (decisión 5).
- **Modificación de `manifest.webmanifest` y `offline.html`:** conservan `/mi-ludoteca`, garantizada por el alias (decisión 3).

## Capabilities

### New Capabilities

- `header-account-gate`: punto de entrada de cuenta en la cabecera global — comportamiento con/sin sesión, identidad visible (`UserName`, sin avatar), enlace a `/login`, y garantías WCAG 2.2 AA del control.
- `account-area-hub`: área privada `/cuenta` — hub raíz con inventario de secciones, rutas hijas `/cuenta/*` con cabecera de sección compartida, integración de ludoteca y conexiones, y alias permanente de `/mi-ludoteca`.

### Modified Capabilities

- `policy-based-authorization`: el requisito de pipeline (`AuthorizeRouteView` con `NotAuthorized`/`RedirectToLogin`) pasa a exigir que la redirección interactiva al inicio de sesión **preserve `ReturnUrl`**, alineada con `LoginRedirect` (hoy `RedirectToLogin.razor:15` la pierde).
- `user-library-view`: la ludoteca gana ruta canónica dentro del área (`/cuenta/ludoteca`) como sección del hub; `/mi-ludoteca` se conserva viva como alias con el mismo comportamiento. El resto de requisitos de la pestañas/contadores/préstamos permanecen intactos.

## Approach

Se adopta el **Enfoque 1 de la exploración**, anclado en el patrón ya estrenado y testificado por INC-49:

1. **Rutas hijas `/cuenta/*` con `[Authorize]`** por página (precedente: `AccountConnections.razor:1-2`), reutilizando `AuthorizeRouteView → RedirectToLogin` de `Routes.razor:3-6` y las rutas centralizadas de INC-49 (`AccountConnectionRoutes.cs:13`) como referencia de estilo. Se descarta el hub monolítico con pestañas: reescribiría código ya testificado de `AccountConnections` y rompería el precedente de página propia sin ganancia proporcional.
2. **Puerta en `MainLayout.razor`** en la zona de utilidades existente (líneas 54-97), condicionada por el patrón ya probado `HasSession => !string.IsNullOrWhiteSpace(CurrentUserService.UserId)` (`MyLibrary.razor:963`, `GameDetail.razor:524`, `GameReportModal.184`), con `UserName` para la identidad visible. **Sin ampliar `ICurrentUserService`** (superficie congelada por `CurrentUserContractTests.cs:41-59`, 21 implementaciones).
3. **`/mi-ludoteca` como alias multi-`@page`** en `MyLibrary.razor` (práctica usada en `Radar.razor`, `MediaModeration.razor`, `CreatorsDirectory.razor`, testificada en `AuthorizationPipelineContractTests.cs:63-74`), reescribiendo al hub solo las referencias Razor internas decididas.
4. **`ReturnUrl` alineado** en `RedirectToLogin.razor`: mism escapada y guard que `LoginRedirect.ToLogin` (`LoginRedirect.cs:30-37`) y `ResolveReturnUrl` (`LoginRedirect.cs:69-82`), de modo que ambos caminos (ruta y servicios) devuelvan al usuario de donde iba.
5. **Tests de contrato de fuente** extienden el precedente `ReadSource(relativePath)` (`AuthorizationPipelineContractTests.cs:178-183`) a la cabecera, el hub, el alias y la redirección.

## Decisiones (§4 del incremento)

### Decisión 1 — Topología de la navegación

**Elección: rutas hijas `/cuenta/*` con cabecera de sección compartida** (Enfoque 1).

Justificación: es el único enfoque que reutiliza íntegramente el patrón de página `[Authorize]` estrenado por INC-49, el alias multi-`@page` ya practicado y testificado en el repo, y las rutas cerradas centralizadas (`AccountConnectionRoutes.cs`). El hub monolítico con pestañas exige reescribir `AccountConnections` (código con tests de contrato) o duplicar su lógica, con esfuerzo Medio-Alto y más regresión sobre código testificado. El enfoque «solo puerta, hub después» no cumple el enunciado del maintainer («dentro de mi cuenta tendré mi ludoteca»). La cabecera de sección compartida da cohesión sin sacrificar la URL por sección.

### Decisión 2 — Destino del anónimo

**Elección: conservar la intención de destino, alineando `RedirectToLogin.razor:15` con `LoginRedirect.cs:36`.**

Justificación: hoy hay dos mecanismos incoherentes — `LoginRedirect.ToLogin` sí preserva `?ReturnUrl=` escapada (con guard anti-bucle en `ResolveReturnUrl`, líneas 69-82) y `RedirectToLogin.razor:15` navega a `LoginPath` con `forceLoad: true` **sin** `ReturnUrl`. Escribir `/cuenta` sin sesión hoy caería en `NotAuthorized → RedirectToLogin → /login` sin retorno garantizado en modo interactivo. La propuesta unifica ambos caminos con la misma semántica ya probada de `LoginRedirect`. El botón anónimo de cabecera enlaza a `/login` sin `ReturnUrl` (no hay destino privado que preservar al pulsarlo desde cualquier página pública); el `ReturnUrl` crítico es el de la redirección por ruta protegida. *Limitación honesta:* el challenge del middleware en SSR estático no se verificó en runtime en exploración — riesgo cubierto abajo.

### Decisión 3 — Compatibilidad de `/mi-ludoteca`

**Elección: alias obligatorio (multi-`@page` en `MyLibrary.razor`) + reescritura selectiva de referencias Razor internas al hub. No se reescriben `manifest.webmanifest` ni `offline.html`.**

Justificación: hay **7 referencias en 6 ficheros**, incluido el `start_url` del manifest PWA (`manifest.webmanifest:5`) — reescribir la ruta sin alias rompería el arranque de la PWA instalada y silenciaría el criterio 6. El alias multi-`@page` es práctica testificada en el repo. Decisiones concretas por referencia: la píldora de cabecera (`MainLayout.razor:94`), `GameDetail.razor:45` y `Radar.razor:41` **se reescriben** al hub (`/cuenta/ludoteca`); `PublicProfile.razor:32,82` **no se tocan** (perfil público fuera de alcance §5, el alias las mantiene vivas); `offline.html:128` y `manifest.webmanifest:5` **no se tocan** — reescribir el `start_url` afectaría a instalaciones PWA existentes sin beneficio para INC-50 y el alias ya garantiza la URL. «Reescritura por referencia» sin alias queda descartada por riesgo (manifest y offline quedan fuera de un grep Razor) y por el criterio 6.

### Decisión 4 — Identidad visible en la cabecera

**Elección: icono de persona + `UserName` con sesión (nombre accesible); sin avatar en INC-50.**

Justificación: `ICurrentUserService` solo expone `UserId`/`UserName` en la superficie congelada (`CurrentUserContractTests.cs:49`), y eso basta — `UserId` vacío distingue el anónimo (patrón ya usado en `AccountEmailNotice.razor:40`, `MyLibrary.razor:963`) y `UserName` da nombre reconocible sin tocar el contrato ni ninguna de sus 21 implementaciones. **No se propone avatar:** no existe URL de avatar en la superficie de sesión (`AuthenticatedCurrentUserService` solo proyecta UserId/UserName/Role/Permissions) y elegirlo implicaría claim nuevo o lectura de `AppUser`, almacenamiento y privacidad de la foto del proveedor — **alcance nuevo de datos/privacidad**, que queda declarado y excluido; si se quiere avatar, deberá abrirse como incremento propio con su justificación (aviso del incremento §7). WCAG 2.2 AA: el estado con/sin sesión se comunica con texto («Entrar» / nombre), no solo icono ni color.

### Decisión 5 — Inventario de ajustes de usuario existentes

**Elección: en INC-50 el hub agrega vialógicamente las cuatro entradas del inventario (Ludoteca, Conexiones, Tema, País); Ludoteca y Conexiones son rutas hijas reales, y Tema y País enlazan a su superficie actual sin duplicar UI. El traslado físico y la consolidación quedan para INC-61+.**

Justificación y reparto, usando la tabla de la exploración §5:

| Ajuste | Superficie hoy | En INC-50 | INC-61+ |
|---|---|---|---|
| **Ludoteca** | `MyLibrary.razor` en `/mi-ludoteca` | Sección real `/cuenta/ludoteca` (segunda ruta) + alias | — |
| **Conexiones** | `AccountConnections.razor` en `/cuenta/conexiones` | Sección real: solo navegación/integración del hub, sin tocar lógica | — |
| **Tema visual** | Pestaña Apariencia de `MyLibrary` (líneas 193-197, 237) + carga en `MainLayout.razor:353` | Entrada «Tema» en el inventario del hub enlazando a la pestaña Apariencia existente | Página `/cuenta/ajustes` con la UI mudada al hub; unificar carga |
| **País** | `LocationSelectorModal.razor:178` (cabecera) + tarjeta `MyLibrary:400-442` | Entrada «País» enlazando al modal/tarjeta existente | Consolidación en `/cuenta/ajustes` |
| **`GetUserPreferenceAsync`/`UserPreferenceDto`** | Sin consumidores en Web (superficie muerta) | No se usa (queda documentado en el inventario) | Consumir desde `/cuenta/ajustes` |
| **`SwitchTheme` muerto** (`MainLayout.razor:401`) | Definido, sin invocaciones en markup | No se toca (documentado) | Eliminar o reutilizar |

La opción vialógica cumple el mandato del maintainer (todo lo de usuario queda inventariado y agrupado en el área) manteniendo **una sola superficie de verdad por ajuste** — sin duplicar la UI de tema ni la de país — y acota el tamaño de un PR ya de por sí amplio. Duplicidad transitoria: ninguno; el traslado físico es el que se aplaza, no se duplica nada. `InstagramModeration.razor:362-365` («Tema Visual de Marca») es tema editorial, **no** entra en el hub.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/Ludeka.Web/Components/Layout/MainLayout.razor` | Modified | Botón de persona en zona de utilidades (líneas 54-97); distinción con/sin sesión con `UserId`/`UserName`; reescritura de la píldora a `/cuenta/ludoteca` (línea 94) |
| `src/Ludeka.Web/Components/Pages/Account.razor` (nuevo) | New | Hub raíz `/cuenta` con `[Authorize]` e inventario de secciones |
| `src/Ludeka.Web/Components/Shared/AccountSectionNav.razor` (nuevo) | New | Cabecera de sección compartida entre rutas hijas `/cuenta/*` |
| `src/Ludeka.Web/Components/Pages/MyLibrary.razor` | Modified | Segunda ruta `@page "/cuenta/ludoteca"` conservando `@page "/mi-ludoteca"` (alias) |
| `src/Ludeka.Web/Components/Pages/AccountConnections.razor` | Modified | Integración de navegación de sección del hub; **sin tocar lógica de INC-49** |
| `src/Ludeka.Web/Components/Shared/RedirectToLogin.razor` | Modified | Preservar `ReturnUrl` en línea 15, alineado con `LoginRedirect.cs:36` |
| `src/Ludeka.Web/Components/Pages/GameDetail.razor:45`, `Radar.razor:41` | Modified | Reescritura de `/mi-ludoteca` a `/cuenta/ludoteca` |
| `src/Ludeka.Web/Components/Pages/PublicProfile.razor:32,82` | Not modified | Perfil público fuera de alcance; el alias mantiene vivos sus enlaces |
| `src/Ludeka.Web/wwwroot/manifest.webmanifest:5`, `wwwroot/offline.html:128` | Not modified | Conservan `/mi-ludoteca`; el alias garantiza la URL |
| `src/Ludeka.Application/Contracts/ICurrentUserService.cs` | Not modified | Contrato congelado; se usa `UserId`/`UserName` existentes |
| `tests/Ludeka.UnitTests/Web/AuthorizationPipelineContractTests.cs` | Modified | Nuevos contratos de fuente: botón de persona, distinción de sesión, enlace `/login`, hub, alias vivo, `ReturnUrl` |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| `MainLayout.razor` es transversal: un error de render afecta a todas las páginas | Med | Cambio acotado a la zona de utilidades + test de contrato de fuente (precedente `AccountEmailNotice`) + suite completa en verde |
| Contrato congelado de `ICurrentUserService` con **21 implementaciones** (el incremento decía 16; recuento real en HEAD) | Med | No ampliar la interfaz: usar solo `UserId`/`UserName`; si hiciera falta más, crear contrato aparte (precedente INC-49), nunca tocar el congelado |
| Rota `/mi-ludoteca` viva con referencias externas (manifest PWA `start_url`, `offline.html`) | Med | **Alias obligatorio** con multi-`@page`; test de contrato de que la ruta antigua sigue resolviendo; no se reescriben manifest ni offline |
| Incoherencia `ReturnUrl` entre `RedirectToLogin` y `LoginRedirect` | Med | Alinear ambos caminos con la semántica ya probada de `LoginRedirect`; el SSR del middleware no se verificó en runtime — verificarlo en apply/verify con el smoke test |
| Tamaño del PR único supera el presupuesto de revisión de 400 líneas | Alta | Ver pronóstico más abajo: `Decision needed before apply: Yes`, `size:exception` pendiente de aprobación del maintainer |
| Criterio 8 del incremento desactualizado (1.417) | Baja | Este propuesta fija la línea base real 1.622 + 10; corregir también en el documento de incremento durante apply |
| Avatar del proveedor arrastraría alcance de datos/privacidad | Baja | Declarado fuera de alcance en decisión 4; si se desea, incremento propio |

## Rollback Plan

El cambio es **íntegramente aditivo en código y rutas**: no hay migraciones de esquema, ni datos persistidos nuevos, ni eliminación de ninguna URL (el alias `/mi-ludoteca` se conserva). Si algo falla en verificación o en producción, basta un `git revert` del único PR de `inc/area-de-cuenta` para restaurar: la cabecera original de `MainLayout.razor`, la ruta única de `MyLibrary.razor`, el `RedirectToLogin.razor` previo y los enlaces reescritos. No se requiere rollback de base de datos ni de configuración. La reversión parcial no recomendada (solo la puerta sin hub) sería un revert quirúrgico del commit del hub; el diseño por capacidades separadas (`header-account-gate` / `account-area-hub`) permite aislarlo si se necesitara.

## Dependencies

- **INC-46** (Autenticación Real, archivado): `AuthorizeRouteView → RedirectToLogin`, `SessionIdentity`, `LoginRedirect`.
- **INC-49** (Vinculación de Cuentas, archivado): patrón de página `[Authorize]` en `/cuenta/conexiones`, rutas centralizadas, precedente de tests de contrato de fuente.
- **INC-50 §8:** smoke test manual de INC-49 pendiente (paso 2 de 7) — se completa en la sesión de este incremento con las credenciales OAuth ya configuradas en `ludeka-web-user-secrets-2026`.
- Sin dependencias externas nuevas: no se añaden paquetes NuGet ni servicios de infraestructura.

## Success Criteria

- [ ] Existe un punto de entrada de cuenta **visible en la cabecera** en todas las páginas (botón de persona en `MainLayout.razor`).
- [ ] Con sesión iniciada, ese punto lleva a `/cuenta` y muestra identidad reconocible (`UserName`).
- [ ] Sin sesión, lleva a `/login` — tras este incremento **sí** existe al menos un enlace a `/login` en la interfaz (hoy: cero resultados).
- [ ] `/cuenta/conexiones` es alcanzable navegando desde el hub, sin escribir la URL y sin depender del aviso de correo no verificado.
- [ ] «Mi ludoteca» se alcanza desde el área de cuenta como sección (`/cuenta/ludoteca`), y la píldora de cabecera entra por la cuenta.
- [ ] **Ninguna URL que hoy funcione deja de funcionar**, `/mi-ludoteca` incluida: alias activo y manifest/offline intactos.
- [ ] WCAG 2.2 AA: el punto de entrada es accesible por teclado con foco visible, y su estado (con sesión / sin sesión) no se comunica solo por color o solo por icono.
- [ ] Suite completa en verde con `dotnet test Ludeka.sln`. **Línea base al abrir este incremento: 1.622 pruebas unitarias + 10 de integración** (corregido respecto al 1.417 desactualizado del documento de incremento).
- [ ] `RedirectToLogin.razor` preserva `ReturnUrl` con la misma semántica que `LoginRedirect` (criterio operacional de la decisión 2, verificable por test de contrato de fuente).

## Size Forecast (Review Workload Guard — `single-pr`)

```
Decision needed before apply: Yes
Chained PRs recommended: No
400-line budget risk: High
```

- **Pronóstico de líneas autoría (adiciones + deletions, sin ficheros generados): 450-650.**
  - Puerta en `MainLayout.razor`: ~40-70.
  - Hub `/cuenta` + cabecera de sección compartida: ~150-240.
  - Alias/`@page` en `MyLibrary.razor` + reescrituras de enlaces: ~10-20.
  - `RedirectToLogin.razor` (`ReturnUrl`): ~15-25.
  - Integración de navegación en `AccountConnections.razor`: ~20-40.
  - Tests de contrato de fuente y ajustes: ~120-200.
- La estrategia de entrega es `single-pr`; el pronóstico **supera las 400 líneas** del Review Workload Guard, por lo que se marca `Decision needed before apply: Yes` con **`size:exception` pendiente de aprobación del maintainer — no aprobado por esta fase**. Si el maintainer rechaza la excepción, la alternativa es dividir en PRs encadenados (capacidad `header-account-gate` primero, `account-area-hub` después); por eso `Chained PRs recommended: No` mientras se plantea la excepción como vía prevista.
- En apply, mantener tareas por debajo de ~400 líneas autoría cada una como referencia de planificación (heurística, no tope duro), con commits de unidad de trabajo convencionales en la rama `inc/area-de-cuenta`.
