# Propuesta SDD — INC-60: Navegación Móvil — Barra Inferior y Safe-Area

## 1. Motivación y Contexto

La arquitectura y las reglas de diseño de Ludeka establecen un principio irrenunciable: **«Mobile-First Radical»** (`AGENTS.md` §3), donde la experiencia de uso en dispositivos móviles debe sentirse como una aplicación ágil, con navegación al alcance del pulgar y tiempos de respuesta instantáneos.

Sin embargo, tras auditar el sistema se ha comprobado una carencia crítica:
1. **Inexistencia de navegación inferior móvil:** No existe ningún componente `BottomNav`, `mobile-nav` ni barra fija inferior. La navegación depende exclusivamente de la cabecera superior y de un menú desplegable `<details>`, obligando al usuario a realizar desplazamientos innecesarios y estirar la mano hacia la parte superior de la pantalla.
2. **Falta de soporte de áreas seguras (`safe-area-inset`):** En dispositivos modernos con barra de inicio (home indicator en iOS y Android) o notch, los elementos inferiores colisionan con el sistema operativo por ausencia de `viewport-fit=cover` y `env(safe-area-inset-bottom)`.
3. **Acciones de colección alejadas del pulgar:** En la ficha de juego (`GameDetail.razor`), las acciones esenciales de interacción lúdica (`En mi ludoteca`, `Jugado`, `Comprar`, `Prestar`) quedan sepultadas tras el scroll inicial.

Este incremento construye la infraestructura completa de navegación móvil de Ludeka, respetando la estética editorial sobria (anti-slop, sin degradados genéricos) y las pautas WCAG 2.2 AA.

---

## 2. Alcance Propuesto

1. **Cimientos de Maquetación y Safe-Area:**
   - Adición de `viewport-fit=cover` al meta viewport en `App.razor`.
   - Variables CSS y clases de reserva de espacio inferior (`safe-area-inset-bottom` + altura de barra) en `input.css` y `MainLayout.razor`.
   - Adaptación de `#blazor-error-ui` para evitar solapes en resoluciones móviles.

2. **Componente Editorial `MobileBottomNav.razor`:**
   - Barra fija en la parte inferior (`fixed bottom-0 inset-x-0 z-40 lg:hidden`).
   - 5 destinos ergonómicos de primer nivel:
     - **Inicio** (`/` · icono `house`)
     - **Catálogo** (`/catalogo` · icono `dices`)
     - **Ludoteca** (`/cuenta/ludoteca` · icono `library`)
     - **Sorteos & Radar** (`/sorteos` · icono `gift`)
     - **Cuenta / Acceso** (`/cuenta` si autenticado, `/login` si invitado · icono `user`)
   - Detección activa de ruta y marcado accesible (`aria-current="page"`, landmark `<nav aria-label="Navegación principal móvil">`).
   - Transición visual adaptativa a los 4 temas del sistema (`charcoal`, `editorial`, `tabletop`, `wood`, `midnight`) mediante tokens CSS existentes.

3. **Acceso al Alcance del Pulgar para Colección en Ficha de Juego:**
   - Optimización ergonómica en `GameDetail.razor` para que las acciones de colección (`CollectionActionBar`) estén accesibles con el pulgar en móvil.

4. **Batería de Pruebas de Contrato y Regresión:**
   - Pruebas unitarias de accesibilidad, rutas y atributos semánticos en `MobileNavigationContractTests.cs`.
   - Verificación de no-regresión en la suite completa (`dotnet test Ludeka.sln`).

---

## 3. Criterios de Aceptación

1. En viewports móviles (<1024px, `lg:hidden`) se renderiza la barra inferior fija con los 5 destinos accesibles con el pulgar.
2. En viewports de escritorio (>=1024px) la barra inferior permanece completamente oculta (`lg:hidden`) y no genera márgenes ni espacios fantasma.
3. El contenido de la página, el pie de página (`footer`) y los elementos flotantes reservan el padding inferior necesario para no quedar tapados por la barra inferior fija en móvil.
4. La barra respeta `env(safe-area-inset-bottom)` en terminales con barra de inicio gestual.
5. El destino activo refleja visualmente su estado y expone `aria-current="page"` a tecnologías de asistencia.
6. Todos los destinos cumplen los requisitos de tamaño táctil mínimo (48x48px) y foco visible (WCAG 2.2 AA).
7. La suite de pruebas automatizadas pasa al 100% en verde.
