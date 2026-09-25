# Documento Vivo ODD — INC-64: Acceso por Correo con Verificación (Evaluar e Implantar si se Aprueba)

> **Feature:** `acceso-por-correo`  
> **Fichero:** `odd/tasks/inc-64-acceso-por-correo.md` (fuente de verdad operativa)  
> **Incremento:** INC-64  
> **Rama:** `inc/acceso-por-correo`  
> **Worktree:** `C:\repos\ludeka-wt\acceso-por-correo`  
> **Creado:** 2026-09-25 · **Ruta:** rama `inc/acceso-por-correo` → PR a `main`  
> **TDD Mode:** Strict TDD (RED ➔ GREEN ➔ REFACTOR)  
> **Línea Base:** 1.711 pruebas unitarias en verde  

---

## 1. Objetivo

Evaluar e implantar el acceso por correo mediante **Magic Link** (enlace de un solo uso con verificación implícita de buzón) en Ludeka, cerrando la contradicción de interfaz en `Login.razor:15` («sin registro, sin contraseña y sin correo de confirmación»), garantizando que ninguna combinación de estado deje al usuario sin vía de acceso y fijando una frontera limpia con las identidades OAuth consolidadas en INC-46, INC-49 e INC-63.

---

## 2. Problema y Diagnóstico Previo (Auditoría Empírica)

1. **Contradicción en la pantalla de acceso:**  
   `Login.razor:15` prometía textualmente: *«sin registro, sin contraseña y sin correo de confirmación»*. Hoy esto solo era cierto por omisión, dado que el único mecanismo habilitado era OAuth social externo (Google, Discord, Facebook).
2. **Cero infraestructura previa de correo transaccional:**  
   No existía ningún servicio de envío de email (`IEmailSender` o SMTP) en toda la solución. El outbox persistente de INC-47 (`NotificationOutbox`) únicamente atiende canales de comunidad (Discord y Telegram).
3. **Decisión del mantenedor (Fase 1 completada):**  
   Tras la valoración de alternativas (Magic Link vs Contraseña vs Descarte), el mantenedor aprobó expresamente la adopción del **Magic Link**:
   - Acceso sin contraseñas (cero almacenamiento de credenciales vulnerables o reseteos).
   - Verificación implícita e inmediata del buzón al hacer clic.
   - Enlace criptográfico de alta entropía (32 bytes), hash SHA-256 en base de datos, caducidad estricta (15 min) y consumo atómico de un solo uso.
   - Envío simulado en desarrollo y tests con `IEmailSender`, preparado para proveedor HTTP transaccional (Resend/Brevo) en producción (Cloud Run bloquea puerto 25).

---

## 3. Alcance

### Dentro de Alcance:
- **ODD-1 — Puerta de Decisión Arquitectónica:** Completada y aprobada (Magic Link).
- **ODD-2 — Dominio y Contratos de Magic Link (`Ludeka.Core` & `Ludeka.Application`):**
  - Entidad `MagicLinkToken` con invariantes de expiración, consumo atómico e integridad de correo.
  - Abstracciones `IMagicLinkTokenRepository`, `IEmailSender` e `IMagicLinkService`.
- **ODD-3 — Persistencia e Infraestructura (`Ludeka.Infrastructure`):**
  - `SqliteMagicLinkTokenRepository` y mapeo en `LudekaDbContext`.
  - Migración SQLite idempotente en `SqliteSchemaMigrator` y migración PostgreSQL EF Core.
  - `DevelopmentEmailSender` con trazabilidad limpia para desarrollo y pruebas.
- **ODD-4 — Servicios de Aplicación y Flujo de Autenticación (`Ludeka.Application` & `Ludeka.Web`):**
  - Implementación de `MagicLinkService`: generación de token seguro, hash SHA-256, expiración 15 min, resolución/creación de `AppUser` sin duplicación.
  - Endpoints en `Program.cs`:
    - `POST /login/magic-link/request`: recepción de email y emisión de enlace.
    - `GET /login/magic-link`: validación de token, consumo atómico, emisión de cookie de sesión (`ExternalLoginEvents.BuildSessionPrincipal`) y redirección a `returnUrl`.
- **ODD-5 — UI Editorial en `Login.razor` y Textos de Interfaz:**
  - Ajuste honesto del lema en cabecera: *«Accede al instante con tu cuenta social o con un enlace mágico a tu correo: sin contraseñas.»*
  - Formulario limpio y accesible (WCAG 2.2 AA) para introducir el correo y solicitar el enlace.
  - Estado de éxito amigable (*«Revisa tu correo...»*) y manejo de avisos en caso de enlace caducado o inválido.
- **ODD-6 — Verificación de pruebas (Strict TDD) y Entrega:**
  - Pruebas unitarias de dominio, repositorio, servicio, endpoints y renderizado Blazor.
  - Mantenimiento estricto del 100% de la suite en verde (1.711+ pruebas).
  - Sincronización de ROADMAP y documentación viva.

