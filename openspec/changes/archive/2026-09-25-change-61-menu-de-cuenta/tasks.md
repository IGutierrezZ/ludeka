# Plan de Tareas SDD / ODD — INC-61: Menú de Cuenta y Estado de Sesión en la Cabecera

- [x] **ODD-1 — Componente de Menú de Cuenta (`AccountMenu.razor`)**
  - [x] 1.1 Crear `src/Ludeka.Web/Components/Shared/AccountMenu.razor` soportando estados autenticado e invitado.
  - [x] 1.2 Implementar cabecera de usuario con avatar de inicial, nombre y rol de moderación/fundador.
  - [x] 1.3 Incorporar los 6 destinos canónicos: Perfil Público (`/u/{UserId}`), Hub de Cuenta (`/cuenta`), Ludoteca (`/cuenta/ludoteca`), Preferencias (`/cuenta/ludoteca?seccion=apariencia`), Conexiones (`/cuenta/conexiones`) y Salir (`/logout`).
  - [x] 1.4 Integrar comprobación de correo verificado con `IAccountConnectionsService` para avisos visuales.

- [x] **ODD-2 — Integración en `MainLayout.razor`**
  - [x] 2.1 Reemplazar el bloque plano `HasSession` de `MainLayout.razor` por el componente `<AccountMenu />`.
  - [x] 2.2 Verificar armonía visual y responsiva con el selector de país, el menú de moderación y la barra móvil de INC-60.

- [x] **ODD-3 — Accesibilidad y Teclado**
  - [x] 3.1 Añadir gestión de tecla `Escape` y cierre en cambio de ruta (`LocationChanged`).
  - [x] 3.2 Asegurar atributos ARIA (`aria-expanded`, `aria-haspopup`, `role="menu"`, `role="menuitem"`).

- [x] **ODD-4 — Pruebas de Contrato y No-Regresión**
  - [x] 4.1 Crear `tests/Ludeka.UnitTests/Web/AccountMenuContractTests.cs` validando renderizado para autenticado/invitado, enlaces y accesibilidad.
  - [x] 4.2 Ejecutar `dotnet test Ludeka.sln` y verificar 100% en verde sin regresiones.

- [ ] **ODD-5 — Especificación Viva, SDD y PR**
  - [x] 5.1 Actualizar especificación viva (`docs/specs/sistema/32-autenticacion-y-autorizacion.md`, `37-area-de-cuenta-y-puerta-de-acceso.md` y `README.md`).
  - [x] 5.2 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
  - [ ] 5.3 Abrir Pull Request con `scripts/sdd-worktree.ps1 pr menu-de-cuenta`.
