# Diseño Técnico — INC-49: Vinculación de Cuentas entre Proveedores

> **Cambio:** `change-49-vinculacion-cuentas` · **Fase:** `sdd-design` · **Fecha:** 2026-09-17
> **Rama / worktree:** `inc/vinculacion-cuentas` — `C:\repos\ludeka-wt\vinculacion-cuentas`
> **Entradas:** [`proposal.md`](proposal.md) (aprobada) · [`specs/`](specs/) (3 capacidades, 11 requisitos, 35 escenarios) · [`exploration.md`](exploration.md)
> **Strict TDD activo.** Runner contractual: `dotnet test Ludeka.sln`.

---

## 0. Resumen ejecutivo

La vinculación se construye como **una segunda intención sobre la misma tubería OAuth ya existente**, no como una tubería nueva. El desafío se emite desde un endpoint propio que marca `intención=vincular` y el `UserId` de la sesión en `AuthenticationProperties.Items` —protegido por Data Protection dentro del parámetro `state`—, y `ExternalLoginEvents.HandleTicketReceivedAsync` (`:37-72`) bifurca hacia `LinkAsync` en lugar de `ResolveAsync`. Todo lo demás cuelga de ahí: la columna `ProviderEmailVerifiedAt` se escribe **solo al crear la fila** y se convierte en el único invariante del que depende el aviso; la guarda del último método vive en `Application` sobre `SessionIdentity.Require(...)` más relectura `AsNoTracking`; y la página es la primera ruta del proyecto con `[Authorize]` sin permiso granular.

Las cuatro preguntas abiertas quedan cerradas en la sección 3. Se ha detectado además **un riesgo que la propuesta no recogía** y que obliga a un ajuste de diseño: `CurrentUserContractTests.cs:41-59` **congela por aserción de igualdad exacta** la superficie de `ICurrentUserService`, así que ampliarlo rompería una prueba verde de INC-46 con intención deliberada. La lectura «¿tiene correo verificado?» se entrega en un contrato nuevo (`IAccountConnectionsService`) en vez de ampliar el congelado (sección 4, decisión D6).

---

## 1. Enfoque técnico

Se sigue el orden de la propuesta §6 —**esquema → contrato → lógica de dominio → transporte → interfaz**— con un principio rector añadido: **ninguna capa nueva reemplaza una existente; todas la envuelven**.

| Principio | Cómo se materializa |
|---|---|
| **Aditivo sobre INC-46** | Ni una firma existente cambia de forma incompatible. Los parámetros nuevos se añaden como **opcionales y al final**, de modo que las 1345 pruebas verdes siguen compilando sin tocarlas |
| **El invariante en el dominio, no en el servicio** | `ProviderEmailVerifiedAt` lo calcula el constructor de `ExternalLogin`, no el llamador: ningún punto de código puede producir una fila incoherente |
| **La decisión en `Application`, la copia en `Web`** | `LinkAsync` devuelve un resultado tipado; la redacción final del mensaje la compone `Web` con `ExternalAuthenticationSchemes.DisplayNameFor` (`:144-150`) |
| **Dos pruebas simultáneas para vincular** | La sesión demuestra la propiedad de la cuenta destino; el desafío OAuth demuestra el control de la identidad entrante. Ninguna vale sola (propuesta §4.3) |
| **El esquema es la última línea, no la primera** | El índice único `(Provider, ProviderKey)` garantiza el invariante incluso bajo carrera; la comprobación previa en `Application` existe para dar **un mensaje honesto**, no para sustituir al índice |

### 1.1 Mapa de capas

```
  Web                                   Application                    Core            Infrastructure
  ───────────────────────────────       ──────────────────────        ──────────       ────────────────────
  AccountConnections.razor        ─┬──▶ IAccountConnectionsService ──▶                  ExternalLoginRepository
    [Authorize] (sin Policy)       │        GetConnectionsAsync                           ListByUserIdAsync
    form POST ──┐                  │        HasVerifiedProviderEmailAsync                 RemoveAsync
    @onclick ───┼──────────────────┤                                                      GetByProviderKeyAsync
                │                  └──▶ IExternalLoginService                             AddAsync
  AccountEmailNotice.razor ────────┘        ResolveAsync (1, 2a, 2b, 3)  ExternalLogin
    (aviso descartable)                     LinkAsync                     +Provider…    SqliteUserRepository
                │                           UnlinkAsync                    VerifiedAt     GetByEmailAsync
  Program.cs    │                                   │                   AuditAction        UpdateAsync
    POST /cuenta/conexiones/vincular ──┐            └──▶ IAuditService     +Linked…
                │                      │                                                SqliteSchemaMigrator
  ExternalLoginEvents ◀────────────────┘                                                  bloque 22 + 23
    HandleTicketReceivedAsync (bifurca)                                                 Migrations/
  ExternalLoginIntent (Items del state)                                                   AddProviderEmail…
```

---

## 2. Flujo de datos

### 2.1 Vinculación desde sesión activa (camino feliz)

```
  Navegador            Program.cs                Proveedor OAuth      ExternalLoginEvents      ExternalLoginService
     │                     │                           │                      │                        │
     │ POST /cuenta/       │                           │                      │                        │
     │ conexiones/vincular │                           │                      │                        │
     ├────────────────────▶│ 1. ValidateRequestAsync   │                      │                        │
     │                     │    (antiforgery, 400)     │                      │                        │
     │                     │ 2. userId := HttpContext  │                      │                        │
     │                     │    .User (SERVIDOR)       │                      │                        │
     │                     │ 3. Items[intent]=link     │                      │                        │
     │                     │    Items[userId]=userId   │                      │                        │
     │                     │ 4. Results.Challenge      │                      │                        │
     │◀────302 + state ────┤ ─────────────────────────▶│                      │                        │
     │      (protegido)    │                           │                      │                        │
     │ consentimiento      │                           │                      │                        │
     ├───────────────────────────────────────────────▶ │                      │                        │
     │◀── 302 /signin-xxx ─────────────────────────────┤                      │                        │
     ├──────────────────────────────────────────────────────────────────────▶ │                        │
     │                     │                           │   5. TryReadLink     │                        │
     │                     │                           │   6. userId(Items)   │                        │
     │                     │                           │      == userId(sesión)?│                      │
     │                     │                           │   7. LinkAsync ──────┼───────────────────────▶│
     │                     │                           │                      │   8. Require + relectura│
     │                     │                           │                      │   9. ¿par en uso?       │
     │                     │                           │                      │  10. AddAsync           │
     │                     │                           │                      │  11. ¿correo sintético? │
     │                     │                           │                      │  12. Auditoría          │
     │                     │                           │  13. SignIn(sesión)◀─┼────── AppUser ──────────┤
     │◀─ 302 /cuenta/conexiones?resultado=vinculado ────────────────────────── ┤                        │
```

**Puntos críticos del flujo**

1. El `UserId` se captura en el paso 2 **del servidor**, nunca del formulario. Un `<input name="userId">` fabricado se ignora porque no se lee.
2. `Items` viaja dentro del parámetro `state` del proveedor, serializado y protegido por el `StateDataFormat` del manejador remoto (Data Protection: firmado y cifrado). El navegador no puede leerlo ni falsificarlo. *(Matiz de precisión frente a la propuesta §4.3, que lo atribuye al manejador de cookies: el protector real es el del esquema remoto, no el de la cookie. La garantía de integridad es la misma o mayor.)*
3. El paso 6 es la **reconfirmación contra la sesión** (sección 4, decisión D1).
4. El paso 13 refirma la cookie de sesión con `BuildSessionPrincipal(user)` (`ExternalLoginEvents.cs:75-89`) del **mismo** usuario. Es deliberado: refresca `ClaimTypes.Email` si el paso 11 reemplazó el correo sintético, de modo que la cabecera lo refleja sin pedir un nuevo acceso.

### 2.2 Cascada de acceso con la rama 2 partida

```
                 ResolveAsync(provider, key, email, emailVerified, displayName)
                                        │
          ┌─────────────────────────────┴─────────────────────────────┐
          │ (1) GetByProviderKeyAsync(provider, key)                  │
          └─────────────────────────────┬─────────────────────────────┘
                     ¿existe y su AppUser vive?
                ┌──── sí ────┐                  └──── no ────┐
                ▼                                            ▼
        devuelve esa cuenta               ┌─────────────────────────────────────┐
        (0 escrituras)                    │ (2) emailVerified && email != null  │
                                          │     && GetByEmailAsync(email) != nul│
                                          └──────────────────┬──────────────────┘
                                        ┌─── sí ─────────────┘        └── no ──┐
                                        ▼                                      │
                            ListByUserIdAsync(match.Id)                        │
                                        │                                      │
                     ┌── Count == 0 ────┴──── Count >= 1 ──┐                   │
                     ▼                                      ▼                  ▼
            (2a) EXCEPCIÓN SEGURA              (2b) COLISIÓN         (3) ALTA NUEVA
            AddAsync(ExternalLogin,            throw ExternalLogin-   AppUser CommunityUser
              providerEmailVerified: true)     CollisionException     + ExternalLogin
            devuelve match                     0 filas, 0 sesión      (VerifiedAt según emailVerified)
            ── conducta de INC-46 intacta ──   ── NUNCA fusiona ──
```

---

## 3. Resolución de las preguntas abiertas (§12 de la propuesta)

### 3.1 P2 — Rechazo de vincular un proveedor que ya pertenece a otra cuenta

**Punto de detección: `ExternalLoginService.LinkAsync`, antes de cualquier escritura**, mediante el `GetByProviderKeyAsync` que el repositorio ya expone (`IExternalLoginRepository.cs:20`).

