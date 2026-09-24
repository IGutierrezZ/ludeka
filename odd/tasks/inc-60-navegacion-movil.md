# Documento Vivo ODD — INC-60: Navegación Móvil — Barra Inferior y Safe-Area

> **Feature:** `navegacion-movil`  
> **Fichero:** `odd/tasks/inc-60-navegacion-movil.md` (fuente de verdad operativa)  
> **Cambio SDD de origen:** `openspec/changes/change-60-navegacion-movil/`  
> **Rama:** `inc/navegacion-movil`  
> **Worktree:** `C:\repos\ludeka-wt\navegacion-movil`  
> **Creado:** 2026-09-24 · **Ruta:** rama `inc/navegacion-movil` → PR a `main`  

---

## 1. Objetivo

Dotar a Ludeka de una experiencia nativa y ergonómica en dispositivos móviles (**Mobile-First Radical**), implementando una barra de navegación inferior fija al alcance del pulgar, soporte estricto de áreas seguras (`safe-area-inset`) para terminales con barra gestual o notch, y acceso directo a las acciones de colección lúdica, garantizando el cumplimiento de WCAG 2.2 AA y preservando la estética editorial sobria de la plataforma.

---

## 2. Problema y Diagnóstico Previo (Auditoría Empírica)

Tras auditar exhaustivamente la maquetación de `MainLayout.razor`, `App.razor`, `input.css` y las vistas móviles:

1. **Ausencia total de navegación inferior móvil:**  
   Búsquedas de `BottomNav`, `bottom-nav`, `mobile-nav` y `safe-area` arrojaron 0 coincidencias en todo el código fuente. Toda la navegación móvil depende actualmente del menú desplegable `<details>` de la cabecera, obligando al usuario a desplazar la vista al tope y forzar el agarre con una sola mano.
2. **Colisión con barras de inicio y gestos del SO:**  
   `App.razor` define `<meta name="viewport" content="width=device-width, initial-scale=1.0" />` omitiendo `viewport-fit=cover`. Esto inhabilita el cálculo nativo de `env(safe-area-inset-bottom)` en Safari iOS y WebViews modernas, causando solapamiento de controles con el home indicator.
3. **Peligro de solape y CLS por barras fijas no compensadas:**  
   La introducción de un contenedor fijo inferior sin la debida reserva de margen de scroll (`padding-bottom` en el contenedor principal) taparía el pie de página (`footer`), avisos del sistema (`#blazor-error-ui`) o los últimos elementos de listas extensas.
4. **Acciones lúdicas desconectadas en el scroll de la ficha:**  
   En la ficha de juego (`GameDetail.razor`), la barra `CollectionActionBar.razor` se ubica bajo el hero. Al consultar detalles, reglas, expansiones o multimedia, el usuario pierde el acceso a sus estados de colección (`En mi ludoteca`, `Jugado`, `Comprar`).

---

## 3. Alcance Autorizado

### Dentro de Alcance:
- **Cimientos de Maquetación y Safe-Area:**
  - Actualizar el meta viewport en `App.razor` incorporando `viewport-fit=cover`.
  - Definir variables CSS y utilidades para `safe-area-inset-bottom` y espacio de reserva en `input.css`.
  - Ajustar `#blazor-error-ui` en `MainLayout.razor.css` para respetar la barra inferior en móvil.
- **Componente de Barra Móvil (`MobileBottomNav.razor`):**
  - Implementar componente con anclaje inferior fijo (`fixed bottom-0 inset-x-0 z-40 lg:hidden`).
  - Configurar 5 destinos: Inicio (`/`), Catálogo (`/catalogo`), Ludoteca (`/cuenta/ludoteca`), Sorteos (`/sorteos`) y Cuenta/Acceso (`/cuenta` / `/login`).
  - Integrar detección reactiva de ruta activa vía `NavigationManager` y atributos ARIA (`aria-current="page"`).
  - Asegurar accesibilidad (landmarks semánticos, tamaño táctil mínimo 48x48px, alto contraste, sin emojis).
- **Acciones de Colección al Alcance del Pulgar:**
  - Ajuste ergonómico en la ficha de juego (`GameDetail.razor`) y optimización de interacción móvil en `CollectionActionBar.razor`.
- **Pruebas y Verificación:**
  - Crear batería de pruebas unitarias y de contrato en `tests/Ludeka.UnitTests/Web/MobileNavigationContractTests.cs`.
  - Verificación del 100% de la suite con `dotnet test Ludeka.sln` (>1.687 pruebas superadas).

### Fuera de Alcance:
- Gestos táctiles complejos (swipe entre pestañas o pull-to-refresh).
- Empaquetado nativo (Capacitor o .NET MAUI).
- Modificación de la barra de navegación de escritorio en pantallas mayores o iguales a 1024px (`lg:`).

