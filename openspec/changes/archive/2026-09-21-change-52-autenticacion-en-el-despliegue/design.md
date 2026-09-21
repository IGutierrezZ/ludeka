# Diseño — INC-52: Autenticación y Acceso Administrativo en el Primer Despliegue de Producción

> **Fase:** `sdd-design` · **Fecha:** 2026-09-21
> **Worktree:** `inc/autenticacion-en-el-despliegue`, base `main` en `69d1e01`
> **Entradas:** `explore.md`, `proposal.md`, `research-dominio-cloud-run.md`, y las tres especificaciones de `specs/` (`reverse-proxy-forwarded-headers`, `production-auth-bootstrap`, delta de `social-login-authentication`).

**Convenio de lectura** (heredado de `proposal.md`):
> **[V]** hecho verificado en esta fase, con `fichero:línea` o fuente citada ·
> **[R]** recomendación de diseño, sujeta a validación en `sdd-apply`/`sdd-verify` ·
> **[NV]** afirmación **no verificada**, declarada como hueco con plan de cierre en §7.

**Decisiones del maintainer ya cerradas, que este diseño no reabre:**
1. **Mapeo de dominio directo** a Cloud Run (`gcloud beta run domain-mappings create`), asumiendo a sabiendas la advertencia oficial de *preview* / «not recommended for production services». Implica **un solo salto de proxy**.
2. **La guarda de identidad ABORTA en `Production`**, sin validación de formato: solo presencia.
3. **NO se añade `Microsoft.AspNetCore.TestHost`.** Se diseña un arnés de host mínimo propio, análogo al precedente del repositorio.

---

## 1. Resumen de la solución

El incremento cierra cinco defectos que hoy harían inaccesible el primer despliegue real. La solución se apoya en **tres patrones que el repositorio ya tiene**, y no introduce ninguno nuevo:

| Patrón existente | Precedente **[V]** | Uso en INC-52 |
|---|---|---|
| **Función pura de arranque** (recibe el entorno explícitamente, devuelve `string?`/valor; `Program.cs` decide qué hacer) | `WebStartupGuards.Evaluate` (`src/Ludeka.Web/WebStartupGuards.cs:20-46`), `MediaStorageWarnings.GetConfigurationWarnings` (`src/Ludeka.Web/MediaStorageWarnings.cs:15-30`) | Construcción de `ForwardedHeadersOptions` (D3), guarda de `AdminUser:Email` (D8), aviso agregado de proveedores (D10) |
| **Prueba de contrato por lectura de fuente** | `AuthorizationPipelineContractTests.ReadSource` (`tests/Ludeka.UnitTests/Web/AuthorizationPipelineContractTests.cs:170-187`) | Orden del pipeline (P1) y contrato del flujo de CI/CD sobre YAML (D12) |
| **Arnés de host ASP.NET Core mínimo con Kestrel real** | `MinimalMediaHostHarness` (`tests/Ludeka.UnitTests/Web/MediaStaticFilesDeliveryTests.cs:166-204`) | Arnés de cabeceras reenviadas (D13), que es lo que convierte la suposición central de B1 en prueba |

### 1.1. Flujo de la petición, antes y después

```
HOY (defecto B1)
  Navegador ──https──▶ Front-end Cloud Run ──http + X-Forwarded-Proto: https──▶ Kestrel :8080
                                                    (cabecera ignorada: nadie la procesa)
                                                                       │
  Program.cs:202 UseStatusCodePages ─▶ :203 UseHttpsRedirection ─▶ ... ─▶ :216 UseAuthentication
                                                                       │
                                         Request.Scheme == "http"  ────┘
                                         redirect_uri = http://dominio/signin-google   ✗ el proveedor lo rechaza

DESPUÉS
  Navegador ──https──▶ Front-end Cloud Run ──http + X-Forwarded-Proto: https──▶ Kestrel :8080
                                                                       │
  [NUEVO] UseForwardedHeaders(ForwardedHeadersConfiguration.Build())  ─┤  Request.Scheme := "https"
                                                                       │
  :196 UseExceptionHandler/UseHsts ─▶ UseStatusCodePages ─▶ UseHttpsRedirection ─▶ ... ─▶ UseAuthentication
                                                                       │
                                         redirect_uri = https://dominio/signin-google  ✓
```

### 1.2. Flujo de arranque, antes y después

```
Program.cs:110  var app = builder.Build();
        :116    Guarda 1 — persistencia (INC-48)                        [sin cambios]
        NUEVO   Guarda 2 — identidad del fundador (D8)                  ← aborta en Production sin AdminUser:Email
        :126    Avisos por proveedor habilitado sin credenciales        [sin cambios]
        NUEVO   Aviso agregado: CERO proveedores utilizables (D10)      ← LogError en Production, LogWarning fuera
        :134    Avisos de almacén de medios (INC-48)                    [sin cambios]
        :183    AdminUserSeeder.EnsureAdminUserAsync  ← el punto irreversible que la Guarda 2 protege
```

### 1.3. Qué queda demostrado y qué no

La tesis central del incremento —«Cloud Run manda `X-Forwarded-Proto: https` y hay que confiar en ella sin lista de proxies»— tiene **dos mitades con estatus de evidencia distinto**, y el diseño las separa a propósito:

- **Mitad nuestra (demostrable aquí):** que con las listas de confianza vacías el esquema cambia, y que **sin vaciarlas no cambia**. Eso es comportamiento del marco dentro de nuestro proceso y lo mide el arnés de D13, incluido el **caso negativo**, que es la prueba de mayor valor del incremento.
- **Mitad de la plataforma (no demostrable aquí):** que Cloud Run emite esa cabecera. La página *Container runtime contract* **no la menciona**; la única cita literal viene de Cloud Run functions, producto hermano **[V]** (`research-dominio-cloud-run.md:103-108`). **Este diseño no la cita como hecho oficial.** Queda como hueco declarado (§7, H1) con un procedimiento de comprobación de coste cero en el primer despliegue.

---

## 2. Decisiones de diseño

### D1 · Posición de `UseForwardedHeaders` en la tubería

**Decisión [R]:** insertarlo como **primer middleware**, en `Program.cs:196`, **antes** del bloque `if (!app.Environment.IsDevelopment()) { UseExceptionHandler; UseHsts; }` (`Program.cs:196-200`) y, por tanto, antes de `UseStatusCodePagesWithReExecute` (`:202`) y `UseHttpsRedirection` (`:203`).

**Por qué ahí, y no en cualquier punto anterior a `UseAuthentication`:** todo lo que va detrás lee `Request.Scheme` o `Request.IsHttps` para decidir, y cada middleware saltado es una decisión tomada con el esquema equivocado:

| Consumidor aguas abajo | Ubicación **[V]** | Qué decide con el esquema |
|---|---|---|
| `UseHsts` | `Program.cs:199` | Si emite `Strict-Transport-Security`. Que el middleware de HSTS solo actúe sobre peticiones HTTPS es comportamiento **[NV]**: la documentación XML del pack instalado describe el tipo (`Microsoft.AspNetCore.HttpsPolicy.xml:56-60`) pero no publica esa condición. No se afirma; simplemente colocarlo antes elimina la pregunta |
| `UseHttpsRedirection` | `Program.cs:203` | Si redirige. Ver D7 |
| Cookie de sesión `SecurePolicy = Always` | `ExternalAuthenticationSchemes.cs:54` | Si el navegador llega a recibir la cookie |
| Manejadores OAuth (`redirect_uri`) | `ExternalAuthenticationSchemes.cs:157-197` | El esquema del `redirect_uri` que viaja al proveedor. **Es el defecto B1** |
| `UseStatusCodePagesWithReExecute` | `Program.cs:202` | Reejecuta la tubería; cualquier URL absoluta generada dentro hereda el esquema |

**Alternativa descartada:** colocarlo justo antes de `app.UseAuthentication()` (`:216`). Arreglaría B1 —que es lo único bloqueante— pero dejaría HSTS, la redirección HTTPS y las páginas de código de estado operando sobre un esquema falso, sin ninguna ventaja a cambio. Un middleware de reescritura de la petición que no va primero es una trampa para el siguiente que lea el fichero.

**Alternativa descartada:** `builder.Services.Configure<ForwardedHeadersOptions>(...)` + `app.UseForwardedHeaders()` sin argumentos. Las dos sobrecargas existen **[V]** (`Microsoft.AspNetCore.HttpOverrides.xml:32,43`). Se descarta la vía por DI porque separa la configuración del punto de uso y obliga a montar un contenedor para comprobarla; la sobrecarga con `ForwardedHeadersOptions` explícito permite construir las opciones en una función pura y probarla sin host (D3).

### D2 · Qué cabeceras se procesan: solo `XForwardedProto`

**Decisión [R]:** `ForwardedHeaders = ForwardedHeaders.XForwardedProto`. Ni `XForwardedHost` ni `XForwardedFor`.

`ForwardedHeaders` es un `enum` de banderas cuyo valor `None` significa «no procesar ningún reenvío» **[V]** (`Microsoft.AspNetCore.HttpOverrides.xml:243-247`); la propiedad no declara valor por defecto en la documentación, de modo que **hay que fijarla explícitamente** **[R]**.

**`XForwardedHost` queda fuera. La propuesta lo argumentó bien pero con un matiz impreciso que conviene corregir aquí**, porque son dos mecanismos independientes y no uno:

- `appsettings.json:8` `"AllowedHosts": "*"` **[V]** configura el filtrado de host del host genérico, **no** `ForwardedHeadersOptions.AllowedHosts`.
- `ForwardedHeadersOptions.AllowedHosts` es una lista propia y separada, y «si la lista está vacía se admiten todos los hosts» **[V]** (`Microsoft.AspNetCore.HttpOverrides.xml:140-144`), con la advertencia literal de que «no restringir estos valores puede permitir a un atacante suplantar los enlaces que genera tu servicio».

La conclusión de la propuesta se sostiene **y se refuerza**: no es que una capa relaje a la otra, es que **ninguna de las dos pararía un host suplantado**. Y procesar la cabecera no aporta nada: con mapeo de dominio directo el `Host` que llega ya es el dominio propio, y en el VPS Nginx preserva el original con `proxy_set_header Host $host` **[V]** (`deploy/nginx/default.conf:37`). Coste alto, beneficio cero.

