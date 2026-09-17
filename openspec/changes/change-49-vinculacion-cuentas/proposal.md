# Propuesta SDD — INC-49: Vinculación de Cuentas entre Proveedores, Recuperación de Acceso y Política de Correo Ausente

> **Cambio:** `change-49-vinculacion-cuentas` · **Fase:** `sdd-propose` · **Fecha:** 2026-09-17
> **Rama / worktree:** `inc/vinculacion-cuentas` — `C:\repos\ludeka-wt\vinculacion-cuentas`
> **Dependencia:** INC-46 (Autenticación Real, archivado) · **Exploración:** [`exploration.md`](exploration.md)
> **Decisiones del maintainer:** las tres de INC-49 §3 están **cerradas**, más una cuarta acordada al aprobar la propuesta (sección 3 de este documento).

**En una frase:** este incremento entrega la pantalla `/cuenta/conexiones` y la vinculación desde sesión activa, que son el único mecanismo de recuperación posible para una cuenta sin correo verificado, y convierte la regla «solo se fusiona con correo verificado» en un comportamiento explícito y auditado en lugar de un efecto colateral de la cascada de acceso.

---

## 1. Intención

INC-46 resolvió el acceso, pero dejó abierta una consecuencia que puede costarle la cuenta a un usuario.

### 1.1 Sin correo, perder el proveedor es perder la cuenta

`AppUser.Email` es obligatorio: la entidad lanza `ArgumentException` si llega vacío (`AppUser.cs:40-41`). Cuando un proveedor no entrega correo —Facebook lo omite si el usuario no autoriza el permiso, o si la aplicación no ha pasado la revisión de Meta—, `ExternalLoginService` aprovisiona la cuenta con un correo sintético no enrutable en el dominio reservado `.invalid` (`ExternalLoginService.cs:16,88-89`; formato real `<clave>@<proveedor>.ludeka.invalid`).

Esa cuenta no tiene ninguna vía de contacto. Si la persona pierde el acceso a su Discord, **no hay forma de recuperar su ludoteca, sus préstamos ni su diario de partidas**: no hay correo al que enviar nada y no hay segundo método de acceso. Vincular un segundo proveedor es la única red de seguridad cuando se ha decidido deliberadamente no usar correo y contraseña.

### 1.2 La cuenta partida en dos

La cascada de `ExternalLoginService.ResolveAsync` (`ExternalLoginService.cs:28-80`) resuelve en este orden:

1. Por `(Provider, ProviderKey)` — la identidad única en ese proveedor (`:43-52`).
2. Por **correo verificado** contra `AppUser.Email` (`:54-64`).
3. Alta de un `AppUser` nuevo como `CommunityUser` (`:66-79`).

Si la cuenta nació en el paso 3 con correo sintético y esa misma persona entra después con Google, que sí entrega correo verificado, **el paso 2 no encuentra nada**: la cuenta está archivada bajo `12345@discord.ludeka.invalid`, no bajo su correo real. El resultado es una segunda cuenta, con la ludoteca, los préstamos y el diario partidos en dos.

### 1.3 Por qué no se fusiona automáticamente

Lo tentador sería «si el correo coincide, fusiono». Es exactamente el vector de robo de cuenta: quien pueda registrarse en un proveedor con `ana@gmail.com` sin que ese correo esté verificado accedería a la cuenta de Ana en Ludeka. La regla **«solo se fusiona con correo verificado»** no es pedantería: es la frontera de seguridad del incremento (sección 4).

### 1.4 La costura ya estaba prevista

INC-46 aplazó esta pantalla a propósito. Su diseño la dejó anotada como «la pantalla futura es UI más `LinkAsync(...)`», la especificación viva la declara trasladada al INC-49 (`docs/specs/sistema/32-autenticacion-y-autorizacion.md:64,152`) y la especificación de la capacidad conserva un requisito abierto —«Vinculación manual de proveedores (decisión pendiente de diseño)»— con las dos ramas posibles (`openspec/specs/social-login-authentication/spec.md:93-107`). Este incremento cierra ese requisito por la rama incluida.

---

## 2. Alcance

### 2.1 Dentro del alcance, por capa

| Capa | Entrega | Rutas concretas |
|---|---|---|
| **Core** | Columna `ProviderEmailVerifiedAt` (`DateTimeOffset?`) en `ExternalLogin`; dos valores nuevos de `AuditAction` (`LinkedProvider`, `UnlinkedProvider`) | `src/Ludeka.Core/Entities/ExternalLogin.cs` · `src/Ludeka.Core/Enums/AuditAction.cs` |
| **Application** | `LinkAsync` / `UnlinkAsync` en `IExternalLoginService`; guarda del último método de acceso **en servidor**; ampliación de `IExternalLoginRepository` con listar por usuario y borrar; lectura «¿tiene correo verificado?» en un contrato **nuevo** `IAccountConnectionsService` (ver nota bajo la tabla); **reemplazo del correo sintético por el verificado** (decisión 4); rama de auditoría en `AuditService.GetActionDisplayName` | `Features/Identity/ExternalLoginService.cs` · `Contracts/IExternalLoginRepository.cs` · `Contracts/IAccountConnectionsService.cs` (nuevo) · `Features/Admin/AuditService.cs` |
| **Infrastructure** | Implementación EF de los dos métodos de repositorio nuevos; migración EF Core **aditiva**; reconciliación SQLite de la columna | `Data/ExternalLoginRepository.cs` · `Data/LudekaDbContext.cs` · `Migrations/` · `Data/SqliteSchemaMigrator.cs` |
| **Web** | Página `/cuenta/conexiones`; endpoints de desafío y de desvinculación; bifurcación de intención en `ExternalLoginEvents`; aviso de correo no verificado en conexiones y en la cabecera | `Components/Pages/` (página nueva) · `Program.cs` · `Authentication/ExternalLoginEvents.cs` · `Components/Layout/MainLayout.razor` · `Services/AuthenticatedCurrentUserService.cs` |
| **Tests** | Cobertura TDD de las cinco conductas de INC-49 §2.8, más regresión explícita de la cascada de INC-46 | `tests/Ludeka.UnitTests/` (Domain, Application, Infrastructure, Web) |

