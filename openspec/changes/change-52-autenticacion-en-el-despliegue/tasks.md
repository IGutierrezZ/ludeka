# Tareas — INC-52: Autenticación y Acceso Administrativo en el Primer Despliegue de Producción

> **Fase:** `sdd-tasks` · **Fecha:** 2026-09-21
> **Worktree:** `inc/autenticacion-en-el-despliegue`, base `main` en `69d1e01`
> **Entradas:** `design.md` (íntegro, fuente principal), `proposal.md` §7, las tres especificaciones de `specs/` (`reverse-proxy-forwarded-headers`, `production-auth-bootstrap`, delta de `social-login-authentication`), `research-dominio-cloud-run.md`
> **Convención de citas:** los ficheros reales del repositorio (código, pruebas, config, documentación entregable) van entre backticks con ruta relativa; cuando una tarea cita un fichero que NO edita, lleva `(read-only)` inmediatamente después. Las citas a los propios artefactos de SDD de este cambio (`proposal.md`, `design.md`, los `spec.md`, `research-dominio-cloud-run.md`) y a `AGENTS.md` van sin backticks, porque ninguna tarea de este documento los edita.

**Corrección de recuento respecto a `design.md`:** `design.md` afirma dos veces que `tests/Ludeka.UnitTests/Web/WebAuthenticationRegistrationTests.cs` tiene «seis pruebas vigentes». Releído en esta fase: son **cinco** (`DisabledProvider_ShouldNotRegisterItsScheme`, `EnabledProviderWithoutCredentials_ShouldNotRegisterItsSchemeAndShouldWarn`, `EnabledProviderWithCredentials_ShouldRegisterSchemeWithCallbackSignInSchemeAndScopes`, `SessionCookie_ShouldBeHttpOnlySecureLaxSlidingWithConfiguredExpiry`, `DefaultConfigurationFile_ShouldDeclareProvidersDisabledAndWithoutUsableCredentials`; confirmado por conteo de `[Fact]`/`[Theory]`). No cambia ninguna decisión de diseño ni ninguna tarea; solo corrige el número usado en la Fase 5.

---

## Review Workload Forecast

