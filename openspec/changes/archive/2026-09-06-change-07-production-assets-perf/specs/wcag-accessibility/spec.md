# Especificación: wcag-accessibility (Auditoría y Accesibilidad Universal WCAG 2.2 Nivel AA)

## 1. Propósito y Contexto
Esta especificación define los requerimientos para que la plataforma Ludeka cumpla de forma integral con las pautas de accesibilidad **WCAG 2.2 Nivel AA**. Esto incluye la correcta identificación del idioma por lectores de pantalla, la navegación ágil por teclado mediante atajos de salto, la semántica ARIA estricta en modales y pestañas, y la accesibilidad de todos los controles de formulario y botones de acción.

---

## 2. Requerimientos de Comportamiento (RFC 2119)

### REQ-A11Y-01: Idioma del Documento (WCAG 3.1.1)
- En `src/Ludeka.Web/Components/App.razor`, el elemento raíz `<html>` **DEBE** definir el atributo:
  `<html lang="es">`.

### REQ-A11Y-02: Enlace de Salto al Contenido Principal (WCAG 2.4.1)
- En `src/Ludeka.Web/Components/Layout/MainLayout.razor`, el documento **DEBE** incluir un enlace accesible como primer elemento enfocable:
  ```html
  <a href="#main-content" class="skip-link sr-only focus:not-sr-only focus:fixed focus:top-4 focus:left-4 focus:z-50 focus:px-4 focus:py-2 focus:bg-orange-600 focus:text-white focus:rounded-lg focus:shadow-xl focus:outline-none">
      Saltar al contenido principal
  </a>
  ```
- La etiqueta `<main>` **DEBE** contar con el atributo `id="main-content"` y `tabindex="-1"`.

### REQ-A11Y-03: Diálogos y Modales Accesibles (WCAG 4.1.2)
- Todos los componentes modales del sistema:
  - `MediaEmbedModal.razor`
  - `SocialCardModal.razor`
  - `LoanModal.razor`
  - `FoundingVerdictModal.razor`
  - `ReviewBottomSheet.razor`
  - Modal de sorteo en `Radar.razor`
- **DEBEN** implementar:
  1. Contenedor con `role="dialog"`, `aria-modal="true"` y `aria-labelledby="[id-del-titulo]"`.
  2. Identificador único en el título del diálogo asociado a `aria-labelledby`.
  3. Botón de cierre con `aria-label="Cerrar modal"` (o texto equivalente en español).

### REQ-A11Y-04: Pestañas Semánticas ARIA (WCAG 1.3.1, 4.1.2)
- En los selectores de pestañas (`MultimediaHub.razor`, `Radar.razor`, `MyLibrary.razor`):
  - El contenedor **DEBE** tener `role="tablist"` y un `aria-label` descriptivo (ej. `aria-label="Formatos multimedia"`).
  - Cada botón de pestaña **DEBE** contar con `role="tab"`, `aria-selected="true/false"` y `aria-controls="[id-del-panel]"`.
  - El contenedor de contenido activo **DEBE** tener `role="tabpanel"`.

### REQ-A11Y-05: Controles de Formulario e Iconos Interactivos (WCAG 1.1.1, 1.3.1)
- Todo elemento `<input>`, `<textarea>` y `<select>` **DEBE** tener un `<label for="...">` asociado o un atributo `aria-label` explícito.
- Los botones que solo contienen iconos (ej. alternar rol en `MainLayout`, cerrar modales, lupa de búsqueda) **DEBEN** contar con `aria-label` descriptivo en español.
- En `ScalabilityTrafficLight.razor`, cada chip de semáforo **DEBE** tener `role="img"` o `role="status"` y su correspondiente `aria-label`.
- Los iconos decorativos SVG **DEBEN** tener `aria-hidden="true"`.

### REQ-A11Y-06: Localización Estricta de Cadenas de Error (Regla de Idioma Español)
- En `MainLayout.razor`, el bloque `#blazor-error-ui` **DEBE** estar traducido al español:
  "Ha ocurrido un error inesperado. Recargar".

---

## 3. Criterios de Aceptación (Escenarios Gherkin)

### Escenario 1: Navegación por teclado con Skip Link
```gherkin
Given un usuario que accede a cualquier página de Ludeka utilizando solo el teclado
When presiona la tecla Tab por primera vez
Then el foco se posiciona en el enlace "Saltar al contenido principal"
And el enlace se hace visible en pantalla
When el usuario pulsa Enter
Then el foco se traslada al contenedor <main id="main-content">
```

### Escenario 2: Accesibilidad en modal de vídeo
```gherkin
Given un usuario que abre el modal de reproducción de vídeo en MultimediaHub
When el diálogo se renderiza en el DOM
Then el contenedor principal cuenta con role="dialog" y aria-modal="true"
And está vinculado mediante aria-labelledby al título del vídeo
And el botón de cerrar contiene aria-label="Cerrar modal"
```

### Escenario 3: Pestañas con semántica para lectores de pantalla
```gherkin
Given la sección Radar de Sorteos en /radar
When se inspecciona el grupo de pestañas
Then el contenedor tiene role="tablist"
And la pestaña activa tiene role="tab" y aria-selected="true"
And la pestaña inactiva tiene role="tab" y aria-selected="false"
```
