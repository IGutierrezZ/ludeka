# Documento Vivo ODD — INC-61: Menú de Cuenta y Estado de Sesión en la Cabecera

> **Feature:** `menu-de-cuenta`  
> **Fichero:** `odd/tasks/inc-61-menu-de-cuenta.md` (fuente de verdad operativa)  
> **Cambio SDD de origen:** `openspec/changes/change-61-menu-de-cuenta/`  
> **Rama:** `inc/menu-de-cuenta`  
> **Worktree:** `C:\repos\ludeka-wt\menu-de-cuenta`  
> **Creado:** 2026-09-24 · **Ruta:** rama `inc/menu-de-cuenta` → PR a `main`  

---

## 1. Objetivo

Implementar un menú desplegable de cuenta y estado de sesión en la cabecera (`AccountMenu.razor`), que reconozca visualmente al usuario autenticado (avatar con inicial o icono, nombre legible y chevron) o proporcione una puerta clara de acceso para invitados (`/login`), ofreciendo accesos directos al perfil público (`/u/{UserId}`), hub de cuenta (`/cuenta`), ludoteca personal (`/cuenta/ludoteca`), preferencias visuales (`/cuenta/ludoteca?seccion=apariencia`), conexiones de proveedores (`/cuenta/conexiones`) y salida segura (`/logout`), cumpliendo con los estándares de accesibilidad WCAG 2.2 AA y la estética editorial sobria de Ludeka.

---

## 2. Problema y Diagnóstico Previo (Auditoría Empírica)

1. **Ausencia de menú desplegable de cuenta:**  
   En `MainLayout.razor`, la cabecera únicamente renderiza un enlace simple `<a>` a `/cuenta` si hay sesión o a `/login` si no la hay. No existe desplegable ni opciones directas.
2. **Imposibilidad de cerrar sesión desde la cabecera:**  
   El endpoint `/logout` existe en el backend (`Program.cs:336`), pero ningún control de la interfaz de cabecera lo expone de forma directa al usuario identificado.
3. **Destinos de usuario dispersos y desarticulados:**  
   Para llegar a `PublicProfile` (`/u/{UserId}`), a la gestión de conexiones OAuth (`/cuenta/conexiones`) o al cambio de apariencia, el usuario debe memorizar URLs o realizar múltiples pasos desde el hub.
4. **Composición con `AccountEmailNotice` y navegación móvil:**  
   `AccountEmailNotice` actúa como aviso descartable superior bajo la cabecera; el nuevo menú debe convivir armónicamente con él e integrar una señal discreta de advertencia si el correo no está verificado. Asimismo, debe complementar la barra inferior móvil (`MobileBottomNav` de INC-60) sin redundancias conflictivas.

---

## 3. Alcance Autorizado

### Dentro de Alcance:
- **Componente `AccountMenu.razor`:**
  - Control de identidad en cabecera: botón interactivo con iniciales / icono de usuario, nombre de usuario y flecha desplegable (`chevron`).
  - Estado invitado: enlace visible a `/login` ("Entrar").
  - Menú desplegable flotante con diseño editorial sobrio:
    - Cabecera con datos del usuario e insignia de rol (Fundador / Moderador si aplica).
    - Enlace al Perfil Público (`/u/{UserId}`).
    - Enlace al Área de Cuenta / Hub central (`/cuenta`).
    - Enlace a Mi Ludoteca (`/cuenta/ludoteca`).
    - Enlace a Preferencias de Apariencia (`/cuenta/ludoteca?seccion=apariencia`).
    - Enlace a Conexiones (`/cuenta/conexiones`), con indicador sutil si el correo está pendiente de verificación.
    - Botón / enlace de Cierre de Sesión (`/logout`).
- **Accesibilidad y Comportamiento:**
  - Soporte de teclado (Escape para cerrar, tabulación accesible, `aria-haspopup="menu"`, `aria-expanded`).
  - Cierre automático al cambiar de ruta (`NavigationManager.LocationChanged`) o al hacer clic en el telón de fondo.
- **Integración y Pruebas:**
  - Sustitución del enlace plano anterior en `MainLayout.razor` por `<AccountMenu />`.
  - Pruebas de contrato y accesibilidad en `tests/Ludeka.UnitTests/Web/AccountMenuContractTests.cs`.
  - Mantenimiento del 100% de la suite de pruebas en verde (`dotnet test Ludeka.sln`).

### Fuera de Alcance:
- Rediseño del hub de cuenta (`/cuenta`, INC-50).
- Modificaciones en los flujos OAuth o endpoints de autenticación (`/login/external`, `/logout`).
- Pantalla dedicada de preferencias de INC-62 (se enlaza a la sección existente).

---

## 4. Decisiones de Arquitectura y Diseño

| ID | Decisión | Fundamento Técnico |
|---|---|---|
| **D-01** | Componente aislado `AccountMenu.razor` | Evita inflar `MainLayout.razor`, encapsula el estado local del desplegable (`_isOpen`) y permite pruebas unitarias focalizadas. |
| **D-02** | Inicial de usuario + Icono editorial | Un avatar sobrio con la inicial de `UserName` (o icono `user` si está vacío) aporta identidad personalizada inmediata sin requerir almacenamiento externo de fotos de perfil. |
| **D-03** | Telón de fondo (*backdrop*) para cierre al clic externo | Mismo patrón probado en el menú de gestión de moderación (`MainLayout.razor:139`), fiable en Blazor SSR e interactivo sin scripts JS invasivos. |
| **D-04** | Cerrar sesión con enlace HTTP `/logout` | Al ser un endpoint HTTP mapeado en ASP.NET Core (`Program.cs:336`), revoca la cookie de autenticación de forma segura y devuelve `Results.Redirect("/")`, reiniciando el circuito anónimo sin estado corrupto. |
| **D-05** | Indicador de correo no verificado en menú | Si `IAccountConnectionsService.HasVerifiedProviderEmailAsync()` es falso, se muestra un punto ámbar de aviso en el botón del menú y junto al enlace de «Conexiones», reforzando a `AccountEmailNotice` sin duplicar el banner. |
| **D-06** | Cumplimiento WCAG 2.2 AA | `aria-haspopup="menu"`, `aria-expanded`, nombres accesibles, contraste suficiente de colores y cierre con la tecla `Escape`. |

---

## 5. Checklist de Tareas (IDs Estables)

- [x] **ODD-1 — Componente de Menú de Cuenta (`AccountMenu.razor`)**
  - [x] 1.1 Crear `src/Ludeka.Web/Components/Shared/AccountMenu.razor` con soporte para estados autenticado e invitado.
  - [x] 1.2 Implementar cabecera de usuario con iniciales/icono, nombre y rol de moderación/fundador.
  - [x] 1.3 Incorporar los 6 destinos canónicos: Perfil Público, Hub de Cuenta, Ludoteca, Preferencias, Conexiones y Salir (`/logout`).
  - [x] 1.4 Integrar comprobación de correo verificado con `IAccountConnectionsService` para avisos sutiles.
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

---

## 6. Verificación Final y Resultados

- **Línea Base Inicial:** 1.685 pruebas unitarias + 10 de integración en verde (1.695 en total).
- **Pruebas Unitarias Finales:** 1.691 superadas (0 fallos).
- **Pruebas de Integración Finales:** 10 superadas (0 fallos).
- **Total Automatizado:** 1.701 pruebas superadas al 100% en verde.