**`XForwardedFor` también queda fuera, y esto resuelve un `[NV]` que la propuesta dejó abierto.** La propuesta condicionaba procesarla a comprobar que ninguna decisión de seguridad dependa de `RemoteIpAddress` (`proposal.md:140`). **Comprobado en esta fase [V]:** una búsqueda de `RemoteIpAddress`, `Connection.RemoteIp` y `X-Forwarded` sobre todo `src/` devuelve **cero coincidencias**. Nadie lee la IP remota: ni para autorizar, ni para limitar, ni para auditar. Por tanto procesarla no arregla nada hoy y sí crea un riesgo diferido: dejaría `RemoteIpAddress` poblado desde una cabecera que aceptamos de cualquier origen (D4), lista para que un futuro incremento la crea de buena fe. **Si algún día se necesita la IP real del cliente, se añade la bandera en el mismo sitio y con su propia prueba**; la excepción queda escrita en la nueva §10.5 de la guía de despliegue.

### D3 · Las opciones se construyen en una función pura

**Decisión [R]:** nuevo fichero `src/Ludeka.Web/ForwardedHeadersConfiguration.cs`, con una única función estática `Build()` que devuelve el `ForwardedHeadersOptions` ya configurado.

**Ubicación plana en `Ludeka.Web`, no en `Ludeka.Web/Extensions/`:** el nivel raíz del proyecto contiene hoy exactamente tres ficheros `.cs` —`Program.cs`, `WebStartupGuards.cs` y `MediaStorageWarnings.cs`— **[V]**, y los dos últimos son precisamente los auxiliares transversales de arranque. La convención del repositorio es que esta familia vive plana. Se sigue.

Forma pretendida (el tipo exacto de las colecciones de confianza no se afirma aquí; se resuelve al compilar):

```csharp
public static class ForwardedHeadersConfiguration
{
    public static ForwardedHeadersOptions Build()
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedProto,  // D2
            ForwardLimit = 1,                                     // D5
        };

        options.KnownProxies.Clear();        // D4
        options.KnownIPNetworks.Clear();     // D4 — propiedad vigente, NO la obsoleta KnownNetworks
        return options;
    }
}
```

**Alternativa descartada:** configurar las opciones en línea dentro de `Program.cs`. Se descarta porque `Program.cs` es un fichero de instrucciones de nivel superior sin tipo que instanciar: comprobar el contenido de esas opciones exigiría o bien levantar el host, o bien una prueba de lectura de fuente que solo verifica que el texto está escrito, no que produzca el objeto esperado. La función pura permite las dos cosas a la vez (prueba unitaria del objeto **y** prueba de contrato de que `Program.cs` la invoca).

### D4 · Listas de confianza vacías, con la propiedad vigente de .NET 10

**Decisión [R]:** `KnownProxies.Clear()` y **`KnownIPNetworks.Clear()`**.

**Detalle crítico de versión [V]:** `ForwardedHeadersOptions.KnownNetworks` está marcada como obsoleta en el pack de referencia instalado, con el texto literal «Obsolete, please use `KnownIPNetworks` instead» (`Microsoft.AspNetCore.HttpOverrides.xml:129-133`), y `KnownIPNetworks` existe con la misma descripción sin la nota (`:135-139`). **Cualquier receta copiada de una guía de .NET 6 o 7 llamaría a `KnownNetworks.Clear()` y compilaría con aviso de obsolescencia.** Se usa la vigente.

**Por qué vaciarlas:** `KnownProxies` son «direcciones de proxies conocidos de los que aceptar cabeceras reenviadas» y `KnownIPNetworks` los rangos equivalentes **[V]** (`:124-139`). Son **el mecanismo de confianza**, no un filtro accesorio. La IP del front-end de Cloud Run es desconocida y variable, y en el despliegue VPS el proxy es una IP privada de la red de Docker: en ningún caso de bucle invertido.

**Lo que sigue siendo `[NV]` y no se afirma:** que los valores por defecto de esas listas sean exactamente el bucle invertido y `127.0.0.0/8`, y que en consecuencia el middleware **descarte en silencio** las cabeceras de un proxy fuera de ellas. La documentación XML del pack no publica esos valores por defecto. **El diseño no lo cita: lo mide.** Es exactamente lo que hace el caso negativo de §4.3, y §7 (H4) fija la regla de decisión para cada resultado posible.

**Alternativa descartada:** declarar los rangos de Google como `KnownIPNetworks`. Google no publica un rango estable para el front-end de Cloud Run **[NV]**, y una lista caducada reintroduce B1 en silencio —que es exactamente el modo de fallo que este incremento existe para eliminar—. Una mitigación que puede fallar sin síntoma es peor que no tenerla.

### D5 · `ForwardLimit = 1`, y qué cambiaría con un balanceador

**Decisión [R]:** fijar `ForwardLimit = 1` explícitamente, aunque coincida con el valor por defecto documentado **[V]** (`Microsoft.AspNetCore.HttpOverrides.xml:117-123`).

**Por qué 1:** la decisión cerrada del maintainer es **mapeo de dominio directo**, que introduce **un solo salto de proxy** **[V]** (`research-dominio-cloud-run.md:113`). Un salto, un valor a consumir.

**Por qué explícito y no heredado:** el valor por defecto es correcto, pero es **invisible**, y la misma documentación advierte que anularlo a `null` «solo debería hacerse si `KnownProxies` o `KnownNetworks` están configurados» **[V]** (`:120-121`) — que es precisamente lo contrario de lo que hacemos en D4. Escribirlo lo convierte en una decisión afirmada por una prueba en vez de un accidente heredado, y deja el aviso anterior atado al sitio donde importa.

**Qué habría que cambiar al migrar a un balanceador global:** el balanceador añade un segundo salto, así que **`ForwardLimit` pasaría a 2** **[V]** (`research-dominio-cloud-run.md:113`). Esto **debe quedar escrito en `google-cloud-run.md` §10.5** (D14), junto a la vía de migración que la investigación deja abierta, porque es el tipo de ajuste que nadie recuerda cuando cambia la topología y que vuelve a producir B1 sin ningún síntoma.

**Beneficio lateral de `ForwardLimit = 1` [R], con su reserva:** si la cabecera llegara como una lista (`X-Forwarded-Proto: http,https`) porque un cliente inyectó un valor y el proxy añadió el suyo, un límite de 1 consume solo la última entrada —la del proxy— y descarta el prefijo del cliente. **Que Cloud Run añada en vez de sobrescribir es `[NV]`** (§7, H2): se apunta como beneficio plausible, no como mitigación acreditada, y la matriz de amenazas de §5 no se apoya en él.

### D6 · Incondicional, sin depender del entorno

**Decisión [R]:** se invoca siempre, sin condicionar a `ASPNETCORE_ENVIRONMENT`. Lo exige el primer requisito de `reverse-proxy-forwarded-headers` (`specs/reverse-proxy-forwarded-headers/spec.md:13`).

**Por qué:** sin proxy delante la cabecera no llega y el middleware es inerte —lo fija el cuarto escenario de ese requisito (`:36-40`)—. Condicionarlo a `Production` dejaría descubiertos dos despliegues reales que también viven detrás de un proxy que termina TLS: el VPS con Nginx **[V]** (`deploy/nginx/default.conf:30,37-40`) y `docker-compose.staging.yml`. Y, sobre todo, crearía una diferencia de comportamiento entre entornos justo en el mecanismo cuyo fallo no produce ningún síntoma.

### D7 · `UseHttpsRedirection()` incondicional: **resuelto, no se toca**

Este es el punto que `explore.md:41` marcó como riesgo sin verificar y que `proposal.md:142-150` dejó explícitamente como hipótesis. **Queda cerrado en esta fase con evidencia documental del pack instalado más una cadena de hechos del repositorio.**

**Hecho 1 [V] — el middleware se apaga solo si no resuelve puerto.** Cita literal de la documentación XML de `HttpsRedirectionOptions.HttpsPort` (`Microsoft.AspNetCore.HttpsPolicy.xml:161-171`):

> «If the HttpsPort is not set, we will try to get the HttpsPort from the following: 1. HTTPS_PORT environment variable 2. IServerAddressesFeature. **If that fails then the middleware will log a warning and turn off.**»

**Hecho 2 [V] — en este despliegue las tres fuentes fallan.**
- `HttpsRedirectionOptions.HttpsPort` no se fija: `AddHttpsRedirection` no aparece en `Program.cs`.
- No existe ninguna variable `HTTPS_PORT` ni `ASPNETCORE_HTTPS_PORT` en el repositorio: la búsqueda de `HTTPS_PORT` sobre todo el worktree solo devuelve `ASPNETCORE_HTTP_PORTS` (HTTP, plural, enlace de Kestrel) en `Dockerfile:65` y `docker-compose.prod.yml:13`.
- `IServerAddressesFeature` no ofrece ninguna dirección HTTPS: Kestrel se vincula solo a HTTP (`Program.cs:53-57`, `http://0.0.0.0:{PORT}`).

**Hecho 3 [V] — el middleware solo actúa sobre peticiones no-HTTPS:** «Middleware that redirects non-HTTPS requests to an HTTPS URL» (`Microsoft.AspNetCore.HttpsPolicy.xml:120-123`).

**Conclusión [R], derivada de los tres hechos:**
1. **Hoy no hay bucle de redirección.** El middleware no puede resolver puerto, así que avisa y se apaga. El riesgo secundario que `explore.md:41` no pudo cerrar era real como pregunta, pero la respuesta es negativa.
2. **Tras corregir B1 la pregunta se vuelve discutible.** Con `Request.Scheme == "https"`, el middleware no tiene nada que redirigir por el Hecho 3.
3. **El bucle solo existiría en una combinación que hoy no se da y que este incremento cierra:** B1 sin corregir **y** un puerto HTTPS resoluble. Añadir `HTTPS_PORT` al despliegue sin corregir B1 sería la receta exacta del bucle infinito. Queda como fila de la matriz de amenazas (§5, A5).

**Decisión [R]: `Program.cs:203` no se toca.** Además, retirarla es imposible sin romper una prueba de contrato vigente que exige literalmente que la llamada exista **[V]** (`AuthorizationPipelineContractTests.cs:37-39`, `Assert.True(https >= 0, "Program.cs no llama a UseHttpsRedirection().")`). Esa prueba se conserva intacta y se le añade una hermana que fija el orden nuevo (§4.1, P1).

**Lo que queda [NV] y cómo se cierra:** lo anterior es comportamiento **documentado**, no **observado** en este repositorio. Se convierte en observado con una prueba del arnés que no cuesta nada: una petición HTTP sin `X-Forwarded-Proto`, sobre una tubería que incluye `UseHttpsRedirection()`, no debe responder 307 (§4.3, prueba N4). Si respondiera 307, el hecho documental estaría refutado y la corrección sería acotada y conocida —fijar `HttpsRedirectionOptions.HttpsPort` o condicionar la llamada—, sin tocar el alcance del incremento.

