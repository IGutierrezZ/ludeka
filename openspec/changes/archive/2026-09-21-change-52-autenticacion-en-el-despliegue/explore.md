# Exploración — INC-52: Autenticación y Acceso Administrativo en el Primer Despliegue de Producción

> **Fase:** `sdd-explore` · **Fecha:** 2026-09-20
> **Worktree:** `inc/autenticacion-en-el-despliegue`, base `main` en `69d1e01`
> **Motivo:** el maintainer va a desplegar Ludeka en producción por primera vez y pidió la lista de variables a informar. Al elaborarla apareció un hueco que no estaba registrado en ningún documento.

---

## 1. Pregunta que responde esta exploración

¿Puede Ludeka desplegarse hoy en Cloud Run con un camino de acceso funcional y con acceso administrativo real para el maintainer?

**No.** Hay **tres defectos bloqueantes independientes**. Ninguno provoca un fallo de arranque, ninguno aparece en `/healthz` ni en `/ready`, y ninguno estaba documentado.

---

## 2. Cómo funciona hoy la autenticación

- **Registro dirigido por configuración** en `ExternalAuthenticationSchemes.AddLudekaAuthentication` (`src/Ludeka.Web/Authentication/ExternalAuthenticationSchemes.cs:43-73`): cookie propia `ludeka.session` más hasta tres esquemas sociales, cada uno registrado solo si `Enabled && HasCredentials`.
- **No existe autenticación por contraseña ni cuentas locales.** Los tres esquemas sociales y la cookie son el **único** mecanismo de acceso.
- **Cookie de sesión** (`:50-61`): `HttpOnly`, `SecurePolicy = CookieSecurePolicy.Always`, `SameSite = Lax`, expiración deslizante.
- **Rutas de retorno fijadas por constante propia** (`:35-37`): `/signin-google`, `/signin-discord`, `/signin-facebook`. Son los valores exactos a registrar en cada consola de proveedor.
- **Cascada de identidad** en `ExternalLoginService.ResolveAsync` (`src/Ludeka.Application/Features/Identity/ExternalLoginService.cs:37-106`): (1) vínculo `(Provider, ProviderKey)` existente; (2) correo verificado, partido por INC-49 en **2a** —vinculación silenciosa si la cuenta destino no tiene ningún proveedor— y **2b** —colisión que nunca fusiona—; (3) alta de `CommunityUser` nuevo sin permisos.
- **Degradación visible solo en pantalla:** `Login.razor:27-37` muestra «El acceso social aún no está configurado» cuando no hay proveedores. Quien mire los registros de Cloud Run no verá nada.
- **Cobertura existente:** `tests/Ludeka.UnitTests/Web/WebAuthenticationRegistrationTests.cs` cubre proveedor deshabilitado, habilitado sin credenciales con su aviso, habilitado con credenciales, política de cookie y que el `appsettings.json` versionado deja todo inoperativo por defecto. **Cero pruebas** tocan cabeceras de proxy, `Request.Scheme` ni la construcción del `redirect_uri`.

---

## 3. Hallazgos

### 🔴 B1 — BLOQUEANTE: el host no procesa las cabeceras del proxy inverso

`UseForwardedHeaders` y `ForwardedHeadersOptions` **no existen en ningún fichero `.cs` del repositorio**. Kestrel se vincula solo a HTTP (`Program.cs:53-57`, `Dockerfile:65` con `ASPNETCORE_HTTP_PORTS=8080`), sin endpoint HTTPS.

Cloud Run termina TLS por delante del contenedor y reenvía HTTP interno con `X-Forwarded-Proto`. Sin procesar esa cabecera, `Request.Scheme` vale siempre `"http"` dentro del proceso.

Los manejadores OAuth construyen el `redirect_uri` que envían al proveedor a partir de `Request.Scheme` y `Request.Host`. **Consecuencia: se enviaría `http://<dominio>/signin-google` cuando en la consola de Google estará registrado el `https://`. El proveedor rechaza el desafío. Los tres proveedores, siempre, en producción.**

No es exclusivo de Cloud Run: el despliegue VPS documentado reproduce el mismo patrón (`deploy/nginx/default.conf:30,40`, `proxy_pass http://` con `X-Forwarded-Proto`).

**Riesgo secundario no verificado:** `Program.cs:203` invoca `app.UseHttpsRedirection()` de forma incondicional para todos los entornos. Sin cabeceras reenviadas, queda por determinar si provoca un bucle de redirección o solo advierte una vez. Requiere comprobación real antes de cerrar el diseño.

### 🔴 B2 — BLOQUEANTE: el pipeline no cablea ninguna credencial de autenticación

`.github/workflows/ci-cd.yml` no contiene ninguna clave `Authentication__*` ni `AdminUser__*`, ni en el despliegue web ni en el de los Cloud Run Jobs. `src/Ludeka.Web/appsettings.json:28-44` trae los tres proveedores con `"Enabled": false` y credenciales vacías.

