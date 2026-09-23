# 37. Área de Cuenta, Puerta de Acceso en Cabecera y Topología de Usuario

> **Módulo:** 37 — Área de Cuenta y Navegación de Usuario  
> **Estado:** Implementado y Verificado (INC-50)  
> **Fuentes:** [`Account.razor`](file:///C:/repos/ludeka-wt/area-de-cuenta/src/Ludeka.Web/Components/Pages/Account.razor), [`AccountSectionNav.razor`](file:///C:/repos/ludeka-wt/area-de-cuenta/src/Ludeka.Web/Components/Shared/AccountSectionNav.razor), [`MainLayout.razor`](file:///C:/repos/ludeka-wt/area-de-cuenta/src/Ludeka.Web/Components/Layout/MainLayout.razor), [`MyLibrary.razor`](file:///C:/repos/ludeka-wt/area-de-cuenta/src/Ludeka.Web/Components/Pages/MyLibrary.razor), [`RedirectToLogin.razor`](file:///C:/repos/ludeka-wt/area-de-cuenta/src/Ludeka.Web/Components/Shared/RedirectToLogin.razor)  
> **Pruebas:** `tests/Ludeka.UnitTests/Web/AccountAreaContractTests.cs` (11 contratos) y `tests/Ludeka.UnitTests/Web/AuthorizationPipelineContractTests.cs`

---

## 1. Contexto y Justificación

Tras la entrega de la autenticación real (INC-46) y la vinculación de identidades sociales (INC-49), la interfaz presentaba un vacío arquitectónico de costura:
1. No existía ningún botón físico ni enlace visible hacia `/login` en toda la interfaz de la aplicación.
2. La pantalla de conexiones sociales ([`/cuenta/conexiones`](file:///C:/repos/ludeka-wt/area-de-cuenta/src/Ludeka.Web/Components/Pages/AccountConnections.razor)) solo era alcanzable mediante el banner de advertencia para cuentas sin correo electrónico verificado. Para usuarios autenticados mediante Google o Discord (cuyos correos vienen verificados de origen), era imposible llegar a la gestión de cuenta sin teclear manualmente la URL.
3. La cabecera principal ignoraba si el visitante disponía de una sesión activa o anónima.

El incremento INC-50 resuelve esta carencia introduciendo una puerta de cabecera universal, un hub de cuenta en `/cuenta`, una ruta canónica en `/cuenta/ludoteca` que coexiste con el alias permanente `/mi-ludoteca`, y la delegación correcta de `ReturnUrl` en redirecciones no autorizadas.

---

## 2. Puerta de Acceso en Cabecera (`MainLayout.razor`)

En la zona de controles y utilidades de la cabecera global se ubica el botón de acceso de usuario:

```html
@if (HasSession)
{
    <a href="/cuenta"
       title="Mi cuenta"
       class="px-2.5 py-1.5 rounded-full bg-[var(--bg-surface-elevated)] text-[var(--text-primary)] border border-[var(--border-subtle)] hover:border-[var(--brand-primary)] transition-all flex items-center gap-1.5 text-xs font-semibold shadow-sm shrink-0 min-h-[24px] min-w-[24px] focus:outline-none focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)] focus-visible:ring-offset-2">
        <Icon Name="user" Size="14" />
        <span class="sr-only sm:not-sr-only truncate max-w-[8rem]">@CurrentUserService.UserName</span>
    </a>
}
else
{
    <a href="/login"
       title="Entrar"
       class="px-2.5 py-1.5 rounded-full bg-[var(--bg-surface-elevated)] text-[var(--text-primary)] border border-[var(--border-subtle)] hover:border-[var(--brand-primary)] transition-all flex items-center gap-1.5 text-xs font-semibold shadow-sm shrink-0 min-h-[24px] min-w-[24px] focus:outline-none focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)] focus-visible:ring-offset-2">
        <Icon Name="user" Size="14" />
        <span class="sr-only sm:not-sr-only">Entrar</span>
    </a>
}
```

### Reglas de Diseño y Accesibilidad (WCAG 2.2 AA)
- **Consulta de sesión:** Se apoya en la propiedad `HasSession => !string.IsNullOrWhiteSpace(CurrentUserService.UserId)` sin alterar la superficie congelada de `ICurrentUserService`.
- **Doble estado:** Con sesión redirige al hub `/cuenta` y exhibe el nombre del usuario (`UserName`); sin sesión redirige a `/login` con el rótulo «Entrar».
- **Sin `ReturnUrl` en la puerta pública:** Pulsar el botón de login desde la navegación pública no inyecta ningún destino privado artificial.
- **Accesibilidad visual y semántica:** Empleo de `sr-only sm:not-sr-only` para que lectores de pantalla reciban el texto en cualquier resolución sin ocultarlo del árbol DOM, foco accesible con anillo (`focus-visible:ring-2`) y área táctil mínima de 24×24 px CSS.

---

## 3. Hub Centralizado de Cuenta (`Account.razor`)

El punto de encuentro privado del usuario se publica en `/cuenta` bajo autorización plana (basta tener sesión activa sin requerir permisos de moderación):

```razor
@page "/cuenta"
@attribute [Authorize]
```

El componente funciona en SSR estático puro (cero dependencias de JavaScript o `@code`) e inventaría las cuatro parcelas del usuario mediante tarjetas de acción rápida:
1. **Mi Ludoteca:** Enlace directo a `/cuenta/ludoteca` (colección, estados de juego, partidas y préstamos).
2. **Conexiones y Acceso:** Enlace a `/cuenta/conexiones` (gestión multi-proveedor OAuth heredada de INC-49).
3. **Tema Visual:** Deep-link contextual `/cuenta/ludoteca?seccion=apariencia`.
4. **País Territorial:** Deep-link contextual `/cuenta/ludoteca?seccion=apariencia`.

---

## 4. Navegación Compartida y Rutas Hijas

### 4.1 Componente de Cabecera de Sección (`AccountSectionNav.razor`)
Las rutas hijas del área de cuenta montan un componente presentacional común que provee navegación cruzada y retorno rápido al hub:

- Enlace de retorno `href="/cuenta"` con icono `arrow-left`.
- Pestañas horizontales para **Ludoteca** (`/cuenta/ludoteca`) y **Conexiones** (`/cuenta/conexiones`).
- Indicador accesible `aria-current="page"` computado reactivamente sobre el parámetro `Active`.

### 4.2 Ludoteca: Ruta Canónica y Alias Permanente (`MyLibrary.razor`)
Para no romper enlaces compartidos, marcadores en navegadores ni accesos de la PWA:
- **Ruta Canónica:** `@page "/cuenta/ludoteca"`
- **Alias Permanente:** `@page "/mi-ludoteca"`
- Ambas rutas comparten exactamente el mismo código y lógica de presentación.
- Acepta el parámetro de consulta `[SupplyParameterFromQuery(Name = "seccion")]` para saltar directamente a la pestaña de configuración de apariencia (`TabType.Appearance`).

---

## 5. Preservación de `ReturnUrl` en Redirecciones Interactivas

En circuitos Blazor interactivos (`InteractiveServer`), cuando un visitante anónimo intenta acceder a una ruta protegida con `[Authorize]` (como `/cuenta` o `/admin`), `RedirectToLogin.razor` intercepta el evento en `OnInitialized`:

```csharp
if (RendererInfo.IsInteractive)
{
    Navigation.ToLogin();
}
```

`LoginRedirect.ToLogin()` resuelve y escapa la URL solicitada (`ReturnUrl`), garantizando que tras autenticarse en Google o Discord, el usuario regrese a la página que pretendía visitar, impidiendo bucles infinitos gracias a la guarda `ResolveReturnUrl`.

---

## 6. Reescritura Selectiva de Enlaces

| Fichero | Enlace Previo | Enlace Actualizado | Justificación |
|---|---|---|---|
| `MainLayout.razor` | `/mi-ludoteca` | `/cuenta/ludoteca` | Píldora de cabecera integrada en la topología de cuenta |
| `GameDetail.razor` | `/mi-ludoteca` | `/cuenta/ludoteca` | Acceso a la ludoteca desde el pie de ficha de juego |
| `Radar.razor` | `/mi-ludoteca` | `/cuenta/ludoteca` | Acceso a juegos en seguimiento de compra |
| `PublicProfile.razor` | `/mi-ludoteca` | `/mi-ludoteca` *(sin cambio)* | Fuera de alcance, resuelve por alias |
| `manifest.webmanifest` | `/mi-ludoteca` | `/mi-ludoteca` *(sin cambio)* | PWA offline, resuelve por alias |
| `offline.html` | `/mi-ludoteca` | `/mi-ludoteca` *(sin cambio)* | Modo offline, resuelve por alias |

---

## 7. Verificación Automática

- **Suite Unitaria:** 11 pruebas de contrato en `tests/Ludeka.UnitTests/Web/AccountAreaContractTests.cs` certifican la sintaxis Razor, los enlaces de las cuatro tarjetas, el marcado WCAG 2.2 AA y la permanencia de los archivos fuera de alcance.
- **Suite de Integración y Pipeline:** Pruebas en `AuthorizationPipelineContractTests.cs` verifican la protección con `[Authorize]`, el enrutado de `AuthorizeRouteView` y la invocación de `Navigation.ToLogin()`.
- **Métricas:** 1.639 pruebas unitarias + 10 de integración verificadas al 100%.