```csharp
var existing = await _externalLogins.GetByProviderKeyAsync(providerName, key, ct);
if (existing is not null)
{
    return string.Equals(existing.UserId, userId, StringComparison.OrdinalIgnoreCase)
        ? new ExternalLoginLinkResult(ExternalLoginLinkOutcome.AlreadyLinkedToThisAccount, providerName, user)
        : new ExternalLoginLinkResult(ExternalLoginLinkOutcome.RejectedOwnedByAnotherAccount, providerName, user);
}
```

**Doble barrera, deliberada.** La comprobación previa existe para **poder redactar un mensaje honesto**; el índice único `(Provider, ProviderKey)` (`LudekaDbContext.cs:485`) es la garantía real y cubre la carrera entre dos intentos simultáneos. Por eso `AddAsync` se envuelve además en un `catch (DbUpdateException)` que devuelve **el mismo** `RejectedOwnedByAnotherAccount`: el usuario ve un único mensaje, dispare quien dispare.

**Texto exacto del mensaje** (constantes en `AccountConnectionMessages`, `Ludeka.Application/Features/Identity/`):

> **Titular:** «Esa cuenta de {Proveedor} ya está vinculada a otra cuenta de Ludeka.»
>
> **Detalle:** «Un mismo acceso de {Proveedor} solo puede pertenecer a una cuenta de Ludeka. Si esa otra cuenta es tuya, cierra sesión y entra directamente con {Proveedor}. Si prefieres usar este acceso desde la cuenta actual, desvincúlalo primero en la pantalla de conexiones de esa otra cuenta.»

**Auditoría de honestidad de cada frase** —esto es lo que exige el escenario «El mensaje de rechazo no promete una resolución inexistente»:

| Frase | ¿Es cierta? | ¿Es alcanzable por el usuario? |
|---|---|---|
| «ya está vinculada a otra cuenta» | Sí: lo dice el índice único | — |
| «solo puede pertenecer a una cuenta» | Sí: es el invariante de esquema | — |
| «cierra sesión y entra directamente con {Proveedor}» | Sí | **Sí**: la rama 1 de `ResolveAsync` lo resuelve a esa cuenta |
| «desvincúlalo primero … en esa otra cuenta» | Sí | **Sí**: `/cuenta/conexiones` de esa cuenta, sujeto a la guarda del último método, que allí se explica |

**No aparece** ninguna de estas palabras: *fusionar, fusión, unir cuentas, transferir, traspasar, soporte, contacta con nosotros*. Ninguna promete algo que INC-49 no entrega (la fusión está fuera de alcance por la decisión 1).

**Por qué no sirve el mensaje de colisión de §4.2.** Aquel dice «inicia sesión con el método que ya usas y vincula este proveedor desde Ajustes → Conexiones». Aquí el proveedor **ya está repartido**: seguir esa instrucción llevaría al mismo rechazo en bucle. Son dos mensajes distintos para dos situaciones distintas, y el diseño los mantiene separados.

**Filtración de información: ninguna.** El mensaje revela que ese `(Provider, ProviderKey)` está registrado, pero quien lo lee **acaba de autenticarse con esa misma identidad ante el proveedor**: es información sobre sí mismo. No se revela el nombre, el correo ni el identificador de la otra cuenta.

**Invariante de no reasignación.** `LinkAsync` **jamás** ejecuta un `Update` sobre una fila `ExternalLogin` existente. El repositorio nuevo solo añade `ListByUserIdAsync` y `RemoveAsync`; **no se añade ningún método de actualización**, de modo que mover una fila de cuenta es estructuralmente imposible, no solo indeseable.

---

### 3.2 P3 — Mecanismo de descarte del aviso de cabecera

**Mecanismo elegido: campo privado en un componente compartido del circuito interactivo.** Cero esquema, cero DI nueva, cero JavaScript, cero migración.

**La evidencia que lo hace viable — y que no estaba en la exploración:** `App.razor:39` monta `<Routes @rendermode="InteractiveServer" />`. La aplicación es **interactiva globalmente**, no un conjunto de islas. Consecuencias directas:

1. `MainLayout` vive dentro del circuito. Blazor **preserva la instancia del layout** entre navegaciones del enrutador mientras el tipo de layout no cambie, así que un campo privado sobrevive a toda la navegación de la pestaña y solo se pierde en una recarga completa.
2. Un `@onclick="() => _dismissed = true"` descarta el aviso **sin recargar la página completa**, que es literalmente lo que exige el escenario «Aviso visible y descartable en la cabecera».
3. El escenario no pide que el descarte sobreviva a una recarga. El mecanismo más barato que cumple el escenario es el que no persiste nada.

**Forma concreta** — `src/Ludeka.Web/Components/Shared/AccountEmailNotice.razor`, montado en la cabecera de `MainLayout.razor`, hermano de `<SessionGuard />` (`:302`):

```razor
@inject ICurrentUserService CurrentUserService
@inject IAccountConnectionsService Connections

@if (_needsNotice && !_dismissed)
{
    <div role="status" class="...">
        <span>Tu cuenta no tiene un correo verificado. Vincula otro proveedor de acceso para no perderla.</span>
        <a href="/cuenta/conexiones">Ir a conexiones</a>
        <button type="button" @onclick="() => _dismissed = true" aria-label="Descartar este aviso">&times;</button>
    </div>
}

@code {
    private bool _needsNotice;
    private bool _dismissed;

    protected override async Task OnInitializedAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentUserService.UserId)) return;   // anónimo: nada que avisar
        _needsNotice = !await Connections.HasVerifiedProviderEmailAsync();
    }
}
```

**Coherencia con `SessionGuard`** (`SessionGuard.razor:15-24`): mismo patrón —componente pequeño en `Components/Shared/`, montado en `MainLayout`, que no toca esquema ni servicios nuevos y se apaga solo cuando no aplica—. La diferencia es que `SessionGuard` necesita `RendererInfo.IsInteractive` porque se suscribe a un evento del circuito; aquí no hace falta guarda, porque en prerenderizado el aviso simplemente se pinta sin que el botón responda todavía, que es el comportamiento normal de toda la aplicación.

**Coste medido y aceptado:** `OnInitializedAsync` se ejecuta dos veces por carga completa (prerenderizado + arranque del circuito) para usuarios autenticados, y **cero veces** en las navegaciones posteriores dentro del circuito. Cada ejecución es un `SELECT` indexado por `UserId` sobre `ExternalLogins` (índice `IX_ExternalLogins_UserId`, creado en `20260915164921_AddExternalLogins.cs:42-45`). Es proporcionado.

**Alternativas descartadas**

| Alternativa | Por qué se descarta |
|---|---|
| Columna o tabla de avisos descartados | Exige esquema nuevo. P3 pide explícitamente evitarlo |
| Formulario POST de descarte (SSR) | Implica recarga completa de página: contradice el escenario de la especificación |
| Servicio `Scoped` de estado de avisos + evento | Mismo alcance efectivo que el campo privado (el ámbito es el circuito) a cambio de registro en DI, contrato nuevo y suscripción que desenganchar |
| `sessionStorage` vía interop JS | Más caro (función JS nueva + llamada de interop) para la única ventaja de sobrevivir a una recarga, que ningún escenario pide. **Queda anotado como la vía de ampliación barata** si el maintainer lo pide después: el patrón ya existe en `App.razor:52-62` (`getLudekaTheme(userId)`) y seguiría sin tocar esquema |

---

### 3.3 P4 — Momento exacto en que se escribe `ProviderEmailVerifiedAt`

**Regla única, en el constructor de la entidad, y en ningún otro sitio:**

> `ProviderEmailVerifiedAt` vale `LinkedAt` **si y solo si** el proveedor entregó un correo utilizable **y** lo declaró verificado. En cualquier otro caso vale `null`. **Se escribe únicamente al crear la fila y nunca se modifica después.**

```csharp
// src/Ludeka.Core/Entities/ExternalLogin.cs
public ExternalLogin(
    string userId,
    string provider,
    string providerKey,
    string? providerEmail = null,
    DateTimeOffset? linkedAt = null,
    bool providerEmailVerified = false)   // ← 6.º parámetro, AL FINAL, opcional
{
    // … validaciones existentes, sin cambios …
    ProviderEmail = string.IsNullOrWhiteSpace(providerEmail) ? null : providerEmail.Trim();
    LinkedAt = linkedAt ?? DateTimeOffset.UtcNow;
    ProviderEmailVerifiedAt = providerEmailVerified && ProviderEmail is not null ? LinkedAt : null;
}
```

**Por qué el 6.º parámetro y no el 5.º.** `ExternalLoginTests.cs:20` pasa `linkedAt` **posicionalmente** como quinto argumento. Insertar el `bool` en esa posición rompería la compilación de una prueba verde de INC-46 y obligaría a editarla. Añadido al final, **las 6 pruebas de `ExternalLoginServiceTests` y las 8 de `ExternalLoginTests` siguen compilando y pasando sin tocar una línea**. Los puntos de llamada nuevos usan argumento con nombre (`providerEmailVerified: true`) por legibilidad.

**Por qué en el dominio y no en el servicio.** La decisión 2 de la propuesta rechazó expresamente «descansar en una convención que la base de datos no impide romper». Situar la regla en el constructor la convierte en un invariante de dominio: es **imposible** construir una fila con `ProviderEmailVerifiedAt` establecido y `ProviderEmail` nulo, venga de donde venga la llamada. Y se verifica con una prueba de Dominio, sin base de datos: el escalón más barato y rápido de toda la pirámide.

**Efecto por rama, fijado por prueba:**