Desglose funcional:

- **Pantalla `/cuenta/conexiones`** para usuario autenticado: listado de los proveedores habilitados por configuración (`ExternalAuthenticationSchemes.GetEnabledProviders`, `:78-99`, y `DisplayNameFor`, `:144-150`, reutilizables tal cual), cuáles están vinculados, y botones de vincular y desvincular.
- **Vinculación desde sesión activa que nunca crea cuenta nueva.** Hoy el único desafío es `POST /login/external` (`Program.cs:455-482`) con `RedirectUri` fijo a `"/"` y sin ningún parámetro de intención (`:481`), y todo desafío desemboca siempre en `ExternalLoginEvents.HandleTicketReceivedAsync` (`:37-72`), que siempre llama `ResolveAsync` (`:60`). Hace falta (a) un endpoint de desafío nuevo que marque la intención y el `UserId` de la sesión en `AuthenticationProperties.Items`, y (b) una bifurcación en ese manejador hacia `LinkAsync` en vez de `ResolveAsync`.
- **Ampliación de `IExternalLoginRepository`.** El contrato actual tiene exactamente dos métodos: `GetByProviderKeyAsync` (`IExternalLoginRepository.cs:20`) y `AddAsync` (`:27`). No hay forma de listar ni de borrar los vínculos de un usuario: toda la pantalla depende de un contrato que aún no existe.
- **Guarda del último método de acceso.** No se puede desvincular el único proveedor que queda. La comprobación es **en servidor**, no solo deshabilitando el botón.
- **Flujo de colisión: avisar, nunca fusionar en silencio.** **Corregido tras `sdd-spec`:** la rama 2 de `ResolveAsync` (`:54-64`) es hoy **más amplia** que la excepción segura de INC-49 §2.4 — vincula a la cuenta que coincide por correo verificado **sin comprobar si esa cuenta ya tiene otros proveedores vinculados**. Hay que partirla en **2a** (cuenta sin proveedores → vinculación automática, conducta actual conservada) y **2b** (cuenta con proveedores → colisión, avisar sin fusionar).
- **Aviso de cuenta sin correo verificado** en `/cuenta/conexiones` y en la cabecera (decisión 3).
- **Reemplazo del correo sintético** (decisión 4): cuando una cuenta nacida con `<clave>@<proveedor>.ludeka.invalid` recibe un correo verificado, `AppUser.Email` pasa a ser ese correo real, previa comprobación del índice único de `AppUsers.Email`.
- **Auditoría de vincular y desvincular** siguiendo el patrón existente `IAuditService.RecordChangeAsync` (`IAuditService.cs:11-15` → `AuditService.cs:27-55`), que ya exige `SessionIdentity.Require(command.UserId)` (`:33`).
- **Configuración:** reutiliza `Authentication__Providers__*` de INC-46. Cero credenciales nuevas.

### 2.2 Fuera del alcance (explícito)

| Se excluye | Motivo |
|---|---|
| **Herramienta de fusión de cuentas ya duplicadas** | Decisión 1 del maintainer: no existe todavía base de datos de producción, así que el escenario no puede darse. Ver sección 3.1. |
| **Guía operativa del procedimiento manual de fusión** | Misma decisión: sería documentación para un escenario que hoy no puede producirse. |
| **Aviso de «sin correo verificado» en el perfil público** | `PublicProfile.razor` lo ve cualquiera; publicar ahí que una cuenta carece de correo verificado filtraría a desconocidos una debilidad de esa cuenta concreta. |
| **Credenciales o proveedores nuevos** | Se reutiliza la configuración de INC-46 sin tocarla. |
| **Cualquier migración destructiva** | INC-49 §5 lo prohíbe; la migración de la decisión 2 es estrictamente aditiva y anulable. |
| **Cuentas locales con correo y contraseña, e infraestructura de correo** | Fuera del modelo de identidad de Ludeka desde INC-46. |

---

## 3. Decisiones cerradas del maintainer

Las tres decisiones de INC-49 §3 están resueltas, más una cuarta que surgió al revisar esta propuesta. Se recogen aquí como hechos, no como opciones.

### 3.1 Decisión 1 — La fusión de cuentas ya duplicadas queda fuera de alcance

El maintainer lo cierra con un hecho verificable del estado del proyecto: *«los datos son ficticios ahora mismo, todavía no se ha generado la base de datos de producción, no hay peligro de cuentas que ya existen»*.

