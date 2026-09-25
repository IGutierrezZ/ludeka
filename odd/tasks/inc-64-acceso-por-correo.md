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

Evaluar e implantar (según la puerta de decisión del mantenedor) el acceso por correo con verificación de buzón en Ludeka, cerrando la contradicción de interfaz en `Login.razor:15` («sin registro, sin contraseña y sin correo de confirmación»), garantizando que ninguna combinación de estado deje al usuario sin vía de acceso y fijando una frontera limpia con las identidades OAuth consolidadas en INC-46, INC-49 e INC-63.

---

## 2. Problema y Diagnóstico Previo (Auditoría Empírica)

1. **Contradicción en la pantalla de acceso:**  
   `Login.razor:15` promete textualmente: *«sin registro, sin contraseña y sin correo de confirmación»*. Hoy esto solo es cierto por omisión, dado que el único mecanismo habilitado es OAuth social externo (Google, Discord, Facebook).
2. **Cero infraestructura previa de correo transaccional:**  
   No existe ningún servicio de envío de email (`IEmailSender` o SMTP) en toda la solución. El outbox persistente de INC-47 (`NotificationOutbox`) únicamente atiende canales de comunidad (Discord y Telegram). Cualquier envío de correo transaccional requeriría definir abstracción, simulación en pruebas/desarrollo y proveedor en producción.
3. **Petición del mantenedor:**  
   > *«valorar incluir loguin con email y verificacion de email .»*  
   Existe una puerta de decisión previa obligatoria: valorar viabilidad, impacto y sobrecarga operativa antes de acometer la implementación.
4. **Bifurcaciones arquitectónicas si se aprueba:**  
   - **Magic Link (enlace de un solo uso por correo):** Sin contraseñas ni almacenamiento de hashes; requiere token efímero de alta entropía, consumo atómico, caducidad estricta e infraestructura de entrega.
   - **Contraseña con verificación:** Exige hash seguro (Argon2id/PBKDF2), almacenamiento de credenciales, flujo de verificación de buzón previo a activación y flujo de restablecimiento de contraseña (*password reset*).
   - **Descarte consciente:** Mantener 100% OAuth social, eliminando la frase contradictoria de `Login.razor` para reflejar con honestidad la realidad.

---

## 3. Alcance

### Dentro de Alcance:
- **ODD-1 — Puerta de Decisión Arquitectónica:** Presentar valoración técnica concisa (pros, contras, coste de mantenimiento y seguridad) al mantenedor para decidir: Aprobar (Magic Link vs Contraseña) o Descartar.
- **ODD-2 — Honestidad en UI (`Login.razor`):** Corregir la promesa textual para alinearla con la arquitectura real de autenticación.
- **ODD-3 — Implementación según Decisión:**
  - Si se aprueba: diseñar e implementar el flujo de acceso por correo y verificación de buzón de forma atómica y segura, vinculable a cuentas existentes sin duplicados.
  - Si se descarta: archivar formalmente por decisión documentada y blindar los textos sin introducir deuda técnica.
- **ODD-4 — Pruebas de Contrato y Regresión:** Asegurar que ningún estado deja al usuario huérfano y que la suite completa permanece 100% en verde.
- **ODD-5 — Sincronización Documental y Cierre:** Actualizar especificaciones vivas, roadmap y registro de memoria persistente en Engram.

---

## 4. Checklist de Tareas (IDs Estables)

- [ ] **ODD-1 — Puerta de decisión: Valoración técnica de acceso por correo**
  - [ ] 1.1 Documentar valoración arquitectónica (Magic Link vs Contraseña vs Descarte).
  - [ ] 1.2 Obtener decisión explícita del mantenedor.
- [ ] **ODD-2 — Corrección y honestidad en `Login.razor`**
  - [ ] 2.1 Ajustar microtexto de bienvenida y promesa de acceso.
  - [ ] 2.2 Pruebas de renderizado y aserciones de interfaz en `LoginContractTests`.
- [ ] **ODD-3 — Ejecución de la rama decidida**
  - [ ] 3.1 Implementar contratos de dominio y aplicación según decisión.
  - [ ] 3.2 Persistencia (migraciones SQLite y PostgreSQL si procede).
  - [ ] 3.3 Integración con `ExternalLoginService` / `AppUser` sin cuentas duplicadas.
- [ ] **ODD-4 — Verificación de pruebas (Strict TDD)**
  - [ ] 4.1 Pruebas unitarias de flujo y expiración de tokens / credenciales.
  - [ ] 4.2 Mantenimiento de las 1.711 pruebas de línea base.
- [ ] **ODD-5 — Sincronización documental y entrega**
  - [ ] 5.1 Actualizar `ROADMAP.md` y `ROADMAP_MVP_SLICES.md`.
  - [ ] 5.2 Volcar especificación viva en `docs/specs/sistema/`.
  - [ ] 5.3 Registrar resumen en Engram MCP.

---

## 5. Registro de Commits por Unidad de Trabajo

*(Pendiente de ejecución)*