| Punto | ¿Escribe fila? | `ProviderEmailVerifiedAt` resultante |
|---|---|---|
| `ResolveAsync` rama 1 — par reincidente (`:43-52`) | **No** | No aplica: la fila existente no se toca |
| `ResolveAsync` rama 2a — correo verificado, cuenta sin vínculos | **Sí** | **Siempre establecido**: la rama solo se alcanza con `emailVerified == true` |
| `ResolveAsync` rama 2b — colisión | **No** | No aplica: 0 escrituras |
| `ResolveAsync` rama 3 — alta nueva (`:66-79`) | Sí | `emailVerified ? LinkedAt : null` |
| `LinkAsync` — vinculación desde sesión | Sí, si el resultado es `Linked` | `emailVerified ? LinkedAt : null` |
| `LinkAsync` — `AlreadyLinkedToThisAccount` / `Rejected…` | **No** | No aplica |
| `UnlinkAsync` | Borra fila | No aplica |

**Limitación aceptada y acotada.** Al ser inmutable, una fila escrita cuando el proveedor no entregaba correo verificado conserva `null` para siempre, aunque ese mismo proveedor empiece a entregarlo después (caso típico: la aplicación de Facebook supera la revisión de Meta). Se acepta por tres razones: (a) la cuenta que está en esa situación **sigue sin una segunda red de seguridad**, que es justo lo que el aviso le pide resolver; (b) la acción que el aviso propone —vincular otro proveedor— sigue siendo correcta y disponible; (c) la alternativa (promoción monótona `null → fecha`) exigiría un tercer método de repositorio de actualización, y ese método es precisamente el que la sección 3.1 quiere que **no exista** para hacer estructuralmente imposible reasignar una fila. Si en el futuro se quiere cubrir el caso, la vía es una promoción monótona explícita en un incremento propio, nunca un `Update` genérico.

---

### 3.4 P5 — Alcance del reemplazo del correo sintético

**Decisión: solo en `LinkAsync`. `ResolveAsync` no escribe nunca en `AppUser.Email` ni muta filas `ExternalLogin` existentes.**

**Razones, en orden de peso:**

1. **Riesgo sobre el camino caliente.** La rama 1 de `ResolveAsync` (`:43-52`) es hoy **solo lectura** —dos `SELECT`, cero escrituras— y la recorre la práctica totalidad de los accesos. Convertirla en escritura condicional multiplica el riesgo nº 1 del incremento (propuesta §8: «tocar `ResolveAsync` … 1345 pruebas en verde», probabilidad alta, impacto alto) a cambio de un escenario secundario.
2. **Coste por acceso.** La comprobación previa del índice único obligaría a un `GetByEmailAsync` adicional (`SqliteUserRepository.cs:32-37`) **en cada inicio de sesión**, no solo en los que pudieran reemplazar algo.
3. **Coherencia con la frontera de seguridad.** §4.3 exige dos pruebas simultáneas para unir identidades. Un reemplazo disparado en el login lo decidiría el proveedor; disparado en `LinkAsync` lo decide **el usuario**, que es el criterio que INC-49 defiende.
4. **Trazabilidad de la entrega.** La propuesta §13.2 aísla el PR #7 porque es «el único PR que escribe en `AppUser.Email`». Extender el reemplazo a `ResolveAsync` rompería esa propiedad y mezclaría el PR #7 con el #5, que es el que toca `ResolveAsync`.

**Comprobación previa del índice único de `AppUsers.Email` (`LudekaDbContext.cs:363`)** — obligatoria en el único camino donde el reemplazo existe:

```csharp
// dentro de LinkAsync, DESPUÉS de crear la fila y SOLO si el resultado es Linked
private async Task<bool> TryReplacePlaceholderEmailAsync(
    AppUser user, string? normalizedEmail, bool emailVerified, CancellationToken ct)
{
    if (!emailVerified || normalizedEmail is null) return false;
    if (!IsPlaceholderEmail(user.Email)) return false;              // (a) ¿es sintético?

    var owner = await _users.GetByEmailAsync(normalizedEmail, ct);   // (b) ¿lo tiene ya alguien?
    if (owner is not null) return false;                             // colisión: NO se reemplaza

    user.UpdateProfile(user.UserName, normalizedEmail);              // AppUser.cs:101-112
    try
    {
        await _users.UpdateAsync(user, ct);                          // IUserRepository.cs:18
        return true;
    }
    catch (DbUpdateException)                                        // (c) carrera contra el índice
    {
        return false;   // el vínculo ya está creado y es válido: no se revierte
    }
}

/// <summary>Un correo del dominio reservado no enrutable, en cualquiera de sus formas.</summary>
public static bool IsPlaceholderEmail(string? email)
{
    if (string.IsNullOrWhiteSpace(email)) return false;
    var at = email.LastIndexOf('@');
    if (at < 0) return false;
    var host = email[(at + 1)..];
    return host.Equals(PlaceholderEmailDomain, StringComparison.OrdinalIgnoreCase)
        || host.EndsWith("." + PlaceholderEmailDomain, StringComparison.OrdinalIgnoreCase);
}
```

**Tres detalles que la especificación exige y aquí quedan fijados:**

- **El reemplazo es de mejor esfuerzo y nunca fatal.** Se intenta *después* de que la fila `ExternalLogin` esté persistida. Si falla —por colisión detectada o por carrera contra el índice— **la vinculación sigue siendo un éxito** y la cuenta conserva su correo sintético. Es lo que pide el escenario «Reemplazo rechazado por colisión de correo».
- **`IsPlaceholderEmail` cubre las dos formas.** `BuildPlaceholderEmail` (`ExternalLoginService.cs:88-89`) produce `{clave}@{proveedor}.ludeka.invalid`; la comprobación acepta también `@ludeka.invalid` por si alguna semilla histórica usara la forma corta. `.invalid` es un TLD reservado por RFC 2606: ningún correo real puede caer aquí por accidente.
- **Una cuenta con correo real no se toca.** La guarda `(a)` lo garantiza, cumpliendo el escenario «Cuenta sin correo sintético no se ve afectada».

**Efecto colateral deseado.** Como el reemplazo ocurre dentro del callback OAuth y el paso 13 del flujo (§2.1) refirma la cookie con `BuildSessionPrincipal(user)`, el `ClaimTypes.Email` de la sesión queda actualizado en el acto. Sin ese refirmado, la cabecera mostraría el correo sintético hasta el siguiente acceso.

---

## 4. Decisiones de arquitectura (ADR)

### D1 — Transporte de la intención: endpoint propio + `Items` + reconfirmación contra la sesión

**Elección.** Un endpoint nuevo `POST /cuenta/conexiones/vincular` en `Program.cs`, mapeado **después** del bloque `/logout` (`:484-488`), con `.RequireAuthorization()`, que reproduce la validación antiforgery manual de `/login/external` (`:460-467`) y emite el desafío marcando la intención.

```csharp
// Incremento 49: desafío de VINCULACIÓN. A diferencia de /login/external, exige sesión y marca
// la intención y el UserId del servidor en AuthenticationProperties.Items, que viajan dentro del
// parámetro `state` protegido por Data Protection. El formulario del navegador no puede alterarlos.
app.MapPost("/cuenta/conexiones/vincular", async (
    HttpContext httpContext,
    [FromServices] IAntiforgery antiforgery,
    [FromServices] IOptions<AuthenticationOptions> options) =>
{
    try { await antiforgery.ValidateRequestAsync(httpContext); }
    catch (AntiforgeryValidationException)
    { return Results.BadRequest(new { error = "Token antiforgery ausente o inválido." }); }

    var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();

    var form = await httpContext.Request.ReadFormAsync();
    var registration = ExternalAuthenticationSchemes
        .GetEnabledProviders(options.Value)
        .FirstOrDefault(c => string.Equals(c.Name, form["provider"].ToString(), StringComparison.OrdinalIgnoreCase));

    if (registration is null)
        return Results.BadRequest(new { error = "El proveedor de acceso indicado no está habilitado." });

    var properties = new AuthenticationProperties { RedirectUri = AccountConnectionRoutes.Page };
    ExternalLoginIntent.MarkLink(properties, userId);
    return Results.Challenge(properties, [registration.Scheme]);
}).RequireAuthorization();
```

**Claves de `Items`** — en `src/Ludeka.Web/Authentication/ExternalLoginIntent.cs`, clase pública y **pura** (sin dependencias de HTTP), para que sea unitariamente comprobable:

| Clave | Valor | Significado |
|---|---|---|
| `ludeka:intent` | `link` | Este desafío vincula; **no** inicia sesión |
| `ludeka:link_user_id` | `UserId` de la sesión al emitir el desafío | La cuenta destino, capturada en servidor |

Ausencia de `ludeka:intent` ⇒ el flujo es el de acceso de INC-46, byte por byte. **Es la compatibilidad hacia atrás: un desafío emitido por `/login/external` no cambia de comportamiento en absoluto.**

**Bifurcación en `ExternalLoginEvents.HandleTicketReceivedAsync` (`:37-72`)**, insertada tras extraer los claims y **antes** de la llamada a `ResolveAsync` (`:60`):

```csharp
if (ExternalLoginIntent.TryReadLink(context.Properties, out var intendedUserId))
{
    var sessionUserId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

    // Reconfirmación: la sesión que vuelve debe ser la misma que emitió la intención.
    if (!string.Equals(intendedUserId, sessionUserId, StringComparison.OrdinalIgnoreCase))
    {
        context.HandleResponse();
        context.Response.Redirect(string.IsNullOrWhiteSpace(sessionUserId)
            ? AccountConnectionRoutes.LoginWithLinkWithoutSession   // sin sesión → /login?aviso=…
            : AccountConnectionRoutes.PageWithSessionChanged);      // otra sesión → /cuenta/conexiones?resultado=…
        return;
    }

    var result = await loginService.LinkAsync(
        intendedUserId, providerName, providerKey, email, emailVerified, context.HttpContext.RequestAborted);

    if (result.Outcome != ExternalLoginLinkOutcome.Linked)
    {
        context.HandleResponse();
        context.Response.Redirect(AccountConnectionRoutes.PageWithResult(result.Outcome));
        return;
    }

    context.Principal = BuildSessionPrincipal(result.User);
    context.Properties ??= new AuthenticationProperties();
    context.Properties.IsPersistent = true;
    context.Properties.AllowRefresh = true;
    context.ReturnUri = AccountConnectionRoutes.PageWithResult(result.Outcome);
    return;
}

// … camino de acceso de INC-46, sin cambios …
```