En consecuencia, ni herramienta de fusión ni guía manual. **La prevención es lo que este incremento entrega** (§2.1-§2.5 del documento de incremento): la pantalla de conexiones es la vía de recuperación, y §2.6 queda reducida a eso.

El inventario de las 13 entidades que cuelgan de `AppUser`, con sus dos puntos de conflicto real (`UserGameReview`, con índice único `(UserId,GameId)`, y `UserPreference`, cuyo `UserId` es la clave primaria), queda registrado en [`exploration.md` § «Decisión 1»](exploration.md) como evidencia para el día en que el problema sea real. No se repite aquí.

### 3.2 Decisión 2 — Columna aditiva `ProviderEmailVerifiedAt` en `ExternalLogins`

Se añade `ProviderEmailVerifiedAt` (`DateTimeOffset?`) a la tabla `ExternalLogins`, con migración EF Core **aditiva y no destructiva** más la actualización de `SqliteSchemaMigrator.cs`.

**Esta decisión corrige una suposición del documento de incremento.** INC-49 §2.5 daba por preferible «derivarlo de `ExternalLogin`: una cuenta está verificada si tiene al menos una fila con `ProviderEmail` no nulo». La exploración demuestra que esa derivación es **falsa hoy**: `ExternalLoginService.cs:76-77` persiste el correo crudo (`normalizedEmail`) en `ProviderEmail` sin condicionarlo a `emailVerified`, y el comentario de las líneas 67-68 declara que es **deliberado** (el correo sin verificar *«queda únicamente como pista en `ExternalLogin.ProviderEmail`»*). Por tanto `ProviderEmail != null` **no** implica verificado, y una cuenta creada por la rama 3 con un correo no verificado daría un falso positivo.

La columna explícita conserva esa pista que INC-46 quiso guardar, deja el invariante reforzado **por el esquema** y no por disciplina de código, y sigue sin ser una migración destructiva, que es lo único que INC-49 §5 prohíbe.

Descartadas:

- **Condicionar la escritura sin migración** (`ProviderEmail = emailVerified ? normalizedEmail : null`): revierte una decisión deliberada de INC-46, destruye la pista y descansa en una convención que la base de datos no impide romper.
- **Bandera en `AppUser`**: duplica el estado en dos sitios que pueden divergir.

### 3.3 Decisión 3 — Aviso en `/cuenta/conexiones` **y** en la cabecera

El aviso *«Tu cuenta no tiene un correo verificado. Vincula otro proveedor de acceso para no perderla.»* se muestra en la pantalla de conexiones **y** como aviso discreto y descartable en la cabecera (`MainLayout.razor`, que ya inyecta `ICurrentUserService` en la línea 6 y renderiza bloques condicionales por rol y permiso en las líneas 100-198), enlazando a `/cuenta/conexiones`.

**Coste explícito:** `ICurrentUserService` (`:11-34`) **no** expone hoy «¿tiene correo verificado?».

> **Corregido tras `sdd-design`.** Esta propuesta daba por mitigación que ampliar ese contrato «lo garantiza la compilación». **Es falso.** `CurrentUserContractTests.cs:41-59` congela la superficie por igualdad exacta (`Assert.Equal(["IsFoundingTeam","Roles","UserId","UserName"], properties)`), con la intención declarada de que «la superficie queda congelada»: añadir una propiedad rompe una prueba verde de INC-46 **en ejecución**, no al compilar. Y el radio es mayor del estimado: implementan el contrato **cuatro** tipos, no dos (`AuthenticatedCurrentUserService`, `StubCurrentUserService`, `FakeCurrentUser` en `GameEditorWebIntegrationTests.cs:152` y `FakeCurrentUserService` en `UserLocationServiceTests.cs:16`).
>
> Y el radio es **mucho** mayor del estimado: implementan el contrato **16 clases** (recuento verificado), una de producción y quince dobles de prueba repartidos por toda la suite.
>
> Por eso el diseño **no amplía `ICurrentUserService`**: introduce un contrato nuevo `IAccountConnectionsService` y deja intacta la guarda de INC-46. Ver `design.md`.

**Descartado el perfil público:** `PublicProfile.razor` lo ve cualquiera.

### 3.4 Decisión 4 — El correo sintético **se reemplaza** por el verificado

Cuando una cuenta nacida con `<clave>@<proveedor>.ludeka.invalid` recibe después un correo verificado —al vincular un proveedor que sí lo entrega—, `AppUser.Email` **se reemplaza** por ese correo real. No se conserva el sintético como histórico.

Es la segunda mitad de INC-49 §3.2, que la decisión 2 no resolvía: aquella fija **cómo se representa** la verificación (`ProviderEmailVerifiedAt`), esta fija **qué pasa con el correo de la cuenta**. Al haber columna, responder «¿tiene correo verificado?» ya no depende de este reemplazo, así que no era bloqueante; el maintainer decide incluirlo igualmente en el alcance.

Motivo: la cuenta queda archivada bajo el correo real de la persona, que es más limpio y deja abierta la vía de contacto. El coste aceptado es perder el rastro de que la cuenta nació sin correo; ese rastro no se pierde del todo, porque `ExternalLogin.ProviderEmail` y `ProviderEmailVerifiedAt` conservan el origen de cada vínculo.

