# Propuesta — INC-52: Autenticación y Acceso Administrativo en el Primer Despliegue de Producción

> **Fase:** `sdd-propose` · **Fecha:** 2026-09-20
> **Worktree:** `inc/autenticacion-en-el-despliegue`, base `main` en `69d1e01`
> **Entrada:** `openspec/changes/change-52-autenticacion-en-el-despliegue/explore.md` (auditado por el orquestador)

**Convenio de lectura.** Cada afirmación técnica lleva marca explícita:
> **[V]** hecho verificado en esta fase, con `fichero:línea` o fuente citada ·
> **[R]** recomendación de esta propuesta, sujeta a validación en `sdd-design` ·
> **[NV]** afirmación **no verificada**, declarada como hueco a cerrar antes de `sdd-apply`.

---

## 1. Problema (qué está roto y por qué importa ahora)

El maintainer va a desplegar Ludeka en producción **por primera vez**. Hoy no existe entorno: ni proyecto de GCP, ni base de Supabase. Todo el despliegue está condicionado a `has_gcp == 'true'`, que exige los secretos `GCP_PROJECT_ID` y `GCP_SA_KEY` **[V]** (`.github/workflows/ci-cd.yml:64-69`). En el instante en que esos dos secretos existan, el siguiente `push` a `main` desplegará el servicio web real.

Ese despliegue, tal y como está el repositorio hoy, produciría un sitio **al que nadie puede entrar, empezando por el propio maintainer**, por tres defectos independientes que no rompen el arranque, no aparecen en `/healthz` ni en `/ready` y no estaban documentados:

- **B1 — El host no procesa las cabeceras del proxy inverso.** `UseForwardedHeaders` no existe en ningún `.cs` del repositorio **[V]** (verificado con búsqueda en todo el worktree en esta fase). Kestrel se vincula solo a HTTP **[V]** (`src/Ludeka.Web/Program.cs:53-57`). Cloud Run termina TLS por delante y reenvía HTTP interno, así que `Request.Scheme` vale `"http"` dentro del proceso y el `redirect_uri` que los manejadores OAuth envían al proveedor se construye con `http://`, mientras que en la consola del proveedor estará registrado el `https://`. El desafío se rechaza. Los tres proveedores, siempre. El mismo patrón se reproduce en el despliegue VPS documentado **[V]** (`deploy/nginx/default.conf:30,37-40`: `proxy_pass http://` con `X-Forwarded-Proto $scheme`).
- **B2 — El pipeline no cablea ninguna credencial de autenticación.** Ni `Authentication__*` ni `AdminUser__*` aparecen en `ci-cd.yml` **[V]** (`:108-129`), y el `appsettings.json` versionado trae los tres proveedores con `"Enabled": false` y credenciales vacías **[V]** (`src/Ludeka.Web/appsettings.json:28-44`). Resultado: cero esquemas registrados, ningún botón en `/login` **[V]** (`src/Ludeka.Web/Components/Pages/Login.razor:27-37`).
- **B3 — El correo del administrador fundador se fija de forma irreversible.** `AdminUserSeeder.EnsureAdminUserAsync` retorna en cuanto existe un `AppUser` con rol `FoundingTeam` **[V]** (`src/Ludeka.Infrastructure/Seeding/AdminUserSeeder.cs:29-36`), de modo que toda la lectura de `AdminUser:Email` queda por debajo de ese `return` y no se vuelve a ejecutar nunca. Sin `AdminUser__Email` en el pipeline, el primer arranque siembra `admin@ludeka.es` **[V]** (valor por defecto en `AdminUserSeeder.cs:41` y en `appsettings.json:21`) y lo fija para siempre.
- **B4 — El aviso de arranque no cubre el caso peor.** `GetConfigurationWarnings` hace `continue` cuando el proveedor **no** está habilitado **[V]** (`src/Ludeka.Web/Authentication/ExternalAuthenticationSchemes.cs:112`), así que con los tres en `false` —el estado por defecto y el resultado del pipeline actual— la lista de avisos queda vacía y los registros de Cloud Run no dicen absolutamente nada.
- **B5 — El ROADMAP miente sobre la puerta del primer despliegue.** `docs/increments/ROADMAP.md:75` afirma que el PR #60 «está abierto y retenido por el maintainer». Es falso: está fusionado desde el 2026-09-19 y el propio documento lo cuenta en pasado tres líneas antes **[V]** (`ROADMAP.md:71,73`; `docs/deployment/google-cloud-run.md:194`). Hace creer al maintainer que dispone de una red de seguridad que ya no existe.

**Por qué ahora y no después:** B3 es **irreversible por la vía normal de la aplicación**. Se consume en el primer arranque contra base vacía. Cualquier orden de trabajo que despliegue antes de cerrar este incremento quema esa decisión sin que nadie la haya tomado.

---

## 2. Objetivo y resultado esperado

Dejar el repositorio en un estado en el que **el primer despliegue real sea correcto a la primera**: que el maintainer pueda mapear su dominio, registrar sus apps OAuth, informar las claves, desplegar y entrar en su propio panel de fundador, sin pasos no escritos y sin decisiones irreversibles tomadas en silencio.

Resultado esperado, observable:

1. Detrás de un proxy inverso que termina TLS, la aplicación construye sus URL y su `redirect_uri` con `https` (B1).
2. El pipeline cablea, de forma explícita y con un criterio razonado de secreto frente a variable, las claves de autenticación y del fundador (B2, B3).
3. Un despliegue sin ningún proveedor operativo, o sin correo de fundador explícito en `Production`, **se nota**: o aborta, o deja una línea inequívoca en los registros (B3, B4).
4. `docs/deployment/google-cloud-run.md` contiene, completando sin contradecir su §9.0, el mapeo de dominio personalizado, el orden de registro de las apps OAuth y **cómo se obtiene el primer acceso de fundador** —hoy conducta emergente entre INC-46 e INC-49, no escrita en ningún sitio.
5. El ROADMAP deja de mentir (B5).

**No** es objetivo de este incremento desplegar nada, ni acreditar nada contra GCP real: no existe entorno (ver §8).

---

## 3. Alcance

### 3.1. Dentro

| # | Entregable | Defecto |
|---|---|---|
| A1 | `UseForwardedHeaders` en `src/Ludeka.Web/Program.cs`, con las opciones construidas por una función pura comprobable | B1 |
| A2 | Pruebas que acreditan el comportamiento real de A1, incluido el caso negativo (sin limpiar las listas de confianza, el esquema **no** cambia) | B1 |
| A3 | Cableado de `Authentication__Providers__*` en `.github/workflows/ci-cd.yml`, repartido entre `env_vars` y `secrets` con criterio explícito | B2 |
| A4 | Cableado de `AdminUser__Email` (y opcionalmente `Id`/`UserName`/`Country`) en `ci-cd.yml` | B3 |
| A5 | Guarda de arranque en `Production`: sin `AdminUser:Email` explícito, el proceso aborta en vez de sembrar un valor por defecto en silencio | B3 |
| A6 | Ajuste de `docker-compose.prod.yml` para no reintroducir el valor por defecto que A5 prohíbe | B3 |
| A7 | Aviso de arranque cuando no hay **ningún** proveedor operativo | B4 |
| A8 | Nueva sección en `docs/deployment/google-cloud-run.md`: dominio personalizado en Cloud Run, orden de registro de apps OAuth y primer acceso del fundador; más los enganches necesarios en §9.0 sin renumerarla | B1–B4 |
| A9 | Corrección de `docs/increments/ROADMAP.md:75` | B5 |

### 3.2. Fuera (explícito)

- **La cascada 2a/2b de INC-49.** Funciona; lo que falta es documentación de uso, no lógica **[V]** (`openspec/specs/social-login-authentication/spec.md:54`).
- **Autenticación por contraseña, cuentas locales o correo de recuperación.** No existen en el producto y este incremento no los introduce.
- **Revisión de aplicaciones de Meta para Facebook.** Trámite externo, ajeno al repositorio.
- **Quinto Cloud Run Job del boletín semanal** (`google-cloud-run.md:276-294`): decisión pendiente y no relacionada.
- **Claves de autenticación en los cuatro Cloud Run Jobs.** No atienden HTTP, no pueden recibir un retorno OAuth y `Ludeka.Jobs` no referencia `AuthenticationOptions` ni `AdminUserOptions`; `AdminUserSeeder` solo se invoca desde el host web **[V]** (`src/Ludeka.Web/Program.cs:183`). El bloque `--set-secrets` de los Jobs (`ci-cd.yml:151`) no se toca.
- **Aprovisionar el entorno real** (proyecto GCP, Supabase, DNS, apps OAuth). Es trabajo manual del maintainer; este incremento le entrega el guion, no lo ejecuta.
- **Volcado a `docs/specs/sistema/`.** Obligación de `sdd-archive` por `AGENTS.md` §1.6, no de esta fase.

### 3.3. Capacidades (contrato con `sdd-spec`)

**Capacidades nuevas**

- `reverse-proxy-forwarded-headers`: cómo la aplicación reconstruye el esquema original de la petición detrás de un proxy que termina TLS (Cloud Run y Nginx), qué cabeceras confía y cuáles no, y qué garantiza eso sobre las URL que genera. Hoy no la cubre ninguna especificación: `nginx-reverse-proxy` describe solo el lado Nginx y **no menciona `X-Forwarded-Proto`** **[V]** (`openspec/specs/nginx-reverse-proxy/spec.md`, comprobado íntegro).
- `production-auth-bootstrap`: qué configuración de autenticación e identidad de fundador debe estar presente para que un despliegue de `Production` sea válido, qué ocurre cuando falta, y cuál es el camino de primer acceso del fundador.

**Capacidades modificadas**

- `social-login-authentication`: se añade el requisito del aviso de arranque cuando **ningún** proveedor resulta utilizable (B4). Cambia el comportamiento observable de la capacidad, no solo su implementación.

**No se modifican** `production-persistence-guard` (su guarda no se toca; la nueva es hermana), `policy-based-authorization`, `account-provider-connections` ni `nginx-reverse-proxy`.

---

## 4. Enfoque propuesto, defecto por defecto

### B1 · Cabeceras de proxy

Insertar `app.UseForwardedHeaders(...)` **al principio del pipeline**, antes de `UseStatusCodePagesWithReExecute` y de `UseHttpsRedirection` **[R]** (hoy en `Program.cs:202-203`). Las opciones se construyen en una función pura estática en un fichero propio de `Ludeka.Web`, siguiendo el precedente exacto de `WebStartupGuards.Evaluate` **[V]** (`src/Ludeka.Web/WebStartupGuards.cs:20-46`): así se comprueban sin levantar el host ni mutar estado del proceso. Detalles en §5 (D1).

### B2 · Cableado del pipeline

