# INC-61: Menú de Cuenta y Estado de Sesión en la Cabecera

> **Estado:** ⏳ En progreso (2026-09-24, Fase D)
> **Fecha de Inicio:** 2026-09-24
> **Rama de Trabajo:** `inc/menu-de-cuenta`
> **Worktree:** `C:\repos\ludeka-wt\menu-de-cuenta`
> **Dependencias:** INC-50 (Área de Cuenta, planificado) — este incremento cuelga de su hub
> **Especificación Viva:** [32. Autenticación y Autorización](file:///c:/repos/Ludeka/docs/specs/sistema/32-autenticacion-y-autorizacion.md) · [33. Vinculación de Cuentas y Recuperación de Acceso](file:///c:/repos/Ludeka/docs/specs/sistema/33-vinculacion-cuentas-y-recuperacion-de-acceso.md)

---

## 1. Cómo se descubrió

Durante la revisión del backlog de cuenta se comprobó qué sabe hoy la cabecera sobre la sesión y qué puertas ofrece al usuario identificado. Resultado: hay servicio de sesión, pero no hay menú de cuenta.

## 2. El agujero, verificado

- `MainLayout.razor` inyecta `ICurrentUserService` y monta `<AccountEmailNotice />` (línea 230): el único componente de cabecera consciente de la sesión es un **aviso de correo**, no una puerta de cuenta.
- **No existe icono de perfil ni menú de cuenta** en toda la UI (barrido sin resultados).
- Enlace a `/mi-ludoteca` en el nav de `MainLayout.razor` (~línea 94) y en `Radar.razor:41`, pero como destino suelto, no como parte de un área de usuario.
- `PublicProfile.razor` existe y **no** toca `AccountEmailNotice` (composición independiente).
- La PWA (`manifest.webmanifest`) fija `start_url` = `/mi-ludoteca`: el usuario aterriza en su colección sin ver nunca su identidad en la cabecera.
- `AccountEmailNotice` solo se muestra a cuentas sin correo verificado (patrón heredado de INC-49): para el resto, la cabecera es muda.

## 3. Lo que pide el maintainer

Que la cabecera reconozca al usuario: un botón de persona con menú (perfil, preferencias, conexiones, salir) que colgando del área de cuenta de INC-50 haga innecesario memorizar rutas.

## 4. Alcance y decisiones que hay que tomar

1. Menú desplegable de cuenta en la cabecera (estado autenticado / invitado) colgando del hub de INC-50.
2. Destinos del menú: perfil público (`PublicProfile`), preferencias (INC-62), conexiones (INC-63), `/mi-ludoteca`, salir.
3. Estado invitado: acceso claro a `/login` (hueco documentado en INC-50 §2.2).
4. **Decisión abierta:** identidad visual del botón (iniciales vs avatar) y tratamiento conjunto con `AccountEmailNotice` (¿se absorbe en el menú?).
5. Composición con la barra móvil de INC-60 sin duplicar destinos.

## 5. Fuera de alcance (salvo que el maintainer diga lo contrario)

El hub de área de cuenta en sí (INC-50), los flujos de login (INC-63/64) y el cambio de contraseña (no existe).

## 6. Criterios de aceptación

1. Con sesión activa, la cabecera muestra el control de cuenta con menú navegable por teclado.
2. Sin sesión, hay una puerta visible a `/login`.
3. Cada destino del menú resuelve a una vista existente (sin rutas huérfanas).
4. Cierre de sesión funciona y actualiza la cabecera sin recarga completa.
5. Línea base: suite completa en verde con `dotnet test` al abrir el incremento.

## 7. Riesgos

- **Solape directo con INC-50** si ambos crean puertas de cuenta en paralelo: ejecutar INC-50 primero y componer, no competir.
- Estado de sesión en Blazor SSR (hidratación diferida del menú).
- Accesibilidad del desplegable (foco, `aria-expanded`, Escape).