### D8 · Guarda de identidad del fundador

**Decisión [R]:** segunda función pura en `src/Ludeka.Web/WebStartupGuards.cs`, con la firma:

```csharp
public static string? EvaluateAdminUserIdentity(IConfiguration configuration, string? environmentName)
```

Devuelve el mensaje de fallo o `null`. **`Program.cs` es quien lanza**, igual que con la guarda de INC-48 (`Program.cs:116-123`).

**Método nuevo, no ampliación de `Evaluate`.** Dos razones: la especificación las describe como hermanas «evaluadas de forma independiente entre sí» (`specs/production-auth-bootstrap/spec.md:5`), y la firma de `Evaluate` está fijada por seis pruebas vigentes (`WebStartupGuardsTests.cs:30-103`) que no tienen por qué cambiar para añadir una guarda de otra familia.

**Lee de `IConfiguration`, NO de `AdminUserOptions`. Esto es la decisión de más peso del apartado y corrige un supuesto de la propuesta.** `AdminUserOptions.Email` tiene un **valor por defecto en C#** de `"admin@ludeka.es"` **[V]** (`src/Ludeka.Infrastructure/Options/AdminUserOptions.cs:23`). Si la guarda leyera de las opciones enlazadas:

- Con `appsettings.json` trayendo `"Email": ""`, el enlazador fija `""` y la guarda dispara. Correcto.
- Pero si alguien **borra** esa clave del fichero en lugar de vaciarla, el valor por defecto de C# resucita, la guarda **pasa en silencio** y se siembra `admin@ludeka.es` para siempre. **Falla en abierto**, que es el defecto B3 reintroducido por la puerta de atrás.

Leyendo `configuration["AdminUser:Email"]` directamente, tanto el valor vacío como la clave ausente se traducen en «no informado» y la guarda **falla en cerrado**. Además es el estilo que ya usa la guarda hermana, que lee `configuration.GetConnectionString(...)` en vez de tirar de opciones (`WebStartupGuards.cs:25-27`).

**`AdminUserOptions.cs` no se toca.** Vaciar su valor por defecto rompería una prueba vigente que lo fija: `Assert.Equal("admin@ludeka.es", options.Email)` **[V]** (`tests/Ludeka.UnitTests/Infrastructure/DatabaseProviderTests.cs:74-80`). Con la guarda leyendo de `IConfiguration`, tocarlo no aporta nada.

**Qué cuenta como «informado»:** `!string.IsNullOrWhiteSpace(configuration["AdminUser:Email"])`. **Sin validación de formato**, por decisión cerrada del maintainer. **Sin comparación contra el literal `admin@ludeka.es`**: bloquearía a un maintainer que sí controle ese buzón, y `ludeka.es` ya figura como dominio propio del proyecto **[V]** (`appsettings.json:50`, `Instagram:PublicBaseUrl`).

**La pieza que lo hace funcionar:** vaciar `appsettings.json:21` a `"Email": ""` **[R]**. Sin eso, `configuration["AdminUser:Email"]` devuelve `admin@ludeka.es` en `Production` y la guarda no dispara nunca. Las dos piezas **solo funcionan juntas**.

**Que esto no rompe el sembrado fuera de `Production` [V]:** `AdminUserSeeder.cs:41` conserva su propio respaldo (`string.IsNullOrWhiteSpace(options.Email) ? "admin@ludeka.es" : ...`), que no se toca. El tercer escenario de la especificación lo exige textualmente (`specs/production-auth-bootstrap/spec.md:28-32`).

**Que esto no afecta a `Ludeka.Jobs` [V]:** `src/Ludeka.Jobs` **enlaza** el mismo `appsettings.json` (`Ludeka.Jobs.csproj:25-26`) pero no referencia `AdminUserOptions` ni `AdminUserSeeder` —búsqueda de `AdminUserOptions|EnsureAdminUserAsync|appsettings` sobre `src/Ludeka.Jobs` devuelve únicamente esas dos líneas del `.csproj`—. Y `EnsureAdminUserAsync` se invoca exclusivamente desde el host web (`Program.cs:183`).

**Ubicación en el arranque [R]:** inmediatamente **después** de la guarda de persistencia (`Program.cs:116-123`) y **antes** del bloque de avisos (`:125`). La guarda de INC-48 conserva su posición documentada de «antes que cualquier otro efecto de arranque» (`Program.cs:112-115`); un despliegue al que le falten las dos cosas debe informar primero del fallo más fundamental. Lo que importa para B3 es que ambas van muy por delante de `AdminUserSeeder` (`:183`), que es el punto sin retorno.

**Mensaje [R]** — nombra la clave, como la guarda de INC-48 nombra `SUPABASE_DB_CONNECTION` (`WebStartupGuards.cs:40-42`):

> «Guarda de arranque (identidad del fundador): en Production se exige un valor explícito y no vacío para `AdminUser:Email` (variable de entorno `AdminUser__Email`, secreto `ADMIN_USER_EMAIL`). El sembrado del Administrador Fundador ocurre una sola vez contra base vacía y es irreversible por la vía de la aplicación: arrancar sin ese valor fijaría un correo por defecto de forma permanente.»

**Alternativa descartada:** aviso en vez de aborto. Rechazada explícitamente por el maintainer, y con razón técnica: el daño de B3 es irreversible por la vía de la aplicación y un aviso en los registros de Cloud Run llega, por definición, **después** del sembrado.

### D9 · `docker-compose.prod.yml`: retirar el valor por defecto

**Decisión [R]:** `docker-compose.prod.yml:19` pasa de `- AdminUser__Email=${AdminUser__Email:-admin@ludeka.es}` a `- AdminUser__Email=${AdminUser__Email}`.

**Por qué es obligatorio y no cosmético:** ese fichero es el **único** que declara `ASPNETCORE_ENVIRONMENT=Production` **[V]** (`:12`), así que es el único despliegue por Compose al que la guarda de D8 le aplica. Con el `:-` intacto, la ausencia de la variable del host se traduciría en `admin@ludeka.es` hacia el proceso y la guarda nunca dispararía: exactamente lo que prohíbe el cuarto escenario de la especificación (`specs/production-auth-bootstrap/spec.md:34-39`).

**Alcance del colateral, medido [V]:**
- `docker-compose.yml:16` arranca en `Staging` por defecto (`ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT:-Staging}`), precisamente porque INC-48 lo ajustó. `docker compose up` local **no se ve afectado**.
- `docker-compose.staging.yml` tampoco: su entorno no es `Production`.
- `AdminUser__Id`, `AdminUser__UserName` y `AdminUser__Country` (`prod.yml:17,18,20`) **conservan** sus `:-`. La especificación solo exige el correo, y son los tres campos que el sembrador puede rellenar con respaldo sin consecuencia irreversible de acceso.

### D10 · Aviso agregado de «cero proveedores utilizables»

**Decisión [R]:** nueva función pura en `ExternalAuthenticationSchemes`, **sin tocar `GetConfigurationWarnings`**:

```csharp
public static AuthenticationStartupNotice? GetNoUsableProviderNotice(
    AuthenticationOptions options, string? environmentName);

public sealed record AuthenticationStartupNotice(LogLevel Level, string Message);
```

Devuelve `null` cuando algún proveedor cumple `IsUsable` **[V]** (`AuthenticationOptions.cs:69`); en caso contrario, un aviso con `LogLevel.Error` si el entorno es `Production` (comparación insensible a mayúsculas, como en las dos guardas existentes) y `LogLevel.Warning` en el resto.

**Por qué una función nueva y no ampliar la existente.** `GetConfigurationWarnings` devuelve `IReadOnlyList<string>` y su llamada actual está fijada por una prueba vigente que trata cada elemento como cadena: `Assert.Contains(warnings, warning => warning.Contains("Discord", ...))` **[V]** (`WebAuthenticationRegistrationTests.cs:75-76`). Cambiar el tipo de retorno para llevar severidad **rompería la compilación de esa prueba**, y añadir el mensaje agregado a la misma lista lo emitiría como `LogWarning` también en `Production` (`Program.cs:126-129`), incumpliendo el requisito. Una función hermana deja las pruebas existentes intactas y hace la severidad comprobable en una prueba unitaria pura, que es lo que pide `strict_tdd: true` **[V]** (`openspec/config.yaml:12`).

**Por qué un `record` con `LogLevel` y no `string?` con la severidad decidida en `Program.cs`.** Si la severidad se decide en `Program.cs`, la única prueba posible es leer el fuente y comprobar que la palabra `LogError` aparece, lo cual verifica la ortografía y no el comportamiento. Llevándola en el valor de retorno, los tres escenarios del delta de `social-login-authentication` (`specs/social-login-authentication/spec.md:11-27`) se prueban como aserciones sobre datos. `AuthenticationStartupNotice` se declara al final de `ExternalAuthenticationSchemes.cs`, junto a `ExternalProviderRegistration` (`:218-223`), que ya es un `record` público en ese mismo fichero.

**Dónde se emite [R]:** en `Program.cs`, inmediatamente después del bucle de avisos por proveedor (`:126-129`) y antes de los avisos de medios (`:133-137`). Firma verificada en el pack instalado: `LoggerExtensions.Log(ILogger, LogLevel, string, object[])` **[V]** (`Microsoft.Extensions.Logging.Abstractions.xml:768`).

```csharp
var noProviderNotice = ExternalAuthenticationSchemes.GetNoUsableProviderNotice(
    authenticationOptions, app.Environment.EnvironmentName);
if (noProviderNotice is not null)
{
    app.Logger.Log(noProviderNotice.Level, "{AuthenticationNotice}", noProviderNotice.Message);
}
```

**Mensaje [R]**, distinguible del aviso por proveedor individual (`ExternalAuthenticationSchemes.cs:117-119`) porque abre con la condición agregada y nombra las claves:

> «Ningún proveedor de autenticación social está operativo: Google, Discord y Facebook están deshabilitados o sin credenciales completas. Nadie puede iniciar sesión, incluido el Administrador Fundador. Revisa `Authentication:Providers:{Proveedor}:Enabled` y sus `ClientId`/`ClientSecret`.»

**Alternativa descartada:** abortar el arranque, como en D8. El catálogo público se lee sin sesión; tumbar el sitio entero por un fallo de inicio de sesión es desproporcionado y, a diferencia de B3, el estado es **reversible con un redespliegue**. La asimetría de severidad entre D8 (aborta) y D10 (registra) es deliberada y sigue el criterio de reversibilidad que INC-48 ya aplicó entre su guarda de persistencia y su aviso de medios.