Añadir a `ci-cd.yml` las claves de los proveedores que entren en el primer despliegue, repartidas por el criterio de §5 (D4), que es el que el repositorio ya aplica a Cloudflare R2 **[V]** (`ci-cd.yml:108-129`, documentado en `docs/deployment/google-cloud-run.md:163`).

### B3 · Correo del fundador

Dos piezas que solo funcionan juntas **[R]**:
1. Vaciar el valor por defecto de `appsettings.json:21` (`"Email": ""`), de modo que «vacío» signifique inequívocamente «no informado». `AdminUserSeeder.cs:41` conserva su propio respaldo para entornos no productivos, así que el desarrollo local sigue sembrando igual.
2. Guarda de arranque en `Production`: si `AdminUser:Email` llega vacío, el proceso aborta con un mensaje que nombre la clave y explique que la decisión es irreversible.

### B4 · Aviso del caso peor

Extender `GetConfigurationWarnings` para que, además de los avisos por proveedor, emita una línea cuando **ninguno** de los tres resulta utilizable (`IsUsable` **[V]**, `AuthenticationOptions.cs:65-69`). Se emite en el punto que ya existe **[V]** (`Program.cs:126-129`), justo después de la guarda de persistencia y antes de inicializar la base de datos, para que sea de las primeras líneas del arranque en Cloud Run.

### B5 · ROADMAP