**Restricción obligatoria:** antes de reemplazar hay que comprobar que ningún otro `AppUser` tenga ya ese correo, o la actualización viola el índice único de `AppUsers.Email` (`LudekaDbContext.cs:363`). Es el mismo criterio que ya aplica `GetByEmailAsync` en la cascada y coincide con el chequeo que el flujo de colisión (§4.2) exige de todos modos. `AppUser.UpdateProfile` (`AppUser.cs:101-112`) e `IUserRepository.UpdateAsync` (`IUserRepository.cs:18`) ya existen: no hace falta mutador nuevo.

---

## 4. Frontera de seguridad

Esta sección es el corazón del incremento, no un detalle de implementación.

### 4.1 La regla

> **Solo se fusiona con correo verificado. Cualquier otra unión de identidades la inicia el usuario desde una sesión ya establecida.**

Si se fusionara por correo no verificado, bastaría registrarse en cualquier proveedor declarando el correo de otra persona para entrar en su cuenta de Ludeka. El `emailVerified` que entrega el proveedor —proyectado como `ludeka:email_verified` desde `email_verified` en Google y `verified` en Discord y Facebook— es la única prueba de que quien llega controla esa dirección.

### 4.2 Las cuatro conductas que materializan la regla

| Situación | Conducta | Por qué es segura |
|---|---|---|
| El par `(Provider, ProviderKey)` ya existe | Resuelve a la misma cuenta, sin filas nuevas | El proveedor certifica la identidad; el índice único `(Provider, ProviderKey)` lo garantiza |
| Correo **verificado** que coincide con una cuenta **sin proveedores previos** | Vinculación automática (rama 2, `:54-64`) | Excepción segura y ya implementada: no hay sesión ajena que secuestrar |
| Correo verificado que coincide con **otra cuenta que ya tiene proveedores**, o identidad entrante nueva sobre cuenta poblada | **Avisar, nunca fusionar**: *«Ya existe una cuenta con este correo. Inicia sesión con el método que ya usas y vincula este proveedor desde Ajustes → Conexiones.»* | La unión la decide quien demuestra controlar la cuenta destino, no quien llega |
| Vinculación desde `/cuenta/conexiones` | Se asocia **siempre** al `UserId` de la sesión activa; jamás crea cuenta | Patrón de Slack y Notion: la sesión es la prueba de propiedad, el desafío OAuth es la prueba de la identidad entrante |

### 4.3 Por qué la vinculación siempre la inicia el usuario desde sesión

Vincular exige **dos pruebas simultáneas**: la sesión de Ludeka demuestra la propiedad de la cuenta destino, y el desafío OAuth —emitido por el proveedor, no por nosotros— demuestra el control de la identidad entrante. Ninguna de las dos por separado basta. Por eso el `UserId` se captura del servidor antes del desafío y viaja en `AuthenticationProperties.Items` (contenido firmado por el manejador de cookies), nunca como parámetro del formulario que el navegador pueda alterar. Y por eso toda vinculación y desvinculación se audita.

### 4.4 La guarda del último método no es comodidad, es seguridad

Desvincular el único proveedor que queda deja la cuenta inaccesible para siempre: no hay correo al que enviar una recuperación. La comprobación vive en el servidor (`SessionIdentity.Require(...)`, `SessionIdentity.cs:23-40`, más relectura del propio `AppUser`), porque deshabilitar el botón en la interfaz no impide una petición fabricada.

---

## 5. Capacidades

> Contrato con la fase `sdd-spec`.

### 5.1 Capacidades nuevas

- `account-provider-connections`: autoservicio de conexiones de la propia cuenta — pantalla `/cuenta/conexiones`, vinculación y desvinculación desde sesión activa, guarda del último método de acceso, y aviso de cuenta sin correo verificado. Incluye el patrón de acceso nuevo «ruta que exige solo sesión, sin permiso granular».

### 5.2 Capacidades modificadas

- `social-login-authentication`: **cierra** el requisito «Vinculación manual de proveedores (decisión pendiente de diseño)» (`spec.md:93-107`) por la **rama incluida**; añade `ProviderEmailVerifiedAt` a la forma documentada de `ExternalLogin`; y fija el comportamiento observable de la colisión en el inicio de sesión (avisar, nunca fusionar en silencio), conservando la rama 2 de la cascada.
- `user-management-permissions-audit`: dos valores nuevos de `AuditAction` (`LinkedProvider`, `UnlinkedProvider`) con su nombre visible en la bitácora.

### 5.3 Deliberadamente no modificada

- `policy-based-authorization`: su requisito «Protección de las rutas administrativas y de moderación» está acotado a rutas de administración y moderación. `/cuenta/conexiones` no es ninguna de las dos: es autoservicio del propio usuario. El patrón de acceso nuevo se documenta dentro de `account-provider-connections`. Si `sdd-spec` concluye que hace falta además una referencia cruzada en `policy-based-authorization`, que la añada de forma razonada; esta propuesta no la impone.

---

## 6. Enfoque

**Construir de dentro hacia fuera: esquema → contrato → lógica de dominio → transporte → interfaz.** Cada capa queda cubierta por pruebas antes de que exista la siguiente, de modo que la conducta crítica (la guarda del último método y la negativa a fusionar) esté fijada en el servidor antes de que ningún botón pueda alcanzarla.