### D11 · Reparto de claves entre `env_vars` y `secrets` en `ci-cd.yml`

**El criterio no se inventa: se lee del fichero [V]** (`ci-cd.yml:108-129`) y de su documentación **[V]** (`google-cloud-run.md:163`). Para Cloudflare R2, a `env_vars` van los valores operativos y públicos (`BucketName`, `PublicCdnBaseUrl`, `Simulate`) y a `secrets` **las tres piezas del par de credenciales, incluida la mitad identificadora** (`AccountId`, `AccessKeyId`, `SecretAccessKey`). Los secretos de GitHub quedan reservados al acceso y la topología del despliegue (`GCP_PROJECT_ID`, `GCP_SA_KEY`, `GCP_REGION`, `GCP_SERVICE_NAME`) **[V]** (`:64-106`), y Secret Manager a la configuración de la aplicación.

**Aplicando ese mismo criterio [R]:**

| Clave de configuración | Bloque | Entrada de Secret Manager | Razón |
|---|---|---|---|
| `Authentication__Providers__{P}__Enabled` | `env_vars` | — | Booleano operativo, sin valor que proteger. Mismo trato que `Cloudflare__Simulate` |
| `Authentication__Providers__Google__ClientId` | `secrets` | `GOOGLE_OAUTH_CLIENT_ID` | Mitad identificadora de un par de credenciales: mismo trato que `Cloudflare__AccessKeyId` **[V]** (`ci-cd.yml:128`). Además evita fijarlo en claro en un fichero versionado |
| `Authentication__Providers__Google__ClientSecret` | `secrets` | `GOOGLE_OAUTH_CLIENT_SECRET` | Secreto puro |
| `Authentication__Providers__Discord__ClientId` | `secrets` | `DISCORD_OAUTH_CLIENT_ID` | Ídem. El sufijo `OAUTH` evita la colisión de lectura con `DISCORD_WEBHOOK_URL` **[V]** (`ci-cd.yml:124`), que es de notificaciones y no tiene nada que ver |
| `Authentication__Providers__Discord__ClientSecret` | `secrets` | `DISCORD_OAUTH_CLIENT_SECRET` | Secreto puro |
| `Authentication__Providers__Facebook__AppId` | `secrets` | `FACEBOOK_APP_ID` | `AppId` es el `ClientId` de Facebook **[V]** (`AuthenticationOptions.cs:59`, `EffectiveClientId`) |
| `Authentication__Providers__Facebook__AppSecret` | `secrets` | `FACEBOOK_APP_SECRET` | Secreto puro |
| `AdminUser__Email` | `secrets` | `ADMIN_USER_EMAIL` | No es un secreto técnico: es un **dato personal** del fundador. En `env_vars` quedaría en claro en un fichero versionado y público. Minimización de datos y coherencia con el almacén ya elegido |
| `AdminUser__Id` / `UserName` / `Country` | No se cablean | — | Sus valores por defecto de código son aceptables **[V]** (`AdminUserOptions.cs:13-28`) y no son irreversibles en términos de acceso |
| `Authentication__Cookie__ExpireMinutes` | No se cablea | — | 43200 por defecto **[V]** (`appsettings.json:26`) |

**Qué proveedores entran en el primer despliegue: decisión de producto pendiente**, `proposal.md` §10 pregunta 1. El diseño fija la **forma** y **recomienda [R] Google + Discord**, dejando Facebook fuera del primer despliegue porque la revisión de aplicaciones de Meta es un trámite externo ya declarado fuera de alcance (`proposal.md:66`). Con dos proveedores se cumple el requisito de «al menos un proveedor» (`specs/production-auth-bootstrap/spec.md:47-51`) y se conserva una segunda vía de acceso si una consola falla el día del despliegue. Añadir Facebook después son tres líneas y dos entradas de Secret Manager; el procedimiento se documenta una sola vez en §10.3 (D14).

Bloques resultantes, a insertar al final de `env_vars` (tras `ci-cd.yml:118`) y de `secrets` (tras `:129`):

```yaml
          env_vars: |
            ...
            Authentication__Providers__Google__Enabled=true
            Authentication__Providers__Discord__Enabled=true
          secrets: |
            ...
            Authentication__Providers__Google__ClientId=GOOGLE_OAUTH_CLIENT_ID:latest
            Authentication__Providers__Google__ClientSecret=GOOGLE_OAUTH_CLIENT_SECRET:latest
            Authentication__Providers__Discord__ClientId=DISCORD_OAUTH_CLIENT_ID:latest
            Authentication__Providers__Discord__ClientSecret=DISCORD_OAUTH_CLIENT_SECRET:latest
            AdminUser__Email=ADMIN_USER_EMAIL:latest
```

**Ninguna de estas claves va a los cuatro Cloud Run Jobs (`ci-cd.yml:138-155`), y el bloque `--set-secrets` de `:151` no se toca.** Cuatro razones independientes, todas verificadas:
1. Los Jobs no atienden HTTP —son ejecutables de vida corta sin servidor ni sondas **[V]** (`google-cloud-run.md:204`)—, así que no pueden recibir un retorno OAuth en `/signin-{proveedor}`.
2. `src/Ludeka.Jobs` no referencia `AuthenticationOptions`, `AdminUserOptions` ni `AddLudekaAuthentication` **[V]** (búsqueda sobre el proyecto).
3. `AdminUserSeeder.EnsureAdminUserAsync` se invoca exclusivamente desde el host web **[V]** (`Program.cs:183`); los Jobs ni siquiera migran (`google-cloud-run.md:116`).
4. Cada entrada de `--set-secrets` es un **prerrequisito duro** de despliegue: exige que el secreto exista en Secret Manager y que la cuenta de servicio tenga `roles/secretmanager.secretAccessor` **[V]** (`google-cloud-run.md:162`). Añadir cinco secretos inútiles a cuatro Jobs son veinte formas nuevas de que falle el primer despliegue a cambio de cero funcionalidad.

**Consecuencia operativa que debe quedar escrita [R]:** cada clave del bloque `secrets:` se convierte en prerrequisito del primer despliegue. **Que Cloud Run rechace un despliegue que referencia un secreto inexistente es `[NV]`** (§7, H3), coherente con el hueco que el propio fichero ya declara (`ci-cd.yml:95-99`). De ahí la lista explícita de entradas a crear **antes** del paso 4 de §9.0, en la nueva §10.3.

**Alternativa descartada:** secretos de GitHub interpolados dentro de `env_vars`. Funciona, pero parte la configuración de la aplicación entre dos almacenes y rompe el criterio vigente del repositorio, que reserva los secretos de GitHub para acceso y topología.

### D12 · Prueba de contrato sobre `ci-cd.yml`: idioma nuevo, técnica conocida

**Decisión [R]:** nueva clase `tests/Ludeka.UnitTests/Deployment/CiCdWorkflowContractTests.cs`, que lee `.github/workflows/ci-cd.yml` **como texto**.

Hoy ninguna prueba lee ficheros de `.github/workflows/` **[V]** (búsqueda sobre `tests/`), así que el idioma es nuevo; la **técnica** no lo es: es exactamente `ReadSource` + `GetRepoRoot` de `AuthorizationPipelineContractTests.cs:170-187`, que localiza la raíz subiendo hasta encontrar `Ludeka.sln`. Se reutiliza ese mismo par de ayudantes.

**Se lee como texto, no se analiza el YAML.** Añadir un analizador sintáctico de YAML sería una dependencia nueva para comprobar presencia de cadenas, y el maintainer ya descartó una dependencia menor que esa. Los bloques `env_vars: |` y `secrets: |` son bloques literales de texto, así que el contenido a comprobar son líneas planas.

**Delimitación de los bloques [R]:** el fichero contiene dos configuraciones de despliegue —el servicio web (`:100-129`) y los Jobs (`:138-155`)—. La prueba acota el paso web tomando la subcadena desde `env_vars: |` hasta `secrets: |`, y desde `secrets: |` hasta el siguiente `      - name:`. Los asertos concretos están en §4.4.

### D13 · Arnés de host mínimo para cabeceras reenviadas

**Decisión [R]:** `ForwardedHeadersHostHarness`, clase privada anidada dentro del nuevo fichero de pruebas, calcada del precedente `MinimalMediaHostHarness` (`MediaStaticFilesDeliveryTests.cs:166-204`): `WebApplication.CreateBuilder(...)`, `Logging.ClearProviders()`, `WebHost.UseUrls("http://127.0.0.1:0")`, `StartAsync()`, `HttpClient` contra `app.Urls.First()`, `IAsyncDisposable`.

**Por qué el precedente basta y no hace falta `Microsoft.AspNetCore.TestHost`:** la contingencia ya está resuelta y documentada en el propio repositorio. `Ludeka.UnitTests.csproj` referencia `Ludeka.Web.csproj` (SDK `Microsoft.NET.Sdk.Web`) **[V]** (`.csproj:25`), y esa referencia arrastra el `FrameworkReference` implícito a `Microsoft.AspNetCore.App` lo bastante para que `WebApplication.CreateBuilder` resuelva y arranque, **sin añadir paquete alguno** **[V]** (documentado en `MediaStaticFilesDeliveryTests.cs:26-31` y confirmado por su ejecución). Un precedente propio que ya compila y pasa vale más que un paquete nuevo.

**La pieza que el precedente no tiene, y que decide todo el incremento.** Las especificaciones exigen que la petición llegue «desde una dirección remota **que no es de bucle invertido**» (`specs/reverse-proxy-forwarded-headers/spec.md:19,25,32,62`). Un `HttpClient` contra `http://127.0.0.1:{puerto}` produce una `RemoteIpAddress` **de bucle invertido**. Si el comportamiento por defecto de las listas de confianza es el que se sospecha —confiar en el bucle invertido, lo que sigue siendo `[NV]` por D4—, el **caso negativo saldría verde por el motivo equivocado o directamente en rojo**, y la prueba más valiosa del incremento no probaría nada.

**Solución [R]:** un middleware de una línea que fija la dirección remota **antes** de `UseForwardedHeaders`, dentro del arnés:

```csharp
app.Use(async (context, next) =>
{
    context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10"); // TEST-NET-3, RFC 5737
    await next();
});
app.UseForwardedHeaders(options);   // options: las de producción, o las del caso negativo
```