**Consecuencia: el primer despliegue arranca con cero proveedores registrados. `Login.razor` no ofrece ningún botón y nadie puede autenticarse, incluido el maintainer.**

El mismo hueco existe en `docker-compose.prod.yml`, que tampoco define ninguna clave `Authentication__Providers__*`.

### 🔴 B3 — BLOQUEANTE: el correo del administrador fundador se fija de forma irreversible

`AdminUserSeeder.EnsureAdminUserAsync` (`src/Ludeka.Infrastructure/Seeding/AdminUserSeeder.cs:29-36`) comprueba si ya existe algún `AppUser` con rol `FoundingTeam` y, si lo hay, **registra un mensaje y retorna**. Toda la lógica que lee `AdminUser:Email` queda por debajo de ese `return` y **no se vuelve a ejecutar nunca** en arranques posteriores.

Como `ci-cd.yml` no define `AdminUser__Email`, el primer despliegue sembraría `admin@ludeka.es` (valor por defecto de `appsettings.json:21`) **para siempre**.

El único camino de código que activa esa fila es la rama **2a** de `ResolveAsync` (`ExternalLoginService.cs:63-89`): exige un inicio de sesión social cuyo correo **verificado** coincida, normalizado a minúsculas, con ese valor. Si el maintainer no controla un buzón verificable por OAuth en ese dominio, queda permanentemente fuera de su propio panel por la vía normal de la aplicación; la única salida sería editar la fila a mano en Supabase.

**Contraevidencia relevante:** `docker-compose.prod.yml:17-20` **ya parametriza** `AdminUser__Id`, `UserName`, `Email` y `Country`. La necesidad estaba identificada en el repositorio; simplemente no se trasladó a `ci-cd.yml`.

### 🟠 B4 — IMPORTANTE: el aviso de arranque no cubre el caso peor

`GetConfigurationWarnings` (`ExternalAuthenticationSchemes.cs:108-120`) hace `continue` cuando el proveedor **no** está habilitado. Solo avisa del caso «habilitado pero sin credenciales».

Con los tres en `false` —el valor por defecto y el resultado del pipeline actual— la lista de avisos queda **vacía**: ni una línea en los registros advierte de que el acceso está completamente inoperativo. `WebStartupGuards.Evaluate` tampoco lo comprueba: solo valida el proveedor de base de datos.

### 🟠 B5 — IMPORTANTE: el ROADMAP miente sobre la puerta del primer despliegue

`docs/increments/ROADMAP.md:75` afirma que el PR #60 (retirada de los cuatro `AddHostedService`) «está abierto y retenido por el maintainer». **Es falso.** Verificado por tres vías: no queda ningún `AddHostedService` en `src/Ludeka.Web/`; `gh pr view 60` devuelve `MERGED` con fecha 2026-09-19; y `docs/deployment/google-cloud-run.md:194` ya lo cuenta en pasado.

Es peligroso precisamente ahora: haría creer al maintainer que dispone de una red de seguridad que ya no existe.

---

## 4. Superficie de configuración a cablear

| Clave | ¿Secreta? | Destino | Obligatoriedad |
|---|---|---|---|
| `Authentication__Providers__Google__Enabled` | No | Solo web | Si se activa Google |
| `Authentication__Providers__Google__ClientId` | No | Solo web | Si se activa Google |
| `Authentication__Providers__Google__ClientSecret` | **Sí** | Solo web | Si se activa Google |
| `Authentication__Providers__Discord__Enabled` | No | Solo web | Si se activa Discord |
| `Authentication__Providers__Discord__ClientId` | No | Solo web | Si se activa Discord |
| `Authentication__Providers__Discord__ClientSecret` | **Sí** | Solo web | Si se activa Discord |
| `Authentication__Providers__Facebook__Enabled` | No | Solo web | Si se activa Facebook |
| `Authentication__Providers__Facebook__AppId` | No | Solo web | Si se activa Facebook |
| `Authentication__Providers__Facebook__AppSecret` | **Sí** | Solo web | Si se activa Facebook |
| `Authentication__Cookie__ExpireMinutes` | No | Solo web | Opcional (43200 por defecto) |
| `AdminUser__Email` | No, pero **crítica e irreversible** | Solo web | De facto obligatoria **antes** del primer arranque |
| `AdminUser__Id` / `UserName` / `Country` | No | Solo web | Opcionales |

**Ninguna aplica a los cuatro Cloud Run Jobs.** No atienden HTTP, así que no pueden recibir un retorno OAuth, y `src/Ludeka.Jobs` no referencia `AuthenticationOptions`, `AdminUserOptions` ni `AddLudekaAuthentication`. `AdminUserSeeder` solo se invoca desde `Program.cs:183` del host web.

---

## 5. Cómo obtiene el maintainer su primer acceso administrativo

Es la pregunta central del incremento y el código la responde sin ambigüedad:

1. `AdminUserSeeder` crea, en el primer arranque contra base vacía, un `AppUser` con `Role = FoundingTeam` y `Permissions = All`, **con cero filas en `ExternalLogins`**. En esa fila no se puede iniciar sesión directamente.
2. La activa la rama **2a** de `ExternalLoginService.ResolveAsync`: el maintainer inicia sesión social con un correo **verificado** que coincide con `AdminUser:Email`; el método localiza la cuenta por correo, comprueba que no tiene proveedores vinculados, crea el vínculo y devuelve **esa misma cuenta** con sus permisos intactos.
3. `ExternalLoginEvents` firma entonces la cookie con rol `FoundingTeam` y permisos completos.

**El primer inicio de sesión social cuyo correo coincida con `AdminUser:Email` ES la puerta de entrada del fundador.** No hay paso adicional. Pero exige que ese valor se haya fijado correctamente **antes** del primer arranque, por B3.

Nada de esto está escrito en ningún documento del repositorio: es conducta emergente de dos incrementos distintos (INC-46 y INC-49).

---

## 6. Orden de operaciones viable

No contradice `docs/deployment/google-cloud-run.md` §9.0; lo complementa.

1. **(§9.0 paso 1)** Aprovisionar Supabase y **decidir ya** el correo real y verificable del fundador.
2. **(§9.0 pasos 2-3)** Proyecto de GCP, Artifact Registry, cuenta de servicio del planificador.
3. **Decidir la estrategia de dominio antes de registrar las apps OAuth.** Dos caminos: mapear un dominio propio a Cloud Run de antemano y registrar `https://<dominio>/signin-{proveedor}` sin depender del despliegue; o desplegar una vez sin proveedores para conocer la URL `*.run.app` autogenerada, registrar las apps contra ella y volver a desplegar. Ninguna documentación actual cubre el mapeo de dominio personalizado en Cloud Run.
4. **Corregir B1** antes de activar ningún proveedor.
5. **(§9.0 paso 4)** Secretos `GCP_PROJECT_ID` y `GCP_SA_KEY` más las claves de §4. **Aquí se dispara el primer despliegue real y el sembrado irreversible de B3.**
6. **(§9.0 pasos 5-7)** Cloud Run Jobs y Cloud Scheduler, independientes de todo lo anterior.

---

## 7. Preguntas abiertas para el maintainer

1. ¿Dominio personalizado desde el primer despliegue, o `*.run.app` temporal con doble pasada?
2. ¿Qué correo real, y bajo qué proveedor, será el del fundador?
3. ¿Entra Facebook en el primer despliegue, pese a la revisión de aplicaciones de Meta, o se limita a Google y Discord?

---

## 8. Alcance propuesto

**Dentro:**
- `UseForwardedHeaders` en `Program.cs`, con cobertura de prueba (B1).
- Cableado de `Authentication__Providers__*` y `AdminUser__*` en `ci-cd.yml` (B2, B3).
- Aviso de arranque cuando no hay ningún proveedor operativo (B4).
- Documentar en `google-cloud-run.md` el orden de registro de apps OAuth, la decisión de dominio y **cómo se obtiene el primer acceso de fundador**, que hoy no está escrito en ninguna parte.
- Corregir `ROADMAP.md:75` (B5).

**Fuera:**
- La cascada 2a/2b de INC-49: funciona correctamente; lo que falta es documentación de uso, no lógica.
- Revisión de aplicaciones de Meta para Facebook.
- Quinto Cloud Run Job del boletín semanal (§9.5), decisión pendiente y no relacionada.

---

## 9. Auditoría del orquestador sobre esta exploración

| Afirmación comprobada | Resultado |
|---|---|
| `UseForwardedHeaders` no existe en `src/Ludeka.Web/` | **Correcta** (verificado de forma independiente antes de leer el informe) |
| Rutas de retorno `/signin-{google,discord,facebook}` en `:35-37` | **Correcta** |
| `SecurePolicy = CookieSecurePolicy.Always` en `:54` | **Correcta** |
| `AdminUserSeeder` retorna si ya existe un `FoundingTeam` | **Correcta**, leído el bloque `:29-36` |
| Rama 2a vincula en silencio si la cuenta no tiene proveedores | **Correcta**, leído `:63-89` |
| `docker-compose.prod.yml:17-20` ya parametriza `AdminUser__*` | **Correcta** |
| `GetConfigurationWarnings` hace `continue` si `!Enabled` | **Correcta** |
| `UseHttpsRedirection()` incondicional en `Program.cs:203` | **Correcta** |
| PR #60 fusionado el 2026-09-19 | **Correcta**, confirmado con `gh pr view 60` |
| «`.env.example` no existe en el repositorio» | **FALSA.** `find` lo localiza en la raíz. El explorador usó `Glob`, que no ve ficheros ocultos. El contenido no se pudo inspeccionar porque el entorno bloquea la lectura de cualquier `.env*`, lo cual es correcto |

El hallazgo menor sobre `.env.example` queda retirado por falso y no entra en el alcance.

Este fichero lo escribió el orquestador: el agente `sdd-explore` no dispone de herramienta de escritura.