1. **Esquema y contrato.** Columna `ProviderEmailVerifiedAt` con migración aditiva, y ampliación de `IExternalLoginRepository` con listar por usuario y borrar. Sin esto, ni la pantalla ni el aviso tienen de dónde leer.
2. **Lógica de aplicación.** `LinkAsync` y `UnlinkAsync` en `ExternalLoginService`, con la guarda del último método. El patrón de sesión reutilizable **no** es `ISessionPermissionGuard` —que exige una bandera `ModeratorPermission` concreta y esto es autoservicio de cualquier autenticado—, sino `SessionIdentity.Require(...)` para obtener el `UserId` de forma fiable más una relectura `AsNoTracking` del propio `AppUser`, el mismo mecanismo sin la comprobación de permiso.
3. **Transporte.** Endpoint de desafío con marca de intención en `AuthenticationProperties.Items` y bifurcación en `ExternalLoginEvents.HandleTicketReceivedAsync`. El botón de vincular debe reproducir el patrón de formulario HTTP clásico de `Login.razor:35` (`<form method="post" … data-enhance="false">`), **no** un `@onclick` de Blazor: `Results.Challenge` necesita escribir cabeceras HTTP de redirección, algo que un circuito interactivo por SignalR no puede hacer.
4. **Interfaz.** Página `/cuenta/conexiones` con `[Authorize]` simple. El mecanismo ya existe (`Routes.razor:1-10` con `AuthorizeRouteView` → `RedirectToLogin`), pero no hay precedente exacto: las 10 páginas `[Authorize]` actuales van todas atadas a una de las 11 políticas de `ModeratorPermission` (`AuthorizationPolicies.cs:14-24`), y la página más parecida por audiencia, `MyLibrary.razor`, es pública y degrada con un aviso en vez de redirigir (`:60-70`). El diseño debe fijar ese patrón nuevo explícitamente.
5. **Colisión y aviso.** Mensaje de colisión en el login y aviso de correo no verificado en conexiones y cabecera, con auditoría de ambas operaciones.

La migración aditiva exige **dos** tratamientos en `SqliteSchemaMigrator.cs`: el bloque 22 actual (`:843-862`) solo crea la tabla `ExternalLogins` cuando **no existe**, así que las bases de datos SQLite ya creadas necesitan además una rama `ALTER TABLE … ADD COLUMN`, siguiendo el patrón que el propio fichero ya usa para `Giveaways.IsPromoted` (`:389-394`) y `Stores.Country` (`:455-460`).

---

## 7. Áreas afectadas

| Área | Impacto | Descripción |
|---|---|---|
| `src/Ludeka.Core/Entities/ExternalLogin.cs` | Modificado | Propiedad `ProviderEmailVerifiedAt` y constructor validante |
| `src/Ludeka.Core/Enums/AuditAction.cs` | Modificado | `LinkedProvider`, `UnlinkedProvider` |
| `src/Ludeka.Application/Contracts/IExternalLoginRepository.cs` | Modificado | Listar por usuario y borrar (hoy solo `GetByProviderKeyAsync` y `AddAsync`) |
| `src/Ludeka.Application/Features/Identity/ExternalLoginService.cs` + `IExternalLoginService.cs` | Modificado | `LinkAsync`, `UnlinkAsync`, guarda del último método, escritura de `ProviderEmailVerifiedAt` en la cascada, y reemplazo del correo sintético vía `AppUser.UpdateProfile` + `IUserRepository.UpdateAsync` (ambos ya existentes) |
| `src/Ludeka.Application/Contracts/IAccountConnectionsService.cs` | Nuevo | Lectura «¿tiene correo verificado?» y estado de conexiones. **No** se amplía `ICurrentUserService`: su superficie está congelada por `CurrentUserContractTests.cs:41-59` |
| `src/Ludeka.Application/Features/Admin/AuditService.cs` | Modificado | Rama en `GetActionDisplayName` (`:114-124`) |
| `src/Ludeka.Infrastructure/Data/ExternalLoginRepository.cs` | Modificado | Implementación EF de los dos métodos nuevos |
| `src/Ludeka.Infrastructure/Data/LudekaDbContext.cs` | Modificado | Mapeo Fluent de la columna (`:481-494`) |
| `src/Ludeka.Infrastructure/Migrations/` | Nuevo | Migración aditiva `ProviderEmailVerifiedAt` |
| `src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs` | Modificado | Columna en la creación (`:843-862`) y rama `ALTER TABLE` para bases ya existentes |
| `src/Ludeka.Web/Program.cs` | Modificado | Endpoints de vincular y desvincular, junto a `POST /login/external` (`:455-482`) |
| `src/Ludeka.Web/Authentication/ExternalLoginEvents.cs` | Modificado | Bifurcación login / vinculación en `HandleTicketReceivedAsync` (`:37-72`) |
| `src/Ludeka.Web/Components/Pages/` | Nuevo | Página `/cuenta/conexiones` (no existe hoy ninguna ruta bajo `/cuenta`) |
| `src/Ludeka.Web/Components/Layout/MainLayout.razor` | Modificado | Aviso discreto y descartable en cabecera (`:100-198`) |
| `src/Ludeka.Web/Services/AuthenticatedCurrentUserService.cs` | Modificado | Implementa la lectura nueva del contrato |
| `tests/Ludeka.UnitTests/` | Nuevo / Modificado | Vinculación, desvinculación, guarda del último método, colisión, aviso, y regresión de la cascada de INC-46; actualización de `StubCurrentUserService` |