`ConnectionInfo.RemoteIpAddress` es **asignable**: «Gets or **sets** the IP address of the remote target» **[V]** (`Microsoft.AspNetCore.Http.Abstractions.xml:60-62`). El rango `203.0.113.0/24` es el bloque de documentación TEST-NET-3 de RFC 5737: nunca enrutable, nunca privado, nunca de bucle invertido, y estable en cualquier máquina y en CI. Se elige por encima de las dos alternativas siguientes.

**Alternativa descartada:** vincular Kestrel a una IP de la red local real de la máquina para que la conexión no sea de bucle invertido. Depende de que exista una interfaz de red adecuada, del cortafuegos y del contenedor de CI. Sería una prueba que falla por el entorno, no por el código.

**Alternativa descartada:** aceptar el bucle invertido y probar solo el caso positivo. Renuncia exactamente a la prueba que convierte la suposición de B1 en conocimiento, que es la razón de ser del incremento.

**El arnés recibe las opciones como parámetro**, para poder arrancar la misma tubería con `ForwardedHeadersConfiguration.Build()` (caso real) o con unas opciones sin vaciar las listas (caso negativo). Punto final de la tubería: un manejador que responde `Request.Scheme` y `Request.Host` en el cuerpo.

**Variante con autenticación**, solo para el requisito del `redirect_uri` (`specs/reverse-proxy-forwarded-headers/spec.md:54-64`): añade `services.AddLudekaAuthentication(options)` con un Google utilizable (el patrón exacto de `WebAuthenticationRegistrationTests.OptionsWithUsableGoogle`, `:36-41`), `app.UseAuthentication()` y un punto final que devuelve `Results.Challenge(..., [GoogleDefaults.AuthenticationScheme])`. El `HttpClient` se construye con `AllowAutoRedirect = false` para poder leer la cabecera `Location` del desafío y extraer de ella el parámetro `redirect_uri`.

**Data Protection efímera [R]:** el desafío OAuth protege el parámetro `state` con Data Protection, que por defecto persiste claves en disco —motivo, entre otros, por el que el repositorio descartó `WebApplicationFactory<Program>` **[V]** (`MediaStaticFilesDeliveryTests.cs:32-38`)—. El arnés lo evita con `builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider()`, método verificado en el pack instalado **[V]** (`Microsoft.AspNetCore.DataProtection.xml:907`), de modo que la prueba no escribe nada fuera de su proceso.

### D14 · Documentación: completar §9.0 sin renumerarla

**Decisión [R]:** no se modifica ningún número de paso existente de `google-cloud-run.md` §9.0 (`:206-218`). Su numeración **se referencia desde fuera**: el propio documento remite a «§9.0, paso 1» (`:116`) y «§9.0, paso 5» (`:185`), y `ROADMAP.md:73` enlaza la sección entera **[V]**. Se intercalan pasos con nomenclatura propia —`1-bis` y `3-bis`— y se añade una sección `## 10` nueva. El desglose completo está en §6.

**Corrección no prevista por la propuesta, detectada en esta fase [V]:** `google-cloud-run.md:115` afirma hoy que el primer arranque contra Supabase crea «el **usuario Administrador Fundador inicial permanente** (`admin-fundador` / `admin@ludeka.es`)». **Tras D8 y D9 esa frase pasa a ser falsa**: en `Production` sin `AdminUser__Email` el proceso aborta, y con él informado el correo será otro. Entra en el alcance de la rebanada de documentación.

---

## 3. Cambios por fichero

| Fichero | Acción | Qué cambia | Decisión |
|---|---|---|---|
| `src/Ludeka.Web/ForwardedHeadersConfiguration.cs` | **Nuevo** | Función pura `Build()` que devuelve el `ForwardedHeadersOptions` de producción | D2, D3, D4, D5 |
| `src/Ludeka.Web/Program.cs` | Modificado | (a) `app.UseForwardedHeaders(ForwardedHeadersConfiguration.Build())` insertado en `:196`, antes del bloque de no-desarrollo; (b) segunda guarda de arranque tras `:123`; (c) emisión del aviso agregado tras `:129` | D1, D8, D10 |
| `src/Ludeka.Web/WebStartupGuards.cs` | Modificado | Nuevo método `EvaluateAdminUserIdentity(IConfiguration, string?)`. `Evaluate` intacto | D8 |
| `src/Ludeka.Web/Authentication/ExternalAuthenticationSchemes.cs` | Modificado | Nuevo `GetNoUsableProviderNotice(...)` y `record AuthenticationStartupNotice`. `GetConfigurationWarnings` intacto | D10 |
| `src/Ludeka.Web/appsettings.json` | Modificado | `:21` `"Email": "admin@ludeka.es"` ➔ `"Email": ""` | D8 |
| `src/Ludeka.Infrastructure/Options/AdminUserOptions.cs` | **Sin cambios** | Su valor por defecto en C# se conserva: lo fija `DatabaseProviderTests.cs:74-80` y la guarda no lo lee | D8 |
| `src/Ludeka.Infrastructure/Seeding/AdminUserSeeder.cs` | **Sin cambios** | Su respaldo de `:41` sigue sirviendo a los entornos no productivos | D8 |
| `docker-compose.prod.yml` | Modificado | `:19` pierde el `:-admin@ludeka.es` | D9 |
| `.github/workflows/ci-cd.yml` | Modificado | Dos líneas en `env_vars` (tras `:118`) y cinco en `secrets` (tras `:129`), solo en el paso del servicio web. El paso de los Jobs (`:138-155`) **no se toca** | D11 |
| `tests/Ludeka.UnitTests/Web/ForwardedHeadersConfigurationTests.cs` | **Nuevo** | Pruebas unitarias puras de las opciones construidas | §4.2 |
| `tests/Ludeka.UnitTests/Web/ForwardedHeadersPipelineTests.cs` | **Nuevo** | Arnés de host mínimo + casos positivo, **negativo**, sin proxy, host suplantado, `redirect_uri` y no-redirección | §4.3, D13 |
| `tests/Ludeka.UnitTests/Web/WebStartupGuardsTests.cs` | Modificado | Bloque nuevo para `EvaluateAdminUserIdentity`. Las seis pruebas existentes no se tocan | §4.5 |
| `tests/Ludeka.UnitTests/Web/WebAuthenticationRegistrationTests.cs` | Modificado | Tres pruebas nuevas para el aviso agregado. Las seis existentes no se tocan | §4.6 |
| `tests/Ludeka.UnitTests/Web/AuthorizationPipelineContractTests.cs` | Modificado | Un aserto nuevo de orden del pipeline. El vigente (`:37-39`) no se toca | §4.1 |
| `tests/Ludeka.UnitTests/Deployment/CiCdWorkflowContractTests.cs` | **Nuevo** | Contrato de texto sobre `ci-cd.yml` | §4.4, D12 |
| `docs/deployment/google-cloud-run.md` | Modificado | Nueva `## 10` (cinco subsecciones); pasos `1-bis` y `3-bis` en §9.0 sin renumerar; corrección de `:115` | D14, §6 |
| `docs/increments/ROADMAP.md` | Modificado | Corrección de `:75`; fila de INC-52 en la tabla; entrada en «Incrementos en Curso» | §6 |
| `docs/increments/inc-52-autenticacion-en-el-despliegue.md` | **Nuevo** | Documento de incremento que la fila del ROADMAP enlaza, por convención de `AGENTS.md` §1.5 | §6 |
| `openspec/changes/change-52-*/tasks.md` | **Nuevo** | Lo produce `sdd-tasks` | — |

**No se añade ninguna dependencia de paquete.** `tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj` **no se modifica** (D13), lo que retira del alcance la línea que `proposal.md:221` preveía y cierra su pregunta abierta número 6.

### 3.1. Reparto en rebanadas

Se conserva la cadena de cuatro PRs de `proposal.md` §7, con un ajuste: el arnés de D13 añade trabajo de pruebas a la rebanada de B1.

| PR | Contenido | Autónoma porque… |
|---|---|---|
| **#1 Contrato** | `spec.md`, `design.md`, `tasks.md` | No toca código |
| **#2 B1** | `ForwardedHeadersConfiguration.cs`, `Program.cs` (solo el middleware), arnés y las seis pruebas de cabeceras, aserto de orden del pipeline | Corrige el defecto más arriesgado y lo acredita en la misma rebanada. Si `sdd-tasks` la pronostica por encima del presupuesto, el corte natural es «función pura + prueba de contrato de orden» / «arnés + pruebas de tubería» |
| **#3 B3 + B4** | `WebStartupGuards.cs`, `appsettings.json`, `ExternalAuthenticationSchemes.cs`, `Program.cs` (guarda y aviso), `docker-compose.prod.yml` y sus pruebas | Guarda y aviso son la misma familia de diagnóstico de arranque y comparten punto de emisión. El ajuste de Compose viaja con la guarda que lo obliga |
| **#4 B2 + documentación + B5** | `ci-cd.yml`, su prueba de contrato, `google-cloud-run.md`, `ROADMAP.md`, `inc-52-*.md` | Configuración declarativa y prosa. Va la última porque documenta el comportamiento que fijan #2 y #3 |

**Los cuatro deben estar fusionados antes de configurar `GCP_PROJECT_ID` y `GCP_SA_KEY`.** Ese es el gate real del incremento. El pronóstico formal de las 400 líneas lo emite `sdd-tasks`.

---

## 4. Estrategia de pruebas

`strict_tdd: true` **[V]** (`openspec/config.yaml:12`): toda prueba de esta sección se escribe en ROJO antes de su código de producción. Orden de ejecución: `dotnet test Ludeka.sln` **[V]** (`config.yaml:14`).

| Capa | Qué se prueba | Cómo |
|---|---|---|
| Contrato de fuente | Orden del pipeline; claves del flujo de CI/CD | Lectura de texto con `ReadSource`/`GetRepoRoot` |
| Unitaria pura | Opciones construidas; guarda de identidad; aviso agregado | xUnit sin host ni contenedor |
| Tubería HTTP real | Reescritura del esquema, caso negativo, host, `redirect_uri`, no-redirección | Arnés de host mínimo con Kestrel en puerto efímero (D13) |
| Regresión | 1.565 unitarias + 10 de integración existentes **[V]** (`ROADMAP.md:76`) | `dotnet test Ludeka.sln` |

### 4.1. Contrato del pipeline (P1)

Aserto nuevo en `AuthorizationPipelineContractTests`, junto al método `Pipeline_ShouldAuthenticateAndAuthorizeBeforeAntiforgery` (`:29-43`) y con su misma técnica de índices:

- `app.UseForwardedHeaders(` existe en `Program.cs`.
- Su índice es **menor** que el de `app.UseHttpsRedirection();`.
- Su índice es **menor** que el de `app.UseExceptionHandler(`, lo que fija que va antes del bloque de no-desarrollo (D1).
- El aserto vigente `https >= 0` (`:39`) se conserva sin tocar: nadie puede retirar `UseHttpsRedirection()` (D7).

### 4.2. Opciones construidas (P2–P5), prueba unitaria pura

Sobre `ForwardedHeadersConfiguration.Build()`, sin host:

| # | Aserto | Requisito |
|---|---|---|
| P2 | `ForwardedHeaders == ForwardedHeaders.XForwardedProto` (igualdad exacta, no `HasFlag`, para que añadir una bandera por descuido rompa la prueba) | D2 |
| P3 | `KnownProxies` y `KnownIPNetworks` vacías | D4 |
| P4 | `ForwardLimit == 1` | D5 |
| P5 | La bandera **no** incluye `XForwardedHost` ni `XForwardedFor` | D2, `spec:42-46` |

### 4.3. Tubería HTTP real (N1–N6), con el arnés de D13

Todas con dirección remota forzada a `203.0.113.10` (no bucle invertido).

| # | Escenario de la especificación | Montaje | Aserto |
|---|---|---|---|
| **N1** | «Proxy no loopback corrige el esquema a `https`» (`spec:17-21`) | Opciones de producción; petición con `X-Forwarded-Proto: https` | El cuerpo devuelve `https` |
| **N2** | **CASO NEGATIVO** — «Sin vaciar las listas de confianza, el esquema no cambia» (`spec:23-28`) | `new ForwardedHeadersOptions { ForwardedHeaders = XForwardedProto }`, **listas de confianza intactas**; misma petición | El cuerpo devuelve `http` |
| **N3** | «Sin proxy delante, el comportamiento local no cambia» (`spec:36-40`) | Opciones de producción; petición **sin** la cabecera | El cuerpo devuelve `http` |
| **N4** | Cierre empírico de D7 | Tubería con `UseForwardedHeaders` **y** `UseHttpsRedirection()`; petición sin `X-Forwarded-Proto`; cliente con `AllowAutoRedirect = false` | La respuesta **no** es 307 ni 308 |
| **N5** | «`X-Forwarded-Host` no altera el host» (`spec:48-52`) | Opciones de producción; petición con `X-Forwarded-Proto: https` **y** `X-Forwarded-Host: dominio-suplantado.ejemplo` | El host del cuerpo es `127.0.0.1:{puerto}`, no el suplantado |
| **N6** | «`redirect_uri` en `https`» (`spec:60-64`) | Arnés con Google utilizable y Data Protection efímera; desafío con `X-Forwarded-Proto: https` | El `redirect_uri` de la cabecera `Location` empieza por `https://` y termina en `/signin-google` **[V]** (`ExternalAuthenticationSchemes.cs:35`) |

**N2 es la prueba de mayor valor del incremento.** Es la que convierte el supuesto de D4 en conocimiento. Su regla de decisión está en §7 (H4): **no se puede reescribir para que pase**.

El escenario «Mismo comportamiento en un entorno distinto de Production» (`spec:30-34`) se cubre parametrizando N1 con `EnvironmentName` en `Development`, `Staging` y `Production`: el arnés ya recibe ese valor en `WebApplicationOptions`, igual que el precedente (`MediaStaticFilesDeliveryTests.cs:180-184`).

El escenario «Desarrollo local sin proxy sigue construyendo el `redirect_uri` en `http`» (`spec:66-70`) es la variante de N6 sin cabecera.

### 4.4. Contrato de `ci-cd.yml` (Y1–Y5)

| # | Aserto | Requisito |
|---|---|---|
| Y1 | El bloque `env_vars` del paso web contiene al menos un `Authentication__Providers__{P}__Enabled=true` | `spec production-auth-bootstrap:53-57` |
| Y2 | Para ese mismo `{P}`, el bloque `secrets` contiene su identificador (`ClientId` o `AppId`) y su secreto (`ClientSecret` o `AppSecret`), ambos con sufijo `:latest` | Ídem |
| Y3 | El bloque `secrets` contiene una línea `AdminUser__Email=` | `spec:41-45` |
| Y4 | El bloque `env_vars` **no** contiene ninguna aparición de `ClientSecret`, `AppSecret` ni `AdminUser__Email` | `spec:59-63` |
| Y5 | El bloque de los cuatro Cloud Run Jobs (`:138-155`) **no** contiene `Authentication__` ni `AdminUser__` | D11 (pin de regresión hacia adelante) |

### 4.5. Guarda de identidad (G1–G5), calcadas de `WebStartupGuardsTests`

Mismo `BuildConfiguration` con `AddInMemoryCollection` que las existentes (`WebStartupGuardsTests.cs:19-28`).

| # | Entrada | Aserto | Escenario |
|---|---|---|---|
| G1 | `Production` + `AdminUser:Email = "fundador@ejemplo.com"` | `null` | `spec:15-19` |
| G2 | `Production` sin la clave | No nulo y **contiene la cadena `AdminUser:Email`** | `spec:21-26` |
| G3 | `Production` + `AdminUser:Email = "   "` | No nulo | `spec:21-26` |
| G4 | `Development` y `Staging` sin la clave | `null` en ambos | `spec:28-32` |
| G5 | `"production"` en minúsculas, sin la clave | No nulo (comparación insensible, como `WebStartupGuardsTests.cs:78-87`) | Coherencia con la guarda hermana |

**G6, prueba de fichero real:** cargar `src/Ludeka.Web/appsettings.json` con `ConfigurationBuilder` —técnica ya usada en `WebAuthenticationRegistrationTests.cs:125-127`— y comprobar que la configuración empaquetada por sí sola, con entorno `Production`, **activa** la guarda. Es la prueba que liga D8 con el vaciado de `appsettings.json:21` y evita que uno se haga sin el otro.

### 4.6. Aviso agregado (A1–A3)

| # | Entrada | Aserto | Escenario |
|---|---|---|---|
| A1 | `Production` + tres proveedores no utilizables | No nulo y `Level == LogLevel.Error` | `spec social-login:11-15` |
| A2 | `Development` + tres proveedores no utilizables | No nulo y `Level == LogLevel.Warning` | `spec:17-21` |
| A3 | `Production` + Google utilizable | `null`; y `GetConfigurationWarnings` sigue devolviendo sin cambios los avisos por proveedor individual | `spec:23-28` |

Las seis pruebas vigentes de `WebAuthenticationRegistrationTests` no se modifican; en particular `EnabledProviderWithoutCredentials_...` (`:62-77`) sigue llamando a `GetConfigurationWarnings(options)` con un solo argumento, que es la razón de D10.

### 4.7. Qué NO queda probado

Está en §7. En resumen: nada de lo que dependa de un despliegue real, porque **no existe entorno de producción**.

---

## 5. Matriz de amenazas y seguridad

### 5.1. Matriz de referencia de la fase

El cambio toca **enrutamiento de peticiones HTTP** (orden de la tubería de middleware), por lo que se evalúa la matriz de `references/threat-matrix.md`. Ninguna de sus cinco filas resulta aplicable: describen fronteras de órdenes de shell, selección de repositorio Git, estado de índice, estado de push y composición de órdenes de PR, y este incremento no introduce ninguna.

| Frontera de la matriz de referencia | Aplicabilidad | Motivo |
|---|---|---|
| Rutas con aspecto de documentación | **N/A** | No se clasifica ni ejecuta ningún fichero por su nombre o extensión |
| Selección de repositorio Git | **N/A** | No se invoca `git` desde el código ni desde las pruebas |
| Estado del índice de commits | **N/A** | Sin automatización de control de versiones |
| Estado de push | **N/A** | Sin automatización de push. `ci-cd.yml` se modifica como datos, no se ejecuta en las pruebas |
| Órdenes de PR | **N/A** | Sin composición de órdenes de PR |

### 5.2. Matriz propia del incremento

La frontera real que abre este diseño es **aceptar una cabecera de petición controlable por el emisor**. Se modela aquí, con conducta segura y prueba asignada.

| # | Amenaza | Aplicabilidad | Conducta esperada | Prueba |
|---|---|---|---|---|
| **A1** | Un cliente que alcanza el contenedor **sin pasar por el proxy** envía `X-Forwarded-Proto: https` sobre HTTP plano | **Aplicable** | Se acepta a sabiendas. El único efecto es que la aplicación cree hablar HTTPS con **ese mismo cliente**: construye enlaces `https` y marca su propia cookie como `Secure`. No degrada el cifrado de nadie más, no eleva privilegios y no cruza a otra sesión | N1 documenta el mecanismo; el análisis está en §5.3 |
| **A2** | El mismo cliente envía `X-Forwarded-Proto: http` para **rebajar** el esquema | **Aplicable** | Solo se perjudica a sí mismo: sus propios enlaces salen en `http` y su cookie `Secure` puede no emitirse. No afecta a terceros. La cookie de sesión conserva `SecurePolicy = Always` **[V]** (`ExternalAuthenticationSchemes.cs:54`) | N3 acredita la ausencia de cabecera; la rebaja explícita es la misma ruta de código |
| **A3** | **Suplantación de host** para falsificar los enlaces que genera el servicio | **Aplicable** | **Bloqueada por diseño:** `XForwardedHost` no se procesa (D2). Es la amenaza de la que advierte literalmente la documentación del marco **[V]** (`HttpOverrides.xml:143`) | **N5** |
| **A4** | Falsificación de la IP del cliente vía `X-Forwarded-For` para saltarse un control basado en origen | **Aplicable, con riesgo actual nulo** | **Bloqueada por diseño:** `XForwardedFor` no se procesa (D2). Y hoy no hay nada que engañar: **cero lecturas de `RemoteIpAddress` en todo `src/`** **[V]** | **P5** (la bandera no está); la ausencia de lectores queda anclada en la nueva §10.5 |
| **A5** | **Bucle de redirección infinito** por `UseHttpsRedirection()` (`Program.cs:203`) | **Aplicable** | No se produce: el middleware no resuelve puerto HTTPS y se apaga **[V]** (§D7). Fijar `HTTPS_PORT` sin corregir B1 sería la receta del bucle; corregido B1, deja de serlo | **N4** |
| **A6** | La guarda de D8 aborta un arranque legítimo y deja el sitio caído | **Aplicable** | Solo dispara en `Production` y solo por ausencia de una clave. El coste colateral está medido: una línea de `docker-compose.prod.yml` (D9). `docker-compose.yml` arranca en `Staging` **[V]** (`:16`) | **G1, G4, G5** |
| **A7** | Fuga del dato personal del fundador a un fichero versionado y público | **Aplicable** | `AdminUser__Email` viaja por Secret Manager, nunca por `env_vars` (D11) | **Y3, Y4** |
| **A8** | Secreto OAuth en claro en el repositorio | **Aplicable** | Todos los `ClientSecret`/`AppSecret` van a `secrets:` | **Y2, Y4** |
| **A9** | Elevación de privilegios por sembrado del fundador con un correo ajeno | **Aplicable** | La guarda de D8 lo impide en `Production`. Fuera de `Production` el respaldo sigue vivo, que es el comportamiento exigido **[V]** (`spec:28-32`) | **G2, G3, G6** |

