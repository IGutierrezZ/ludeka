# Documento Vivo ODD — INC-63: Conexiones OAuth (Múltiples Proveedores sin Cuentas Duplicadas)

> **Feature:** `conexiones-oauth`  
> **Fichero:** `odd/tasks/inc-63-conexiones-oauth.md` (fuente de verdad operativa)  
> **Incremento:** INC-63  
> **Rama:** `inc/conexiones-oauth`  
> **Worktree:** `C:\repos\ludeka-wt\conexiones-oauth`  
> **Creado:** 2026-09-25 · **Ruta:** rama `inc/conexiones-oauth` → PR a `main`  
> **TDD Mode:** Strict TDD (RED ➔ GREEN ➔ REFACTOR)  
> **Línea Base:** 1.703 pruebas unitarias en verde  

---

## 1. Objetivo

Garantizar de forma verificable la vinculación multi-proveedor a una misma cuenta de usuario (por ejemplo, acceder con Google, vincular Discord, cerrar sesión y poder entrar indistintamente con Google o Discord llegando exactamente a la misma cuenta sin duplicar registros), consolidar la visibilidad de datos del proveedor (`ProviderEmail` y `ProviderEmailVerifiedAt`) en la pantalla de conexiones de cuenta, deshabilitar la acción de desvincular cuando solo existe un único método de acceso, y blindar con pruebas end-to-end los escenarios de colisión y resolución multi-proveedor.

---

## 2. Problema y Diagnóstico Previo (Auditoría Empírica)

1. **Garantía Multi-proveedor ➔ Misma Cuenta no probada en ciclo de vida completo:**  
   Aunque `ExternalLoginService.ResolveAsync` y `LinkAsync` fueron implementados en INC-46 e INC-49, no existía una prueba de integración/ciclo de vida que validase la secuencia real solicitada por el mantenedor:
   *Alta con Google ➔ Vincular Discord ➔ Salir ➔ Entrar con Discord ➔ Mismos datos y misma cuenta sin duplicados*.
2. **`ProviderEmailVerifiedAt` y `ProviderEmail` huérfanos de visualización:**  
   `ExternalLogin` almacena `ProviderEmail` y calcula `ProviderEmailVerifiedAt = providerEmailVerified && ProviderEmail is not null ? LinkedAt : null;`. Sin embargo, `AccountConnectionDto` solo exponía un booleano `ProviderEmailVerified`, ocultando el correo concreto asociado al proveedor y la fecha de verificación, impidiendo al usuario saber qué cuenta social tiene enlazada.
3. **Botón de desvinculación activo incluso con un solo método:**  
   En `AccountConnections.razor`, el botón `Desvincular` permanece habilitado aunque `_view.CanUnlink == false` (`links.Count <= 1`). Al pulsarlo, el backend lanza `LastAccessMethodException` y muestra el banner de error. La experiencia de usuario mejora si el botón se deshabilita preventivamente cuando solo hay un proveedor, con un mensaje claro que indique que no se puede desvincular el único acceso.
4. **Alcanzabilidad de la pantalla de conexiones:**  
   Auditada y verificada: INC-61 ya incorporó `/cuenta/conexiones` en el menú de cabecera (`AccountMenu.razor`), e INC-62 en `AccountSectionNav.razor`. La pantalla es plenamente accesible para cualquier usuario autenticado.

---

## 3. Alcance Autorizado

### Dentro de Alcance:
- **Ampliación de `AccountConnectionDto`:**
  - Incluir `string? ProviderEmail` y `DateTimeOffset? ProviderEmailVerifiedAt`.
  - Actualizar `AccountConnectionsService.BuildViewAsync` para suministrar estos campos desde la entidad `ExternalLogin`.
- **Mejoras de UI en `AccountConnections.razor`:**
  - Mostrar el correo asociado al proveedor (`ProviderEmail`) junto al estado de vinculación.
  - Mostrar la fecha de vinculación o verificación cuando esté disponible.
  - Deshabilitar el botón `Desvincular` cuando `!_view.CanUnlink` (`links.Count <= 1`), mostrando aviso/badge explicativo accesible.
