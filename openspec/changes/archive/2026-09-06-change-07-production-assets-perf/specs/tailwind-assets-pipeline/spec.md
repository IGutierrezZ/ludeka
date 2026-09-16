# Especificación: tailwind-assets-pipeline (Pipeline de Compilación, Purga y Assets de Tailwind CSS)

## 1. Propósito y Contexto
Esta especificación define los requerimientos para la compilación optimizada, purga de clases no utilizadas y entrega de assets estáticos de Tailwind CSS en Ludeka. Garantiza que el stylesheet final descargado por navegadores móviles sea mínimo en bytes, no contenga `@import` bloqueantes de CSS y preserve todas las utilidades y componentes editoriales requeridos por las 33 vistas Razor del monorepo.

---

## 2. Requerimientos de Comportamiento (RFC 2119)

### REQ-ASSET-01: Configuración de Tailwind CLI y Escaneo de Contenidos
- El proyecto **DEBE** contar con un archivo `src/Ludeka.Web/tailwind.config.js`.
- La propiedad `content` **DEBE** escanear todos los componentes Razor y plantillas HTML:
  `["./Components/**/*.razor", "./wwwroot/**/*.html"]`.
- El archivo de configuración **DEBE** declarar un `safelist` para clases generadas dinámicamente en helpers de C# (e.g., `status-mustplay`, `status-recommended`, `status-notrecommended`, badges de colores `bg-emerald-950/70`, `bg-cyan-950/70`, `bg-pink-950/70`, etc.).
- El archivo de configuración **DEBE** extender la paleta cromática con las variables de marca de Ludeka (`brand-primary: #FF5A36`, `bg-main: #0B0F17`, `bg-card: #182232`).

### REQ-ASSET-02: Hoja de Entrada y Componentes Editoriales
- El archivo `src/Ludeka.Web/Styles/input.css` **DEBE** incorporar las directivas maestras:
  ```css
  @tailwind base;
  @tailwind components;
  @tailwind utilities;
  ```
- Dicho archivo **DEBE** albergar los estilos de componentes editoriales (`.traffic-chip`, `.badge-pill`, `.game-card-editorial`, `.search-input`, `.container-ludeka`, `.rating-badge-float`).
- El archivo **NO DEBE** incluir directivas `@import url(...)` de Google Fonts u otras fuentes remotas.

### REQ-ASSET-03: Optimización y Minificación del Bundle
- La compilación mediante `tailwindcss --minify` **DEBE** generar `src/Ludeka.Web/wwwroot/app.css`.
- El archivo generado **DEBE** estar purgado (eliminando selectores CSS no referenciados).
- El servicio de static assets de ASP.NET Core 10 (`MapStaticAssets()`) **DEBE** mapear y servir este archivo con fingerprinting (`@Assets["app.css"]`).

### REQ-ASSET-04: Carga No Bloqueante de Tipografías
- En `src/Ludeka.Web/Components/App.razor`, la carga de tipografías ('Plus Jakarta Sans' y 'JetBrains Mono') **DEBE** realizarse mediante elementos `<link>` en `<head>` con preconexiones anticipadas:
  ```html
  <link rel="preconnect" href="https://fonts.googleapis.com" />
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
  <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&family=JetBrains+Mono:wght@500;700&display=swap" />
  ```

---

## 3. Criterios de Aceptación (Escenarios Gherkin)

### Escenario 1: Purga y minificación exitosa de estilos
```gherkin
Given las plantillas Razor en src/Ludeka.Web/Components
When se ejecuta la compilación de Tailwind CLI con la bandera --minify
Then el archivo src/Ludeka.Web/wwwroot/app.css se genera sin errores
And el archivo no contiene sentencias @import hacia fonts.googleapis.com
And las clases editoriales .traffic-chip y .badge-pill están preservadas
```

### Escenario 2: Preconexiones de fuentes en App.razor
```gherkin
Given la plantilla raíz App.razor
When el navegador procesa el bloque <head>
Then existen etiquetas <link rel="preconnect"> hacia fonts.googleapis.com y fonts.gstatic.com
And la hoja de estilos de Google Fonts se carga con display=swap
```