| Campo | Valor |
|---|---|
| Líneas estimadas | ~780–1.490 (excluyendo PR #1, ya producido) |
| Riesgo de presupuesto de 400 líneas | **Alto** para el incremento sin partir; **Bajo-Medio** rebanada a rebanada tras la partición de abajo |
| PRs encadenados recomendados | Sí |
| Partición sugerida | PR #1 (contrato) → PR #2 → PR #3 → PR #4 → PR #5 → PR #6 → PR #7 (documentación, fases 7 y 8 fusionadas) |
| Estrategia de entrega | auto-chain (fijada en el preflight de sesión) |
| Estrategia de cadena | stacked-to-main |

```text
Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High
```

**Por qué `stacked-to-main` y no `feature-branch-chain`:** este repositorio no tiene el concepto de "rama tracker" que solo se fusiona al final — `scripts/sdd-worktree.ps1` gestiona un único par worktree/rama por incremento (`new`/`pr`/`done`), y los dos precedentes más recientes (INC-47 con 26 PRs, INC-48 con 12 PRs) fusionaron cada PR directamente a `main` en orden secuencial, no a través de una rama acumuladora. `stacked-to-main` es la continuación de un patrón ya establecido, no una elección nueva.

**`Decision needed before apply: No`** porque la estrategia de entrega ya fijada en el preflight es `auto-chain` (el orquestador procede con la primera rebanada sin bloquear en una pregunta).

**Sobre el riesgo "Alto":** la propia `proposal.md` §6 estimaba ~400 líneas de código+pruebas para todo el incremento; solo la Fase 2-4 de abajo (la acreditación completa de B1, con el arnés de host real) ya se estima en 293-540 líneas por sí sola, y B1 es apenas uno de los cinco defectos. La lección de INC-47 citada en el encargo (estimaciones que se quedaron cortas hasta 3,2×, sobre todo en pruebas) se aplica aquí: el corte natural que ya apunta `design.md` §3.1 («función pura + prueba de contrato de orden» / «arnés + pruebas de tubería») no basta por sí solo para bajar de 400 en la rebanada de B1, así que se parte en tres (Fases 2, 3, 4) en vez de dos. Ver la respuesta explícita al final de este documento.

### Suggested Work Units

| Unit (PR) | Objetivo | Comando de prueba enfocado (iteración local) | Arnés de runtime | Límite de reversión |
|---|---|---|---|---|
| **#1** | Contrato SDD (`proposal.md`, `design.md`, `specs/*`, este `tasks.md`) — **ya completo**, no genera tareas nuevas | N/A — sin código | N/A | N/A |
| **#2** | Arnés de host mínimo + validación del supuesto crítico de D4 (puerta H4/N2) | `dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj --filter "FullyQualifiedName~ForwardedHeadersPipelineTests"` | Real: Kestrel en `http://127.0.0.1:0` vía `ForwardedHeadersHostHarness` (nuevo, calcado de `MinimalMediaHostHarness`) | Revertir el PR: borra un fichero de pruebas nuevo; cero impacto en producción |
| **#3** | Middleware `UseForwardedHeaders` + función pura `ForwardedHeadersConfiguration.Build()` + pruebas de contrato de orden y de opciones | `dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj --filter "FullyQualifiedName~ForwardedHeadersConfigurationTests|FullyQualifiedName~AuthorizationPipelineContractTests"` | N/A — funciones puras y lectura de fuente, sin host | Revertir el PR: `Request.Scheme` vuelve a `http` (estado actual); sin migraciones ni persistencia implicadas |
| **#4** | Acreditación completa de B1 de extremo a extremo: N1, N3, N4, N5, N6 + variante de autenticación | `dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj --filter "FullyQualifiedName~ForwardedHeadersPipelineTests"` | Real: mismo arnés de la Unidad #2, extendido con `AddLudekaAuthentication` + Data Protection efímera | Revertir el PR: borra pruebas añadidas al fichero de la Unidad #2; no toca código de producción |
| **#5** | Guarda de identidad del fundador (B3) + aviso agregado de "cero proveedores" (B4) | `dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj --filter "FullyQualifiedName~WebStartupGuardsTests|FullyQualifiedName~WebAuthenticationRegistrationTests"` | N/A — funciones puras + lectura de `appsettings.json` real, sin host | Revertir el PR: restituye `appsettings.json:21` y el `:-` de `docker-compose.prod.yml:19`; guarda = función pura + `throw`, si abortó no sembró nada |
| **#6** | Cableado de autenticación y fundador en `ci-cd.yml` (B2) + su prueba de contrato | `dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj --filter "FullyQualifiedName~CiCdWorkflowContractTests"` | N/A — lectura de texto del YAML, no se ejecuta el workflow | Revertir las claves añadidas; el servicio conserva su revisión previa de Cloud Run |
| **#7** | Documentación de despliegue (§9.0 completada + nueva §10) + corrección del ROADMAP (B5) | N/A — solo documentación | N/A | Revertir el PR; documentación y ROADMAP son revertibles sin efectos |
| **#8** | Documento de incremento `inc-52-autenticacion-en-el-despliegue.md` | N/A — solo documentación | N/A | Revertir el PR; fichero nuevo sin dependencias de código |

**Nota sobre el comando de prueba enfocado:** es para iterar rápido en local durante ROJO/VERDE. **La verificación de aceptación de cada rebanada, antes de abrir o fusionar su PR, es siempre `dotnet test Ludeka.sln` completo — nunca el filtro** (regla explícita del encargo).

**Orden de la cadena:** #1 (hecho) ← #2 ← #3 ← #4 ← #5 ← #6 ← #7 ← #8, cada rama apuntando a la anterior desde el mismo worktree, según `AGENTS.md` §1-bis.7. Los nombres exactos de rama por rebanada (más allá de la única rama `inc/autenticacion-en-el-despliegue` que ya existe) y el mecanismo concreto de apertura de cada PR con `scripts/sdd-worktree.ps1`/`gh` son una decisión de `sdd-apply` en el momento de entrega, no de esta fase.

**Prerrequisito de todas las fases:** confirmar que `dotnet test Ludeka.sln` está en verde en el punto de partida (`main` en `69d1e01`) antes de tocar el primer fichero de la Fase 2.

---

## Fase 1 (PR #1 — Contrato): ya completo

Sin tareas pendientes. Esta rebanada agrupa `explore.md`, `proposal.md`, `specs/reverse-proxy-forwarded-headers/spec.md`, `specs/production-auth-bootstrap/spec.md`, `specs/social-login-authentication/spec.md`, `research-dominio-cloud-run.md`, `design.md` y este mismo `tasks.md`. No toca código y ya existe en el worktree.

---

## Fase 2 (PR #2 — Arnés de host mínimo + validación del supuesto crítico D4/H4)

Antes de escribir una sola línea de código de producción de B1. Esta fase es, literalmente, la puerta de decisión H4 del diseño: si la prueba 2.2 no confirma lo esperado, el incremento se detiene aquí.

- [x] **2.1** Crear `tests/Ludeka.UnitTests/Web/ForwardedHeadersPipelineTests.cs` con la clase privada anidada `ForwardedHeadersHostHarness`, calcada de `MinimalMediaHostHarness` (`tests/Ludeka.UnitTests/Web/MediaStaticFilesDeliveryTests.cs:166-204`, read-only): `WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = ... })`, `Logging.ClearProviders()`, `WebHost.UseUrls("http://127.0.0.1:0")`, `StartAsync()`, `HttpClient` construido contra `app.Urls.First()`, `IAsyncDisposable`. El arnés recibe las `ForwardedHeadersOptions` como parámetro. **Pieza que decide todo el incremento:** antes de `app.UseForwardedHeaders(options)`, montar `app.Use(async (context, next) => { context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10"); await next(); })` — TEST-NET-3 (RFC 5737), nunca de bucle invertido. Punto final de la tubería: un manejador que devuelve `Request.Scheme` y `Request.Host` en el cuerpo de la respuesta como texto plano.
  - No usar una IP de red local real ni vincular Kestrel a una interfaz física (alternativa descartada en `design.md` D13: dependería del entorno de CI).
- [x] **2.2** En el mismo fichero, escribir la prueba **N2 (caso negativo)**: instanciar `new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto }` **sin** llamar a `KnownProxies.Clear()` ni `KnownIPNetworks.Clear()` (listas de confianza en su estado por defecto), montar el arnés de 2.1 con esas opciones, enviar una petición con cabecera `X-Forwarded-Proto: https`, y afirmar que el cuerpo de la respuesta sigue devolviendo `http` (spec reverse-proxy-forwarded-headers, escenario «Sin vaciar las listas de confianza, el esquema no cambia», líneas 23-28). Esta prueba no depende de ningún código de producción nuevo: construye sus propias opciones inline.
- [x] **2.3** **[Tarea de decisión, no de código]** Ejecutar `dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj --filter "FullyQualifiedName~ForwardedHeadersPipelineTests"` y aplicar la regla de decisión (diseño §7 H4 / §8.3):
  - **Si N2 pasa** (el esquema permanece en `http`): el supuesto de D4 queda confirmado. Continuar con la Fase 3.
  - **Si N2 falla** porque el esquema cambia a `https` sin vaciar las listas: **PARAR.** No reescribir la prueba. No continuar con las Fases 3-8. Devolver el control al orquestador para volver a `sdd-design`: la conclusión sería que vaciar las listas no es la corrección de B1 y que D4 sobra o es contraproducente.
  - Todo lo que sigue en este documento asume que 2.3 resuelve en el primer caso.

**Verificación de la rebanada:** `dotnet test Ludeka.sln` completo.

**Estimación:** producción 0 líneas · pruebas 85-150 líneas (arnés ~60-100 + prueba N2 ~15-30 + `using`/namespace/clase ~10-20) · documentación 0 líneas. **Total: 85-150.**

---

## Fase 3 (PR #3 — Middleware, función pura y pruebas de contrato/opciones — núcleo de B1)

Depende de que la Fase 2 haya confirmado el gate H4. No depende de ningún otro fichero nuevo de la Fase 2 para compilar (el arnés no se reutiliza aquí).

- [x] **3.1** RED: crear `tests/Ludeka.UnitTests/Web/ForwardedHeadersConfigurationTests.cs` con las pruebas P2-P5 sobre `ForwardedHeadersConfiguration.Build()` (sin host, pruebas unitarias puras): **P2** `ForwardedHeaders == ForwardedHeaders.XForwardedProto` (igualdad exacta con `==`, no `HasFlag`, para que añadir una bandera de más rompa la prueba); **P3** `KnownProxies` y `KnownIPNetworks` vacías; **P4** `ForwardLimit == 1`; **P5** la bandera resultante no incluye `XForwardedHost` ni `XForwardedFor`. Fallará en compilación: `ForwardedHeadersConfiguration` todavía no existe.
- [x] **3.2** RED: modificar `tests/Ludeka.UnitTests/Web/AuthorizationPipelineContractTests.cs`, añadiendo junto a `Pipeline_ShouldAuthenticateAndAuthorizeBeforeAntiforgery` (`:29-43`, mismo fichero) el aserto **P1**: el índice de `"app.UseForwardedHeaders("` en el `Program.cs` leído por `ReadSource("src/Ludeka.Web/Program.cs")` es **menor** que el de `"app.UseHttpsRedirection();"` y **menor** que el de `"app.UseExceptionHandler("`. **No tocar** el aserto vigente `Assert.True(https >= 0, ...)` (`:39`, mismo fichero).
- [x] **3.3** GREEN: crear `src/Ludeka.Web/ForwardedHeadersConfiguration.cs`, clase estática con el único método `public static ForwardedHeadersOptions Build()` que devuelve unas opciones con `ForwardedHeaders = ForwardedHeaders.XForwardedProto`, `ForwardLimit = 1`, y con `KnownProxies.Clear()` + `KnownIPNetworks.Clear()` (la propiedad **vigente** en .NET 10; **no usar** la obsoleta `KnownNetworks`). Fichero plano en `Ludeka.Web`, no en una carpeta `Extensions/` (convención: `WebStartupGuards.cs` y `MediaStorageWarnings.cs` ya viven ahí).
- [x] **3.4** GREEN: modificar `src/Ludeka.Web/Program.cs`: insertar `app.UseForwardedHeaders(ForwardedHeadersConfiguration.Build());` como **primer middleware** de la tubería HTTP, en la línea 196, **antes** del bloque `if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler(...); app.UseHsts(); }` (`:196-200`, mismo fichero) y, por tanto, antes de `UseStatusCodePagesWithReExecute` (`:202`) y de `UseHttpsRedirection()` (`:203`). Llamada **incondicional**, sin comprobar `ASPNETCORE_ENVIRONMENT`. **No modificar la línea 203** (`UseHttpsRedirection()`): la exige `AuthorizationPipelineContractTests.cs:39` tal cual queda tras 3.2.
- [x] **3.5** Confirmar en verde P1-P5, las pruebas preexistentes de `AuthorizationPipelineContractTests.cs` (las 10 filas de `ProtectedPages` y el resto de `[Fact]`, sin cambios) y el resto de la suite.

**Verificación de la rebanada:** `dotnet test Ludeka.sln` completo.

**Estimación:** producción 25-60 líneas (`ForwardedHeadersConfiguration.cs` ~20-45 + diff de `Program.cs` ~5-15) · pruebas 43-88 líneas (aserto P1 ~8-18 + fichero P2-P5 ~35-70) · documentación 0. **Total: 68-148.**

---

## Fase 4 (PR #4 — Prueba de tubería real completa: N1, N3, N4, N5, N6)

Depende de la Fase 2 (arnés) y de la Fase 3 (`ForwardedHeadersConfiguration.Build()` + middleware ya mergeados). Todas las tareas modifican el mismo fichero: `tests/Ludeka.UnitTests/Web/ForwardedHeadersPipelineTests.cs`.

- [x] **4.1** RED→GREEN: añadir la prueba **N1** («Proxy no loopback corrige el esquema a https», spec reverse-proxy-forwarded-headers líneas 17-21), parametrizada por `EnvironmentName` en `Development`, `Staging` y `Production` (cubre también el escenario «Mismo comportamiento en un entorno distinto de Production», líneas 30-34): arnés con `ForwardedHeadersConfiguration.Build()`, petición con `X-Forwarded-Proto: https`, el cuerpo devuelve `https` en los tres entornos.
- [x] **4.2** RED→GREEN: añadir la prueba **N3** («Sin proxy delante, el comportamiento local no cambia», líneas 36-40): arnés con opciones de producción, petición **sin** la cabecera, el cuerpo devuelve `http`.
- [x] **4.3** RED→GREEN: añadir la prueba **N4** (cierre empírico de D7 / fila A5 de la matriz de amenazas propia): montar la tubería con `UseForwardedHeaders` **y** `UseHttpsRedirection()`, petición sin `X-Forwarded-Proto`, `HttpClient` con `AllowAutoRedirect = false`; la respuesta **no** es 307 ni 308. Si saliera 307/308, el hueco H5 quedaría refutado: no forma parte del alcance de esta tarea decidir la corrección — registrar el hallazgo y detener la Fase 4 hasta que el orquestador decida (fijar `HttpsRedirectionOptions.HttpsPort` o condicionar la llamada son las dos correcciones acotadas que ya anticipa el diseño, pero no se aplican sin decisión explícita). **Ejecutada de verdad: respuesta 200 OK, sin `Location`. El hueco H5 queda cerrado empíricamente; no hizo falta ninguna corrección.**
- [x] **4.4** RED→GREEN: añadir la prueba **N5** («X-Forwarded-Host no altera el host», líneas 48-52 / fila A3 de la matriz de amenazas): opciones de producción, petición con `X-Forwarded-Proto: https` **y** `X-Forwarded-Host: dominio-suplantado.ejemplo`; el host del cuerpo es `127.0.0.1:{puerto}`, nunca el suplantado.
- [x] **4.5** RED→GREEN: extender el arnés, en el mismo fichero, con una **variante de autenticación**: `services.AddLudekaAuthentication(options)` con un Google utilizable (mismo patrón que `OptionsWithUsableGoogle`, `tests/Ludeka.UnitTests/Web/WebAuthenticationRegistrationTests.cs:36-41`, read-only), `app.UseAuthentication()`, un `endpoint` que devuelve `Results.Challenge(new AuthenticationProperties(), [GoogleDefaults.AuthenticationScheme])`, y `builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider()` para no persistir claves en disco. El `HttpClient` de esta variante se construye con `AllowAutoRedirect = false` para poder leer la cabecera `Location` del desafío. **Implementada como clase anidada nueva `ForwardedHeadersAuthenticationHostHarness`, hermana de `ForwardedHeadersHostHarness`, no como parámetros sobre la misma.**
- [x] **4.6** RED→GREEN: añadir la prueba **N6** («redirect_uri en https», líneas 60-64): con la variante de autenticación de 4.5, desafío con `X-Forwarded-Proto: https`; el `redirect_uri` extraído de la cabecera `Location` empieza por `https://` y termina en `/signin-google` (`src/Ludeka.Web/Authentication/ExternalAuthenticationSchemes.cs:35`, read-only).
- [x] **4.7** RED→GREEN: añadir la variante de N6 para «Desarrollo local sin proxy sigue construyendo el redirect_uri en http» (líneas 66-70): mismo montaje de 4.5, sin cabecera; el `redirect_uri` conserva `http`.
- [x] **4.8** Confirmar en verde N1, N3, N4, N5, N6 y sus variantes, más N2 (Fase 2) sin cambios, y la suite completa. **Confirmado: 9/9 en el fichero (dotnet test filtrado) y 1.578 unitarias + 10 de integración en `dotnet test Ludeka.sln`, exit 0.**

**Verificación de la rebanada:** `dotnet test Ludeka.sln` completo.

**Estimación:** producción 0 (solo pruebas, todo el código de producción que necesitan ya está mergeado desde la Fase 3) · pruebas 140-242 líneas (N1 ~20-35 + N3 ~12-22 + N4 ~18-30 + N5 ~15-25 + variante de autenticación ~40-70 + N6 ~20-35 + variante dev de N6 ~15-25) · documentación 0. **Total: 140-242.**

---

## Fase 5 (PR #5 — Guarda de identidad del fundador (B3) + aviso agregado de proveedores (B4))

Independiente de las Fases 2-4 en lo funcional (B3/B4 no tocan cabeceras de proxy); se apila después de ellas solo por orden de rama en el mismo worktree.

- [x] **5.1** RED: añadir a `tests/Ludeka.UnitTests/Web/WebStartupGuardsTests.cs` las pruebas G1-G5 sobre `WebStartupGuards.EvaluateAdminUserIdentity(IConfiguration, string?)`, reutilizando `BuildConfiguration` con `AddInMemoryCollection` (`:19-28`, mismo fichero): **G1** `Production` + `AdminUser:Email` informado → `null`; **G2** `Production` sin la clave → no nulo, el mensaje contiene la cadena `AdminUser:Email`; **G3** `Production` + `"   "` (solo espacios) → no nulo; **G4** `Development` y `Staging` sin la clave → `null` en ambos; **G5** entorno `"production"` en minúsculas sin la clave → no nulo (comparación insensible a mayúsculas, mismo patrón que `Evaluate_ConEntornoProductionEnMinusculas_SiActivaLaGuarda`, `:78-87`, mismo fichero). Fallará en compilación: el método no existe todavía. **RED confirmado: `error CS0117 'WebStartupGuards' no contiene una definición para 'EvaluateAdminUserIdentity'` en los 5 puntos de llamada nuevos.**
- [x] **5.2** GREEN: añadir a `src/Ludeka.Web/WebStartupGuards.cs` el nuevo método estático `public static string? EvaluateAdminUserIdentity(IConfiguration configuration, string? environmentName)`. Debe leer `configuration["AdminUser:Email"]` **directamente**, nunca a través de `AdminUserOptions` enlazado — `src/Ludeka.Infrastructure/Options/AdminUserOptions.cs:23` (read-only) fija `Email` con valor por defecto `"admin@ludeka.es"` en C#, y leer de ahí haría que la guarda **fallara en abierto** si alguien borra la clave del `appsettings.json` en vez de vaciarla. Condición de disparo: `Production` (insensible a mayúsculas) **y** `string.IsNullOrWhiteSpace(configuration["AdminUser:Email"])`. Mensaje que nombra explícitamente `AdminUser:Email`, la variable de entorno `AdminUser__Email` y el secreto `ADMIN_USER_EMAIL`, y explica la irreversibilidad del sembrado. **No tocar** el método `Evaluate` existente en el mismo fichero. **No tocar** `src/Ludeka.Infrastructure/Options/AdminUserOptions.cs` (read-only): su valor por defecto lo fija `tests/Ludeka.UnitTests/Infrastructure/DatabaseProviderTests.cs:80` (read-only, `Assert.Equal("admin@ludeka.es", options.Email);`). **GREEN confirmado: 13/13 en el fichero filtrado (7 preexistentes + 6 casos G1-G5); `Evaluate` y `AdminUserOptions.cs` sin tocar.**
- [x] **5.3** RED: añadir a `tests/Ludeka.UnitTests/Web/WebAuthenticationRegistrationTests.cs` las pruebas A1-A3 sobre una nueva `ExternalAuthenticationSchemes.GetNoUsableProviderNotice(AuthenticationOptions, string?)`: **A1** `Production` + los tres proveedores no utilizables → no nulo, `Level == LogLevel.Error`; **A2** `Development` + los tres no utilizables → no nulo, `Level == LogLevel.Warning`; **A3** `Production` + Google utilizable → `null`, **y** `GetConfigurationWarnings(options)` sigue devolviendo, sin cambios, los avisos por proveedor individual. Fallará en compilación: el método y el `record` no existen todavía. **RED confirmado: `error CS0117 'ExternalAuthenticationSchemes' no contiene una definición para 'GetNoUsableProviderNotice'` en los 3 puntos de llamada nuevos.**
- [x] **5.4** GREEN: añadir a `src/Ludeka.Web/Authentication/ExternalAuthenticationSchemes.cs` el método estático `public static AuthenticationStartupNotice? GetNoUsableProviderNotice(AuthenticationOptions options, string? environmentName)` y el `public sealed record AuthenticationStartupNotice(LogLevel Level, string Message)`, declarado al final del fichero, junto a `ExternalProviderRegistration` (`:218-223`, mismo fichero). Devuelve `null` si algún proveedor cumple `IsUsable`; si no, un aviso con `LogLevel.Error` en `Production` (insensible a mayúsculas) y `LogLevel.Warning` en cualquier otro entorno. Mensaje que abre con la condición agregada y nombra Google, Discord y Facebook. **No tocar** `GetConfigurationWarnings` en el mismo fichero: su tipo de retorno `IReadOnlyList<string>` lo fija `tests/Ludeka.UnitTests/Web/WebAuthenticationRegistrationTests.cs:75-76` (read-only, `Assert.Contains(warnings, warning => warning.Contains("Discord", ...))`), que dejaría de compilar si el retorno cambiara de forma. **GREEN confirmado: 8/8 en el fichero filtrado (5 preexistentes + 3 nuevas A1-A3); `GetConfigurationWarnings` sin tocar.**
- [x] **5.5** Modificar `src/Ludeka.Web/Program.cs`: (a) insertar la segunda guarda de arranque —llamar a `WebStartupGuards.EvaluateAdminUserIdentity(app.Configuration, app.Environment.EnvironmentName)` y lanzar `InvalidOperationException` si devuelve no nulo, igual que la guarda de persistencia— inmediatamente después de esa guarda (`:116-123`, mismo fichero) y antes del bucle de avisos por proveedor (`:125-129`, mismo fichero); (b) insertar la emisión del aviso agregado —`ExternalAuthenticationSchemes.GetNoUsableProviderNotice(authenticationOptions, app.Environment.EnvironmentName)` y, si no es nulo, `app.Logger.Log(noProviderNotice.Level, "{AuthenticationNotice}", noProviderNotice.Message)`— inmediatamente después de ese bucle y antes de los avisos de medios (`:131-137`, mismo fichero). **Sin rojo propio: ningún test ejecuta `Program.cs` contra esta rama; verificado con `dotnet build` limpio y la suite completa en verde tras el cableado.**
- [x] **5.6** Modificar `src/Ludeka.Web/appsettings.json`, línea 21: `"Email": "admin@ludeka.es"` → `"Email": ""`. Sin esta pieza, la guarda de 5.2 nunca se dispara en `Production` con la configuración empaquetada. **Confirmado que ningún test existente dependía del valor anterior** (`DatabaseProviderTests.cs:80` construye `new AdminUserOptions()` en C#, no lee el JSON).
- [x] **5.7** Modificar `docker-compose.prod.yml`, línea 19: `- AdminUser__Email=${AdminUser__Email:-admin@ludeka.es}` → `- AdminUser__Email=${AdminUser__Email}`. Confirmar (sin tocarlos) que `docker-compose.yml` (read-only, `ASPNETCORE_ENVIRONMENT` por defecto `Staging`) y `docker-compose.staging.yml` (read-only) no se ven afectados: ninguno de los dos declara `Production`. **Confirmado por lectura: ambos fijan `Staging` y ninguno referencia `AdminUser__Email`.**
- [x] **5.8** RED→GREEN: añadir **G6** a `tests/Ludeka.UnitTests/Web/WebStartupGuardsTests.cs` (mismo fichero de 5.1): cargar `src/Ludeka.Web/appsettings.json` (read-only en esta tarea — ya se modificó en 5.6) con `ConfigurationBuilder().AddJsonFile(...)` (técnica de `tests/Ludeka.UnitTests/Web/WebAuthenticationRegistrationTests.cs:125-127`, read-only) y comprobar que, con entorno `Production`, la guarda de 5.2 **sí** se activa contra la configuración empaquetada real. Esta prueba solo puede pasar después de 5.6: liga D8 con el vaciado del fichero para que ninguna de las dos piezas se aplique sin la otra. **Acoplamiento verificado empíricamente: con 5.6 revertido de forma temporal (`"Email": "admin@ludeka.es"`), G6 en solitario falla con `Assert.NotNull() Failure: Value is null`; restaurado a `""`, vuelve a pasar. Estado final del repositorio: restaurado y en verde (14/14 en el fichero).**
- [x] **5.9** Confirmar en verde G1-G6, A1-A3, las **cinco** pruebas existentes de `tests/Ludeka.UnitTests/Web/WebAuthenticationRegistrationTests.cs` (read-only en esta tarea de verificación) sin cambios de comportamiento, y la suite completa. **Confirmado: 22/22 en el filtro combinado (`WebStartupGuardsTests|WebAuthenticationRegistrationTests`) y 1.588 unitarias + 10 de integración en `dotnet test Ludeka.sln`, exit 0.**

**Verificación de la rebanada:** `dotnet test Ludeka.sln` completo.

**Estimación:** producción 57-110 líneas (guarda ~20-35 + aviso ~20-35 + cableado en `Program.cs` ~15-30 + `appsettings.json` 1 + `docker-compose.prod.yml` 1) · pruebas 85-190 líneas (G1-G5 ~40-75 + G6 ~15-25 + A1-A3 ~30-54, con margen alto) · documentación 0. **Total: 142-300.**

---

## Fase 6 (PR #6 — Cableado de autenticación y fundador en CI/CD (B2) + su prueba de contrato)

Independiente en lo funcional de las Fases 2-5 (configuración declarativa, no código ejecutado en pruebas). Se apila al final porque documenta/cablea sobre comportamiento ya fijado por ellas.

**Decisión de producto pendiente heredada (`proposal.md` §10, pregunta 1):** ¿entra Facebook en el primer despliegue? `design.md` D11 recomienda **Google + Discord** y deja Facebook fuera (revisión de Meta pendiente, fuera de alcance). Las tareas siguientes implementan esa recomendación; si el maintainer decide otro conjunto antes de fusionar este PR, solo cambian las líneas concretas de 6.2, no la prueba de contrato de 6.1 (que comprueba "al menos un proveedor", no cuál).

- [ ] **6.1** RED: crear `tests/Ludeka.UnitTests/Deployment/CiCdWorkflowContractTests.cs`, con una copia propia (no compartida — `design.md` no crea ninguna clase de ayudantes común) de los ayudantes `ReadSource`/`GetRepoRoot` de `tests/Ludeka.UnitTests/Web/AuthorizationPipelineContractTests.cs:170-187` (read-only), leyendo `.github/workflows/ci-cd.yml` (read-only en esta tarea; se edita en 6.2) como texto plano. Delimitar el bloque del paso web tomando la subcadena desde `"env_vars: |"` hasta `"secrets: |"`, y desde `"secrets: |"` hasta el siguiente `"      - name:"`. Escribir: **Y1** el bloque `env_vars` del paso web contiene al menos un `Authentication__Providers__{P}__Enabled=true`; **Y2** para ese mismo `{P}`, el bloque `secrets` contiene su `ClientId`/`AppId` y su `ClientSecret`/`AppSecret`, ambos con sufijo `:latest`; **Y3** el bloque `secrets` contiene una línea que empieza por `AdminUser__Email=`; **Y4** el bloque `env_vars` no contiene ninguna aparición de `ClientSecret`, `AppSecret` ni `AdminUser__Email`; **Y5** el bloque de los cuatro Cloud Run Jobs (delimitado por el `- name: Publicar la revisión de los Cloud Run Jobs`) no contiene `Authentication__` ni `AdminUser__` (pin de regresión hacia delante). Fallarán: las claves aún no existen en el YAML.
- [ ] **6.2** GREEN: modificar `.github/workflows/ci-cd.yml`, **solo** en el paso "Desplegar revisión en Google Cloud Run" (el del servicio web; **no tocar** el paso "Publicar la revisión de los Cloud Run Jobs"): añadir tras la línea `Cloudflare__Simulate=false` del bloque `env_vars` las líneas `Authentication__Providers__Google__Enabled=true` y `Authentication__Providers__Discord__Enabled=true`; añadir tras la línea `Cloudflare__SecretAccessKey=CLOUDFLARE_SECRET_ACCESS_KEY:latest` del bloque `secrets` las cinco líneas `Authentication__Providers__Google__ClientId=GOOGLE_OAUTH_CLIENT_ID:latest`, `Authentication__Providers__Google__ClientSecret=GOOGLE_OAUTH_CLIENT_SECRET:latest`, `Authentication__Providers__Discord__ClientId=DISCORD_OAUTH_CLIENT_ID:latest`, `Authentication__Providers__Discord__ClientSecret=DISCORD_OAUTH_CLIENT_SECRET:latest`, `AdminUser__Email=ADMIN_USER_EMAIL:latest`.
- [ ] **6.3** Confirmar en verde Y1-Y5 y la suite completa.

**Verificación de la rebanada:** `dotnet test Ludeka.sln` completo.

**Estimación:** producción/config 7-10 líneas (7 líneas de YAML nuevas, con margen) · pruebas 93-170 líneas (ayudantes propios ~18-20 + 5 pruebas de extracción/aserto ~15-30 cada una) · documentación 0. **Total: 100-180.**

---

## Fase 7 (PR #7 — Documentación de despliegue (§9.0 + nueva §10), corrección del ROADMAP (B5) y documento de incremento)

Solo documentación. Va después de las Fases 2-6 porque describe el comportamiento que ellas fijan. No aplica ninguna fila de la matriz de amenazas (prosa, no código ejecutable).

- [ ] **7.1** Corregir `docs/deployment/google-cloud-run.md`, línea 115: la frase actual afirma que el primer arranque contra Supabase crea «el usuario Administrador Fundador inicial permanente (`admin-fundador` / `admin@ludeka.es`)». Sustituir por el comportamiento real tras la Fase 5: en `Production` se exige `AdminUser__Email` explícito y el proceso aborta si falta; el correo sembrado es el que se informe; fuera de `Production` sigue vigente el respaldo existente de `AdminUserSeeder.cs:41` (read-only).
- [ ] **7.2** Insertar en `docs/deployment/google-cloud-run.md` §9.0 (**sin renumerar** los pasos 1-7 existentes, líneas 206-218 — se citan desde la propia línea 116 y desde `ROADMAP.md:73`, read-only) el paso **`1-bis`**, junto al paso 1 (base de datos): decidir el correo del Administrador Fundador, real y verificable por el proveedor social elegido, guardado como secreto `ADMIN_USER_EMAIL`; aviso de irreversibilidad y puntero a la nueva §10.4.
- [ ] **7.3** Insertar en la misma sección §9.0 el paso **`3-bis`**, antes del paso 4 (el que arma el despliegue): dominio, apps OAuth y claves de autenticación; puntero a §10.1, §10.2 y §10.3.
- [ ] **7.4** Añadir la nueva sección `## 10` al final de `docs/deployment/google-cloud-run.md` (tras la actual §9, sin tocar ningún encabezado existente), con cinco subsecciones:
  - **10.1 Dominio propio en Cloud Run:** `gcloud domains verify DOMINIO_BASE` (paso de cuenta, independiente del servicio); `gcloud beta run domain-mappings create --service --domain --region europe-west1`; `gcloud beta run domain-mappings describe --domain DOMAIN --region europe-west1` para obtener los registros DNS concretos (no inventar valores); `A`/`AAAA` para ápex, `CNAME` para subdominio; **trampa del registro CAA** — si el dominio tiene registros CAA restrictivos, autorizar `pki.goog` y `letsencrypt.org` o la emisión del certificado falla en silencio; plazo habitual del certificado gestionado (~15 min, hasta 24 h); y el razonamiento escrito de la decisión ya tomada por el maintainer de asumir el estado *preview* / «not recommended for production services» de Google (research-dominio-cloud-run.md §2, read-only): sin tráfico todavía, el balanceador global cobraría exista o no tráfico, y la migración sigue abierta.
  - **10.2 Registro de las tres apps OAuth:** URLs de retorno exactas `https://<dominio>/signin-google`, `https://<dominio>/signin-discord`, `https://<dominio>/signin-facebook` (`src/Ludeka.Web/Authentication/ExternalAuthenticationSchemes.cs:35-37`, read-only); orden: mapear el dominio antes de registrar, para no retocar tres consolas después.
  - **10.3 Entradas de Secret Manager a crear antes del paso 4:** lista exacta de las claves cableadas en la Fase 6 (`GOOGLE_OAUTH_CLIENT_ID`, `GOOGLE_OAUTH_CLIENT_SECRET`, `DISCORD_OAUTH_CLIENT_ID`, `DISCORD_OAUTH_CLIENT_SECRET`, `ADMIN_USER_EMAIL`) con su clave de configuración de destino, y el recordatorio de `roles/secretmanager.secretAccessor`; procedimiento de añadir un proveedor (p. ej. Facebook) después del primer despliegue.
  - **10.4 Primer acceso del Administrador Fundador:** el sembrador crea la fila con rol `FoundingTeam` y permisos completos **sin ninguna fila en `ExternalLogins`** (`AdminUserSeeder.cs:56-69`, read-only); la activa la rama **2a** de `ExternalLoginService.ResolveAsync` (escenario «Correo verificado coincide con una cuenta sin identidades externas previas» de social-login-authentication); el primer inicio de sesión social cuyo correo verificado coincida con `AdminUser:Email` **es** la puerta de entrada, sin paso adicional; condición dura: el buzón debe ser verificable por el proveedor elegido (`email_verified` en Google, `verified` en Discord/Facebook — `ExternalAuthenticationSchemes.cs:165,179,194`, read-only); el correo se normaliza a minúsculas al sembrar (`AdminUserSeeder.cs:41`, read-only); camino manual de recuperación si se sembró un correo equivocado (corregir el campo `Email` de esa fila en Supabase, o degradarla y redesplegar, con el sembrador **promoviendo** la cuenta existente con ese correo en vez de crear otra — `AdminUserSeeder.cs:44-53`, read-only).
  - **10.5 Qué hace la aplicación detrás del proxy:** qué cabecera se confía (`X-Forwarded-Proto`) y cuáles no (`X-Forwarded-Host`, `X-Forwarded-For`) y por qué; que las listas de confianza van vacías a propósito y qué lo mitiga; `ForwardLimit = 1` por el salto único del mapeo directo, y que pasaría a `2` con un balanceador global; que nada del código lee la IP remota hoy y que quien la necesite deberá añadir `XForwardedFor` con su propia prueba; que la garantía descansa en que todo el tráfico entre por el front-end de Cloud Run; nota sobre por qué `UseHttpsRedirection()` se conserva sin tocar y por qué no produce bucle.
- [ ] **7.5** Corregir `docs/increments/ROADMAP.md`, línea 75: sustituir la afirmación falsa de que el PR #60 «está abierto y retenido por el maintainer» por el hecho verificado (fusionado el 2026-09-19), conservando el puntero al gate real de despliegue ya redactado en la línea 73 (mismo fichero, no se toca esa línea).
- [ ] **7.6** Añadir en `docs/increments/ROADMAP.md` (mismo fichero) una fila para INC-52 en la tabla de incrementos, tras la fila de INC-51 (línea 67), estado `⏳ En progreso`, enlazando `docs/increments/inc-52-autenticacion-en-el-despliegue.md` (se crea en la Fase 8).
- [ ] **7.7** Añadir en `docs/increments/ROADMAP.md` (mismo fichero), sección «🌿 Incrementos en Curso» (líneas 80-92), una entrada para INC-52 con el mismo formato que la de INC-50 (línea 92): worktree `C:\repos\ludeka-wt\autenticacion-en-el-despliegue`, rama `inc/autenticacion-en-el-despliegue`, y el gate del incremento (los ocho PRs de la cadena fusionados antes de configurar `GCP_PROJECT_ID` y `GCP_SA_KEY`).

**Verificación de la rebanada:** `dotnet test Ludeka.sln` completo (sin pruebas nuevas en este PR; confirma que la edición de prosa no rompió nada).

**Estimación:** producción 0 · pruebas 0 · documentación 156-266 líneas (`google-cloud-run.md` ~142-244 + `ROADMAP.md` ~14-22). **Total: 156-266.**

---

### Documento de incremento (antes Fase 8, fusionado aquí por decisión del maintainer: ambas fases son prosa y no tocan código ejecutable)

Depende de las tareas anteriores de esta misma fase: la fila del ROADMAP ya debe enlazar este fichero.

- [x] **7.8** (ADELANTADA al PR del contrato, por AGENTS.md §1.5: el incremento debe figurar en el ROADMAP al iniciarlo, y la fila no puede enlazar a un fichero inexistente) Crear `docs/increments/inc-52-autenticacion-en-el-despliegue.md`, con el mismo formato que `docs/increments/inc-50-area-de-cuenta.md` (read-only, referencia de formato): cabecera con metadatos (estado, rama `inc/autenticacion-en-el-despliegue`, worktree `C:\repos\ludeka-wt\autenticacion-en-el-despliegue`, dependencias — INC-47 e INC-48, archivados); el problema (los cinco defectos B1-B5 de la propuesta y por qué importan en el primer despliegue real); qué pide/decide el maintainer (las tres decisiones ya cerradas listadas al principio de `design.md`); alcance dentro/fuera (de la propuesta §3); criterios de aceptación (de la propuesta §8.3); riesgos (de `design.md` §8.1); y los huecos de evidencia declarados H1-H8 (`design.md` §7.2) con su plan de cierre, incluida la puerta H4/N2 de la Fase 2 de este documento.

**Verificación de la rebanada:** `dotnet test Ludeka.sln` completo (sin pruebas nuevas).

**Estimación:** producción 0 · pruebas 0 · documentación 90-200 líneas. **Total: 90-200.**

---

## Respuesta explícita: ¿alguna rebanada supera las 400 líneas en la estimación alta?

**No, ninguna de las ocho rebanadas la supera, y es así porque ya se partió para conseguirlo.** El corte natural de una sola rebanada para B1 (`design.md` §3.1: "función pura + prueba de contrato de orden" / "arnés + pruebas de tubería") se estimó primero como **PR único de 286-490 líneas** — el extremo alto ya cruza el presupuesto—, así que se partió en **tres** rebanadas (Fases 2, 3 y 4) en vez de dos, precisamente para que la puerta de decisión H4/N2 (Fase 2) quedara aislada, verificable de forma independiente, y sin arrastrar producción todavía sin validar. De la misma manera, el PR #4 original de la propuesta (B2 + documentación + B5, estimado en solitario en 342-617 líneas) se partió en **tres** rebanadas (Fases 6, 7 y 8) por el mismo motivo.

Estimación alta por rebanada, para que quede a la vista: Fase 2 = 150 · Fase 3 = 148 · Fase 4 = 242 · Fase 5 = 300 · Fase 6 = 180 · Fase 7 = 266 · Fase 8 = 200. La más alta (Fase 5, 300) deja un margen del 25 % por debajo de 400 incluso en su extremo alto.

## Resumen de tareas y estimación total

- **Tareas totales:** 33 (Fase 2: 3 · Fase 3: 5 · Fase 4: 8 · Fase 5: 9 · Fase 6: 3 · Fase 7: 7 · Fase 8: 1), repartidas en 7 rebanadas pendientes (PR #2 a PR #8) más el PR #1 ya completo.
- **Estimación total en rango (excluyendo el PR #1 ya producido):** **~780 a ~1.490 líneas autoradas** (adiciones + supresiones), de las cuales ~110-280 son de código de producción/config, ~460-1.050 de pruebas, y ~250-470 de documentación entregable.
- **Ninguna rebanada individual supera las 400 líneas** en su estimación alta (ver tabla anterior); la más próxima es la Fase 5 con 300.