- **Suite de Pruebas de Ciclo de Vida Multi-Proveedor y Colisiones:**
  - `tests/Ludeka.UnitTests/Application/MultiProviderLifecycleTests.cs`:
    - Alta Google ➔ Vincular Discord ➔ Re-login Discord ➔ Misma cuenta sin duplicados.
    - Intentar vincular cuenta ya ligada a otro usuario ➔ Rechazo sin alteración de datos.
    - Desvinculación de método secundario y persistencia de acceso con el principal.
  - Pruebas web de visualización en `AccountConnectionsPageContractTests.cs`.
- **Mantenimiento del 100% de la suite en verde (1.703+ unitarias).**

### Fuera de Alcance:
- Nuevos proveedores OAuth no previstos (GitHub, Apple, etc.): se consolidan los tres configurados (Google, Discord, Facebook).
- Sistema de contraseñas o Magic Links (objeto de INC-64).

---

## 4. Decisiones de Arquitectura y Diseño

| ID | Decisión | Fundamento Técnico |
|---|---|---|
| **D-01** | Consolidar Google, Discord y Facebook | Cumple exactamente el requerimiento de negocio sin introducir dependencias externas ni credenciales no provisionadas. |
| **D-02** | Exponer `ProviderEmail` y `ProviderEmailVerifiedAt` en DTO | Permite transparencia al usuario sobre qué dirección de correo está respaldando cada acceso social vinculado. |
| **D-03** | Deshabilitar botón «Desvincular» si `links.Count <= 1` | Previene frustración y llamadas innecesarias al servidor, manteniendo la guarda defensiva en `ExternalLoginService.UnlinkAsync` intacta ante cualquier intento directo. |
| **D-04** | Suite unificada `MultiProviderLifecycleTests` | Formaliza la garantía de no duplicación de cuentas entre múltiples proveedores bajo SQLite en memoria idéntico a producción. |

---

## 5. Checklist de Tareas (IDs Estables)

- [x] **ODD-1 — Enriquecimiento de `AccountConnectionDto` y `AccountConnectionsService`**
  - [x] 1.1 Añadir `ProviderEmail` y `ProviderEmailVerifiedAt` a `AccountConnectionDto`.
  - [x] 1.2 Mapear campos desde `ExternalLogin` en `AccountConnectionsService.BuildViewAsync`.
  - [x] 1.3 Actualizar pruebas existentes de `AccountConnectionsServiceTests`.
- [x] **ODD-2 — UI Editorial en `AccountConnections.razor` (Datos y Guarda Preventiva)**
  - [x] 2.1 Mostrar correo del proveedor y etiqueta de verificación si está disponible.
  - [x] 2.2 Deshabilitar botón `Desvincular` cuando `!_view.CanUnlink`, mostrando indicativo «Único método».
  - [x] 2.3 Mantener atributos de accesibilidad WCAG 2.2 AA (`aria-disabled`, `title`).
- [x] **ODD-3 — Suite de Ciclo de Vida Multi-Proveedor (`MultiProviderLifecycleTests`)**
  - [x] 3.1 Test: Alta Google ➔ Vincular Discord ➔ ResolveAsync con Discord devuelve mismo `AppUser`.
  - [x] 3.2 Test: Alta Discord ➔ Vincular Google ➔ Desvincular Discord ➔ ResolveAsync Google mantiene acceso.
  - [x] 3.3 Test: Prevención de duplicados cuando un proveedor ya está en uso por otra cuenta.
  - [x] 3.4 Test: Intentos de re-vinculación idempotentes.
- [x] **ODD-4 — Pruebas de Contrato y Regresión en Web**
  - [x] 4.1 Actualizar `AccountConnectionsPageContractTests` con los nuevos datos visuales y botón deshabilitado.
  - [x] 4.2 Verificar que el manejo de errores de colisión y última cuenta sigue intacto.
- [ ] **ODD-5 — Sincronización Documental y Verificación Final**
  - [ ] 5.1 Suite completa de pruebas unitarias en verde (`dotnet test`).
  - [ ] 5.2 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md` con INC-63 en progreso.

---

## 6. Registro de Commits por Unidad de Trabajo

*(Se completará conforme se implemente cada tarea)*