---

## 4. Decisiones de Arquitectura y Diseño

| ID | Decisión | Fundamento Técnico |
|---|---|---|
| **D-01** | `viewport-fit=cover` en `App.razor` | Habilita en WebKit / Blink el cálculo preciso de variables `env(safe-area-inset-*)`, evitando que el home indicator del sistema operativo corte los botones táctiles inferiores. |
| **D-02** | Breakpoint de visibilidad en `lg:` (1024px) | La navegación de escritorio de Ludeka utiliza `hidden lg:flex`. La barra móvil debe operar en simetría estricta con `block lg:hidden`, sin solapamiento ni doble navegación en pantallas medianas/grandes. |
| **D-03** | 5 destinos de primer nivel con tokens Lucide | Cubren el 95% de la frecuencia de uso: Inicio (`house`), Catálogo (`dices`), Ludoteca (`library`), Sorteos (`gift`), Cuenta (`user`). Mantiene coherencia con `IconCatalog.cs`. |
| **D-04** | Padding inferior de reserva en el layout (`pb-[calc(4rem+env(safe-area-inset-bottom,0px))] lg:pb-0`) | Previene que el contenido con scroll o el footer queden ocultos tras la barra fija inferior. |
| **D-05** | Marcado semántico accesible WCAG 2.2 AA | Elemento `<nav aria-label="Navegación principal móvil">`, enlaces con `aria-current="page"` reactivo ante cambios en `NavigationManager`, etiquetas de texto compactas bajo el icono para no depender exclusivamente de la memoria iconográfica. |
| **D-06** | Fondo traslúcido editorial con `backdrop-blur` | Respeta la paleta editorial (`--bg-nav`, `--border-subtle`, `--brand-primary`), integrándose de forma limpia con los 4 temas activos (`charcoal`, `editorial`, `tabletop`, `wood`, `midnight`). |

---

## 5. Checklist de Tareas (IDs Estables)

- [x] **ODD-1 — Cimientos de Maquetación, Viewport y Safe-Area**
  - [x] 1.1 Configurar `viewport-fit=cover` en la etiqueta `<meta name="viewport">` de `App.razor`.
  - [x] 1.2 Definir variables CSS y clases utilitarias de soporte safe-area y espaciado de reserva inferior en `input.css`.
  - [x] 1.3 Adaptar `#blazor-error-ui` en `MainLayout.razor.css` para respetar la barra inferior en resoluciones móviles.
- [x] **ODD-2 — Componente de Barra de Navegación Móvil (`MobileBottomNav.razor`)**
  - [x] 2.1 Diseñar e implementar `src/Ludeka.Web/Components/Shared/MobileBottomNav.razor` (`fixed bottom-0 inset-x-0 z-40 lg:hidden`).
  - [x] 2.2 Configurar los 5 destinos principales con iconos Lucide (`house`, `dices`, `library`, `gift`, `user`) y microtextos.
  - [x] 2.3 Conectar la detección reactiva de ruta activa con `NavigationManager.LocationChanged` y emitir `aria-current="page"`.
  - [x] 2.4 Integrar `MobileBottomNav` dentro de `MainLayout.razor` asegurando el comportamiento adaptativo multi-tema.
- [x] **ODD-3 — Acciones de Colección al Alcance del Pulgar en Móvil**
  - [x] 3.1 Revisar y optimizar la ergonomía táctil en móvil de `CollectionActionBar.razor`.
  - [x] 3.2 Asegurar que en la ficha del juego (`GameDetail.razor`) las acciones de colección mantengan visibilidad y accesibilidad ergonómica.
- [x] **ODD-4 — Pruebas de Contrato y Verificación de No-Regresión**
  - [x] 4.1 Crear `tests/Ludeka.UnitTests/Web/MobileNavigationContractTests.cs` (verificación de marked landmark, safe-area, endpoints y accesibilidad).
  - [x] 4.2 Ejecutar suite completa `dotnet test Ludeka.sln` y validar el 100% en verde (1.695 pruebas superadas).
- [x] **ODD-5 — Especificación Viva, SDD y PR**
  - [x] 5.1 Actualizar especificaciones vivas en `docs/specs/sistema/` (`38-navegacion-movil-y-safe-area.md` y `README.md`).
  - [x] 5.2 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
  - [x] 5.3 Preparar PR hacia `main` (`scripts/sdd-worktree.ps1 pr navegacion-movil`).

---

## 6. Verificación Final y Resultados

- **Línea Base Inicial:** 1.677 pruebas unitarias + 10 de integración en verde (1.687 en total).
- **Pruebas Unitarias Finales:** 1.685 superadas (0 fallos).
- **Pruebas de Integración Finales:** 10 superadas (0 fallos).
- **Total Automatizado:** 1.695 pruebas superadas al 100%.
