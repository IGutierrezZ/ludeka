# INC-64: Acceso por Correo con Verificación (Evaluar e Implantar si se Aprueba)

> **Estado:** ⏳ En progreso  
> **Fecha de Inicio:** 2026-09-25  
> **Rama de Trabajo:** `inc/acceso-por-correo`
> **Worktree:** `C:\repos\ludeka-wt\acceso-por-correo`
> **Dependencias:** INC-49 (política de correo ausente, archivado), INC-63 (frontera con OAuth)
> **Especificación Viva:** [33. Vinculación de Cuentas y Recuperación de Acceso](file:///c:/repos/Ludeka/docs/specs/sistema/33-vinculacion-cuentas-y-recuperacion-de-acceso.md) · [36. Persistencia de Identidad](file:///c:/repos/Ludeka/docs/specs/sistema/36-persistencia-de-identidad.md)

---

## 1. Cómo se descubrió

El proyecto promete explícitamente un acceso sin fricción en su propia pantalla de login, que dice textualmente:

> «sin registro, sin contraseña y sin correo de confirmación»

(`src/Ludeka.Web/Components/Pages/Login.razor:15`)

Pero al auditar la Fase D del backlog de cuenta se comprobó que esa promesa y la realidad de la identidad no encajan: hay cuentas con correo y no existe ningún mecanismo de acceso por correo.

## 2. El agujero, verificado

- **No existe magic link, ni password, ni reset** en todo el repositorio (barrido sin resultados).
- `Login.razor:15` promete «sin registro, sin contraseña y sin correo de confirmación»: hoy es cierto solo porque el único acceso es OAuth puro.
- INC-49 (archivado) fijó la «política de correo ausente» y `AccountEmailNotice` avisa a cuentas sin correo verificado — pero no hay flujo de verificación ni de acceso alternativo por correo.
- `ExternalLogin.ProviderEmailVerifiedAt` existe en el modelo sin flujo asociado (ver INC-63).
- El outbox persistente de INC-47 sí daría soporte de envío (módulo `34-trabajos-en-segundo-plano-cloud-run.md`), pero nada lo consume para correo transaccional de acceso.

**Puerta de decisión de partida:** el maintainer pidió *valorar* el acceso por correo con verificación (no está aprobado). Si se aprueba, segunda bifurcación abierta: contraseña vs magic link (enlace de un solo uso). Si se descarta, el copy de `Login.razor` debe corregirse igualmente para no mentir. En cualquier caso hay que cerrar la contradicción de la promesa.

## 3. Lo que pide el maintainer

> «valorar incluir loguin con email y verificacion de email .»

Primero valorar si se incluye login por email con verificación de buzón (puerta de decisión explícita del maintainer) y, solo si se aprueba, implantarlo. De paso, cerrar la contradicción entre la promesa de `Login.razor` y la realidad.

## 4. Alcance y decisiones que hay que tomar

1. **Fase 1 — Puerta de decisión (bloqueante):** propuesta con pros, contras y esfuerzo, y decisión del maintainer: aprobar o descartar el acceso por correo con verificación. Si se descarta, el incremento se archiva honestamente como «descartado por decisión» (y el copy de `Login.razor` se corrige igual).
2. **Decisión abierta (solo si se aprueba):** contraseña vs magic link. Con contraseña: alta, login y (rebanada aparte, si decide) recuperación. Con magic link: enlace de un solo uso, caducidad corta, consumo atómico y envío por el outbox persistente (no SMTP en el host web).
3. Verificación real del buzón en cualquier caso (es lo que pide el maintainer: «verificacion de email»).
4. Actualizar `Login.razor:15` para que refleje exactamente lo implementado (también si se descarta: hoy promete «sin correo de confirmación» y eso solo es cierto por omisión).
5. Frontera explícita con INC-63 (el correo es vía de acceso o identidad verificada, no proveedor OAuth más).

## 5. Fuera de alcance (salvo que el maintainer diga lo contrario)

2FA/TOTP, login por teléfono, email marketing y (salvo decisión expresa) la recuperación de contraseña como primera rebanada si se elige el modelo de contraseña.

## 6. Criterios de aceptación

1. La puerta de decisión (aprobar/descartar el acceso por correo) queda documentada con el análisis de la fase 1; si se aprueba, también la elección contraseña vs magic link.
2. El copy del login refleja exactamente lo implementado (pase lo que se decida).
3. Si se aprueba: acceso por correo con verificación real del buzón funcional end-to-end, y el correo puede ligarse a una cuenta OAuth sin crear duplicado.
4. Si hay magic link: un enlace se consume una sola vez, expira, y hay test que lo demuestre.
5. Ninguna combinación de estado (sin correo, correo sin verificar, OAuth solo) deja al usuario sin vía de acceso.
6. Línea base: suite completa en verde con `dotnet test` al abrir el incremento.

## 7. Riesgos

- **Falsedad documental en UI** si el copy del login sigue prometiendo lo que no hay (el defecto B5 de INC-52 fue exactamente esto en el ROADMAP).
- Superficie de ataque: enlaces de acceso reenviados/interceptados si la caducidad es laxa.
- Dependencia de entregabilidad de correo (SPF/DKIM) que el proyecto aún no ha acreditado en producción.