### 5.3. El riesgo de vaciar las listas de confianza, valorado explícitamente

Es la pregunta de fondo del incremento, y merece respuesta directa en vez de una tabla.

**Qué protege realmente al contenedor.** Con `--allow-unauthenticated` **[V]** (`ci-cd.yml:107`), el servicio acepta peticiones anónimas. Pero el contenedor **no tiene dirección propia alcanzable**: Cloud Run no expone el contenedor, expone el servicio a través de su propio front-end, y lo que llega al proceso ya pasó por él. **Que no exista ninguna vía de alcanzar el contenedor saltándose ese front-end es `[NV]`** (§7, H6): es lo esperable del modelo de la plataforma, pero este diseño no lo cita como hecho oficial, por la misma disciplina que aplica a H1.

**Por qué el diseño aguanta aunque ese supuesto fuera falso.** Es la parte importante: la mitigación no descansa en que el contenedor sea inalcanzable, sino en **limitar qué se puede hacer con la cabecera aceptada**.

1. Solo se procesa `XForwardedProto` (D2). La única mentira posible es sobre el esquema de la propia conexión del atacante.
2. Declarar `https` siendo `http` (A1) no rompe ninguna garantía para terceros: la aplicación no toma decisiones de autorización a partir de `Request.Scheme`, no lo usa para elegir claves, y la política `Secure` de la cookie es incondicional (`ExternalAuthenticationSchemes.cs:54`).
3. Declarar `http` siendo `https` (A2) solo se perjudica a sí mismo.
4. Las dos cabeceras con las que de verdad se suplantan enlaces y controles —`XForwardedHost` (A3) y `XForwardedFor` (A4)— **no se procesan**.

**¿Procede acotar el *ingress* del servicio?** Cloud Run ofrece restringir el *ingress*, pero Ludeka es un sitio público cuyo catálogo se lee sin sesión: la única configuración compatible con el producto es la que ya usa el pipeline. **No procede acotarlo, y no es donde está la mitigación** —limitar el *ingress* a tráfico interno haría inaccesible el sitio, y cualquier valor intermedio no cambia nada para el tráfico público que es precisamente el vector de A1—. **Lo que sí procede, y entra en la nueva §10.5 [R]**, es dejar escrito que la garantía depende de que **todo el tráfico entre por el front-end de Cloud Run**, para que quien algún día exponga el contenedor por otra vía —una VPC, un puerto adicional, un despliegue paralelo— sepa que está tocando esta decisión.

---

## 6. Documentación a producir

### 6.1. `docs/deployment/google-cloud-run.md`

**Regla dura: no se renumera §9.0.** Sus pasos se citan desde `:116`, desde `:185` y desde `ROADMAP.md:73` **[V]**.

**Pasos intercalados en §9.0**, con nomenclatura propia:

- **`1-bis`. Decide el correo del Administrador Fundador.** Junto al paso 1 (base de datos), porque es la misma decisión de «antes de que exista la primera fila». Debe ser un buzón **real y verificable por el proveedor social elegido**, y se guarda como secreto `ADMIN_USER_EMAIL`. Aviso de irreversibilidad y puntero a §10.4.
- **`3-bis`. Dominio, apps OAuth y claves de autenticación.** Antes del paso 4, que es el que arma el despliegue. Puntero a §10.1, §10.2 y §10.3.

**Corrección en §5, paso 3 (`:115`) [V]:** hoy dice que el primer arranque crea el fundador «(`admin-fundador` / `admin@ludeka.es`)». Pasa a describir el comportamiento real tras D8: en `Production` se exige `AdminUser__Email` explícito y el proceso aborta si falta; el correo sembrado es el que se informe, y fuera de `Production` sigue vigente el respaldo.

**Nueva sección `## 10`:**

| Subsección | Contenido | Fuente |
|---|---|---|
| **10.1** Dominio propio en Cloud Run | `gcloud domains verify DOMINIO_BASE` (paso de cuenta, independiente del servicio, se puede hacer antes del primer despliegue); `gcloud beta run domain-mappings create --service --domain --region europe-west1`; `describe` para obtener los registros DNS concretos; `A`/`AAAA` para ápex y `CNAME` para subdominio; **trampa del registro CAA** (autorizar `pki.goog` y `letsencrypt.org` o el certificado falla en silencio); plazos del certificado gestionado. **Más el razonamiento escrito de la decisión del maintainer**: Google marca esta funcionalidad como *preview* y «not recommended for production services», y se asume igualmente porque no hay tráfico, el riesgo de latencia es bajo y reversible, un balanceador global cobra exista o no tráfico y rompería el coste cero del despliegue, y la migración sigue abierta | `research-dominio-cloud-run.md` §2, §3 |
| **10.2** Registro de las tres apps OAuth | URL de retorno exactas contra el dominio elegido: `https://<dominio>/signin-google`, `/signin-discord`, `/signin-facebook` **[V]** (`ExternalAuthenticationSchemes.cs:35-37`). Orden: dominio mapeado **antes** de registrar, para no tener que retocar tres consolas después | Repositorio |
| **10.3** Entradas de Secret Manager a crear antes del paso 4 | Lista exacta con su clave de configuración de destino (tabla de D11) y el recordatorio de `roles/secretmanager.secretAccessor` **[V]** (`:162`). Más el procedimiento de **añadir un proveedor después** del primer despliegue: crear las dos entradas, añadir las dos líneas a `secrets:` y la de `Enabled` a `env_vars:` | D11 |
| **10.4** Primer acceso del Administrador Fundador | **Es el contenido que hoy no existe en ningún documento**: el sembrador crea la fila con rol `FoundingTeam` y permisos completos **sin ninguna fila en `ExternalLogins`** **[V]** (`AdminUserSeeder.cs:56-69`), y quien la activa es la rama **2a** de `ExternalLoginService.ResolveAsync` **[V]** (`openspec/specs/social-login-authentication/spec.md:54`). **El primer inicio de sesión social cuyo correo verificado coincida con `AdminUser:Email` ES la puerta de entrada**; no hay paso adicional. Condición dura: el buzón debe ser verificable por el proveedor elegido, y lo que cada uno entiende por «verificado» difiere (`email_verified` en Google, `verified` en Discord y Facebook **[V]**, `ExternalAuthenticationSchemes.cs:165,179,194`). El correo se normaliza a minúsculas al sembrar **[V]** (`AdminUserSeeder.cs:41`). Más el **camino manual de recuperación** si se sembró un correo equivocado: corregir el campo `Email` de esa fila en Supabase, o degradarla y redesplegar, en cuyo caso el sembrador **promueve** la cuenta existente con ese correo en lugar de crear otra **[V]** (`AdminUserSeeder.cs:44-53`) | D14, `proposal.md` §5 D7 |
| **10.5** Qué hace la aplicación detrás del proxy | Qué cabecera se confía y cuáles no, y por qué (D2). Que las listas de confianza van vacías a propósito y qué lo mitiga (§5.3). **`ForwardLimit = 1` porque el mapeo directo tiene un salto, y pasaría a 2 con un balanceador global** (D5). Que **nada del código lee la IP remota hoy**, y que quien la necesite deberá añadir `XForwardedFor` con su propia prueba (D2). Que la garantía descansa en que todo el tráfico entre por el front-end de Cloud Run (§5.3). Nota sobre `UseHttpsRedirection()`: por qué se conserva y por qué no produce bucle (D7) | D2, D5, D7 |

**Ubicación de §10:** al final del documento, tras §9. Sin tocar ningún encabezado existente.

### 6.2. `docs/increments/ROADMAP.md`

1. **Corrección de `:75` (defecto B5) [V]:** la nota afirma que el PR #60 «está abierto y retenido por el maintainer». Es falso: está fusionado desde el 2026-09-19, y el propio documento lo cuenta en pasado dos líneas antes (`:73`) y en `google-cloud-run.md:194`. Se sustituye por el hecho verificado y se deja el puntero al gate real, que es **de despliegue** y ya está bien redactado en `:73`.
2. **Fila de INC-52** en la tabla de incrementos, tras INC-51 (`:67`), en estado `⏳ En progreso`, enlazando `inc-52-autenticacion-en-el-despliegue.md` (`AGENTS.md` §1.5).
3. **Entrada en «Incrementos en Curso»** (`:80-92`) con worktree, rama y el gate del incremento: los cuatro PRs fusionados antes de configurar `GCP_PROJECT_ID` y `GCP_SA_KEY`.

### 6.3. `docs/increments/inc-52-autenticacion-en-el-despliegue.md`

Documento de incremento que la fila del ROADMAP enlaza, con el formato de sus hermanos (`inc-50-area-de-cuenta.md`). `sdd-archive` lo trasladará a `docs/increments/archive/` según `AGENTS.md` §1.6.

### 6.4. Fuera de esta fase

El volcado a `docs/specs/sistema/` y la actualización de su `README.md` son obligación de `sdd-archive` (`AGENTS.md` §1.6), no de este diseño.

---

## 7. Huecos de evidencia y plan de verificación

**No existe entorno de producción.** Ni proyecto de GCP, ni base de Supabase, ni dominio mapeado. **Nada de este incremento puede acreditarse contra un despliegue real**, igual que ocurrió en INC-47 e INC-48 **[V]** (`ci-cd.yml:95-99`, `google-cloud-run.md:185`). Se declara, no se disimula.

### 7.1. Lo que este incremento SÍ deja probado, sin GCP

Las pruebas P1–P5, N1–N6, Y1–Y5, G1–G6 y A1–A3 de §4, más la suite existente. En una frase: **queda probado todo el lado nuestro del salto de red, incluido el caso negativo, y ninguno del lado de la plataforma.**

### 7.2. Huecos declarados

