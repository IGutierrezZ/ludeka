# INC-113: Tokens CSS, Paleta Editorial «Revista Lúdica», Par Claro/Oscuro y Tipografía Plus Jakarta

## Estado
⏳ Planificado (Fase 1 del Rediseño Integral «Revista Lúdica»)

## Rama y Worktree Sugerido
- **Rama:** `inc/rediseño-revista-ludica` (o rama atómica `inc/tokens-paleta-editorial`)
- **Worktree:** `C:\repos\ludeka-wt\rediseño-revista-ludica`

## Contexto y Motivación
La dirección visual C «Revista Lúdica» redefine las bases cromáticas, tipográficas y semánticas de Ludeka. Para soportar los nuevos componentes sin romper la compatibilidad regresiva en producción:
1. Se sustituye la matriz dispersa de 5 temas por un par canónico de alto contraste: **Claro** (`--paper: #FFF8EE`, `--ink: #22180F`) y **Oscuro** (`--paper: #17120F`, `--ink: #FFF1E2`).
2. Se unifica la tipografía en **Plus Jakarta Sans** (eliminando la serif *Fraunces*), introduciendo utilidades de tracking negativo (`letter-spacing: -0.055em`), interlineado compacto (`line-height: .88`) y numerales display.
3. Se añaden los tokens de bandas cromáticas (`--brand: #C9481C`, `--green: #1B7D52`, `--mustard: #FFC145`, `--blue-band: #2F5FBF`, `--plum: #6E3B5F`), separadores editoriales (`--rule: 3px`) y tarjetas pastel (`--p-mint`, `--p-peach`, `--p-blue`, `--p-pink`).

## Alcance de la Solución
1. **`src/Ludeka.Web/Styles/input.css`**:
   - Definición canónica de variables en `:root, [data-theme="light"]` y `[data-theme="dark"]`.
   - Ajuste de contraste accesible (terracota `#C9481C`, verde `#1B7D52` $\ge 4.5:1$ con texto claro).
   - Sombras físicas (`--shadow`, `--shadow-card`, `--shadow-sticker`) y soporte de micro-rotaciones seguras (`will-change: transform; backface-visibility: hidden`).
   - Mantenimiento de alias retrocompatibles para variables existentes (`--brand-primary`, `--bg-main`, etc.) mapeadas a los nuevos tokens durante la transición.
2. **`src/Ludeka.Web/tailwind.config.js`**:
   - Incorporar nombres semánticos de la Revista Lúdica (`paper`, `paper-2`, `card`, `ink`, `muted`, `brand`, `mustard`, `green`, `blue-band`, `plum`, `rule`, `pastels`).
   - Retirar *Fraunces* de `fontFamily` y afianzar *Plus Jakarta Sans*.
3. **Persistencia y Migración en `UserPreferenceService`**:
   - Lógica de normalización determinista: `editorial` o `wood` $\rightarrow$ `light`; `charcoal`, `tabletop` o `midnight` $\rightarrow$ `dark`.
   - Actualización del selector en `GuestThemePicker.razor` a botón binario luna/sol de 40 px con borde `currentColor`.
4. **Pruebas de Contrato y Regresión**:
   - Pruebas unitarias de mapeo de temas en `UserPreferenceServiceTests.cs`.
   - Pruebas de renderizado de tokens y contrastes en `ThemeContractTests.cs`.

## Criterios de Aceptación
- Compilación limpia de Tailwind CSS (`npm run build:css` / pipeline .NET).
- Cero regresiones en la suite de pruebas unitarias existente.
- Compatibilidad transparente para usuarios con temas guardados antiguos.