---

## 8. Riesgos

| Riesgo | Prob. | Impacto | Mitigación |
|---|---|---|---|
| Tocar `ResolveAsync` para el flujo de colisión rompe la cascada de INC-46, hoy cubierta por **1345 pruebas en verde** | Alta | Alto | TDD estricto con pruebas de regresión explícitas de las tres ramas (`ExternalLoginServiceTests.cs:51-146`) **antes** de modificar el método; la rama 2 (excepción segura) se fija por aserción, no por confianza |
| Un error en la guarda del último método deja cuentas inaccesibles para siempre | Media | Muy alto | Comprobación en servidor, no solo en la interfaz; prueba dedicada obligatoria; la desvinculación se audita |
| **12 de las 13 entidades** que referencian `AppUser` no tienen clave foránea real (solo `ExternalLogin` la tiene, `LudekaDbContext.cs:491-494`): ni fusión ni borrado tienen red de integridad referencial | Media | Alto | Este incremento **no borra ni reasigna cuentas**: solo añade y quita filas de `ExternalLogins`, la única tabla con FK real. El riesgo queda documentado para cuando exista la herramienta de fusión |
| `/cuenta/conexiones` es la primera página que exige **solo autenticación** sin permiso granular; sin precedente exacto | Media | Medio | El diseño fija el patrón explícitamente; prueba de que un anónimo es redirigido a `/login` tanto en SSR como en render interactivo (`RedirectToLogin.razor:10-17` solo fuerza navegación en interactivo) |
| La migración aditiva no llega a bases SQLite ya creadas si solo se toca el bloque de creación de tabla | Media | Medio | Rama `ALTER TABLE … ADD COLUMN` en `SqliteSchemaMigrator.cs` además de la columna en el `CREATE TABLE`, con prueba de persistencia |
| ~~Ampliar `ICurrentUserService` rompe implementaciones y dobles de prueba en cascada~~ **Riesgo reevaluado por `sdd-design`: la mitigación original era falsa** | Alta | Medio | **No se amplía.** `CurrentUserContractTests.cs:41-59` congela la superficie por igualdad exacta y el fallo sería en ejecución, no al compilar; además lo implementan 16 clases, no dos. El diseño introduce `IAccountConnectionsService` y deja la guarda de INC-46 intacta |
| La vinculación desde sesión abre una vía de apropiación si la intención se puede falsificar | Baja | Muy alto | El `UserId` se captura en servidor y viaja en `AuthenticationProperties.Items`, no en el formulario; se reconfirma contra la sesión al volver del proveedor; toda vinculación se audita |
| El aviso de cuenta sin correo se percibe como alarma innecesaria | Baja | Bajo | Redacción clara y accionable, sin lenguaje técnico; descartable en la cabecera; nunca en el perfil público |
| El reemplazo del correo sintético (decisión 4) viola el índice único de `AppUsers.Email` si ese correo ya pertenece a otra cuenta | Media | Alto | Comprobación previa obligatoria con el mismo criterio de `GetByEmailAsync`; si hay conflicto, **no se reemplaza** y la cuenta conserva el sintético; prueba dedicada del caso en conflicto, no solo del camino feliz |

---

## 9. Plan de reversión

1. **Antes del merge:** revertir el PR de la cadena que falle. Cada PR de la sección 11 es autónomo y deja la suite en verde por sí mismo, así que se puede descartar el último sin tocar los anteriores.
2. **Después del merge:** revertir los PR en orden inverso. Nada de este incremento borra ni reasigna datos existentes.
3. **La migración es segura de revertir:** `ProviderEmailVerifiedAt` es aditiva y anulable. Una reversión de código sin revertir la migración deja una columna anulable sin lectores — inerte, no corruptora. En PostgreSQL, el `Down` de la migración la elimina; en SQLite, la columna sobrante es ignorada por el reconciliador.
4. **La línea base conocida** es `main` con INC-46 archivado y 1345 pruebas en verde.

---

## 10. Dependencias

- **INC-46 archivado**, que aporta `ExternalLogin`, la cascada, la cookie de sesión y las políticas de permiso.
- **Credenciales OAuth existentes** (`Authentication__Providers__*`). Cero credenciales nuevas: las URIs de callback `/signin-google`, `/signin-discord` y `/signin-facebook` ya están registradas y este incremento no añade esquemas.
- **`dotnet test Ludeka.sln`** como runner contractual con Strict TDD activo.
- Sin dependencias de INC-47 ni INC-48: este incremento no toca despliegue ni persistencia de producción más allá de una columna aditiva.

---

## 11. Criterios de aceptación

Alineados con INC-49 §4 y ajustados a las decisiones cerradas.