---

## 4. Checklist de Tareas (IDs Estables)

- [x] **ODD-1 — Puerta de decisión: Valoración técnica de acceso por correo**
  - [x] 1.1 Documentar valoración arquitectónica (Magic Link vs Contraseña vs Descarte).
  - [x] 1.2 Obtener decisión explícita del mantenedor (Aprobado: Magic Link).
  - [x] 1.3 Registrar decisión en Engram persistent memory.
- [x] **ODD-2 — Dominio y Contratos de Aplicación (`Ludeka.Core` y `Ludeka.Application`)**
  - [x] 2.1 Entidad `MagicLinkToken` en `Ludeka.Core.Entities`.
  - [x] 2.2 Interfaz `IMagicLinkTokenRepository` en `Ludeka.Application.Contracts`.
  - [x] 2.3 Interfaz `IEmailSender` y `IMagicLinkService` en `Ludeka.Application.Contracts`.
  - [x] 2.4 DTOs y opciones `MagicLinkOptions` en `Ludeka.Application.Features.Identity`.
  - [x] 2.5 Pruebas unitarias de dominio en `Ludeka.UnitTests/Domain/MagicLinkTokenTests.cs`.
- [x] **ODD-3 — Persistencia e Infraestructura (`Ludeka.Infrastructure`)**
  - [x] 3.1 Mapeo de `MagicLinkToken` en `LudekaDbContext`.
  - [x] 3.2 Repositorio `SqliteMagicLinkTokenRepository` / `MagicLinkTokenRepository`.
  - [x] 3.3 Reconciliador de esquema SQLite en `SqliteSchemaMigrator.cs` (bloque 29).
  - [x] 3.4 Migración EF Core para PostgreSQL en `20260925164853_AddMagicLinkTokens.cs`.
  - [x] 3.5 Implementación `DevelopmentEmailSender` en `Ludeka.Infrastructure`.
  - [x] 3.6 Pruebas de repositorio en `Ludeka.UnitTests/Infrastructure/MagicLinkTokenRepositoryTests.cs`.
  - [x] 3.7 Verificación de frescura de esquema de Supabase (`docs/database/supabase_schema.sql`).
- [x] **ODD-4 — Servicio de Aplicación e Integración de Identidad (`Ludeka.Application`)**
  - [x] 4.1 Implementar `MagicLinkService` (generación segura, hashing SHA-256, verificación, resolución/creación de `AppUser`).
  - [x] 4.2 Registro de servicios en DI (`AddLudekaApplicationCore` / `AddLudekaInfrastructure`).
  - [x] 4.3 Pruebas unitarias de servicio en `Ludeka.UnitTests/Application/MagicLinkServiceTests.cs`.
- [x] **ODD-5 — Endpoints HTTP y UI en `Login.razor` (`Ludeka.Web`)**
  - [x] 5.1 Endpoints `POST /login/magic-link/request` y `GET /login/magic-link` en `Program.cs`.
  - [x] 5.2 Emisión de cookie de sesión `ExternalAuthenticationSchemes.SessionCookieScheme`.
  - [x] 5.3 Actualización de `Login.razor` (formulario de email, estados de envío, soporte de avisos de enlace expirado/inválido).
  - [x] 5.4 Actualización de `AccountConnectionMessages` y códigos cerrados de `LoginRedirect`.
  - [x] 5.5 Pruebas de contrato web en `Ludeka.UnitTests/Web/LoginContractTests.cs` y `LoginRedirectTests.cs`.
- [x] **ODD-6 — Verificación Final y Sincronización Documental**
  - [x] 6.1 Suite completa de pruebas unitarias en verde (`dotnet test`: 1.756 superadas, 0 fallos).
  - [x] 6.2 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
  - [x] 6.3 Archivar documento de incremento a `docs/increments/archive/inc-64-acceso-por-correo.md`.
  - [x] 6.4 Volcado a la especificación viva `docs/specs/sistema/40-acceso-por-correo-magic-link.md` y `README.md`.
  - [x] 6.5 Cierre de memoria en Engram con `mem_session_summary`.

---

## 5. Registro de Commits por Unidad de Trabajo

1. `6d52373` — `docs(odd): iniciar inc-64 y registrar plan de tareas de acceso por correo`
2. `77121c8` — `docs(odd): detallar tareas ODD para acceso por correo con Magic Link tras aprobacion`
3. `c1139a5` — `feat(identity): add MagicLinkToken domain entity and application contracts`
4. `488ec44` — `feat(infrastructure): implement MagicLinkToken persistence, migrations, and DevelopmentEmailSender`
5. `5a9d728` — `feat(identity): implement MagicLinkService with secure token generation, verification, and tests`
6. `33f5a8b` — `feat(web): add magic link request and verify endpoints and interactive login UI`