Sustituir la nota falsa de `ROADMAP.md:75` por el hecho verificado (PR #60 fusionado el 2026-09-19) y dejar el puntero al gate real, que es de despliegue y ya está bien redactado tres líneas antes **[V]** (`ROADMAP.md:73`).

### Documentación

Nueva sección `## 10` en `docs/deployment/google-cloud-run.md` con tres bloques: (a) mapeo de dominio personalizado a Cloud Run; (b) registro de las tres apps OAuth contra `https://<dominio>/signin-{google,discord,facebook}` —rutas fijadas por constante **[V]** (`ExternalAuthenticationSchemes.cs:35-37`)—; (c) cómo obtiene el fundador su primer acceso. Más los enganches mínimos en §9.0 descritos en §5 (D6).

---

## 5. Decisiones de diseño y sus alternativas descartadas

### D1 · Qué confía `UseForwardedHeaders`, y dónde va

**Lo verificado sobre el framework**, contra la documentación XML oficial del paquete de referencia instalado (`C:\Program Files\dotnet\packs\Microsoft.AspNetCore.App.Ref\10.0.11\ref\net10.0\Microsoft.AspNetCore.HttpOverrides.xml`):

- `KnownProxies` son «direcciones de proxies conocidos de los que aceptar cabeceras reenviadas» y `KnownNetworks` los rangos equivalentes **[V]** (`:124-134`). Es decir: **son el mecanismo de confianza**, no un filtro accesorio.
- **`KnownNetworks` está marcada como obsoleta en .NET 10**; la propiedad vigente es `KnownIPNetworks` **[V]** (`:129-139`). Cualquier receta copiada de una guía de .NET 6/7 que llame a `KnownNetworks.Clear()` compilará con aviso de obsolescencia.
- `ForwardLimit` vale 1 por defecto y «solo debería anularse si `KnownProxies` o `KnownNetworks` están configurados» **[V]** (`:117-123`).
- `AllowedHosts` vacía admite **todos** los hosts en `X-Forwarded-Host`, y «no restringir estos valores puede permitir a un atacante suplantar los enlaces que genera tu servicio» **[V]** (`:140-155`).
- `ForwardedHeaders.None` significa «no procesar ningún reenvío» **[V]** (`:243-247`); la propiedad `ForwardedHeaders` no declara valor por defecto en la documentación, y siendo un `enum` de banderas su valor por defecto es `None`, de modo que **hay que fijarla explícitamente** **[R]**.

**Lo no verificado, y es el punto crítico:** que los valores por defecto de `KnownProxies`/`KnownIPNetworks` sean exactamente la dirección de bucle invertido y `127.0.0.0/8`, y que en consecuencia el middleware **descarte en silencio** las cabeceras de un proxy cuya IP no esté en esas listas **[NV]**. La documentación XML del paquete de referencia no publica esos valores por defecto y este agente no dispone de herramienta web para citar la página oficial. **No se afirma aquí.** Se cierra de dos maneras, y las dos entran en el alcance: citando en `sdd-design` la página oficial *Configure ASP.NET Core to work with proxy servers and load balancers*, y —sobre todo— con la prueba negativa de A2, que convierte la suposición en un aserto verde o la refuta (§8).

**Recomendación [R]**, condicionada a que esa comprobación confirme el comportamiento:

| Decisión | Valor propuesto | Razón |
|---|---|---|
| Cabeceras procesadas | `XForwardedProto` **únicamente** (`XForwardedFor` opcional, ver abajo) | Es la única que arregla B1. `XForwardedHost` queda fuera a propósito: Nginx ya preserva el `Host` original **[V]** (`deploy/nginx/default.conf:37`) y procesarla con `AllowedHosts` vacía abre exactamente la suplantación que advierte el propio framework **[V]** (`:140-144`), agravada porque `appsettings.json:8` tiene `"AllowedHosts": "*"` **[V]** |
| Listas de confianza | `KnownProxies` y `KnownIPNetworks` vacías | La IP del front-end de Cloud Run es desconocida y variable, y en el VPS el proxy es una IP privada de Docker, tampoco de bucle invertido |
| Ubicación | Primer middleware del pipeline | Todo lo que va detrás lee `Request.Scheme`/`IsHttps`: redirección HTTPS, HSTS, política `Secure` de la cookie **[V]** (`ExternalAuthenticationSchemes.cs:54`) y el `redirect_uri` de OAuth |
| Condicional o incondicional | **Incondicional** | Sin proxy delante la cabecera no llega y el middleware es inerte. Condicionarlo a `Production` dejaría descubiertos el VPS con Nginx y `docker-compose.staging.yml` **[V]** (`:15`, entorno `Staging`), que también viven detrás de proxy |

**Riesgo de seguridad de vaciar las listas, y por qué se asume [R].** Vaciarlas significa aceptar `X-Forwarded-Proto` de cualquier origen. El daño exige que alguien alcance el contenedor **saltándose** el proxy. Se acota así: (a) procesando solo `XForwardedProto`, la suplantación se reduce a «declarar https cuando es http», que es precisamente el valor que queremos, mientras que declarar `http` solo se perjudica a sí mismo; (b) no se procesa `XForwardedHost`, que es la cabecera con la que de verdad se suplantan enlaces; (c) queda por comprobar en `sdd-design` que ninguna decisión de seguridad del repositorio dependa de `RemoteIpAddress` **[NV]**, condición necesaria si finalmente se procesa también `XForwardedFor`. **Alternativa descartada:** declarar los rangos de Google como `KnownIPNetworks`. Se descarta porque Google no publica un rango estable para el front-end de Cloud Run **[NV]** y una lista caducada reintroduce B1 en silencio, que es el fallo original.

### D2 · `UseHttpsRedirection()` incondicional (`Program.cs:203`)

La exploración lo dejó como riesgo secundario sin verificar. **Sigue sin verificarse, y esta propuesta no lo afirma** **[NV]**.

Lo que sí se puede decir con evidencia: el middleware recibe `IConfiguration` e `IServerAddressesFeature` en su constructor **[V]** (`Microsoft.AspNetCore.HttpsPolicy.xml:134-143` del mismo paquete de referencia), lo que indica que **resuelve el puerto HTTPS de destino** a partir de la configuración y de las direcciones del servidor. Y en este despliegue no hay ninguna: Kestrel se vincula solo a HTTP **[V]** (`Program.cs:53-57`, `Dockerfile` con `ASPNETCORE_HTTP_PORTS=8080`).

**Hipótesis de trabajo [R]:** sin puerto HTTPS resoluble el middleware no redirige y se limita a avisar una vez, de modo que hoy no hay bucle; y **una vez corregido B1 la cuestión se vuelve discutible**, porque `Request.IsHttps` pasará a ser verdadero y el middleware no tendrá nada que redirigir. El bucle solo sería posible en la combinación «B1 sin corregir **y** puerto HTTPS configurado a mano».

**Decisión propuesta:** no tocar `Program.cs:203` en este incremento **[R]** —además, una prueba de contrato vigente exige que la llamada exista **[V]** (`tests/Ludeka.UnitTests/Web/AuthorizationPipelineContractTests.cs:37-40`)— y **cerrar el hueco con una prueba**, no con una opinión: en `sdd-design` se decide si la prueba de A2 cubre también este caso (petición HTTP entrante sin puerto HTTPS configurado ⇒ no hay 307). Si la prueba revelara redirección, la corrección es acotada y conocida: condicionar la llamada o fijar `HttpsRedirectionOptions.HttpsPort`.

### D3 · Guarda de arranque para `AdminUser:Email`

**Recomendación: sí, guarda que aborta, y solo en `Production`** **[R]**. El precedente es exacto y reciente: INC-48 introdujo `WebStartupGuards.Evaluate` con la misma forma —función pura, entorno recibido explícitamente, `throw` en `Program.cs`— para eliminar un respaldo silencioso en `Production` **[V]** (`WebStartupGuards.cs:20-46`, `Program.cs:116-123`). B3 es el mismo defecto de familia: un valor por defecto que en producción es una decisión irreversible.

**Detección de «no informado»:** vaciar `appsettings.json:21` y exigir valor no vacío, en lugar de comparar contra la cadena literal `admin@ludeka.es` **[R]**. Comparar contra el literal bloquearía a un maintainer que sí controle ese buzón —y el dominio `ludeka.es` ya aparece como propio en el repositorio **[V]** (`appsettings.json:50`)—, y además deja el valor por defecto vivo en el fichero, que es justo lo que INC-48 eliminó para la cadena de conexión.

**Coste, medido y acotado [V]:**
- `docker-compose.prod.yml` declara `ASPNETCORE_ENVIRONMENT=Production` (`:12`) y `AdminUser__Email=${AdminUser__Email:-admin@ludeka.es}` (`:19`). La guarda lo rompería: **hay que quitar ese valor por defecto**, una línea (entregable A6). Es exactamente el tipo de colateral que INC-48 ya pagó.
- `docker-compose.yml` **no** se ve afectado: su entorno por defecto es `Staging`, no `Production` (`:16`), precisamente porque INC-48 lo ajustó. `docker compose up` local sigue funcionando.
- `docker-compose.staging.yml` tampoco (`:15`).

**Alternativa descartada:** aviso en vez de aborto. Se descarta porque el daño de B3 es irreversible por la vía de la aplicación y un aviso en los registros de Cloud Run llega, por definición, **después** del sembrado.

### D4 · Reparto entre `env_vars` y `secrets` en `ci-cd.yml`

El criterio no se inventa: se lee del fichero **[V]** (`ci-cd.yml:108-129`) y de su documentación **[V]** (`google-cloud-run.md:163`). Hoy, para Cloudflare R2, van a `env_vars` los valores operativos y públicos (`BucketName`, `PublicCdnBaseUrl`, `Simulate`) y a `secrets` **las tres piezas del par de credenciales, incluida la mitad identificadora** (`AccountId`, `AccessKeyId`, `SecretAccessKey`). El repositorio reserva los secretos de GitHub para el acceso y la topología del despliegue (`GCP_PROJECT_ID`, `GCP_SA_KEY`, `GCP_REGION`, `GCP_SERVICE_NAME`) y Secret Manager para la configuración de la aplicación **[V]** (`ci-cd.yml:64-106`).

Aplicando ese mismo criterio **[R]**:

| Clave | Destino | Razón |
|---|---|---|
| `Authentication__Providers__{P}__Enabled` | `env_vars` | Booleano operativo, sin valor que proteger |
| `Authentication__Providers__{Google,Discord}__ClientId` | `secrets` | Mitad identificadora de un par de credenciales: mismo trato que `Cloudflare__AccessKeyId`. Además evita fijarlo en claro en un fichero versionado |
| `Authentication__Providers__Facebook__AppId` | `secrets` | Ídem (`AppId` es el `ClientId` de Facebook **[V]**, `AuthenticationOptions.cs:59`) |
| `...__ClientSecret` / `...__AppSecret` | `secrets` | Secreto puro |
| `AdminUser__Email` | `secrets` | No es un secreto técnico: es un **dato personal** del fundador. Ponerlo en `env_vars` lo fija en claro en un fichero versionado. Minimización y coherencia con el almacén ya elegido |
| `AdminUser__Id` / `UserName` / `Country` | `env_vars` si se cablean | Sin carga personal ni credencial. Opcionales: sus valores por defecto del código son aceptables |
| `Authentication__Cookie__ExpireMinutes` | No se cablea | 43200 por defecto **[V]** (`appsettings.json:26`) |

**Consecuencia operativa que debe quedar escrita [R]:** cada clave añadida al bloque `secrets:` se convierte en **prerrequisito duro del primer despliegue** —la entrada debe existir en Secret Manager y la cuenta de servicio necesita `roles/secretmanager.secretAccessor` **[V]** (`google-cloud-run.md:162`)—. Que Cloud Run rechace el despliegue al referenciar un secreto inexistente es **[NV]**, coherente con el hueco que el propio fichero ya declara **[V]** (`ci-cd.yml:95-99`). De ahí se sigue la recomendación de **cablear solo los proveedores que entren en el primer despliegue** y documentar en un único punto cómo añadir uno después (ver §10, pregunta 1).

**Alternativa descartada:** secretos de GitHub interpolados en `env_vars`. Funciona, pero parte la configuración de la aplicación entre dos almacenes y rompe el criterio vigente del repositorio.

### D5 · Dónde y con qué severidad se emite el aviso de B4

**Dónde [R]:** el punto existente, `Program.cs:126-129`, extendiendo `GetConfigurationWarnings`. Se ejecuta tras la guarda de persistencia y antes de la inicialización de la base de datos, así que la línea sale al principio del arranque.

**Severidad [R]:** `LogError` cuando el entorno es `Production` y no hay ningún proveedor utilizable; `LogWarning` en el resto de casos y para los avisos por proveedor que ya existen. Razón: en `Production`, cero proveedores significa que **nadie puede autenticarse, incluido el fundador**, mientras que en desarrollo es el estado normal del `appsettings.json` versionado **[V]** (`:28-44`) y elevarlo a error sería ruido. El precedente de aviso consciente del entorno ya está en el repositorio: `MediaStorageWarnings.GetConfigurationWarnings(..., environmentName)` **[V]** (`Program.cs:133-137`).

**Alternativa descartada:** abortar el arranque, como en D3. Se descarta porque el catálogo público se lee sin sesión: tumbar el sitio entero por un fallo de inicio de sesión es desproporcionado y, a diferencia de B3, el estado es **reversible** con un redespliegue.

### D6 · Documentación: completar §9.0 sin contradecirla ni renumerarla

`google-cloud-run.md` §9.0 fija siete pasos **[V]** (`:206-218`), y **su numeración es referenciada desde fuera**: el propio documento remite a «§9.0, paso 5» **[V]** (`:185`) y el ROADMAP remite a §9.0 entera **[V]** (`ROADMAP.md:73`). **Renumerar rompería esas referencias**, así que la propuesta es **no tocar los números existentes** **[R]** e insertar pasos intercalados con nomenclatura propia —`1-bis` (decidir el correo del fundador junto con la base de datos) y `3-bis` (dominio, apps OAuth y claves de autenticación, antes del paso 4, que es el que arma el despliegue)—, más un puntero a la nueva §10. El orden resultante es el de `explore.md` §6, que ya se declaró compatible con §9.0.

**Sobre el mapeo de dominio personalizado:** es el único entregable cuyo contenido **no puede redactarse con las fuentes disponibles en esta fase** **[NV]**. Las órdenes exactas, los registros DNS y las restricciones de región de los *domain mappings* de Cloud Run no están en el repositorio —comprobado por búsqueda— y este agente no tiene acceso a documentación externa. **Esta propuesta no inventa órdenes.** Ver §10, pregunta 7.

### D7 · Documentar el arranque del fundador

La cadena real, que hoy no está escrita en ningún documento **[V]** y es conducta emergente entre INC-46 e INC-49:

1. En el primer arranque contra base vacía, `AdminUserSeeder` crea un `AppUser` con rol `FoundingTeam` y permisos completos, **sin ninguna fila en `ExternalLogins`** **[V]** (`AdminUserSeeder.cs:56-69`). En esa fila no se puede iniciar sesión directamente.
2. La activa la rama **2a** de `ExternalLoginService.ResolveAsync`: un acceso social con correo **verificado** que coincide con `AdminUser:Email` sobre una cuenta sin proveedores vinculados crea el vínculo y devuelve esa misma cuenta con sus permisos intactos **[V]** (`openspec/specs/social-login-authentication/spec.md:54`, escenario `:64-70`; código en `ExternalLoginService.cs:63-89` según `explore.md`, no releído en esta fase).
3. El correo se normaliza a minúsculas al sembrar **[V]** (`AdminUserSeeder.cs:41`), así que la comparación es insensible a mayúsculas por ese lado.

**El primer inicio de sesión social cuyo correo verificado coincida con `AdminUser:Email` ES la puerta de entrada del fundador.** No hay paso adicional. La documentación debe decirlo con esas palabras, junto con la condición dura: **el buzón tiene que ser verificable por el proveedor elegido**.

---

## 6. Impacto: ficheros y magnitud estimada

| Área | Impacto | Qué cambia | Líneas aprox. |
|---|---|---|---|
| `src/Ludeka.Web/Program.cs` | Modificado | `UseForwardedHeaders` al frente del pipeline; emisión del aviso de B4 | ~25 |
| `src/Ludeka.Web/ForwardedHeadersConfiguration.cs` *(nuevo, nombre tentativo)* | Nuevo | Función pura que construye `ForwardedHeadersOptions` | ~45 |
| `src/Ludeka.Web/WebStartupGuards.cs` | Modificado | Segunda guarda: `AdminUser:Email` explícito en `Production` | ~35 |
| `src/Ludeka.Web/Authentication/ExternalAuthenticationSchemes.cs` | Modificado | Aviso de «ningún proveedor utilizable», consciente del entorno | ~20 |
| `src/Ludeka.Web/appsettings.json` | Modificado | `AdminUser:Email` a cadena vacía | 1 |
| `tests/Ludeka.UnitTests/**` | Nuevo/Modificado | Contrato de pipeline, opciones, caso negativo de confianza, guarda, aviso | ~250 |
| `tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj` | Modificado | Dependencia de prueba `Microsoft.AspNetCore.TestHost` (ver §8 y §10, pregunta 6) | 1 |
| `.github/workflows/ci-cd.yml` | Modificado | Claves de autenticación y de fundador en `env_vars`/`secrets` | ~12 |
| `docker-compose.prod.yml` | Modificado | Retirada del valor por defecto de `AdminUser__Email` | 1-8 |
| `docs/deployment/google-cloud-run.md` | Modificado | §9.0 completada y nueva §10 | ~150 |
| `docs/increments/ROADMAP.md` | Modificado | Corrección de `:75` y registro del incremento | ~5 |
| `openspec/changes/change-52-*/` | Nuevo | `spec.md`, `design.md`, `tasks.md` | ~450-650 |

**Magnitud total estimada: ~1.000-1.250 líneas autoradas** (adiciones + supresiones), de las cuales ~400 de código y pruebas, ~155 de documentación y el resto artefactos de SDD. **Muy por encima del presupuesto de 400 líneas por PR**, así que procede partir en rebanadas apiladas. La estimación es orientativa; `sdd-tasks` emitirá el pronóstico formal.

---

## 7. Partición en rebanadas propuesta

Cadena de ramas apiladas desde `inc/autenticacion-en-el-despliegue`, criterio rector: **el código arriesgado viaja con sus pruebas**, y ninguna rebanada deja el repositorio en un estado peor que el actual.

| PR | Contenido | Líneas aprox. | Autónoma porque… |
|---|---|---|---|
| **#1 Contrato** | `spec.md`, `design.md`, `tasks.md` | ~450-650 | No toca código. **Se parte en dos si `sdd-tasks` la pronostica por encima de 400** |
| **#2 B1** | `Program.cs`, fichero de opciones, dependencia de prueba y **todas** las pruebas de cabeceras de proxy, incluido el caso negativo | ~210 | Corrige el defecto más arriesgado y lo acredita en la misma rebanada. Sin ella, cablear credenciales (PR #4) sería inútil |
| **#3 B3 + B4** | `WebStartupGuards.cs`, `appsettings.json`, `ExternalAuthenticationSchemes.cs`, `Program.cs`, `docker-compose.prod.yml` y sus pruebas | ~185 | Guarda y aviso son la misma familia (diagnóstico de arranque) y comparten punto de emisión y pruebas. El ajuste de compose viaja con la guarda que lo obliga |
| **#4 B2 + documentación + B5** | `ci-cd.yml`, `google-cloud-run.md` §9.0 y §10, `ROADMAP.md` | ~200 | Es la rebanada sin riesgo de ejecución: configuración declarativa y prosa. Debe ir **la última** porque documenta el comportamiento que fijan #2 y #3 |

**Orden de la cadena:** #1 ← #2 ← #3 ← #4, cada una apuntando a la anterior, según `AGENTS.md` §1-bis.7. **Los cuatro deben estar fusionados antes de configurar `GCP_PROJECT_ID` y `GCP_SA_KEY`**: ese es el gate real del incremento.

---

## 8. Verificación: qué quedará probado y qué será hueco de evidencia

**No existe entorno de producción.** Ni proyecto de GCP, ni base de Supabase, ni dominio mapeado. Nada de este incremento puede acreditarse contra un despliegue real, igual que ocurrió en INC-47 e INC-48 **[V]** (`ci-cd.yml:95-99`, `google-cloud-run.md:185`). Eso se declara aquí, no se disimula.

### 8.1. Lo que quedará probado, sin GCP

| Qué | Cómo | Idioma existente |
|---|---|---|
| El pipeline llama a `UseForwardedHeaders` antes de `UseHttpsRedirection` | Prueba de contrato que lee `Program.cs` | Exactamente el patrón de `AuthorizationPipelineContractTests.cs:29-43,170-187` **[V]** |
| Las opciones procesan `XForwardedProto`, no `XForwardedHost`, y con las listas de confianza vacías | Prueba unitaria de la función pura | Patrón de `WebStartupGuards` **[V]** |
| **Que una petición con `X-Forwarded-Proto: https` desde una IP remota que no es de bucle invertido acaba con `Request.Scheme == "https"`** | Prueba con servidor de prueba en memoria | **Requiere añadir `Microsoft.AspNetCore.TestHost`**: hoy ni `Ludeka.UnitTests` **[V]** (`.csproj:10-15`) ni `Ludeka.IntegrationTests` **[V]** (`.csproj:10-25`, solo Postgres/Testcontainers e Infrastructure) tienen con qué levantar un host |
| **El caso negativo**: sin limpiar las listas de confianza, el esquema **no** cambia | Misma prueba, variante | Es la que convierte el **[NV]** de D1 en hecho o lo refuta. **Es la prueba más valiosa del incremento** |
| La guarda de `AdminUser:Email` aborta en `Production` y no en otros entornos | Prueba de la función pura, con entorno y configuración inyectados | `WebStartupGuards` **[V]** |
| El aviso aparece con cero proveedores utilizables y no aparece con uno | Prueba de `GetConfigurationWarnings` | `WebAuthenticationRegistrationTests.cs` ya cubre los casos vecinos **[V]**, según `explore.md` §2 |
| `ci-cd.yml` contiene las claves esperadas | Prueba de contrato que lee el YAML | **Idioma nuevo**: hoy ninguna prueba lee `.github/workflows/` **[V]** (búsqueda en `tests/`). Extensión pequeña y coherente del patrón de lectura de fuente. Decisión de `sdd-design` |
| Toda la suite existente sigue verde | `dotnet test` | 1.565 unitarias + 10 de integración en INC-48 **[V]** (`ROADMAP.md:76`) |

### 8.2. Huecos de evidencia declarados (no se cierran en este incremento)

1. **El viaje OAuth real** contra Google/Discord/Facebook con el dominio definitivo. Solo se acredita con el primer despliegue.
2. **Que Cloud Run envía `X-Forwarded-Proto: https` y preserva el `Host` original** **[NV]**. Es comportamiento documentado de la plataforma, no verificable aquí.
3. **Que Cloud Run rechaza un despliegue que referencia un secreto inexistente** **[NV]**, y la sintaxis real del bloque `secrets:` ampliado.
4. **Órdenes y registros DNS del mapeo de dominio personalizado** **[NV]** (ver §10, pregunta 7).
5. **El sembrado del fundador y la rama 2a sobre PostgreSQL real**: se prueba la lógica, no el arranque en Supabase.
6. **`UseHttpsRedirection` en Cloud Run** (D2): se cierra con prueba en memoria si `sdd-design` lo acepta; si no, queda declarado.

### 8.3. Criterios de éxito

- [ ] Existe una prueba verde que demuestra que, detrás de un proxy no de bucle invertido, `Request.Scheme` pasa a `https`; y otra que demuestra que sin limpiar las listas de confianza **no** pasa.
- [ ] `dotnet test` verde sobre toda la solución, sin regresiones en las 1.565+ pruebas existentes.
- [ ] Un arranque en `Production` sin `AdminUser:Email` explícito aborta con mensaje que nombra la clave; con valor explícito, arranca.
- [ ] Un arranque sin ningún proveedor utilizable deja una línea inequívoca en los registros; con uno, no la deja.
- [ ] `ci-cd.yml` cablea las claves de los proveedores acordados y `AdminUser__Email`, con el reparto de D4.
- [ ] `docker compose -f docker-compose.prod.yml config` sigue siendo válido tras A6, y `docker compose up` local sigue arrancando.
- [ ] `google-cloud-run.md` explica el mapeo de dominio, el registro de apps OAuth con las tres rutas exactas y el primer acceso del fundador, sin contradecir ni renumerar §9.0.
- [ ] `ROADMAP.md:75` ya no afirma que el PR #60 está abierto.
- [ ] Los cuatro PR de la cadena están fusionados **antes** de que existan `GCP_PROJECT_ID` y `GCP_SA_KEY`.

---

## 9. Riesgos y plan de reversión

| Riesgo | Prob. | Impacto | Mitigación |
|---|---|---|---|
| El comportamiento por defecto de las listas de confianza **[NV]** no es el supuesto y la corrección de B1 es otra | Media | Alto: B1 seguiría vivo tras el despliegue | La prueba negativa de A2 lo decide antes de `sdd-apply`. Si se refuta, cambia la configuración, no el alcance |
| Vaciar las listas habilita suplantación de `X-Forwarded-Proto` si el contenedor fuera alcanzable sin pasar por el proxy | Baja | Medio | Procesar solo `XForwardedProto`; no procesar `XForwardedHost`; verificar en diseño que ninguna decisión de seguridad depende de `RemoteIpAddress` |
| La guarda de D3 rompe un flujo no previsto | Baja | Medio | Medido: solo `docker-compose.prod.yml:19` está en `Production` **[V]**; `docker-compose.yml` y `.staging.yml` no. A6 lo cubre |
| Añadir claves a `secrets:` hace fallar el primer despliegue por secretos inexistentes | **Alta** | Medio | Documentar en §10 la lista exacta de entradas de Secret Manager a crear **antes** del paso 4 de §9.0, y cablear solo los proveedores acordados |
| La nueva dependencia de prueba arrastra conflicto de versiones | Baja | Bajo | `Microsoft.AspNetCore.TestHost` es del propio marco, alineado con `net10.0`; se valida al compilar en PR #2 |
| La documentación de dominio personalizado se redacta sin fuente y sale mal | Media | Alto: es el paso del que depende el registro de las apps OAuth | **No redactar órdenes sin fuente.** Ver §10, pregunta 7 |
| Cuatro PR apilados generan conflictos con otros incrementos en curso (INC-50, INC-51) | Baja | Bajo | Ninguno de los ficheros de §6 coincide con el alcance declarado de INC-50 **[NV]**, a confirmar antes de abrir la cadena |

### Plan de reversión

- **B1 (PR #2):** revertir el PR. Solo añade un middleware y un fichero; `Request.Scheme` vuelve a `"http"`, es decir, al estado actual. Sin migraciones ni persistencia implicadas.
- **B3/B4 (PR #3):** revertir el PR y restituir el valor por defecto de `appsettings.json:21` y de `docker-compose.prod.yml:19`. La guarda es una función pura más un `throw`: si aborta, **no se ha sembrado nada**, que es el objetivo.
- **B2 (PR #4):** revertir las claves de `ci-cd.yml`; el servicio conserva su revisión anterior y Cloud Run permite dirigir el tráfico a una revisión previa **[NV]**, procedimiento estándar de la plataforma.
- **Documentación y ROADMAP:** revertibles sin efectos.
- **Lo único no revertible por código** es el sembrado del fundador con un correo equivocado, que es precisamente lo que este incremento impide. Si llegara a ocurrir, el camino manual es: corregir el campo `Email` de esa fila en Supabase; o degradarla/eliminarla y redesplegar con el valor correcto, en cuyo caso el sembrador **promueve** la cuenta existente que ya tenga ese correo en lugar de crear otra **[V]** (`AdminUserSeeder.cs:44-53`). Este camino debe quedar escrito en la nueva §10.

---

## 10. Preguntas abiertas para el maintainer

> Decisiones de producto que esta propuesta **no toma**. Cada una indica qué bloquea.

1. **¿Entra Facebook en el primer despliegue, o se limita a Google y Discord?** Determina cuántas entradas de Secret Manager hay que crear antes del paso 4 de §9.0 y cuántas claves cablea `ci-cd.yml`. *Bloquea:* el contenido exacto de PR #4. *No bloquea:* PR #1, #2 ni #3.
2. **¿Cuál es el dominio definitivo?** El repositorio contiene dos, y no son el mismo: `https://ludeka.es` **[V]** (`appsettings.json:50`) y `https://cdn.ludeka.com` **[V]** (`ci-cd.yml:117`). Las tres rutas de retorno se registran contra el dominio elegido y cambiarlas después obliga a retocar las tres consolas. *Bloquea:* la redacción de §10 y el registro de las apps OAuth.
3. **¿De qué proveedor será el correo del fundador, y está verificado en él?** No hace falta el valor aquí, solo la decisión: la rama 2a exige correo **verificado**, y lo que cada proveedor entiende por «verificado» difiere (`email_verified` en Google frente a `verified` en Discord/Facebook **[V]**, `ExternalAuthenticationSchemes.cs:165,179,194`). *Bloquea:* el paso 1-bis de §9.0.
4. **¿Se acepta que la guarda de D3 aborte el arranque en `Production`?** Es la recomendación, con el coste medido de una línea en `docker-compose.prod.yml`. *Bloquea:* PR #3.
5. **¿`AdminUser__Email` viaja como secreto de Secret Manager (recomendado) o como variable en claro en el flujo versionado?** *Bloquea:* PR #4.
6. **¿Se acepta añadir `Microsoft.AspNetCore.TestHost` como dependencia de prueba?** Es lo que permite convertir la suposición central de D1 en una prueba verde en vez de dejarla como hueco. *Bloquea:* el alcance de verificación de PR #2, no su corrección.
7. **El mapeo de dominio personalizado a Cloud Run no puede documentarse con las fuentes de esta fase.** ¿Se lanza `sdd-research` con acceso a documentación oficial para obtener órdenes, registros DNS y restricciones de región, o lo aporta el maintainer desde su propia consola? *Bloquea:* el bloque (a) de la nueva §10; el resto de la documentación puede escribirse igual.

---

## 11. Dependencias

- **Ninguna dependencia de código.** Los prerrequisitos de salida a producción, INC-47 e INC-48, están archivados **[V]** (`ROADMAP.md:71`).
- **Dependencia de decisión:** las siete preguntas de §10, con el mapa de bloqueo indicado.
- **Dependencia externa de documentación:** fuentes oficiales para D1 (comportamiento por defecto de las listas de confianza) y para el mapeo de dominio (§10, pregunta 7).
- **Dependencia operativa del maintainer, fuera del repositorio:** proyecto de GCP, Supabase, DNS del dominio y las apps OAuth. Este incremento entrega el guion; no lo ejecuta.
