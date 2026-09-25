# INC-63: Conexiones OAuth — Múltiples Proveedores sin Cuentas Duplicadas

> **Estado:** ✅ Archivado  
> **Fecha de Inicio:** 2026-09-25  
> **Fecha de Cierre:** 2026-09-25  
> **Rama de Trabajo:** `inc/conexiones-oauth` (PR #113)  
> **Worktree:** `C:\repos\ludeka-wt\conexiones-oauth`  
> **Dependencias:** INC-49 (Vinculación de Cuentas, archivado), INC-50 (Área de Cuenta), INC-61 (menú de cuenta)  
> **Especificación Viva:** [33. Vinculación de Cuentas y Recuperación de Acceso](file:///c:/repos/Ludeka/docs/specs/sistema/33-vinculacion-cuentas-y-recuperacion-de-acceso.md)  
> **Total Verificado:** 1.711 pruebas unitarias en verde (0 fallos, 0 omitidas)

---

## 1. Contexto y Objetivos

Al auditar la Fase D del backlog de cuenta sobre la vinculación social de identidades heredada de INC-46 e INC-49, se identificaron áreas de mejora operativas y de experiencia de usuario:
1. **Garantía multi-proveedor ➔ Misma cuenta sin duplicados:** Validar formalmente de extremo a extremo que un usuario que inicia sesión con Google, vincula su cuenta de Discord y posteriormente entra con Discord accede a la misma cuenta, manteniendo íntegros sus datos y evitando la duplicación de usuarios.
2. **Exposición de datos de proveedor:** `ExternalLogin` ya calculaba `ProviderEmail` y `ProviderEmailVerifiedAt`, pero `AccountConnectionDto` no los proyectaba en su contrato, impidiendo al usuario saber qué dirección de correo respalda cada servicio vinculado.
3. **Guarda preventiva de desvinculación en UI:** En `AccountConnections.razor`, el botón «Desvincular» permanecía habilitado aunque el usuario solo contara con un único método de acceso (`links.Count <= 1`), desembocando en una llamada innecesaria al servidor que lanzaba `LastAccessMethodException`.

---

## 2. Unidades de Trabajo Implementadas

### Unidad 1: Enriquecimiento de DTO y Servicio de Conexiones (`6e5b2d4`)
- Ampliado `AccountConnectionDto` con `string? ProviderEmail` y `DateTimeOffset? ProviderEmailVerifiedAt`.
- Mapeo de dichos campos en `AccountConnectionsService.BuildViewAsync` a partir de la entidad `ExternalLogin`.
- Pruebas unitarias de contrato y servicio actualizadas.

### Unidad 2: Interfaz Editorial y Guarda Preventiva (`c8db172`)
- Actualizado `AccountConnections.razor` para renderizar el correo asociado al proveedor y su fecha de verificación.
- Deshabilitación preventiva del botón «Desvincular» cuando `links.Count <= 1` mediante badge `Único método`, `aria-disabled="true"` y cursor no permitido, respetando accesibilidad WCAG 2.2 AA.
- Pruebas web actualizadas en `AccountConnectionsPageContractTests.cs`.

### Unidad 3: Suite de Ciclo de Vida Multi-Proveedor y Colisiones (`7ba4227`)
- Formalizada la suite `MultiProviderLifecycleTests.cs`:
  - Alta Google ➔ Vincular Discord ➔ Entrada con Discord resuelve al mismo `AppUser` sin duplicados.
  - Alta Discord ➔ Vincular Google ➔ Desvincular Discord ➔ Acceso preservado mediante Google.
  - Intento de vincular un proveedor ya asignado a otra cuenta ➔ Rechazo por colisión (`ExternalLoginCollisionException`) e invariancia de datos.
  - Re-vinculación idempotente con el mismo proveedor.

---

## 3. Criterios de Aceptación y Verificación

1. Multi-proveedor unificado: acceder con cualquiera de las identidades externas vinculadas conduce siempre a la misma cuenta.
2. Prevención estricta de cuentas duplicadas por colisión de proveedor social.
3. Imposibilidad de desvincular el único método de acceso remanente, con defensa en UI y guarda en backend.
4. Transparencia total en pantalla del correo de cada proveedor externo.
5. Suite completa de 1.711 pruebas unitarias al 100% en verde.