- [ ] Existe `/cuenta/conexiones`, accesible solo con sesión iniciada, donde el usuario ve los proveedores habilitados, cuáles tiene vinculados, y puede vincular y desvincular. Un anónimo es redirigido a `/login`.
- [ ] Vincular desde la sesión asocia la identidad externa al `UserId` autenticado y **nunca crea una cuenta nueva**.
- [ ] Es **imposible desvincular el último método de acceso**, comprobado en servidor: una petición fabricada que salte la interfaz también se deniega.
- [ ] Un inicio de sesión en colisión **no fusiona en silencio** y muestra el mensaje que dirige a Ajustes → Conexiones.
- [ ] Un correo **verificado** que coincide con una cuenta **sin proveedores vinculados** sigue vinculando automáticamente (rama 2 intacta).
- [ ] Una cuenta sin correo verificado ve el aviso en `/cuenta/conexiones` **y** en la cabecera; **nunca** en el perfil público.
- [ ] `ExternalLogins.ProviderEmailVerifiedAt` existe en PostgreSQL vía migración aditiva y en SQLite vía reconciliador, tanto al crear la tabla como al reconciliar una base preexistente. Ninguna migración destructiva.
- [ ] Una cuenta con correo sintético que vincula un proveedor con correo **verificado** ve su `AppUser.Email` reemplazado por el correo real; si ese correo ya pertenece a otro `AppUser`, el reemplazo **no se hace** y no se viola el índice único.
- [ ] Vincular y desvincular quedan registrados en la bitácora con `LinkedProvider` / `UnlinkedProvider` y su nombre visible.
- [ ] `dotnet test Ludeka.sln` en verde, sin regresión sobre las 1345 pruebas de INC-46.
- [ ] Smoke test con navegador real del ciclo vincular → desvincular → intento de desvincular el último.

---

## 12. Preguntas abiertas para el diseño

Ninguna reabre las cuatro decisiones cerradas. Son cuestiones distintas que la fase de diseño debe resolver. **P1 ya no figura aquí: el maintainer lo resolvió al aprobar esta propuesta y es ahora la decisión 4 (§3.4).**

| # | Cuestión | Quién decide | Si no se resuelve |
|---|---|---|---|
| **P2** | **Cabo suelto del alcance recortado: vincular un proveedor que ya pertenece a otra cuenta.** El índice único `(Provider, ProviderKey)` lo hace imposible por esquema, y sin herramienta de fusión no hay salida. El mensaje de colisión de §2.4 («inicia sesión con el método que ya usas y vincula desde Conexiones») **no sirve aquí**: el proveedor ya está repartido. El diseño debe especificar un rechazo claro que no prometa una resolución inexistente, y que **jamás** mueva la fila de una cuenta a otra | `sdd-design` | Riesgo real de un mensaje que promete algo que el sistema no puede hacer |
| **P3** | **Mecanismo de descarte del aviso de cabecera.** «Descartable» implica estado, y no existe hoy ningún campo ni componente de avisos reutilizable. El diseño debe elegir el mecanismo más barato que no exija esquema nuevo (por ejemplo, ámbito de sesión o de circuito), coherente con `SessionGuard` (`MainLayout.razor:302`) | `sdd-design` | Se implementaría como aviso no descartable, contra la decisión 3 |
| **P4** | **Momento exacto en que se escribe `ProviderEmailVerifiedAt`.** Afecta a las tres ramas de `ResolveAsync` y a `LinkAsync`. Debe quedar fijado por prueba, porque es el invariante del que cuelga todo el aviso | `sdd-design` | Lectura de «correo verificado» incorrecta desde el primer commit |
| **P5** | **Alcance del reemplazo del correo sintético (decisión 4).** ¿Se dispara solo en `LinkAsync` —vinculación explícita desde `/cuenta/conexiones`— o también al volver a entrar por la rama 1 de `ResolveAsync` cuando el proveedor ya vinculado entrega ahora un correo verificado que antes no daba? La primera opción es acotada y predecible; la segunda cubre más casos reales pero convierte cada inicio de sesión en una posible escritura. El diseño debe elegir y fijarlo por prueba, junto con la comprobación previa del índice único | `sdd-design` | Comportamiento inconsistente entre vincular e iniciar sesión, o escrituras inesperadas en cada acceso |

---

## 13. Plan de entrega en PRs encadenados

**Estrategia:** `auto-chain`, presupuesto de **400 líneas modificadas** (añadidas + borradas, contenido autorado) por PR.

### 13.1 Volumen recalculado

La exploración estimó **870-1460 líneas** con las opciones más baratas de cada par (sin migración, con guía manual). Las decisiones cerradas cambian ese cálculo en dos direcciones y **hacia arriba** en neto: la decisión 1 no reduce el cálculo —la herramienta de fusión nunca estuvo en la estimación, y la guía manual era documentación— mientras que la decisión 2 añade entidad, mapeo, migración y reconciliador SQLite, la decisión 3 lleva el aviso a su banda alta por la ampliación de `ICurrentUserService` y sus implementaciones, y la decisión 4 añade la unidad **I** completa.

Recálculo por unidad de trabajo, **con sus pruebas TDD incluidas en cada fila** (la exploración las contabilizaba aparte, lo que duplicaba parte del conteo):