**Qué ocurre si al volver del proveedor la sesión ya no coincide** —la pregunta explícita del encargo—:

| Estado al volver | Conducta | Por qué |
|---|---|---|
| Sesión = `UserId` marcado | Se vincula | Las dos pruebas se sostienen |
| **Sin sesión** (expiró o cerró) | **No se vincula nada.** Redirección a `/login?aviso=vinculacion-sin-sesion`. No se firma cookie | Vincular sería atribuir una identidad a una cuenta que el navegador ya no demuestra controlar |
| **Sesión de otro usuario** | **No se vincula nada.** Redirección a `/cuenta/conexiones?resultado=sesion-cambiada` | Ni al `UserId` marcado (la sesión que lo emitió ya no existe) ni al nuevo (esa sesión no emitió esta intención). Es exactamente la vía de apropiación que la propuesta §8 marca como «baja probabilidad, impacto muy alto» |

**Alternativas descartadas**

| Alternativa | Por qué no |
|---|---|
| `UserId` en un campo oculto del formulario | Lo altera cualquiera con las herramientas del navegador. Es la vía de apropiación directa |
| `returnUrl` con la intención codificada en la URL | Visible y manipulable; además abriría una superficie de redirección abierta |
| Reutilizar `POST /login/external` con un parámetro extra | El endpoint es `AllowAnonymous` (`Program.cs:482`) y lo cubre `AuthorizationPipelineContractTests.cs:68-83`. Mezclar en él una operación que exige sesión rompería esa garantía comprobada |
| Un esquema de autenticación adicional solo para vincular | Exigiría URIs de callback nuevas registradas en los tres proveedores. La propuesta §10 fija «cero credenciales nuevas» |

**Punto a fijar con prueba RED, no a dar por supuesto.** `context.HandleResponse()` debe impedir que el manejador remoto ejecute su `SignInAsync` posterior. Es el contrato documentado de `HandleResponse()` («el evento se hace cargo de la respuesta y el manejador deja de procesar»), pero **el diseño no lo asume**: el PR #3 escribe primero una prueba que construye un `TicketReceivedContext` real y afirma `context.Result.Handled == true`, `Response.StatusCode == 302` y el `Location` esperado, **antes** de escribir la bifurcación. La comprobación de extremo a extremo la da el smoke test con navegador de la propuesta §11.

*Consecuencia de esta prueba: `HandleTicketReceivedAsync` pasa de `private static` a `public static`, siguiendo el precedente de `BuildSessionPrincipal` (`ExternalLoginEvents.cs:75`), que ya es público por la misma razón. No se introduce `InternalsVisibleTo`: el repositorio no lo usa en ningún proyecto y esta fase no inaugura convenciones.*

---

### D2 — Reconciliación de esquema en dos frentes (PostgreSQL y SQLite)

**Elección.** Migración EF Core aditiva **más** un **bloque 23 nuevo** en `SqliteSchemaMigrator`, además de la columna en el `CREATE TABLE` del bloque 22.

**Frente 1 — EF Core (PostgreSQL en producción).**

```csharp
// src/Ludeka.Infrastructure/Migrations/<ts>_AddProviderEmailVerifiedAtToExternalLogins.cs
protected override void Up(MigrationBuilder migrationBuilder) =>
    migrationBuilder.AddColumn<DateTimeOffset>(
        name: "ProviderEmailVerifiedAt",
        table: "ExternalLogins",
        type: "timestamp with time zone",
        nullable: true);

protected override void Down(MigrationBuilder migrationBuilder) =>
    migrationBuilder.DropColumn(name: "ProviderEmailVerifiedAt", table: "ExternalLogins");
```

Mapeo Fluent, a continuación de `LudekaDbContext.cs:489`:

```csharp
externalLogin.Property(l => l.ProviderEmail).HasMaxLength(200);
externalLogin.Property(l => l.ProviderEmailVerifiedAt);   // DateTimeOffset? anulable (INC-49)
```

**Frente 2 — SQLite (desarrollo y pruebas).** El bloque 22 (`SqliteSchemaMigrator.cs:843-862`) **solo crea la tabla cuando no existe**, así que por sí solo deja fuera a toda base ya creada. Se aplican **los dos** cambios:

*(a) Columna en el `CREATE TABLE` del bloque 22* — para bases nuevas:

```sql
"ProviderEmail" TEXT NULL,
"ProviderEmailVerifiedAt" TEXT NULL,     -- ← INC-49
"LinkedAt" TEXT NOT NULL,
```

*(b) Bloque 23 nuevo, inmediatamente después del 22* — para bases preexistentes, siguiendo literalmente el patrón de `Giveaways.IsPromoted` (`:389-394`) y `Stores.Country` (`:455-460`):

```csharp
// 23. Reconciliar columna ProviderEmailVerifiedAt en ExternalLogins (INC-49)
if (existingTables.Contains("ExternalLogins"))
{
    var externalLoginColumns = await GetTableColumnsAsync(connection, "ExternalLogins", ct);
    if (!externalLoginColumns.Contains("ProviderEmailVerifiedAt"))
    {
        using var alterCmd = connection.CreateCommand();
        alterCmd.CommandText =
            "ALTER TABLE \"ExternalLogins\" ADD COLUMN \"ProviderEmailVerifiedAt\" TEXT NULL;";
        await alterCmd.ExecuteNonQueryAsync(ct);
    }
}
```

**Por qué un bloque 23 y no un `else` del 22.** El bloque 22 hace `existingTables.Add("ExternalLogins")` tras crear la tabla (`:861`), así que el bloque 23 se ejecuta en **ambos** caminos y resulta **idempotente**: si la columna se acaba de crear, el `PRAGMA table_info` la ve y no hace nada. Un `else` funcionaría, pero rompería el idioma que el propio fichero ya usa para reconciliar columnas y dejaría la reconciliación dependiente del orden. `TEXT` es el tipo que el proveedor SQLite de EF Core usa para `DateTimeOffset`, igual que el `"LinkedAt" TEXT NOT NULL` vecino.

**Prueba de la reconciliación** (amplía `SqliteSchemaMigratorTests.cs`, siguiendo su patrón de `:11-76`): crear a mano la tabla `ExternalLogins` con las **6 columnas de INC-46**, insertar una fila, ejecutar `EnsureSchemaUpToDateAsync`, y afirmar que (1) `PRAGMA table_info` incluye `ProviderEmailVerifiedAt`, (2) la fila preexistente sigue ahí intacta y (3) `db.ExternalLogins.ToListAsync()` no lanza `no such column`. Cubre literalmente el escenario «Columna reconciliada en una base SQLite preexistente».

**Prerrequisito para `sdd-apply`, no verificable en esta fase.** La migración debe generarse con las herramientas de EF Core contra `Ludeka.Infrastructure` (proyecto de arranque `Ludeka.Web`), igual que `20260915164921_AddExternalLogins`. Los ficheros `.Designer.cs` y `LudekaDbContextModelSnapshot.cs` son **artefactos generados** y quedan fuera del presupuesto de 400 líneas (propuesta §13.1), pero la descripción del PR #1 debe decirlo explícitamente.

---

### D3 — Patrón de página con `[Authorize]` simple

**Elección.** `@attribute [Authorize]` **sin `Policy =`**, la primera del proyecto. El mecanismo ya existe y no hace falta política nueva.

```razor
@page "/cuenta/conexiones"
@attribute [Authorize]
@using Microsoft.AspNetCore.Authorization
```

**Por qué basta con eso, en los dos modos de renderizado:**

| Modo | Quién protege | Resultado para un anónimo |
|---|---|---|
| **Estático (SSR, primera petición HTTP)** | El middleware `app.UseAuthorization()` (`Program.cs:401`), sobre los metadatos de endpoint que `MapRazorComponents<App>()` (`:501-502`) genera a partir del atributo. El manejador de cookies emite el desafío hacia `LoginPath = "/login"` (`ExternalAuthenticationSchemes.cs:58`) | **302 a `/login` antes de renderizar nada.** No se pinta ni una fila de proveedor |
| **Interactivo (navegación dentro del circuito)** | `AuthorizeRouteView` de `Routes.razor:1-10` → `<NotAuthorized><RedirectToLogin /></NotAuthorized>` → `RedirectToLogin.razor:10-17`, que fuerza `NavigateTo(LoginPath, forceLoad: true)` solo cuando `RendererInfo.IsInteractive` | Navegación forzada a `/login` |

Esta división es exactamente la que documenta el comentario de `RedirectToLogin.razor:3-8`, escrito por INC-46: *«en render estático las facilidades de navegación de Blazor no operan y la protección real la ejerce el middleware de autorización»*. **No se inventa un mecanismo: se usa el que ya estaba y no tenía ningún consumidor sin política.**

**Por qué no se toca `AuthorizationPolicies` ni `policy-based-authorization`.** Las 11 políticas (`AuthorizationPolicies.cs:14-24`) existen para exigir un `ModeratorPermission` concreto. Aquí no hay permiso que exigir: `[Authorize]` sin política aplica la política por defecto, que es `RequireAuthenticatedUser()`. Registrar una política «solo autenticado» sería un alias vacío. Coincide con la decisión de la propuesta §5.3.

