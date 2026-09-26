# 23. Portada Editorial: Hero Narrativo, Carriles Componentizados, Fallbacks de Imagen e Iconografía Lucide

> **Estado del Módulo:** ✅ Implementado y Verificado (ampliado por INC-36)  
> **Incrementos Asociados:** [INC-35 — `portada-editorial` (archivado)](file:///c:/repos/Ludeka/openspec/changes/archive/2026-09-10-portada-editorial/proposal.md) · [INC-36 — `rediseno-paginas-editoriales` (archivado)](file:///c:/repos/Ludeka/openspec/changes/archive/2026-09-10-rediseno-paginas-editoriales/proposal.md)  
> **Pruebas Automatizadas:** suite total **855/855** en verde (INC-35: +108 sobre el baseline real 739 de INC-34; INC-36: baseline 854 + el Fact acotado del ancho del hero, con `WebMarkupContractTests` en 103/103); verificación SDD de INC-36 **PASS** con 13/13 requerimientos y 34/34 escenarios COMPLIANT  
> **Specs Vivas:** [`home-landing-hero`](file:///c:/repos/Ludeka/openspec/specs/home-landing-hero/spec.md) (modificada por INC-35/INC-36), [`editorial-page-foundations`](file:///c:/repos/Ludeka/openspec/specs/editorial-page-foundations/spec.md) (nueva, INC-36), [`home-dashboard-rails`](file:///c:/repos/Ludeka/openspec/specs/home-dashboard-rails/spec.md), [`default-image-fallbacks`](file:///c:/repos/Ludeka/openspec/specs/default-image-fallbacks/spec.md) e [`iconography-lucide`](file:///c:/repos/Ludeka/openspec/specs/iconography-lucide/spec.md)

---

## 1. Propósito y Filosofía

Este módulo transforma la portada `/` de un dashboard utilitario (INC-21/INC-31) en una **portada editorial con narrativa hogareña**: titular en serif display, foto de ambiente, carriles con lenguaje visual común, imágenes por defecto con identidad carbón+terracota (sin púrpura) e iconografía Lucide en toda la web.

1. **Narrativa sobre minimalismo:** el hero recupera un titular visible («La mesa está servida») como invitación a la mesa; el badge decorativo «✨ PORTADA EDITORIAL» sigue prohibido.
2. **Fallback por dominio:** ninguna tarjeta muestra una imagen rota o una zona vacía; ante ausencia o fallo se muestra el default de Ludeka del dominio.
3. **Componentes sobre duplicación:** cada carril renderiza su tarjeta desde un componente dedicado; el orquestador no repite markup.
4. **Iconografía sistémica:** cero emojis de interfaz; todo icono es SVG Lucide inline vía `Icon.razor` con catálogo whitelist.

---

## 2. Hero Editorial con Narrativa (`HeroEditorial.razor`)

- **Composición (estado vigente):** hero de imagen protagonista — la foto/ilustración llena el bloque y SOLO el buscador rápido se superpone como tarjeta propia (form → `/catalogo?q={término}`), arriba a la izquierda. La revisión del maintainer (2026-09-10) retiró el titular visible, el párrafo y las 4 píldoras de acceso: el hero queda limpio y la navegación vive en el menú superior/móvil y en los carriles.
- **Jerarquía accesible:** exactamente un `<h1>` en el documento, `sr-only` («La mesa está servida») para lectores de pantalla y SEO. El `<PageTitle>` del navegador no cambia.
- **Variantes de fondo (D1):** enum `HeroBackgroundVariant` con 5 valores — `FotoPrimerPlano`, `FotoMesaAmigos`, `FotoEurogame` (default), `CssScene` e `Ilustracion` — mapeados por `HeroBackgroundAssets` (4 fotos × 3 formatos AVIF/WebP/JPG). La conmutación es un cambio de 1 línea (parámetro `Background`) que no altera el markup del resto del hero.
- **Patrón de rendimiento INC-07:** `<picture>` con `<source>` AVIF/WebP y `<img>` JPEG de fallback, `fetchpriority="high"`, `width="1600"`/`height="900"`, `alt` descriptivo en castellano; pesos verificados en disco < 200 KB (máximo 193,9 KB). Objetivos de portada: LCP < 2,5 s y CLS = 0 — medidos en la verificación de INC-36 con Chrome real: **LCP 115 ms** (AVIF servido) y **CLS 0,00** a 360×740 en servidor local.

### 2.1 Fix Responsivo del Hero y Botón Buscar (INC-36)

- **Modelo de altura (DD-01, estrategia A):** el bloque `.hero-editorial` de `Styles/input.css` deriva el alto del ancho (`aspect-ratio: 16 / 9`, suelo `min-height: 200px`, cap progresivo `max-height: clamp(200px, 36vw, 460px)`); el markup elimina `min-h-[360px] sm:min-h-[460px]`. Sin media queries, la transición entre breakpoints es continua y el CLS se mantiene en 0.
- **Verificación runtime (Chrome DevTools MCP):** a 360×740 el hero mide 305×200 (ancho = contenedor útil; sin desborde propio, antes 355,55 px transferidos del ratio); a 640×800 mide 585×230,4; a 1240×900 mide 1185×446,4. Traza de rendimiento: LCP 115 ms, CLS 0,00 y 0 entradas `layout-shift`.
- **Foco autoral por variante (DD-02):** `HeroBackgroundAssets.FocalClass` resuelve la clase `hero-focal--{eurogame|mesa-amigos|primer-plano|ilustracion}` (vacía en `CssScene`) y `input.css` declara su `object-position`: 62% 70%, 45% 55%, 50% 30% y 35% 45% respectivamente — ninguno deja el `50% 50%` por defecto.
- **Botón Buscar accesible (D3):** el botón usa `text-[var(--on-brand)]` sobre `--brand-primary`, con contraste ≥ 4,5:1 en los 5 `data-theme` (antes 3,69:1 con `text-white` en charcoal).
- **Congelado por contrato:** `<picture>` AVIF/WebP/JPG, `fetchpriority="high"`, `width`/`height`, `onerror` → escena CSS, alt castellano, h1 `sr-only` y el handler `HandleQuickSearch` permanecen intactos.
- **Scrim y escena CSS:** `.hero-scrim` aplica doble gradiente sobre `var(--bg-main)` sobre el cuadrante del texto (contraste objetivo ≥ 4,5:1 en los 5 `data-theme`); `.hero-scene` (100% CSS, sin peticiones de imagen) queda siempre bajo la foto como fallback de serie. Sin `preload` (razón documentada en el design).
- **Fuente:** Fraunces variable cargada en la **misma petición** de Google Fonts existente (sin `<link>` nuevo; test dedicado lo exige), `display=swap` intacto.

---

## 3. Carriles Componentizados (`home-dashboard-rails`)

### 3.1 Estructura de Componentes (`Components/Home/`)
`HomeDashboard.razor` queda como **orquestador**: los contratos de markup exigen `<HeroEditorial`, `<RailHeader`, `<HomeGameCard`, `<HomeGiveawayCard`, `<HomeReleaseCard`, `<HomeEventCard` y prohíben el markup de tarjeta inline (sin `BggRating`/`RemainingTimeText` duplicados). Las cards son componentes puros (solo DTO, sin servicios). Cada carril conserva destino, `aria-label` y contenido informativo (35 `.rail-card` con paridad de comportamiento).

| Carril | Componente | Fuente de datos (sin cambios) |
|---|---|---|
| Top 20 Juegos | `HomeGameCard` | `ICatalogService.GetCatalogAsync` (Top 20 por `BggRank`) |
| Sorteos Activos | `HomeGiveawayCard` | `IGiveawayService.GetGiveawaysAsync` (promocionados primero) |
| Novedades en Tiendas | `HomeReleaseCard` | `IWeeklyReleaseService.GetReleasesAsync` |
| Ferias y Eventos | `HomeEventCard` | `IBoardGameEventRepository.GetUpcomingEventsAsync` |

### 3.2 Render de Imagen con Fallback por Dominio (D2)
- **Top 20:** conserva su fallback actual (`game-placeholder.svg` / `expansion-placeholder.svg`).
- **Sorteos / Novedades / Eventos:** renderizan `GiveawayDto.ThumbnailUrl`, `WeeklyReleaseDto.CoverImageUrl` y `BoardGameEventDto.ImageUrl` con fallback al default del dominio (ver §4).
- Toda imagen de carril lleva `width`/`height` o contenedor de aspecto fijo (`.rail-cover` con `aspect-ratio`), `loading="lazy"` y `decoding="async"`.

### 3.3 Microinteracciones con Equivalencia de Foco
- **Tokens (input.css, líneas 193-263):** `--ease-*`, `--dur-*` y `--rail-*` compartidos; clase común `.rail-card`.
- **Hover:** lift + glow con `--brand-glow` + zoom de imagen 1,03.
- **Foco:** `.rail-card:hover, .rail-card:focus-visible, .rail-card:has(:focus-visible)` (línea 215) y `outline` ≥ 2 px (línea 221). El selector `:has(:focus-visible)` (commit 44e5ae4) da a las cards `<div>` (`HomeGiveawayCard`, `HomeReleaseCard`, `HomeEventCard`, donde el foco recae en el enlace interior) la misma evidencia visual de tarjeta que en hover; `HomeGameCard` es un `<a>` y ya lo tenía.
- **Movimiento reducido:** `@media (prefers-reduced-motion: reduce)` anula transiciones y transforms en `.rail-card` y `.rail-cover img`.
- **Targets:** ningún elemento interactivo del carril mide menos de 24×24 px (WCAG 2.5.8).

### 3.4 Scroll y Clases Corregidas
- Scroll horizontal con `snap-x snap-mandatory` en los 4 carriles y scrollbar no visible: `.scrollbar-none` definida realmente (`scrollbar-width: none` + `::-webkit-scrollbar { display: none }`) — ya no es clase muerta.
- Eliminada la clase inválida `sm:w-68` (Tailwind 3.4); tarjetas de Novedades con `w-60 sm:w-72`.
- Títulos `<h2>` de los 4 carriles con `--font-display` (`.rail-title`); el resto de la web no se ve afectada.

### 3.5 Refinamiento Móvil, Enfoque Editorial y Arrastre en Escritorio (INC-69)
- **Cabeceras (`RailHeader.razor`):** Eliminación del badge de conteo numérico redundante (`@Count @CountLabel`), manteniendo únicamente el icono temático, título display del carril y el enlace de navegación rápida («Ver todos»).
- **Encuadre de Tarjetas Top 20 (`HomeGameCard.razor`):**
  - **Affordance Móvil:** Dimensiones ajustadas a `w-[28vw] min-w-[105px] max-w-[125px] sm:w-44 md:w-48`. En anchos de pantalla móvil estándar (360px a 430px) entran exactamente 3 tarjetas y media, otorgando una invitación visual clara (*peek affordance*) al desplazamiento horizontal.
  - **Formato Compacto de Comensales:** Sustitución de etiquetas extensas por formato condensado legible `👥 1-4J` (o `1J`, `1-5J+`).
  - **Alineación Vertical Milimétrica:** Altura mínima fija en el bloque de título (`min-h-[2.25rem]`, hasta 2 líneas) y anclaje del diseñador/año al borde inferior (`mt-auto`), garantizando uniformidad estética entre tarjetas con nombres cortos y largos.
- **Interacción de Arrastre en Escritorio (`rail-scroll.js`):**
  - Soporte de desplazamiento mediante clic y arrastre con ratón (*drag-to-scroll*) sobre cualquier contenedor con el atributo `data-rail-scroll`. Permite una experiencia fluida en monitores de sobremesa no táctiles, suprimiendo la activación accidental de enlaces durante el arrastre. Registrado e inicializado globalmente en `App.razor`.

---

## 4. Imágenes por Defecto por Dominio (`default-image-fallbacks`)

### 4.1 Assets Estáticos
`wwwroot/images/defaults/`: `evento-default.svg` (carrusel de feria), `sorteo-default.svg` (caja de sorteo), `novedad-default.svg` (etiqueta de novedad) y `generico-default.svg` — SVG planos carbón+terracota (1,0–1,3 KB) con paleta `#E05A38`. El placeholder de juego **no se reutiliza** para estos dominios.

### 4.2 Componente `DefaultImage.razor`
- SVG **inline** con `viewBox="0 0 400 225"` y rellenos por variables de tema (`var(--brand-*)`, `var(--bg-*)`): hereda los 5 temas.
- Variante por dominio (evento, sorteo, novedad) vía `@switch`; dominio sin variante → motivo genérico con microtexto «LUDEKA» sin fallar.
- `aria-hidden="true"` condicionado a la presencia de `Alt` (modo decorativo cuando acompaña texto).
- **Invariante de dominio (importante):** la entidad `BoardGameEvent` (Core) **exige imagen en su constructor** («La imagen o cartel del evento es obligatoria»). Por tanto la rama inline de `DefaultImage` en las cards de evento es **defensa en profundidad** ante datos legacy/manuales, no alcanzable por el camino de datos normal; el fallback de evento que opera en runtime es el `onerror` ante URL externa caída.

### 4.3 Comportamiento Dual
| Situación | Mecanismo |
|---|---|
| Campo imagen del DTO nulo o vacío | `DefaultImage.razor` inline en el mismo contenedor de aspecto fijo (sin `<img>` roto ni `src` vacío) |
| URL externa que falla en cliente | `onerror="this.onerror=null; this.src='/images/defaults/{dominio}-default.svg'"` (bucle anulado) |

### 4.4 Reutilización Fuera de la Portada (D5)
`Events.razor` (líneas 131-148) y `EventsManagement.razor` (81-97) adoptan el mismo mecanismo (`onerror` → `evento-default.svg`); el componente es reutilizable en cualquier página sin lógica de portada.

---

## 5. Iconografía Lucide (`iconography-lucide`)

- **`Icon.razor` (`Components/Shared/`):** SVG inline del catálogo Lucide, `viewBox="0 0 24 24"`, `stroke="currentColor"` (hereda el tema), tamaño configurable, `aria-hidden="true"` por defecto, cero peticiones de red. Icono desconocido → sin salida y sin error (nunca un sustituto).
- **Catálogo whitelist:** `IconCatalog.cs` con 94 iconos; cualquier icono nuevo se añade al catálogo y se consume vía `Icon.razor`.
- **Migración global (D3):** la portada (~18 emojis) en PR-3a y el resto de la web en PR-3b; 0 emojis en la carpeta `Components` (barrido de fuente verificado). Donde el emoji era la única señal semántica se añadió texto visible o accesible equivalente. Los contratos `mustNotContain` por archivo impiden reintroducir emojis.
- **Excepciones iconográficas documentadas:** `CountryCatalog.GetFlag`/`FlagEmoji` y `Stats.Badge.IconEmoji` (capa Application, data-driven, fuera de `Components`) y los glifos tipográficos ★ U+2605 (rating) y ✓ U+2713.

---

## 6. Arquitectura Tocada (por capa)

| Capa | Componentes | Cambio |
|---|---|---|
| **Web** | `Pages/HomeDashboard.razor` | Orquestador limpio: hero por componente y carriles por componentes |
| **Web** | `Components/Home/` (6 archivos) + `HeroBackgroundVariant.cs` | Nuevo: `HeroEditorial`, `RailHeader`, 4 cards, assets de variantes |
| **Web** | `Components/Shared/Icon.razor`, `Components/Shared/DefaultImage.razor` | Nuevo |
| **Web** | `Pages/Events.razor`, `Pages/EventsManagement.razor` | Fallback de imagen por dominio (D5) |
| **Web** | `Styles/input.css` | Tokens de transición, `.rail-card` (+`:has(:focus-visible)`), serif display, `reduced-motion`, fixes de clases muertas |
| **Web** | `Components/App.razor` | Fraunces en la URL existente de Google Fonts |
| **Assets** | `wwwroot/images/defaults/*` (4 SVG), `wwwroot/images/home/hero-ambiente-*.{avif,webp,jpg}` | Nuevo |
| **Core / Application / Infrastructure** | — | Sin cambios: se consumen los campos imagen ya existentes (`GiveawayDto.ThumbnailUrl`, `WeeklyReleaseDto.CoverImageUrl`, `BoardGameEventDto.ImageUrl`) |

---

## 7. Decisiones de Diseño (D1–D10)

| # | Decisión | Implementación |
|---|---|---|
| **D1** | Hero con variantes intercambiables | Enum + parámetro `Background` (default `FotoEurogame`); cambio en 1 línea sin tocar markup |
| **D2** | Imagen con fallback en los 3 carriles con datos | Inline si falta, `onerror` si falla; Top 20 conserva placeholders |
| **D3** | Estructura `Components/Home` + iconografía global | 6 componentes; `Icon.razor` con whitelist; migración global de emojis |
| **D4** | Tokens de microinteracción y serif display | Literal del design en `input.css`; Fraunces solo en hero + títulos de carril |
| **D5** | `DefaultImage` inline + estática | Enum de 4 dominios; `DefaultImageAssets.Url()` nunca null; adopción en páginas de eventos |
| **D8** | Assets por defecto por dominio | 4 SVG estáticos carbón+terracota con microtextos |
| **D9** | PRs encadenados | 4 PRs apilados (corte honesto PR-3a/3b en iconografía) |
| **D10** | Rendimiento y accesibilidad | Sin `preload` (razonado), alt en castellano, targets ≥ 24 px, CLS 0 por dimensiones fijas |

Trazabilidad completa (AD-1 a AD-n): [`design.md del cambio archivado`](file:///c:/repos/Ludeka/openspec/changes/archive/2026-09-10-portada-editorial/design.md).

---

## 8. Estrategia de Pruebas

Suite **855/855** (INC-35: baseline real 739 de INC-34 + 108; INC-36: baseline 854 + el Fact acotado del ancho del hero, con `WebMarkupContractTests` en 103/103). Herramientas y focos:

| Foco | Mecanismo |
|---|---|
| Contratos de markup (`.razor`/`.css` leídos como fuente) | `WebMarkupContractTests` (`mustContain`/`mustNotContain` por archivo), sin bUnit (decisión de INC-31) |
| Catálogo de iconos y assets | `IconCatalogTests`, tests de `HeroBackgroundAssets`/`HeroBackgroundVariant` (TheoryData 4×3 formatos) |
| Render real | Verificación runtime con `dotnet run` (puerto 5081): portada, `/eventos`, `/images/defaults/*` HTTP 200 |
| Fallback REAL (datos sin imagen) | Seed temporal marcado `TEMP-VERIFY` (sorteo y novedad sin imagen) → SVG inline sin `<img>`; reversión completa del seed y de la BD tras verificar |
| Runtime INC-36 (navegador real) | Chrome DevTools MCP: hero ×3 viewports con traza LCP/CLS y observador independiente, modales de Radar/News ×2 con instrumentación de foco, 7 rutas y consola |

---

## 9. Seguimiento Posterior (Checklist del Maintainer)

1. **Scrim:** contraste visual del texto del hero sobre la foto en los 5 `data-theme`.
2. **LCP/CLS reales:** medidos en la verificación de INC-36 (Chrome real: LCP 115 ms y CLS 0,00 a 360×740 en local); la corrida Lighthouse móvil del maintainer queda como comprobación opcional.
3. **Variantes del hero:** probar las 5 (cambio de 1 línea; la escena CSS debe verse bajo la foto y cubrirla entera con `CssScene`).
4. **URLs BGG sintéticas del seed:** 404 que activan `onerror`; en producción apuntar a imágenes reales o a los defaults (SUGGESTION-2).
5. **Smoke del `onerror`:** ejecución real en navegador (la verificación SSR es de wiring) (SUGGESTION-3).
