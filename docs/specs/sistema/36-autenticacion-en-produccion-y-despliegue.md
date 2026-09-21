# 36. Autenticación en Producción: Proxy Inverso, Identidad del Fundador y Cableado del Despliegue

> **Estado:** Implementado y verificado (1.593 pruebas unitarias + 10 de integración en verde, 0 errores, 0 omitidas)
> **Incremento:** [INC-52](file:///c:/repos/Ludeka/docs/increments/archive/inc-52-autenticacion-en-el-despliegue.md) — 8 PRs (#77–#84), `main` en `189ef27`
> **Módulos relacionados:** [32. Autenticación Social y Autorización](file:///c:/repos/Ludeka/docs/specs/sistema/32-autenticacion-y-autorizacion.md) · [33. Vinculación de Cuentas](file:///c:/repos/Ludeka/docs/specs/sistema/33-vinculacion-cuentas-y-recuperacion-de-acceso.md) · [34. Trabajos en Cloud Run](file:///c:/repos/Ludeka/docs/specs/sistema/34-trabajos-en-segundo-plano-cloud-run.md) · [35. Persistencia de Producción y Medios](file:///c:/repos/Ludeka/docs/specs/sistema/35-persistencia-produccion-y-medios-con-fallback.md)

---

## 1. Qué resuelve este módulo

El módulo 32 describe **cómo funciona** la autenticación social. Este describe **qué hace falta para que funcione en producción**, que resultó ser otra cosa.

Al preparar el primer despliegue real aparecieron **tres defectos bloqueantes independientes**. Ninguno provoca fallo de arranque, ninguno aparece en `/healthz` ni en `/ready`, y ninguno estaba documentado. Juntos habrían producido una web que levanta, se ve bien y **en la que no puede entrar nadie, tampoco el maintainer**.

| # | Defecto | Consecuencia |
|---|---|---|
| **B1** | No se procesaban las cabeceras del proxy inverso | El `redirect_uri` de OAuth salía en `http` y los tres proveedores rechazaban el desafío |
| **B2** | El pipeline no inyectaba ninguna credencial de autenticación | Cero proveedores registrados, y **no existe acceso por contraseña** |
| **B3** | El correo del administrador fundador se fijaba de forma irreversible | El maintainer podía quedar fuera de su propio panel, permanentemente |
| **B4** | Con los tres proveedores deshabilitados no se emitía ninguna línea en los registros | El caso peor era también el más silencioso |

---

## 2. Reconstrucción del esquema detrás del proxy inverso

### 2.1. Por qué hace falta

Cloud Run termina TLS por delante del contenedor: el navegador va por HTTPS, pero la aplicación recibe la petición como **HTTP** interno. Kestrel se vincula solo a HTTP (`Dockerfile:65`, `ASPNETCORE_HTTP_PORTS=8080`), sin endpoint HTTPS.

Los manejadores OAuth construyen el `redirect_uri` a partir de `Request.Scheme` y `Request.Host`. Sin procesar `X-Forwarded-Proto`, ese esquema vale **siempre `http`**, y lo que llega al proveedor no coincide con el `https://` registrado en su consola.

El mismo patrón se reproduce en el despliegue VPS documentado, con `proxy_pass http://` y `X-Forwarded-Proto` en `deploy/nginx/default.conf`.

### 2.2. La configuración

[`src/Ludeka.Web/ForwardedHeadersConfiguration.cs`](file:///c:/repos/Ludeka/src/Ludeka.Web/ForwardedHeadersConfiguration.cs) expone una función pura, al estilo de `WebStartupGuards.Evaluate` y `MediaStorageWarnings.GetConfigurationWarnings`:

```csharp
public static ForwardedHeadersOptions Build()
{
    var options = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedProto,
        ForwardLimit = 1,
    };

    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();

    return options;
}
```

| Decisión | Motivo |
|---|---|
| **Solo `XForwardedProto`** | `XForwardedHost` permitiría suplantar el host, y **ninguna capa lo detendría**: `appsettings.json` tiene `AllowedHosts: "*"`, que además es una lista distinta de la de `ForwardedHeadersOptions`. `XForwardedFor` queda fuera porque **nadie lee la IP remota**: cero coincidencias de `RemoteIpAddress` en todo `src/` |
| **`ForwardLimit = 1`** | Salto único del mapeo de dominio directo. Con un balanceador de aplicación externo global serían **dos**, y este valor tendría que subir |
| **Listas de confianza vacías** | El origen del proxy de Cloud Run no es una IP conocida de antemano. Por defecto el middleware **descarta** las cabeceras, comportamiento que este incremento demostró en lugar de suponer (§2.4) |

> ⚠️ **`KnownIPNetworks` es la propiedad vigente en .NET 10.** `KnownNetworks` está marcada obsoleta —«Obsolete, please use `KnownIPNetworks` instead»— así que cualquier receta copiada de .NET 6 o 7 usaría la equivocada y compilaría con aviso.

### 2.3. Dónde se cablea

En [`src/Ludeka.Web/Program.cs:223`](file:///c:/repos/Ludeka/src/Ludeka.Web/Program.cs), como **primer middleware de la tubería HTTP** y de forma **incondicional**, sin comprobar el entorno:

```csharp
app.UseForwardedHeaders(ForwardedHeadersConfiguration.Build());
```

Va antes del bloque de no-desarrollo y, por tanto, antes de `UseHttpsRedirection()`. Todo lo que sigue —HSTS, la redirección HTTPS, la cookie de sesión y el `redirect_uri`— decide a partir de ese esquema.

Una prueba de contrato fija ese orden leyendo el propio fuente (`AuthorizationPipelineContractTests.cs`), con una aserción de guarda que impide un verde accidental: sin ella, un índice ausente daría `-1 < https` y la prueba pasaría aunque el middleware no existiera.

### 2.4. Cómo se demostró, en vez de suponerse

Cloud Run **no documenta `X-Forwarded-Proto` para su propio producto**: la página *Container runtime contract* no lo menciona, y la única confirmación literal procede de Cloud Run functions, que es un producto hermano. Por eso el incremento convirtió la suposición en prueba.

[`tests/Ludeka.UnitTests/Web/ForwardedHeadersPipelineTests.cs`](file:///c:/repos/Ludeka/tests/Ludeka.UnitTests/Web/ForwardedHeadersPipelineTests.cs) levanta un host ASP.NET Core mínimo real con Kestrel, calcado del arnés de INC-48, **sin añadir ninguna dependencia de pruebas**.

> **La pieza que hace que estas pruebas valgan algo:** el arnés fuerza `Connection.RemoteIpAddress` a `203.0.113.10` (TEST-NET-3, RFC 5737) **antes** de `UseForwardedHeaders`. Un `HttpClient` contra `127.0.0.1` produce una dirección remota **de bucle invertido** —justo la que se sospecha aceptada por defecto—, y sin forzarla las pruebas pasarían por el motivo equivocado.

| Prueba | Qué acredita |
|---|---|
| **N1** | Con proxy no loopback y la cabecera, el esquema pasa a `https`, en `Development`, `Staging` y `Production` |
| **N2** | **Sin vaciar las listas de confianza, el esquema NO cambia.** Es la prueba que justifica vaciarlas |
| **N3** | Sin cabecera, el comportamiento local no cambia |
| **N4** | Con `UseHttpsRedirection()` en la tubería y sin cabecera, no hay redirección 307 ni 308 |
| **N5** | `X-Forwarded-Host` no altera el host |
| **N6** | El `redirect_uri` de un desafío real de Google **empieza por `https://`** y termina en `/signin-google`. Su variante sin cabecera lo conserva en `http` |

N6 es la demostración de extremo a extremo: lee la cabecera `Location` del desafío y comprueba la cadena que el proveedor recibiría.

> **Sobre `UseHttpsRedirection()`:** se conserva intacto. Su documentación indica que, si no logra resolver un puerto HTTPS, «the middleware will log a warning and turn off», y en Ludeka las tres vías fallan: no hay `AddHttpsRedirection`, no existe `HTTPS_PORT` y Kestrel solo escucha HTTP. N4 lo confirma de forma observada, no solo documental. Además hay una prueba de contrato que **exige** esa llamada.

---

## 3. Identidad del Administrador Fundador

### 3.1. Por qué es irreversible

`AdminUserSeeder.EnsureAdminUserAsync` ([`src/Ludeka.Infrastructure/Seeding/AdminUserSeeder.cs:29-36`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Seeding/AdminUserSeeder.cs)) comprueba si ya existe algún `AppUser` con rol `FoundingTeam` y, si lo hay, **registra un mensaje y retorna**. Toda la lógica que lee `AdminUser:Email` queda por debajo de ese `return` y **no se vuelve a ejecutar nunca**.

El correo queda fijado en el primer arranque contra base vacía. Y ese correo es la única puerta de entrada del fundador (§3.4).

### 3.2. La guarda

[`src/Ludeka.Web/WebStartupGuards.cs:55-81`](file:///c:/repos/Ludeka/src/Ludeka.Web/WebStartupGuards.cs) añade una segunda guarda junto a la de coherencia de proveedor de INC-48:

```csharp
public static string? EvaluateAdminUserIdentity(IConfiguration configuration, string? environmentName)
{
    ArgumentNullException.ThrowIfNull(configuration);

    var isProduction = string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
    var adminUserEmail = configuration["AdminUser:Email"];

    if (isProduction && string.IsNullOrWhiteSpace(adminUserEmail))
    {
        return "Guarda de arranque (identidad del Administrador Fundador): ...";
    }

    return null;
}
```

> 🔑 **Lee de `IConfiguration` directamente, nunca de `AdminUserOptions` enlazado.** Esa clase fija `Email = "admin@ludeka.es"` como valor por defecto en C#. Leer de ahí haría que la guarda **fallara en abierto** si alguien borra la clave del JSON en lugar de vaciarla, reintroduciendo el defecto por la puerta de atrás.

El mensaje completo nombra la clave en sus tres formas —`AdminUser:Email`, `AdminUser__Email` y `ADMIN_USER_EMAIL`— y explica la irreversibilidad y su consecuencia.

Se invoca en [`Program.cs:129-135`](file:///c:/repos/Ludeka/src/Ludeka.Web/Program.cs), que lanza `InvalidOperationException` con el mensaje devuelto. La guarda en sí es una **función pura**, comprobable sin componer servicios.

### 3.3. Las tres piezas son inseparables

| Pieza | Sin ella |
|---|---|
| La guarda | El correo se siembra en silencio |
| `appsettings.json` con `"Email": ""` | La guarda nunca se dispara con la configuración empaquetada |
| `docker-compose.prod.yml:19` sin valor por defecto | El compose de producción reintroduciría el correo fijo |

La prueba **G6** carga el `appsettings.json` real y comprueba que la guarda se activa contra la configuración empaquetada, de modo que ninguna pieza pueda aplicarse sin la otra.

`docker-compose.yml` y `docker-compose.staging.yml` no se ven afectados: ninguno declara `Production`.

### 3.4. Cómo entra el fundador la primera vez

**Esto no estaba escrito en ninguna parte antes de INC-52.** Es conducta emergente entre INC-46 e INC-49, y de ella depende el acceso administrativo:

1. El sembrador crea un `AppUser` con rol `FoundingTeam` y permisos completos, **con cero filas en `ExternalLogins`**. En esa fila no se puede iniciar sesión.
2. La activa la rama **2a** de `ExternalLoginService.ResolveAsync`: un inicio de sesión social cuyo correo **verificado** coincide con `AdminUser:Email`; el servicio localiza la cuenta, comprueba que no tiene proveedores vinculados, crea el vínculo y devuelve **esa misma cuenta** con sus permisos intactos.

**El primer inicio de sesión social cuyo correo verificado coincida con `AdminUser:Email` es la puerta de entrada.** No hay paso adicional. El buzón debe ser verificable por el proveedor elegido, y el correo se normaliza a minúsculas al sembrar.

> ⚠️ **La recuperación manual no es la que parece.** Si se siembra un correo equivocado, **degradar la fila y redesplegar no basta**: `AdminUserSeeder` busca por `Id` **o** `Email` y, al promover, **nunca reescribe el `Email`**. Como nada fija `AdminUser__Id`, la fila se volvería a encontrar por su `Id` y se promovería con el correo antiguo. Hay que **borrar la fila o cambiarle el `Id`** antes de redesplegar.

---

## 4. El aviso que faltaba

`GetConfigurationWarnings` solo avisa cuando un proveedor está habilitado **y** le faltan credenciales. Con los tres deshabilitados —el valor por defecto— la lista quedaba vacía: ni una línea advertía de que el acceso estaba inoperativo.

[`ExternalAuthenticationSchemes.cs:135-156`](file:///c:/repos/Ludeka/src/Ludeka.Web/Authentication/ExternalAuthenticationSchemes.cs) añade una función **hermana**, no una modificación: cambiar el tipo de retorno de la existente habría roto sus pruebas.

```csharp
public static AuthenticationStartupNotice? GetNoUsableProviderNotice(AuthenticationOptions options, string? environmentName)
{
    ArgumentNullException.ThrowIfNull(options);

    foreach (var name in ExternalProviderNames.All)
    {
        if (options.Providers.TryGetValue(name, out var provider) && provider.IsUsable)
        {
            return null;
        }
    }

    var isProduction = string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
    var level = isProduction ? LogLevel.Error : LogLevel.Warning;
    var message = "Ningún proveedor de autenticación social está operativo: ...";

    return new AuthenticationStartupNotice(level, message);
}
```

Se emite en [`Program.cs:146-150`](file:///c:/repos/Ludeka/src/Ludeka.Web/Program.cs), después del bucle de avisos por proveedor:

```csharp
var noProviderNotice = ExternalAuthenticationSchemes.GetNoUsableProviderNotice(authenticationOptions, app.Environment.EnvironmentName);
if (noProviderNotice is not null)
{
    app.Logger.Log(noProviderNotice.Level, "{AuthenticationNotice}", noProviderNotice.Message);
}
```

Severidad `Error` en `Production` y `Warning` fuera de ella: en producción bloquea también el primer acceso del fundador.

---

## 5. Cableado del despliegue

[`.github/workflows/ci-cd.yml`](file:///c:/repos/Ludeka/.github/workflows/ci-cd.yml), **solo en el paso del servicio web**:

| Bloque | Claves |
|---|---|
| `env_vars` (no sensible) | `Authentication__Providers__Google__Enabled`, `Authentication__Providers__Discord__Enabled` |
| `secrets` (Secret Manager) | `Authentication__Providers__{Google,Discord}__{ClientId,ClientSecret}`, `AdminUser__Email` |

El criterio es el que el fichero ya aplicaba a Cloudflare: a secretos va también **la mitad identificadora** del par, no solo el secreto. `AdminUser__Email` va ahí **por ser dato personal**, no por ser un secreto técnico.

Facebook queda fuera del primer despliegue: la revisión de aplicaciones de Meta está fuera de alcance. Añadirlo después es una línea en `env_vars` y dos en `secrets`, más sus entradas de Secret Manager.

### 5.1. Nada se cablea en los Cloud Run Jobs

Los cuatro Jobs **no atienden HTTP**, no pueden recibir un retorno OAuth y no siembran el usuario fundador: `AdminUserSeeder` solo se invoca desde el host web. Cada clave añadida allí sería un **prerrequisito duro de despliegue a cambio de cero funcionalidad**, porque una referencia de secreto que no resuelve hace fallar `gcloud run deploy`.

La prueba **Y5** lo fija como pin de regresión: añadir una clave `Authentication__` o `AdminUser__` al bloque de los Jobs rompe la suite.

### 5.2. Consecuencia operativa

Cada clave del bloque `secrets` es un prerrequisito del despliegue. Con este incremento el primer despliegue pasa de diez a **quince** entradas de Secret Manager. El procedimiento completo —mapeo de dominio, registro de las apps OAuth y creación de secretos— está en [`docs/deployment/google-cloud-run.md`](file:///c:/repos/Ludeka/docs/deployment/google-cloud-run.md) §10.

---

## 6. Deuda y huecos abiertos

1. **No existe entorno de producción.** Ni GCP, ni Supabase, ni dominio mapeado. **Nada de este módulo está acreditado contra un despliegue real**: lo verificado es coherencia de código, pruebas y configuración.
2. **Cloud Run no documenta `X-Forwarded-Proto` para su propio producto.** La emisión real de la cabecera por la plataforma no se ha observado.
3. **El mapeo de dominio está en fase *preview*** y Google lo desaconseja para producción. Decisión consciente del maintainer, razonada en la guía de despliegue §10.1: sin tráfico el riesgo es bajo, el balanceador global cobraría exista o no tráfico, y la migración sigue abierta.
4. **No se ha observado un viaje OAuth real** contra Google ni un sembrado real del fundador.
5. **N4 es más débil de lo que podría:** solo excluye 307 y 308, en vez de fijar el `200 OK` observado. Un futuro 500 o 404 seguiría pasándola.
6. **La guarda de identidad no tiene contrato de posición.** `UseForwardedHeaders` sí lo tiene; retirar la llamada a la guarda de `Program.cs` no rompería ninguna prueba, y es la pieza **irreversible** del incremento. Comparte esta característica con la guarda de INC-48.
7. **Alcanzabilidad del contenedor saltándose el front-end de Cloud Run:** no verificada. La mitigación no descansa en ella, sino en limitar qué se puede hacer con la cabecera confiada.

---

## 7. Estado de entrega

- **8 PRs fusionados** en `main` (`189ef27`): #77 contrato, #78 puerta de decisión, #79 middleware, #80 pruebas de tubería, #81 guarda y aviso, #82 cableado, #83 documentación, #84 verificación.
- **Suite:** 1.593 pruebas unitarias + 10 de integración, 0 errores, 0 omitidas, exit 0. Compilación: exit 0, 0 errores, 13 advertencias.
- **Veredicto de verificación:** `pass_with_warnings` — 6 requisitos y 17 escenarios conformes, 36 tareas, **0 CRITICAL**, 3 WARNING, 1 SUGGESTION.