**Prueba, en el idioma del repositorio.** El proyecto comprueba la protección de rutas **leyendo el código fuente** (`AuthorizationPipelineContractTests.cs:45-53`). Se añade un `[Fact]` hermano —no un caso más en `ProtectedPages` (`:15-27`), porque esa `TheoryData` exige `Policy =` en la aserción (`:52`)— que afirma que `AccountConnections.razor` declara `@page "/cuenta/conexiones"`, declara `@attribute [Authorize]` y **no** declara `[Authorize(Policy`. Y otro que afirma que el endpoint de vinculación declara `.RequireAuthorization()`, espejo del que ya existe para `.AllowAnonymous()` (`:68-83`).

**Cuidado de no regresión localizado.** `PublicEndpoint_ShouldDeclareAllowAnonymous` (`:73-83`) delimita el bloque de cada endpoint desde su ruta hasta **el siguiente `app.Map`**. Mapear el endpoint nuevo **después** de `/logout` deja intactos los bloques de `/login/external` y `/logout`. Es el motivo concreto de la posición elegida en D1.

---

### D4 — Vincular por formulario HTTP clásico, desvincular por evento del circuito

**Elección.** Dos mecanismos distintos en la misma página, cada uno por su razón técnica.

| Acción | Mecanismo | Por qué |
|---|---|---|
| **Vincular** | `<form method="post" action="/cuenta/conexiones/vincular" data-enhance="false">` + `<AntiforgeryToken />` + `<input type="hidden" name="provider">` | `Results.Challenge` escribe **cabeceras HTTP de redirección**. Un circuito SignalR no puede hacerlo: cuando llega el evento, la respuesta HTTP ya se cerró. Es el mismo patrón exacto de `Login.razor:35-37` |
| **Desvincular** | `<button @onclick="() => UnlinkAsync(provider.Name)">` | No necesita cabeceras: llama a `IExternalLoginService.UnlinkAsync`, refresca el listado en el circuito y pinta el resultado en el sitio. Evita un segundo endpoint, su antiforgery y una recarga completa |

**Cómo conviven formulario y render interactivo — con evidencia, no con suposición.** `App.razor:39` monta `<Routes @rendermode="InteractiveServer" />`: **toda** la aplicación es interactiva, incluida `Login.razor`. Y `Login.razor:35-36` ya renderiza hoy, dentro de ese mismo subárbol interactivo, un `<form method="post">` con `<AntiforgeryToken />` que funciona en producción. Es decir:

- El componente **no declara `@rendermode`**: hereda el interactivo del padre. Declararlo sería redundante (como en `MyLibrary.razor:2`) y declarar uno distinto sería un error de ejecución.
- `data-enhance="false"` desactiva el manejo mejorado de formularios de Blazor, de modo que el navegador ejecuta un POST de navegación real y recibe el 302 del `Challenge`.
- `<AntiforgeryToken />` funciona en render interactivo en esta aplicación **porque ya lo hace en `Login.razor`**. No es una apuesta del diseño.

**Resultado del viaje de ida y vuelta.** La página lee un código cerrado de la cadena de consulta con `[SupplyParameterFromQuery]` (patrón ya usado en `InstagramModeration.razor:558,561`) y lo traduce a un mensaje fijo:

| Código en `?resultado=` | Mensaje |
|---|---|
| `vinculado` | «Listo: {Proveedor} ya es un método de acceso de tu cuenta.» (+ mención del correo si se reemplazó) |
| `ya-vinculado` | «Ese acceso de {Proveedor} ya estaba vinculado a tu cuenta.» |
| `en-uso` | El mensaje de rechazo de §3.1 |
| `sesion-cambiada` | «La vinculación se canceló porque tu sesión cambió mientras completabas el acceso. No se ha vinculado nada.» |
| *ausente o desconocido* | No se muestra ningún mensaje |

**Decisión de seguridad asociada: nunca se transporta el texto del mensaje por la URL, solo un código de un conjunto cerrado**, y la traducción ocurre en servidor. Evita inyectar texto arbitrario en la página y cierra cualquier redirección abierta: todos los destinos de redirección son literales fijos elegidos en servidor.

---

### D5 — Guarda del último método: `SessionIdentity.Require` + relectura `AsNoTracking`, en `Application`

**Elección.** La guarda vive en `ExternalLoginService.UnlinkAsync`, en la capa de `Application`, y usa el mecanismo de `SessionPermissionGuard` **menos la comprobación de permiso**.

```csharp
public async Task UnlinkAsync(string userId, string provider, CancellationToken ct = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(provider);

    // (1) Invariante de anonimia: sin sesión no hay operación. SessionIdentity.cs:23-40
    var id = SessionIdentity.Require(userId);

    // (2) Relectura sin rastreo: el estado actual de la cuenta manda, como en SessionPermissionGuard.cs:37
    var user = await _users.GetByIdAsync(id, ct);
    if (user is null || user.Status == UserStatus.Suspended)
        throw new UnauthorizedAccessException(SessionIdentity.SessionRequiredMessage);

    // (3) Solo entre las filas PROPIAS: reasignar o borrar la de otro es estructuralmente imposible
    var links = await _externalLogins.ListByUserIdAsync(id, ct);
    var target = links.FirstOrDefault(l => string.Equals(l.Provider, provider.Trim(), StringComparison.OrdinalIgnoreCase));
    if (target is null) return;                                   // idempotente: ya no estaba vinculado

    // (4) LA GUARDA
    if (links.Count <= 1) throw new LastAccessMethodException();

    await _externalLogins.RemoveAsync(target, ct);
    await RecordAuditAsync(user, AuditAction.UnlinkedProvider, target.Provider, ct);
}
```

**Por qué `SessionIdentity.Require` y no `ISessionPermissionGuard`.** `SessionPermissionGuard.RequireAsync` exige una bandera `ModeratorPermission` concreta (`:41`). Esto es **autoservicio de cualquier autenticado sobre su propia cuenta**: no hay bandera que pedir. Se reutiliza el mecanismo (identidad de sesión + relectura sin rastreo) sin la comprobación que no aplica, tal y como fija la propuesta §6.2.

**Por qué la firma es `(userId, provider)` y no `(externalLoginId)`.** Con el identificador de fila, una petición fabricada podría nombrar la fila de otra cuenta y la defensa sería una comprobación de propiedad que alguien puede olvidar. Con `(userId, provider)`, **solo se busca dentro de las filas propias**: tocar una ajena no es un caso denegado, es un caso que no existe. Seguridad por construcción, no por vigilancia.

**Por qué excepción y no resultado.** `LastAccessMethodException` (deriva de `InvalidOperationException`) es imposible de ignorar por accidente y es el idioma que ya usa el repositorio para las denegaciones controladas (`UnauthorizedAccessException` con `SessionIdentity.SessionRequiredMessage`). El escenario «Una petición que evita la interfaz también se deniega» se comprueba llamando directamente a `UnlinkAsync` desde una prueba de `Application`, sin pasar por ningún botón: es exactamente la petición fabricada.

**Mensaje:** «No puedes desvincular tu único método de acceso: te quedarías sin ninguna forma de volver a entrar. Vincula antes otro proveedor.»

---

### D6 — La lectura «¿tiene correo verificado?» va en un contrato nuevo, **no** en `ICurrentUserService`

**Elección.** Contrato nuevo `IAccountConnectionsService` en `Ludeka.Application/Features/Identity/`. **`ICurrentUserService` (`ICurrentUserService.cs:11-34`) no se toca.**

**El hallazgo que lo obliga —y que la propuesta no recogía.** `CurrentUserContractTests.cs:41-59` congela por **igualdad exacta** la superficie del contrato:

```csharp
Assert.Equal(["IsFoundingTeam", "Roles", "UserId", "UserName"], properties);
Assert.Equal(["HasPermission", "IsInRole"], methods);
```

Su documentación declara la intención: *«La superficie queda congelada a la identidad derivada de la sesión»* (`:12`). Añadir cualquier miembro **rompe esa prueba verde de INC-46**, y no como error de compilación —que es lo que la propuesta §8 daba por mitigación («lo garantiza la compilación»)— sino como fallo de ejecución de una guarda deliberada. El radio real es **mucho** mayor de lo estimado: **16 clases** implementan el contrato (recuento verificado con `grep -rn ": ICurrentUserService" src tests`). Solo una está en producción (`AuthenticatedCurrentUserService.cs:20`); las otras 15 son dobles de prueba —`StubCurrentUserService.cs:13`, `FakeCurrentUser` en `GameEditorWebIntegrationTests.cs:152`, `TestCurrentUserService` en `GranularPermissionsTests.cs` y `MediaServiceTests.cs`, y once `FakeCurrentUserService` repartidos por `BggImportServiceTests`, `BggImportServiceProgressTests`, `BggSearchAssistedServiceTests`, `BggSimulationIntegrationTests`, `FoundingVerdictServiceTests`, `GameEditorServiceTests`, `GamePlayLogServiceTests`, `UserLibraryServiceTests`, `UserLibraryStatsServiceTests`, `UserManagementAndAuditServiceTests` y `UserLocationServiceTests`—. Ampliar el contrato obligaría a tocar las 16.

**Razón técnica adicional, independiente de la prueba.** `ICurrentUserService` es un **snapshot síncrono** poblado por `UserIdentitySnapshot` (`:13-42`) o proyectado de los claims de la cookie (`AuthenticatedCurrentUserService.cs:73-101`). La fuente de verdad de «¿correo verificado?» la fija la especificación en `ExternalLogins.ProviderEmailVerifiedAt`, que exige una consulta. Encajarla en una propiedad síncrona obligaría a `sync-over-async`, prohibido por la guía `csharp-async` del repositorio.

**Contrato resultante:**

