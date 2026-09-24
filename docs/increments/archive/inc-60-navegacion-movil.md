# INC-60: Navegación Móvil — Barra Inferior y Safe-Area

> **Estado:** ✅ Verificado y Archivado (entregado el 2026-09-24, 1.695 pruebas al 100% en verde)
> **Fecha de Inicio:** 2026-09-24 · **Fecha de Cierre:** 2026-09-24
> **Rama de Trabajo:** `inc/navegacion-movil`
> **Worktree:** `C:\repos\ludeka-wt\navegacion-movil`
> **Dependencias:** ninguna fuerte (fijados los 5 destinos canónicos de bottom nav en sintonía con INC-61)
> **Especificación Viva:** [38. Navegación Móvil y Safe-Area](file:///c:/repos/Ludeka/docs/specs/sistema/38-navegacion-movil-y-safe-area.md)

---

## 1. Cómo se descubrió

La identidad del proyecto exige «mobile-first radical» (`AGENTS.md`) con la barra de acciones al alcance del pulgar (`Tengo`, `Jugado`, `Deseado`, `Prestar`), pero al revisar la UI el patrón de navegación móvil simplemente no existía.

## 2. El agujero, verificado

- **Cero resultados** para `BottomNav`, `bottom-nav`, `mobile-nav` y `safe-area` en todo el repositorio: no había navegación inferior ni respeto de áreas seguras de notch/home-indicator.
- La navegación era exclusivamente de escritorio (header/nav) sin alternativa optimizada para pulgar.
- La PWA (INC-16, módulo `09-pwa-y-modo-offline.md`) estaba operativa pero sin chrome móvil propio: `manifest.webmanifest` definía `start_url` = `/mi-ludoteca` sin shell de navegación inferior.
- Las acciones de colección (`Tengo`, `Jugado`, `Deseado`, `Prestar`) vivían en la ficha/ludoteca sin acceso persistente ni adaptado a una sola mano en móvil.

## 3. Lo que pedía el maintainer

Que la app se sienta como una aplicación moderna de alta fidelidad en el móvil: navegación al alcance del pulgar, sin depender del header ni del scroll al inicio, y sin que la barra del navegador del sistema o el home indicator se coman los controles.

## 4. Alcance y decisiones tomadas

1. **Barra de navegación inferior fija en móvil (`MobileBottomNav.razor`):** 5 destinos canónicos (Inicio `/`, Catálogo `/catalogo`, Ludoteca `/cuenta/ludoteca`, Sorteos `/sorteos`, Cuenta `/cuenta` o `/login`).
2. **Soporte estricto de Safe-Area:** Incorporación de `viewport-fit=cover` en `App.razor`, utilidades `.mobile-safe-bottom` y `.mobile-nav-spacer` en `input.css` / `app.css`.
3. **Reserva de scroll y elevación de avisos:** Padding inferior en el layout (`calc(4.25rem + env(safe-area-inset-bottom, 0px))`) que se anula en `lg:`, y elevación de `#blazor-error-ui`.
4. **Ergonomía en acciones de colección:** Optimización a 3 columnas táctiles con etiqueta «Tengo» adaptativa en `CollectionActionBar.razor`.
5. **Cumplimiento WCAG 2.2 AA:** Landmark semántico `<nav aria-label="Navegación principal móvil">`, atributos `aria-current="page"`, zonas táctiles >= 48px y soporte para los 5 temas visuales.

## 5. Criterios de aceptación cumplidos

1. ✅ En viewport móvil se renderiza la barra inferior fija con 5 destinos alcanzables con una mano.
2. ✅ Ningún control queda bajo el home-indicator ni el notch (`safe-area-inset-bottom` verificado).
3. ✅ En escritorio (`lg:`) la barra desaparece completamente sin dejar huecos ni duplicar la cabecera.
4. ✅ Navegación accesible con teclado y lectores de pantalla (`aria-current` reactivo y foco nítido).
5. ✅ Suite completa en verde con `dotnet test Ludeka.sln`: 1.685 pruebas unitarias + 10 de integración (1.695 en total).

## 6. Verificación Automatizada

- **Contratos:** `tests/Ludeka.UnitTests/Web/MobileNavigationContractTests.cs` (8 pruebas específicas superadas).
- **Regresiones:** 0 fallos en los 1.685 tests unitarios y 10 de integración en Testcontainers PostgreSQL.
