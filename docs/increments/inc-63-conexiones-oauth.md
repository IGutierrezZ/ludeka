# INC-63: Conexiones OAuth — Múltiples Proveedores sin Cuentas Duplicadas

> **Estado:** ⏳ En progreso (2026-09-25, Fase D)
> **Fecha de Inicio:** 2026-09-25
> **Rama de Trabajo:** `inc/conexiones-oauth`
> **Worktree:** `C:\repos\ludeka-wt\conexiones-oauth`
> **Dependencias:** INC-49 (Vinculación de Cuentas, archivado), INC-50 (Área de Cuenta), INC-61 (menú de cuenta)
> **Especificación Viva:** [32. Autenticación y Autorización](file:///c:/repos/Ludeka/docs/specs/sistema/32-autenticacion-y-autorizacion.md) · [33. Vinculación de Cuentas y Recuperación de Acceso](file:///c:/repos/Ludeka/docs/specs/sistema/33-vinculacion-cuentas-y-recuperacion-de-acceso.md)

---

## 1. Cómo se descubrió

Al perfilar la Fase D del backlog de cuenta se auditó el estado real de la vinculación OAuth heredada de INC-46/INC-49 y aparecieron huecos entre el modelo de datos y los flujos de usuario.

## 2. El agujero, verificado

Lo que SÍ existe (evidencia de código):

- `ExternalLogin` con `Provider`/`ProviderKey` e índice único `("Provider","ProviderKey")`; campo `ProviderEmailVerifiedAt`.
- `ExternalLoginService.ResolveAsync` / `LinkAsync` / `UnlinkAsync`, `ExternalLoginCollisionException`.
- `AccountConnectionsService` y ruta `/cuenta/conexiones/vincular`.
- Proveedores registrados en `ExternalAuthenticationSchemes.cs`: Google, Discord, Facebook.

Huecos verificados:

- `ProviderEmailVerifiedAt` **existe en el modelo pero no se ha verificado ningún flujo que lo escriba ni que lo muestre** (hueco de evidencia: campo huérfano posible).
- La pantalla de conexiones es inalcanzable para quien tiene correo verificado (documentado en INC-50 §2.1): este incremento hereda ese lastre de acceso.
- Política de «última cuenta» al desvincular no verificada: riesgo de cuenta huérfana sin ningún proveedor.
- Sin tests verificados de la ruta de colisión `ExternalLoginCollisionException` end-to-end.

## 3. Lo que pide el maintainer

> «o lo de sincronizar cuentas por ejemplo si te has logueado con google sincronizar tu cuenta de discord y poder loguearte con cualquiera de las dos y te lleve a tu cuenta y no duplique cuenta.»

Varios proveedores (p. ej. Google y Discord) ligados a la MISMA cuenta: entrar con cualquiera de ellos lleva siempre a esa cuenta y no se duplican cuentas.

## 4. Alcance y decisiones que hay que tomar

1. Garantía multi-proveedor → misma cuenta: vincular Google y Discord (mínimo) en una misma cuenta y entrar con cualquiera de los dos llega siempre a ella (test end-to-end).
2. Prevención de cuentas duplicadas: un proveedor ya ligado a otra cuenta no crea una segunda cuenta. **Decisión abierta:** política para duplicados históricos ya existentes (fusión asistida, bloqueo con soporte, o ninguno).
3. Endurecer `UnlinkAsync` con política de última cuenta (bloquear o exigir alternativa).
4. Escribir y mostrar `ProviderEmailVerifiedAt` (derivado del proveedor; ayuda a la anti-duplicados por correo).
5. Tests de `ExternalLoginCollisionException` y de resolución multi-proveedor.
6. **Decisión abierta:** ampliar proveedores (GitHub, Apple, Microsoft) o consolidar los tres actuales.
7. Alinear la ruta de conexiones con el menú de cuenta (INC-61) para que sea alcanzable.

## 5. Fuera de alcance (salvo que el maintainer diga lo contrario)

Cambio de contraseña (no existe modelo de contraseña), SSO corporativo/SAML y recuperación por preguntas de seguridad.

## 6. Criterios de aceptación

1. Test end-to-end: alta con Google → vincular Discord → salir → entrar con Discord → misma cuenta y mismos datos.
2. Intentar vincular un proveedor ya ligado a otra cuenta no crea duplicado y devuelve un aviso claro (test de colisión).
3. No se puede desvincular el último proveedor sin dejar una vía de acceso (probado con test).
4. `ProviderEmailVerifiedAt` se escribe y se muestra, o se retira del modelo (decisión explícita).
5. La pantalla de conexiones es alcanzable desde el menú de cuenta con sesión activa.
6. Tests de colisión de `ProviderKey` en verde.
7. Línea base: suite completa en verde con `dotnet test` al abrir el incremento.

## 7. Riesgos

- Cuenta huérfana (sin proveedor y sin contraseña) = pérdida de acceso permanente.
- Colisión de correos entre proveedores (`ExternalLoginCollisionException`) mal gestionada bloquea altas.
- Solape con INC-64 (acceso por correo): definir frontera entre vínculo OAuth y magic link.