```csharp
namespace Ludeka.Application.Features.Identity;

/// <summary>
/// Conexiones de identidad externa de la propia cuenta de la sesión (INC-49). Es una lectura de
/// estado de cuenta para la interfaz: nunca decide autorización, que la ejercen el pipeline y la
/// revalidación de los servicios de escritura.
/// </summary>
public interface IAccountConnectionsService
{
    /// <summary>Vínculos de la cuenta de la sesión. Sin sesión devuelve una vista vacía.</summary>
    Task<AccountConnectionsView> GetConnectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si la cuenta de la sesión conserva al menos una identidad externa cuyo correo entregó
    /// el proveedor como verificado. Sin sesión devuelve <c>false</c>: no hay cuenta que avisar.
    /// </summary>
    Task<bool> HasVerifiedProviderEmailAsync(CancellationToken cancellationToken = default);
}
```

**Comparativa exigida por el encargo: claim en la cookie frente a lectura por consulta.**

| Criterio | Claim en la cookie | **Lectura por consulta (elegida)** |
|---|---|---|
| Coste por render | Cero | Un `SELECT` indexado por `UserId`, cacheado por ámbito |
| Coste por acceso | Una consulta extra en cada inicio de sesión + segundo argumento en `BuildSessionPrincipal` | Cero |
| **Corrección tras desvincular** | **Incorrecto**: la desvinculación ocurre en el circuito y no refirma la cookie; el aviso no aparecería hasta el siguiente acceso | Correcto |
| Corrección tras vincular | Correcto (el callback refirma la cookie) | Correcto |
| Cookies emitidas antes de INC-49 | Sin el claim: cualquier valor por defecto es incorrecto para alguien | No aplica |
| Superficie de cambio | `BuildSessionPrincipal`, `CookieClaimProjection`, y las pruebas que fijan los claims | Un servicio nuevo, sin tocar nada existente |

La desvinculación es precisamente el caso en que el aviso **debe** aparecer, y es el único que el claim no puede resolver. Decide la corrección, no el coste.

**Caché de ámbito y su rebaba conocida.** La implementación cachea el resultado durante el ámbito (petición SSR o circuito). Tras una desvinculación dentro del circuito, la cabecera puede tardar una navegación completa en mostrar el aviso; **la página `/cuenta/conexiones` lo muestra de inmediato** porque recarga su propio listado. Es un desfase de un solo salto, en la dirección conservadora (el aviso llega tarde, nunca de más), y queda documentado como limitación aceptada.

---

### D7 — Auditoría: dos valores nuevos, entidad `User`, una entrada por operación con éxito

**Elección.** Se añaden `LinkedProvider` y `UnlinkedProvider` a `AuditAction` (`AuditAction.cs:6-47`) y sus ramas a `AuditService.GetActionDisplayName` (`:114-124`). **No se añade ningún `AuditEntityType`**: la entidad afectada es la cuenta, y `AuditEntityType.User` ya existe.

```csharp
AuditAction.Published => "Publicación",
AuditAction.LinkedProvider => "Vinculación de Proveedor",       // ← INC-49
AuditAction.UnlinkedProvider => "Desvinculación de Proveedor",  // ← INC-49
_ => "Operación"
```

Los dos textos son específicos y distintos del genérico `"Operación"`, que es lo que exigen los dos escenarios de `user-management-permissions-audit`.

**Qué se registra exactamente**, vía `IAuditService.RecordChangeAsync(RecordAuditCommand)` (`AuditDtos.cs:46-55` → `AuditService.cs:27-55`):

| Campo | Vinculación | Desvinculación |
|---|---|---|
| `UserId` | `user.Id` de la cuenta de la sesión (lo exige `SessionIdentity.Require`, `AuditService.cs:33`) | idem |
| `UserName` | `user.UserName` de la relectura `AsNoTracking` | idem |
| `Action` | `LinkedProvider` | `UnlinkedProvider` |
| `EntityType` | `AuditEntityType.User` | idem |
| `EntityId` | `user.Id` | idem |
| `EntityName` | `user.UserName` | idem |
| `Summary` | «Vinculación del proveedor de acceso {Proveedor}» | «Desvinculación del proveedor de acceso {Proveedor}» |
| `Changes` | `("Provider", null, "{Proveedor}")` y, **solo si hubo reemplazo**, `("Email", correoSintético, correoVerificado)` | `("Provider", "{Proveedor}", null)` |

**Nunca se audita un intento denegado ni rechazado.** Las excepciones (`LastAccessMethodException`, `UnauthorizedAccessException`) y los resultados `Rejected…` / `AlreadyLinked…` salen **antes** de la llamada de auditoría. Es lo que exige el escenario «Un intento denegado o rechazado no registra una auditoría de éxito». *No se añade auditoría de fracasos: sería alcance nuevo sobre una bitácora que hoy solo registra mutaciones completadas.*

**`IAuditService` entra en `ExternalLoginService` como dependencia opcional** (`IAuditService? audit = null`, el mismo idioma que `GameIssueReportService` y `UserLocationService` usan con `ICurrentUserService?`). Motivo concreto y no estético: `ExternalLoginServiceTests.cs:35` construye `new ExternalLoginService(new ExternalLoginRepository(_context), new SqliteUserRepository(_context))`. Con el parámetro opcional, **esa línea y las 6 pruebas de INC-46 siguen compilando sin tocarlas**; en producción `Program.cs` lo inyecta siempre. Una prueba dedicada verifica que, cuando se suministra, ambas operaciones auditan.

---

## 5. Cambios por fichero

### 5.1 Producción

| Fichero | Acción | Qué cambia |
|---|---|---|
| `src/Ludeka.Core/Entities/ExternalLogin.cs` | Modificar | Propiedad `ProviderEmailVerifiedAt`; 6.º parámetro opcional `providerEmailVerified` al **final** del constructor; invariante de la sección 3.3 |
| `src/Ludeka.Core/Enums/AuditAction.cs` | Modificar | `LinkedProvider`, `UnlinkedProvider` |
| `src/Ludeka.Application/Contracts/IExternalLoginRepository.cs` | Modificar | `ListByUserIdAsync(userId, ct)` y `RemoveAsync(externalLogin, ct)`. **Ningún método de actualización** (§3.1) |
| `src/Ludeka.Application/Features/Identity/IExternalLoginService.cs` | Modificar | `LinkAsync`, `UnlinkAsync`; documentación de la cascada con 2a y 2b |
| `src/Ludeka.Application/Features/Identity/ExternalLoginService.cs` | Modificar | Partición 2a/2b; `LinkAsync`; `UnlinkAsync` con la guarda; `TryReplacePlaceholderEmailAsync`; `IsPlaceholderEmail`; `IAuditService?` opcional |
| `src/Ludeka.Application/Features/Identity/IAccountConnectionsService.cs` | **Crear** | Contrato de lectura (D6) |
| `src/Ludeka.Application/Features/Identity/AccountConnectionsService.cs` | **Crear** | Implementación sobre `IExternalLoginRepository` + `ICurrentUserService`, con caché de ámbito |
| `src/Ludeka.Application/Features/Identity/AccountConnectionDtos.cs` | **Crear** | `AccountConnectionsView`, `AccountConnectionDto`, `ExternalLoginLinkOutcome`, `ExternalLoginLinkResult` |
| `src/Ludeka.Application/Features/Identity/AccountConnectionMessages.cs` | **Crear** | Textos exactos de §3.1, §4.5 (D5) y del aviso |
| `src/Ludeka.Application/Features/Identity/AccountConnectionExceptions.cs` | **Crear** | `ExternalLoginCollisionException`, `LastAccessMethodException` |
| `src/Ludeka.Application/Features/Admin/AuditService.cs` | Modificar | Dos ramas en `GetActionDisplayName` (`:114-124`) |
| `src/Ludeka.Infrastructure/Data/ExternalLoginRepository.cs` | Modificar | Implementación EF de los dos métodos nuevos |
| `src/Ludeka.Infrastructure/Data/LudekaDbContext.cs` | Modificar | Una línea de mapeo Fluent tras `:489` |
| `src/Ludeka.Infrastructure/Migrations/<ts>_AddProviderEmailVerifiedAtToExternalLogins.cs` | **Crear** (generado) | Migración aditiva (+ `.Designer.cs` y snapshot, artefactos generados) |
| `src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs` | Modificar | Columna en el `CREATE TABLE` del bloque 22 (`:848-856`) **y** bloque 23 nuevo |
| `src/Ludeka.Web/Authentication/ExternalLoginIntent.cs` | **Crear** | Claves y lectura/escritura pura de la intención en `Items` |
| `src/Ludeka.Web/Authentication/ExternalLoginEvents.cs` | Modificar | Bifurcación en `HandleTicketReceivedAsync` (`:37-72`); el método pasa a `public static` |
| `src/Ludeka.Web/Program.cs` | Modificar | `POST /cuenta/conexiones/vincular` con `.RequireAuthorization()` tras el bloque `/logout` (`:488`); registro de `IAccountConnectionsService` junto a `:72-73` |
| `src/Ludeka.Web/Components/Pages/AccountConnections.razor` | **Crear** | Página `/cuenta/conexiones` con `[Authorize]` simple |
| `src/Ludeka.Web/Components/Shared/AccountEmailNotice.razor` | **Crear** | Aviso descartable de cabecera (§3.2) |
| `src/Ludeka.Web/Components/Layout/MainLayout.razor` | Modificar | Montaje de `<AccountEmailNotice />` en la cabecera |
| `src/Ludeka.Web/Components/Pages/Login.razor` | Modificar | `[SupplyParameterFromQuery]` y bloque de aviso de colisión (código cerrado) |

**Nada se borra.** Ningún fichero desaparece y ninguna firma existente cambia de forma incompatible.

### 5.2 Pruebas

