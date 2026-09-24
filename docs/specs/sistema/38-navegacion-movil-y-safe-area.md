# 38. Navegación Móvil, Barra Inferior y Áreas Seguras (Safe-Area)

> **Módulo:** 38 — Navegación Móvil, Barra Inferior y Áreas Seguras  
> **Estado:** Implementado y Verificado (INC-60)  
> **Fuentes:** [`MobileBottomNav.razor`](file:///C:/repos/ludeka-wt/navegacion-movil/src/Ludeka.Web/Components/Shared/MobileBottomNav.razor), [`App.razor`](file:///C:/repos/ludeka-wt/navegacion-movil/src/Ludeka.Web/Components/App.razor), [`MainLayout.razor`](file:///C:/repos/ludeka-wt/navegacion-movil/src/Ludeka.Web/Components/Layout/MainLayout.razor), [`MainLayout.razor.css`](file:///C:/repos/ludeka-wt/navegacion-movil/src/Ludeka.Web/Components/Layout/MainLayout.razor.css), [`CollectionActionBar.razor`](file:///C:/repos/ludeka-wt/navegacion-movil/src/Ludeka.Web/Components/Shared/CollectionActionBar.razor), [`input.css`](file:///C:/repos/ludeka-wt/navegacion-movil/src/Ludeka.Web/Styles/input.css)  
> **Pruebas:** `tests/Ludeka.UnitTests/Web/MobileNavigationContractTests.cs` (8 contratos) y suite completa (1.695 pruebas superadas al 100%)

---

## 1. Contexto y Filosofía de Diseño

La filosofía arquitectónica de Ludeka consagra el principio de **«Mobile-First Radical»** (`AGENTS.md` §3): la plataforma no debe sentirse como una web de escritorio encogida, sino como una aplicación editorial ágil y natural al tacto, operable íntegramente con una sola mano mediante el alcance natural del pulgar.

Antes de INC-60 existían dos deficiencias estructurales en dispositivos móviles:
1. **Ausencia total de navegación inferior:** El usuario dependía de un desplegable `<details>` ubicado en el extremo superior derecho de la cabecera, forzando continuos desplazamientos hacia arriba para cambiar de vista.
2. **Colisiones por omisión de Safe-Area:** `App.razor` carecía de `viewport-fit=cover` en su directiva viewport, provocando que `env(safe-area-inset-bottom)` evaluara a `0px` en Safari iOS y WebViews modernas, lo que causaba solapamientos físicos con la barra gestual del sistema operativo (*home indicator*).
3. **Acciones lúdicas desconectadas del pulgar:** En pantallas estrechas, los botones de colección de `CollectionActionBar.razor` se apilaban verticalmente consumiendo espacio excesivo y alejando la interacción táctil.

---

## 2. Cimientos de Viewport y Safe-Area

### 2.1 Directiva Viewport (`App.razor`)
Se incorpora `viewport-fit=cover` para autorizar a los navegadores móviles el renderizado bajo el notch y las barras gestuales del sistema:
```html
<meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover" />
```

### 2.2 Variables y Clases Utilitarias (`input.css`)
Se añaden las clases dedicadas de safe-area y reserva de scroll en `Styles/input.css`:
```css
/* Utilidades Safe-Area para Navegación Móvil (INC-60) */
.mobile-safe-bottom {
    padding-bottom: env(safe-area-inset-bottom, 0px);
}

.mobile-nav-spacer {
    padding-bottom: calc(4.25rem + env(safe-area-inset-bottom, 0px));
}

@media (min-width: 1024px) {
    .mobile-nav-spacer {
        padding-bottom: 0 !important;
    }
}
```

### 2.3 Convivencia con Avisos del Sistema (`MainLayout.razor.css`)
El aviso flotante `#blazor-error-ui` ajusta su posición inferior en vista móvil para quedar por encima de la barra de navegación:
```css
#blazor-error-ui {
    bottom: calc(4.25rem + env(safe-area-inset-bottom, 0px));
}

@media (min-width: 1024px) {
    #blazor-error-ui {
        bottom: 0;
    }
}
```

---

## 3. Componente `MobileBottomNav.razor`

Ubicado en `src/Ludeka.Web/Components/Shared/MobileBottomNav.razor`, se integra de forma global en `MainLayout.razor`.

### 3.1 Estructura Visual y Composición de Destinos
Consta de 5 destinos de primer nivel en rejilla equilibrada de 5 columnas, con altura base de 64px (`h-16`) más el padding seguro `mobile-safe-bottom`:
1. **Inicio** (`/` · icono Lucide `house` · «Inicio»): Portada editorial y carriles destacados.
2. **Catálogo** (`/catalogo` · icono Lucide `dices` · «Catálogo»): Exploración de catálogo, paginación y filtros en mesa.
3. **Ludoteca** (`/cuenta/ludoteca` · icono Lucide `library` · «Ludoteca»): Colección lúdica, partidas y préstamos.
4. **Sorteos** (`/sorteos` · icono Lucide `gift` · «Sorteos»): Radar comunitario y sorteos activos.
5. **Cuenta / Acceso** (`/cuenta` o `/login` · icono Lucide `user` · «Cuenta» / «Entrar»): Hub privado para usuarios autenticados o acceso para invitados.

### 3.2 Marcado Semántico y Accesibilidad (WCAG 2.2 AA)
- Landmark `<nav aria-label="Navegación principal móvil">`.
- Destino activo identificado dinámicamente con `aria-current="page"` y destacado visual en color de marca (`text-[var(--brand-primary)] bg-[var(--brand-primary)]/10`).
- Tamaño táctil mínimo garantizado de 48×48px con `min-h-[48px]`.
- Foco visible claro (`focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)]`).
- Reactividad instantánea mediante suscripción a `NavigationManager.LocationChanged` y limpieza en `Dispose()`.

### 3.3 Simetría Responsiva
- En móviles y tabletas (`< 1024px`): activo con `fixed bottom-0 inset-x-0 z-40 lg:hidden`.
- En escritorio (`>= 1024px`): estrictamente oculto con `lg:hidden`, restableciendo `padding-bottom: 0` mediante `mobile-nav-spacer`.

---

## 4. Ergonomía en Acciones de Colección (`CollectionActionBar.razor`)

En la ficha del juego, los controles de estado de colección se transforman de una pila vertical a una fila segmentada de 3 columnas compacta (`grid-cols-3 gap-1.5 sm:gap-2`):
- **Botón 1:** Icono `library` + etiqueta adaptativa: `Tengo` en pantallas móviles (<640px) y `En mi ludoteca` en pantallas medianas/grandes.
- **Botón 2:** Icono `dices` + etiqueta `Jugado` con marca de verificación reactiva `✓`.
- **Botón 3:** Icono `shopping-cart` + etiqueta `Comprar`.

Esta disposición permite marcar y alternar estados de colección de forma inmediata con el pulgar mientras se explora el catálogo o se lee una ficha.