| Unidad | Producción | Pruebas | Total |
|---|---|---|---|
| **A.** Repositorio: listar por usuario y borrar (contrato + EF) | 45-70 | 40-70 | **85-140** |
| **B.** Columna `ProviderEmailVerifiedAt`: entidad, Fluent, migración, `SqliteSchemaMigrator` | 60-90 | 40-70 | **100-160** |
| **C.** `LinkAsync` / `UnlinkAsync` + guarda del último método en servidor | 90-140 | 90-150 | **180-290** |
| **D.** Endpoints + bifurcación de intención en `ExternalLoginEvents` | 80-120 | 50-90 | **130-210** |
| **E.** Página `/cuenta/conexiones` | 150-250 | 40-80 | **190-330** |
| **F.** Flujo de colisión + regresión de la cascada | 60-110 | 80-140 | **140-250** |
| **G.** Aviso de correo no verificado: contrato, 2 implementaciones, conexiones y cabecera | 80-130 | 50-80 | **130-210** |
| **H.** Auditoría de vincular y desvincular | 20-35 | 15-30 | **35-65** |
| **I.** Reemplazo del correo sintético por el verificado (decisión 4), con comprobación previa del índice único | 40-70 | 40-70 | **80-140** |
| **Total** | **625-1015** | **445-780** | **1070-1795** |

**Nota de conteo:** los ficheros `.Designer.cs` y `LudekaDbContextModelSnapshot.cs` que genera `dotnet ef migrations add` son artefactos generados, no contenido autorado, y quedan fuera del presupuesto de 400 líneas aunque aparezcan en el diff. El PR que los incluya debe decirlo en su descripción para que el revisor no cargue con ellos.

### 13.2 Partición propuesta: 7 PRs encadenados

| PR | Contenido | Líneas autoradas | Por qué aquí |
|---|---|---|---|
| **#1 — Cimientos de datos y contrato** | A + B + H | **220-365** | Nada visible para el usuario: esquema, contrato de repositorio y valores de auditoría. Es lo que todo lo demás lee y escribe, y lo más barato de revertir si algo falla |
| **#2 — Vinculación y desvinculación en Application** | C | **180-290** | La conducta crítica del incremento (guarda del último método) queda fijada por prueba en el servidor **antes** de que exista ningún endpoint ni botón que la alcance |
| **#3 — Transporte OAuth** | D | **130-210** | Aislado a propósito: es la única pieza que toca `ExternalLoginEvents`, compartido con el camino de login. Si aparece una regresión de acceso, el PR culpable es evidente |
| **#4 — Pantalla `/cuenta/conexiones`** | E | **190-330** | Necesita un destino `POST` que funcione (#3) y la lógica ya probada (#2). Establece el patrón de página con `[Authorize]` simple |
| **#5 — Flujo de colisión** | F | **140-250** | Separado porque modifica `ResolveAsync`, cubierto por las 1345 pruebas de INC-46. Un PR propio mantiene el radio de impacto identificable |
| **#6 — Aviso de correo no verificado** | G | **130-210** | Depende de la columna de #1; es el cambio de mayor superficie y menor riesgo: contrato `ICurrentUserService` y sus implementaciones |
| **#7 — Reemplazo del correo sintético** | I | **80-140** | Lógica de Application que solo necesita la columna de #1 y `LinkAsync` de #2. Va al final a propósito: es independiente de #3 en adelante y no tiene superficie de interfaz, así que puede aterrizar en cualquier punto posterior a #2 sin bloquear la cadena, y aislarla deja identificable el único PR que escribe en `AppUser.Email` |

Todos los PR quedan bajo el presupuesto de 400 líneas **incluso en la banda alta**. Si al medir el trabajo real las unidades caen en la banda baja, `sdd-tasks` puede evaluar fusionar **#6 y #7** (210-350 conjunto, ambos posteriores a #2 e independientes entre sí) o **#5 y #7** (220-390): solo procede si la medición real lo deja bajo 400, nunca por estimación. Fusionar **#5 y #6** (270-460) queda descartado por rebasar el presupuesto en banda alta.

> **Ajuste introducido por `sdd-design`.** La unidad **G** se parte en dos: **G1** (contrato `IAccountConnectionsService`, 70-120 líneas) viaja con el **PR #3**, y **G2** (componente de aviso en cabecera, 60-100) se queda en el **PR #6**. Motivo: el contrato de lectura lo necesitan dos consumidores —la página (#4) y el aviso de cabecera (#6)—; si viajase entero con G, la página no tendría de dónde leer, y si el aviso llegara antes que la página, `main` tendría durante la vida de un PR un enlace de cabecera hacia una ruta que todavía no existe. Todos los PR siguen bajo 400 líneas en banda alta. Ver `design.md` §10.

### 13.3 Encadenado

PR #1 parte de `inc/vinculacion-cuentas`; cada PR posterior parte de la rama del anterior, desde el mismo worktree, según el flujo de `scripts/sdd-worktree.ps1`. Si GitHub muestra los cambios de una rebanada previa dentro del diff de una hija, se rebasa hasta que el diff quede limpio. Los nombres exactos de rama y el orden de apertura los fija `sdd-tasks`.

---

## 14. Sincronización de roadmap

`docs/increments/ROADMAP.md` ya refleja INC-49 como `⏳ En progreso` en la tabla maestra (línea 65) y en la sección «Incrementos en Curso» (línea 80). **No requiere cambios en esta fase.** Al archivar pasará a `✅ Archivado`, junto con el volcado obligatorio a `docs/specs/sistema/`.