| Fichero | Acción | Cobertura |
|---|---|---|
| `tests/…/Domain/ExternalLoginTests.cs` | Ampliar | Invariante de `ProviderEmailVerifiedAt` (4 casos: verificado con correo, verificado sin correo, no verificado, `linkedAt` explícito) |
| `tests/…/Application/ExternalLoginCascadeRegressionTests.cs` | **Crear** | Regresión explícita de las 3 ramas de INC-46 **antes** de tocar `ResolveAsync` (mitigación de la propuesta §8) |
| `tests/…/Application/ExternalLoginLinkingTests.cs` | **Crear** | `LinkAsync` (4 resultados), `UnlinkAsync`, guarda del último método, petición fabricada, auditoría, reemplazo de correo y su colisión |
| `tests/…/Application/AccountConnectionsServiceTests.cs` | **Crear** | Vista de conexiones y `HasVerifiedProviderEmailAsync` (con y sin sesión, con y sin fila verificada) |
| `tests/…/Application/AccountConnectionMessagesTests.cs` | **Crear** | Los mensajes no contienen «fusion», «transferir», «traspas», «soporte»; el de colisión dirige a Conexiones |
| `tests/…/Application/ExternalLoginServiceTests.cs` | **Sin tocar** | Las 6 pruebas de INC-46 (`:51-146`) deben seguir pasando **sin modificación**: es el criterio de no regresión |
| `tests/…/Infrastructure/ExternalLoginPersistenceTests.cs` | Ampliar | Persistencia de la columna nueva; `ListByUserIdAsync`; `RemoveAsync`; el índice único rechaza el duplicado |
| `tests/…/Infrastructure/SqliteSchemaMigratorTests.cs` | Ampliar | Reconciliación sobre base preexistente (§D2) |
| `tests/…/Web/ExternalLoginIntentTests.cs` | **Crear** | La intención se lee de `Items` y solo de ahí; `Items` ausentes ⇒ camino de acceso intacto |
| `tests/…/Web/ExternalLoginEventsLinkBranchTests.cs` | **Crear** | Bifurcación, `HandleResponse`, 302, y los tres estados de sesión de D1 |
| `tests/…/Web/AccountConnectionsPageContractTests.cs` | **Crear** | `@page`, `[Authorize]` sin `Policy`, formulario clásico con `data-enhance="false"`, `<AntiforgeryToken />` |
| `tests/…/Web/AuthorizationPipelineContractTests.cs` | Ampliar | El endpoint de vinculación declara `.RequireAuthorization()` |

---

## 6. Interfaces y contratos

```csharp
// ── src/Ludeka.Application/Contracts/IExternalLoginRepository.cs (ampliación) ──
Task<IReadOnlyList<ExternalLogin>> ListByUserIdAsync(string userId, CancellationToken cancellationToken = default);
Task RemoveAsync(ExternalLogin externalLogin, CancellationToken cancellationToken = default);

// ── src/Ludeka.Application/Features/Identity/IExternalLoginService.cs (ampliación) ──
/// <summary>
/// Vincula la identidad externa entrante a la cuenta indicada por la sesión. NUNCA aprovisiona un
/// <c>AppUser</c> nuevo, sea cual sea el resultado del desafío.
/// </summary>
/// <exception cref="UnauthorizedAccessException">No hay sesión, o la cuenta no existe o está suspendida.</exception>
Task<ExternalLoginLinkResult> LinkAsync(
    string userId, string provider, string providerKey,
    string? email, bool emailVerified, CancellationToken cancellationToken = default);

/// <summary>
/// Desvincula el proveedor indicado de la cuenta de la sesión. Es idempotente si no estaba vinculado.
/// </summary>
/// <exception cref="LastAccessMethodException">Es el último método de acceso de la cuenta.</exception>
/// <exception cref="UnauthorizedAccessException">No hay sesión, o la cuenta no existe o está suspendida.</exception>
Task UnlinkAsync(string userId, string provider, CancellationToken cancellationToken = default);

// ── src/Ludeka.Application/Features/Identity/AccountConnectionDtos.cs ──
/// <summary>Resultado de un intento de vinculación desde sesión activa.</summary>
public enum ExternalLoginLinkOutcome
{
    /// <summary>Se creó la fila y el proveedor queda vinculado a la cuenta de la sesión.</summary>
    Linked,
    /// <summary>Ese mismo par ya pertenecía a esta cuenta: no se crea nada y no es un error.</summary>
    AlreadyLinkedToThisAccount,
    /// <summary>El par pertenece a otra cuenta: se rechaza sin mover ni duplicar la fila.</summary>
    RejectedOwnedByAnotherAccount
}

public sealed record ExternalLoginLinkResult(
    ExternalLoginLinkOutcome Outcome,
    string Provider,
    AppUser User,
    bool AccountEmailReplaced = false);

public sealed record AccountConnectionDto(
    string Provider, bool IsLinked, DateTimeOffset? LinkedAt, bool ProviderEmailVerified);

public sealed record AccountConnectionsView(
    IReadOnlyList<AccountConnectionDto> Connections,
    bool HasVerifiedProviderEmail,
    bool CanUnlink);   // false cuando solo queda un método de acceso

// ── src/Ludeka.Web/Authentication/ExternalLoginIntent.cs ──
/// <summary>
/// Intención de un desafío externo, transportada en <see cref="AuthenticationProperties.Items"/> y
/// por tanto dentro del parámetro <c>state</c> que el manejador remoto protege con Data Protection.
/// Nunca se lee del formulario ni de la cadena de consulta: ahí sería alterable por el navegador.
/// </summary>
public static class ExternalLoginIntent
{
    public const string IntentKey = "ludeka:intent";
    public const string LinkValue = "link";
    public const string UserIdKey = "ludeka:link_user_id";

    public static void MarkLink(AuthenticationProperties properties, string userId);
    public static bool TryReadLink(AuthenticationProperties? properties, out string userId);
}
```

---

## 7. Estrategia de pruebas (Strict TDD)

Toda unidad empieza por su prueba RED. El runner contractual es `dotnet test Ludeka.sln`.

| Capa | Qué se prueba | Cómo |
|---|---|---|
| **Dominio** | Invariante de `ProviderEmailVerifiedAt`; `IsPlaceholderEmail` | xUnit `[Fact]`/`[Theory]` puros, sin base de datos. El escalón más rápido |
| **Aplicación** | Las 4 ramas de `ResolveAsync`; los 3 resultados de `LinkAsync`; la guarda del último método; la petición fabricada; el reemplazo de correo y su colisión; la auditoría | SQLite `Filename=:memory:` con `IAsyncLifetime`, siguiendo `ExternalLoginServiceTests.cs:17-42` |
| **Infraestructura** | Persistencia de la columna; `ListByUserIdAsync`; `RemoveAsync`; el índice único rechaza el duplicado; reconciliación SQLite sobre base preexistente | `EnsureCreatedAsync` para el esquema nuevo; `CREATE TABLE` manual con el esquema de INC-46 para la reconciliación |
| **Web** | `ExternalLoginIntent` (puro); bifurcación de `HandleTicketReceivedAsync` con `TicketReceivedContext` real; contratos de fuente de la página y del endpoint | Pruebas puras + lectura de fuente, el idioma que ya usa `AuthorizationPipelineContractTests.cs` |
| **Manual** | Ciclo vincular → desvincular → intento de desvincular el último, con navegador real | Smoke test de la propuesta §11 (no automatizable sin credenciales OAuth en el repositorio) |

**Pruebas de regresión obligatorias de la partición 2a/2b.** Se escriben **antes** de tocar `ResolveAsync`:

1. `RepeatPair_ShouldStillResolveWithoutCreatingRows` — rama 1 intacta.
2. `VerifiedEmailMatchingAccountWithoutLinks_ShouldStillLinkAutomatically` — **rama 2a**, la excepción segura, fijada por aserción y no por confianza; afirma además `ProviderEmailVerifiedAt` establecido.
3. `VerifiedEmailMatchingAccountWithExistingLinks_ShouldThrowCollisionWithoutCreatingRows` — **rama 2b**: `Assert.ThrowsAsync<ExternalLoginCollisionException>` más recuento invariante de `AppUsers` y `ExternalLogins`.
4. `Collision_ShouldNeverChangeTheExistingLinkOwner` — repetidos intentos; el `UserId` de la fila original no cambia.
5. `UnverifiedEmail_ShouldStillProvisionANewAccount` — rama 3 intacta.
6. `NoEmail_ShouldStillUseThePlaceholderDomain` — rama 3 sin correo.

**Comprobación de no regresión hecha en esta fase.** Se ha recorrido una a una las 6 pruebas de `ExternalLoginServiceTests.cs:51-146` contra la cascada nueva: `VerifiedEmailMatchingExistingAccount_…` (`:65-86`) siembra `laura@ludeka.es` **sin ninguna fila `ExternalLogin`**, así que cae en la rama **2a** y su resultado es idéntico; las otras cinco no alcanzan la rama 2 en absoluto. **Las seis siguen verdes sin modificarse.** Si `sdd-apply` necesita editar alguna de ellas, eso es señal de que el diseño se ha desviado, no de que la prueba estuviera mal.

---

## 8. Matriz de amenazas

### 8.1 Matriz canónica de la plantilla

Ninguna de sus cinco filas aplica: este cambio no ejecuta órdenes de intérprete, no lanza subprocesos, no automatiza control de versiones ni Pull Requests y no clasifica ficheros ejecutables.

| Frontera | Aplicabilidad | Motivo |
|---|---|---|
| Rutas con aspecto de documentación | **N/A** | El cambio no clasifica ni ejecuta ficheros; no lee `requirements.txt`, `CMakeLists.txt`, Markdown ejecutable ni `README.sh` |
| Selección de repositorio Git | **N/A** | No se invoca `git` ni se resuelve ningún directorio de trabajo desde código |
| Estado del índice de commits | **N/A** | No hay automatización de commits |
| Estado de push | **N/A** | No hay automatización de push |
| Órdenes de Pull Request | **N/A** | No se componen órdenes de `gh` ni equivalentes |