| # | Hueco | Estatus | Plan de cierre |
|---|---|---|---|
| **H1** | **Que Cloud Run emite `X-Forwarded-Proto: https`.** La página *Container runtime contract* no lo menciona; la única cita literal es de Cloud Run functions, producto hermano **[V]** (`research:103-108`) | **[NV]** — no se cita como hecho oficial | No se cierra en este incremento. **Comprobación de coste cero en el primer despliegue, a escribir en §10.2:** pulsar el botón de acceso y mirar la barra de direcciones cuando el navegador llegue a la pantalla de consentimiento del proveedor — el parámetro `redirect_uri` viaja visible en la URL. Si empieza por `https`, H1 queda confirmado en el acto; si empieza por `http`, el problema está en la plataforma y no en nuestra configuración, que N1 ya acreditó |
| **H2** | Si Cloud Run **añade** o **sobrescribe** `X-Forwarded-Proto` cuando el cliente ya mandó una | **[NV]** (`research:136`) | No bloquea. Solo afecta al beneficio lateral de `ForwardLimit = 1` (D5), que está declarado como plausible y no como mitigación. La matriz de §5 no se apoya en él. Se comprueba con el mismo vistazo de H1 si alguna vez importa |
| **H3** | Que Cloud Run **rechaza** un despliegue que referencia un secreto inexistente | **[NV]**, coherente con el hueco que el propio flujo ya declara **[V]** (`ci-cd.yml:95-99`) | Se mitiga documentalmente: §10.3 lista las entradas exactas a crear **antes** del paso 4 de §9.0. Se comprueba en el primer despliegue |
| **H4** | **Los valores por defecto de `KnownProxies`/`KnownIPNetworks`**, y por tanto si el middleware descarta en silencio las cabeceras de un proxy no conocido | **[NV]** — la documentación XML del pack no los publica | **Se cierra en este incremento con N2**, no con una cita. Reglas de decisión, que `sdd-apply` debe respetar: **(a)** si N2 sale verde (el esquema no cambia sin vaciar las listas), el supuesto queda confirmado y el diseño se mantiene íntegro. **(b)** si N2 sale rojo porque el esquema **sí** cambia sin vaciarlas, entonces vaciar las listas no era la corrección de B1 y **hay que parar antes de `sdd-apply` de PR #2 y volver a diseño**: la conclusión sería que el defecto está en otro sitio y que D4 sobra o es incluso contraproducente. **Bajo ningún concepto se reescribe N2 para que pase.** La prueba que descubre que el diseño estaba equivocado es la prueba que ha hecho su trabajo |
| **H5** | **`UseHttpsRedirection` en Cloud Run.** D7 lo resuelve con documentación del pack instalado más hechos del repositorio, pero eso es comportamiento **documentado**, no **observado** aquí | **[V] documental, [NV] observacional** | **Se cierra con N4** dentro de este incremento. Si N4 revelara una 307, la corrección es acotada y conocida: fijar `HttpsRedirectionOptions.HttpsPort` o condicionar la llamada, sin tocar el alcance |
| **H6** | Que no exista ninguna vía de alcanzar el contenedor saltándose el front-end de Cloud Run | **[NV]** | No se cierra. §5.3 explica por qué el diseño aguanta aunque fuera falso, y §10.5 deja escrito que la garantía depende de esa premisa |
| **H7** | Órdenes exactas, valores DNS y ventana de aprovisionamiento del certificado del mapeo de dominio | **Parcial**: las órdenes y los tipos de registro están verificados por el orquestador contra la página oficial **[V]** (`research:14-18,54-60,143-147`); los **valores** concretos los entrega Google al crear el mapeo y se consultan con `describe`; el número exacto de registros `A`/`AAAA` para el ápex y si el servicio queda inaccesible mientras se emite el certificado siguen sin confirmar **[NV]** (`research:79,95`) | §10.1 escribe lo verificado e instruye a obtener los valores con `describe`. **No se inventan valores DNS** |
| **H8** | El viaje OAuth real contra Google/Discord/Facebook con el dominio definitivo, y el sembrado del fundador sobre PostgreSQL real | **[NV]** | Solo se acredita con el primer despliegue. §10.4 describe el camino esperado para que la desviación se note |

### 7.3. Decisiones de producto pendientes que este diseño NO toma

Heredadas de `proposal.md` §10, con el mapa de bloqueo actualizado:

| Pregunta | Qué bloquea | Estado tras este diseño |
|---|---|---|
| ¿Entra Facebook en el primer despliegue? | Contenido exacto de PR #4 | **Abierta.** D11 recomienda Google + Discord y deja escrito el procedimiento de añadir el tercero después |
| ¿Cuál es el dominio definitivo? El repositorio contiene dos y no son el mismo: `https://ludeka.es` **[V]** (`appsettings.json:50`) y `https://cdn.ludeka.com` **[V]** (`ci-cd.yml:117`) | Redacción de §10.1 y §10.2, y el registro de las tres apps OAuth | **Abierta.** El diseño no la necesita: las rutas de retorno son constantes del código y el dominio es un marcador en la documentación |
| ¿De qué proveedor será el correo del fundador y está verificado en él? | Paso `1-bis` de §9.0 | **Abierta.** §10.4 explica la condición dura; el valor no entra en el repositorio |
| ¿Se acepta que la guarda aborte en `Production`? | PR #3 | **CERRADA** por el maintainer: aborta |
| ¿`AdminUser__Email` como secreto o como variable en claro? | PR #4 | **Resuelta por D11:** secreto, por minimización de dato personal y coherencia con el criterio de R2 |
| ¿Se acepta `Microsoft.AspNetCore.TestHost`? | Alcance de verificación de PR #2 | **CERRADA por el maintainer: no.** D13 la sustituye por el arnés propio y **retira la dependencia del alcance** |
| ¿Cómo se documenta el mapeo de dominio? | Bloque (a) de §10 | **CERRADA:** `sdd-research` lo resolvió y el orquestador verificó las afirmaciones pivote contra la fuente oficial |

---

## 8. Riesgos y reversión

### 8.1. Riesgos

| Riesgo | Prob. | Impacto | Mitigación |
|---|---|---|---|
| **El supuesto de D4 es falso** y vaciar las listas no era la corrección de B1 | Media | Alto: B1 seguiría vivo tras el despliegue | **N2 lo decide antes de escribir el código de producción** (`strict_tdd`). Regla de decisión escrita en H4, incluida la de parar y volver a diseño |
| H1 falso: Cloud Run no emite la cabecera | Baja | Alto: B1 seguiría vivo | Comprobación visual de coste cero en el primer despliegue (H1). Si fallara, el problema estaría en la plataforma, no en nuestra configuración, que N1 ya acreditó |
| Aceptar `X-Forwarded-Proto` de cualquier origen | Baja | Bajo-Medio | §5.3: solo se procesa el esquema; `XForwardedHost` y `XForwardedFor` quedan fuera; ninguna decisión de seguridad depende de `Request.Scheme` ni de `RemoteIpAddress` **[V]** |
| La guarda de D8 rompe un flujo no previsto | Baja | Medio | Medido: solo `docker-compose.prod.yml:19` está en `Production` **[V]**; `docker-compose.yml:16` arranca en `Staging` **[V]**. D9 lo cubre y G4 lo fija |
| Añadir claves a `secrets:` hace fallar el primer despliegue por entradas inexistentes | **Alta** | Medio | §10.3 lista las entradas exactas a crear antes del paso 4, y D11 cablea solo los proveedores acordados. Es el riesgo más probable de todo el incremento, y es **ruidoso**: falla el despliegue, no la aplicación |
| El arnés de D13 resulta inestable en CI (puerto efímero, cortafuegos) | Baja | Bajo | El precedente `MinimalMediaHostHarness` ya corre en esta misma suite con la misma técnica **[V]**. La dirección remota es forzada, no depende de la red |
| La prueba de contrato de `ci-cd.yml` (D12) se vuelve frágil al reordenar el YAML | Media | Bajo | Asertos de **presencia y ausencia dentro de un bloque acotado**, nunca de posición ni de número de línea |
| Conflictos con INC-50 e INC-51, ambos activos | Baja | Bajo | Ninguno de los ficheros de §3 aparece en el alcance declarado de INC-50 (cabecera, área de cuenta) **[NV]**, a confirmar al abrir la cadena de PRs |

### 8.2. Plan de reversión, por rebanada

- **PR #2 (B1):** revertir el PR. Añade un fichero y una línea de tubería; `Request.Scheme` vuelve a `"http"`, es decir, al estado actual. Sin migraciones ni persistencia implicadas. **Reversión limpia.**
- **PR #3 (B3 + B4):** revertir el PR restituye el valor por defecto de `appsettings.json:21` y el `:-` de `docker-compose.prod.yml:19`. La guarda es una función pura más un `throw`: **si abortó, no se sembró nada**, que es exactamente el objetivo. **Reversión limpia.**
- **PR #4 (B2 + documentación):** revertir las claves de `ci-cd.yml`. El servicio conserva su revisión anterior y Cloud Run permite dirigir el tráfico a una revisión previa **[NV]**, procedimiento estándar de la plataforma. Documentación y ROADMAP, revertibles sin efectos.
- **Lo único no revertible por código** es el sembrado del fundador con un correo equivocado, que es precisamente lo que este incremento impide. Si llegara a ocurrir, el camino manual queda escrito en §10.4: corregir el campo `Email` de esa fila en Supabase, o degradarla y redesplegar, con el sembrador **promoviendo** la cuenta existente con ese correo en lugar de crear otra **[V]** (`AdminUserSeeder.cs:44-53`).

### 8.3. Criterio de parada

Si **N2 refuta el supuesto de D4** (H4, rama b), `sdd-apply` **debe detenerse** al final de la fase ROJA de PR #2 y devolver el control al orquestador. Un caso negativo que refuta la premisa no se reescribe para que pase: se rediseña.

---

## 9. Preguntas abiertas de diseño

- [ ] **Qué proveedores entran en el primer despliegue** (Google + Discord recomendados; Facebook pendiente de la revisión de Meta). No bloquea PR #1, #2 ni #3; bloquea el contenido exacto de PR #4.
- [ ] **Dominio definitivo** (`ludeka.es` frente a otro). No bloquea el código; bloquea la redacción concreta de §10.1 y §10.2 y el registro de las apps OAuth.
- [ ] **Correo y proveedor del fundador.** No entra en el repositorio; bloquea el paso `1-bis` de §9.0 en la práctica del maintainer.

Ninguna de las tres bloquea `sdd-tasks`.