### 8.2 Matriz específica del cambio (fronteras reales: enrutado HTTP y redirección OAuth)

| Frontera | Caso adversario | Conducta segura | Conducta ante fallo | Prueba RED planificada |
|---|---|---|---|---|
| **Intención de vinculación** | Formulario con `userId` de otra cuenta | El `UserId` se lee **solo** de `HttpContext.User` y de `Items`; el formulario nunca | El campo sobrante se ignora, no se rechaza: no existe | `ExternalLoginIntentTests`: `TryReadLink` solo lee de `Items` |
| **Intención de vinculación** | `state` manipulado o reutilizado | Data Protection invalida el `state`; el manejador remoto falla antes de llegar al evento | No hay vinculación ni sesión nueva | Verificación por smoke test; el contrato lo garantiza el framework |
| **Sesión en el retorno** | La sesión expira o cambia durante el desafío | Se aborta sin escribir; redirección según D1 | Nunca se vincula al `UserId` marcado ni al nuevo | `ExternalLoginEventsLinkBranchTests`: 3 casos |
| **Identidad ya vinculada** | El par pertenece a otra cuenta | Rechazo previo + índice único como red | Mensaje honesto; 0 filas nuevas; la fila original no cambia de dueño | `ExternalLoginLinkingTests`: rechazo y no reasignación |
| **Carrera de vinculación** | Dos intentos simultáneos sobre el mismo par | `DbUpdateException` del índice único mapeada al **mismo** resultado de rechazo | Mensaje idéntico venga de donde venga | `ExternalLoginLinkingTests`: duplicado rechazado |
| **Antiforgery** | POST sin token al endpoint de vinculación | Validación previa a leer el formulario, calcada de `Program.cs:460-467` | `400 Bad Request` | Contrato de fuente + smoke test |
| **Autorización del endpoint** | POST anónimo al endpoint de vinculación | `.RequireAuthorization()`: desafío de cookie → 302 a `/login` | No se emite desafío OAuth | `AuthorizationPipelineContractTests` (ampliación) |
| **Redirección abierta** | `?resultado=` con URL o texto arbitrario | Solo se aceptan **códigos de un conjunto cerrado**; el texto se compone en servidor; todos los destinos son literales fijos | Código desconocido ⇒ no se muestra mensaje | `AccountConnectionsPageContractTests` |
| **Guarda del último método** | POST fabricado que evita la interfaz | La guarda está en `Application`, no en el botón | `LastAccessMethodException` | `ExternalLoginLinkingTests`: llamada directa a `UnlinkAsync` |
| **Enumeración de cuentas** | Sondear si existe una cuenta con cierto correo | Los mensajes de 2b y de P2 solo se muestran a quien **acaba de demostrar ante el proveedor** que controla esa identidad o ese correo verificado | No hay respuesta diferencial accesible sin completar un desafío OAuth | Revisión de redacción en `AccountConnectionMessagesTests` |
| **Índice único de `AppUsers.Email`** | Reemplazo que colisiona con otra cuenta | Comprobación previa + `catch (DbUpdateException)` no fatal | La cuenta conserva el correo sintético; el vínculo sigue creado | `ExternalLoginLinkingTests`: reemplazo con colisión |

---

## 9. Migración y despliegue

**Migración de datos: ninguna.** La columna es aditiva, anulable y sin valor por defecto. Las filas `ExternalLogin` existentes quedan con `ProviderEmailVerifiedAt = NULL`, que es semánticamente correcto: **nadie registró esa verificación, así que no consta**. No se intenta inferirla retroactivamente desde `ProviderEmail`, porque la exploración demostró que esa inferencia es falsa (`ExternalLoginService.cs:67-68,76-77`).

**Efecto visible del arranque.** Toda cuenta preexistente verá el aviso de correo no verificado hasta que vincule un proveedor que entregue correo verificado. Con datos ficticios y sin base de producción (decisión 1) es intrascendente, pero es la **conducta esperada, no un defecto**: el aviso dice la verdad sobre lo que el sistema tiene registrado.

**Sin banderas de funcionalidad ni despliegue por fases.** Cada PR de la cadena es autónomo y la propuesta §9 ya fija la reversión.

**Reversión.** Idéntica a la propuesta §9: revertir PRs en orden inverso; el `Down` de la migración elimina la columna en PostgreSQL; en SQLite la columna sobrante queda inerte, ignorada por el reconciliador.

---

## 10. Plan de entrega

**Se mantiene la partición de 7 PRs de la propuesta §13.2, su orden y sus dependencias.** El único ajuste es que la unidad **G** se parte en dos, por un motivo técnico concreto.

**Por qué se parte G.** El contrato de lectura `IAccountConnectionsService` (D6) lo necesitan **dos** consumidores: la página (unidad E, PR #4) y el aviso de cabecera (unidad G, PR #6). Si viajara con G, la página del PR #4 no tendría de dónde leer. Y si el aviso de cabecera viajara antes que la página, `main` tendría durante la vida de un PR un enlace de cabecera hacia una ruta que todavía no existe. La partición resuelve ambas cosas sin mover ninguna otra pieza:

- **G1** — contrato `IAccountConnectionsService` + implementación + pruebas (70-120 líneas) → **viaja con el PR #3**.
- **G2** — componente `AccountEmailNotice.razor` + montaje en `MainLayout` + pruebas (60-100 líneas) → **se queda en el PR #6**.

| PR | Contenido | Líneas autoradas | Depende de |
|---|---|---|---|
| **#1 — Cimientos de datos y contrato** | A + B + H | 220-365 | `inc/vinculacion-cuentas` |
| **#2 — Vinculación y desvinculación en Application** | C (+ resultados, excepciones y mensajes) | 210-340 | #1 |
| **#3 — Transporte OAuth y contrato de lectura** | D + `ExternalLoginIntent` + **G1** | 220-360 | #2 |
| **#4 — Pantalla `/cuenta/conexiones`** | E | 190-330 | #3 |
| **#5 — Flujo de colisión (2a/2b)** | F | 140-250 | #1 |
| **#6 — Aviso de correo no verificado en cabecera** | **G2** | 60-100 | #3, #4 |
| **#7 — Reemplazo del correo sintético** | I | 80-140 | #2 |

Todos quedan bajo el presupuesto de 400 líneas **incluso en la banda alta**. Se conserva la observación de la propuesta §13.2: `sdd-tasks` puede evaluar fusionar **#6 y #7** (140-240 conjunto) **solo si la medición real** lo deja bajo 400, nunca por estimación. Los ficheros `.Designer.cs` y `LudekaDbContextModelSnapshot.cs` del PR #1 son generados y quedan fuera del conteo; la descripción del PR debe decirlo.

**Cada PR deja la suite en verde por sí mismo — verificado pieza a pieza en esta fase:**

| PR | Qué garantiza que compila y pasa solo |
|---|---|
| #1 | El 6.º parámetro del constructor es opcional y va al final: `ExternalLoginTests.cs:20` y las 6 pruebas de `ExternalLoginServiceTests` no se tocan. Los valores nuevos de `AuditAction` sin consumidor siguen el precedente vivo de `LinkedFounderIdentity` (`AuditAction.cs:46`) |
| #2 | `IAuditService` entra como parámetro **opcional**: `ExternalLoginServiceTests.cs:35` no cambia. `LinkAsync`/`UnlinkAsync` son métodos nuevos, sin consumidores todavía |
| #3 | El endpoint se mapea tras `/logout`, así que `PublicEndpoint_ShouldDeclareAllowAnonymous` (`:73-83`) sigue delimitando igual sus bloques. Sin `Items`, el camino de acceso es idéntico |
| #4 | La página nueva no entra en la `TheoryData` de `ProtectedPages` (`:15-27`), que exige `Policy =`; se cubre con un `[Fact]` propio |
| #5 | Las 6 pruebas de INC-46 se han recorrido una a una contra la cascada nueva (sección 7): ninguna cambia de resultado |
| #6 | Componente nuevo montado en la cabecera; `SessionGuard_ShouldForceAFullReload…` (`:99-113`) solo afirma la presencia de `<SessionGuard />`, que no se mueve |
| #7 | Método privado nuevo dentro de `LinkAsync`, sin superficie pública nueva |

**Encadenado.** PR #1 parte de `inc/vinculacion-cuentas`; cada PR posterior parte de la rama del anterior según sus dependencias, desde el mismo worktree y con `scripts/sdd-worktree.ps1`. El PR #5 depende solo del #1, así que puede abrirse en paralelo si el maintainer lo prefiere, aunque el encadenado lineal es más simple de revisar.

---

## 11. Preguntas abiertas

- [ ] **Ninguna bloquea la implementación.** P2, P3, P4 y P5 quedan cerradas en la sección 3.
- [ ] **Confirmación operativa (para `sdd-apply`, no para el maintainer):** `dotnet ef migrations add` debe ejecutarse con `Ludeka.Infrastructure` como proyecto de destino y `Ludeka.Web` como proyecto de arranque, igual que se generó `20260915164921_AddExternalLogins`. Esta fase no puede verificarlo porque tiene prohibido compilar.
- [ ] **Anotado, no propuesto:** la promoción monótona de `ProviderEmailVerifiedAt` (`null → fecha`) para la cuenta cuyo proveedor empieza a entregar correo verificado después. Queda fuera de INC-49 por §3.3 y exigiría un método de actualización en el repositorio que este diseño deja deliberadamente fuera. Es candidato a incremento propio si el caso aparece con datos reales.
