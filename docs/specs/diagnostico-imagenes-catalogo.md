# Diagnóstico Empírico de Rendimiento y Optimización de Imágenes del Catálogo

> **Incremento Asociado:** INC-57 (`change-57-imagenes-catalogo`)  
> **Fecha de Elaboración:** 2026-09-23  
> **Autor / Rol:** Arquitecto Principal de Sistemas (Ludeka)  
> **Entorno:** .NET 10 (C# 13), Blazor Web App, SkiaSharp, Cloudflare R2  

---

## 1. Motivación y Principio Rector: «Medir antes de cortar»

El pipeline de almacenamiento y compresión en Cloudflare R2 con SkiaSharp y formato WebP fue implantado en el incremento INC-40. No obstante, las vistas críticas del sistema (la portada `/` y el catálogo `/catalogo`) presentaban una percepción de sobrepeso de red sin respaldo de mediciones objetivas.

Este informe documenta el diagnóstico cuantitativo y cualitativo de los recursos visuales, los componentes Razor responsables de su renderizado y el comportamiento de la infraestructura de almacenamiento.

---

## 2. Inventario Cuantitativo de Recursos en Disco

### 2.1. Directorio `wwwroot/images/games` (Assets Estáticos Locales)

La inspección directa del directorio local revela las siguientes métricas:

| Métrica | Valor Medido | Observaciones |
|---|---|---|
| **Total de Archivos** | 71 archivos | Catálogo base de imágenes empaquetadas |
| **Peso Total en Disco** | 7.984,8 KB (~7,80 MB) | Volumen excesivo para un catálogo de 31 juegos |
| **Archivos PNG (.png)** | 38 archivos · 5.157,1 KB | Representan el **64,6%** del peso total |
| **Archivos JPG (.jpg)** | 30 archivos · 2.582,6 KB | Representan el 32,3% del peso total |
| **Archivos WebP (.webp)** | 3 archivos · 245,15 KB | Solo el **3,1%** utiliza el formato moderno |

### 2.2. Top de Archivos con Mayor Sobrepeso

| Archivo | Peso Actual | Diagnóstico |
|---|---|---|
| `patchwork.png` | **1.898,08 KB** (~1,9 MB) | PNG sin comprimir con dimensiones desproporcionadas. Una única tarjeta de Patchwork consume el 24% de todo el peso de imágenes locales. |
| `everdell.png` / `everdell.jpg` | 255,54 KB c/u | Archivos idénticos duplicados en disco con diferente extensión. |
| `the-color-monster.*` / `el-monstruo-de-colores.*` | 249,83 KB (×4 copias = ~1 MB) | Cuatro copias exactas del mismo archivo bajo nombres en inglés y español con extensiones `.jpg` y `.png`. |
| `7-wonders-duel.png` | 181,33 KB | PNG sin optimizar. |
| `carcassonne.png` / `carcassonne.jpg` | 164,16 KB c/u | Duplicidad de fichero idéntico. |
| `heat.png` / `heat.jpg` | 125,76 KB c/u | Duplicidad de fichero idéntico. |

---

## 3. Auditoría de Semillas y Dominio (`seed-games.json`)

De los 31 juegos sembrados en `src/Ludeka.Infrastructure/Seeding/seed-games.json`:

1. **Inexistencia de Miniaturas Reales en Datos Locales:**  
   En los 31 juegos (100%), `ThumbnailUrl` contiene exactamente la misma cadena literal que `CoverImageUrl`.
2. **Dependencia Exclusiva de PNG:**  
   Los 31 juegos apuntan por defecto a extensiones `.png` (ej. `/images/games/catan.png`).
3. **Carga Teórica de la Portada y Catálogo:**
   - La vista de `/catalogo` renderiza las 31 tarjetas a la vez.
   - Payload total de carátulas descargadas por el navegador: **~4,8 MB**.
   - Con miniaturas WebP a 400px (calidad 80), el peso medio estimado por miniatura es de **~18 KB**, lo que situaría el payload de 31 tarjetas en **~558 KB** (una reducción neta superior al **87%**).

---

## 4. Auditoría de Componentes Razor y Riesgos de CLS (Cumulative Layout Shift)

### 4.1. `GameCard.razor` (Tarjetas del Catálogo y Búsqueda)
- **Problema:** Enlaza exclusivamente a `Game.CoverImageUrl`:
  ```razor
  <img src="@Game.CoverImageUrl" ... />
  ```
  Ignora por completo la propiedad `Game.ThumbnailUrl`. Cuando el servicio de almacenamiento (R2) provee una miniatura optimizada de 400px, el componente sigue obligando al cliente a descargar la carátula completa (1000px).
- **Aspectos Positivos:** Ya implementa `width="240"`, `height="240"`, `loading="lazy"`, `decoding="async"` y fallback con `onerror`.
- **Acción:** Priorizar `ThumbnailUrl` (si no es nula ni vacía), recurriendo a `CoverImageUrl` y finalmente al placeholder SVG.

### 4.2. `HomeGameCard.razor` (Carril Top 20 en Portada)
- **Problema:** Enlaza a `Game.CoverImageUrl` para un contenedor de **192px**:
  ```razor
  <img src="@(!string.IsNullOrWhiteSpace(Game.CoverImageUrl) ? Game.CoverImageUrl : placeholder)" ... />
  ```
- **Acción:** Conmutar a `ThumbnailUrl` como primer candidato.

### 4.3. `HomeGiveawayCard.razor` (Carril de Sorteos en Portada)
- **Problema Crítico de CLS:**
  ```razor
  <img src="@Giveaway.ThumbnailUrl"
       alt="Imagen del sorteo @Giveaway.Title"
       loading="lazy"
       decoding="async"
       onerror="this.onerror=null; this.src='/images/defaults/sorteo-default.svg';" />
  ```
  **Carece de atributos `width` y `height`.** El navegador no puede calcular la relación de aspecto antes de recibir la cabecera de la imagen, reservando 0 px de alto e introduciendo saltos de página (*Cumulative Layout Shift*) al completarse la descarga asíncrona.
- **Acción:** Añadir `width="288"` y `height="144"` (relación 2:1 correspondiente a la clase `.rail-cover--wide`).

### 4.4. `HomeReleaseCard.razor` (Carril de Novedades en Portada)
- **Problema Crítico de CLS:**
  Al igual que la tarjeta de sorteos, renderiza `Release.CoverImageUrl` con `loading="lazy"` pero **sin dimensiones intrínsecas** `width` y `height`.
- **Acción:** Añadir `width="288"` y `height="144"`.

### 4.5. `HomeEventCard.razor` (Carril de Eventos en Portada)
- **Estado:** Ya cuenta con `width="320"` y `height="144"`, `loading="lazy"` y `decoding="async"`. Conforme con WCAG y Core Web Vitals.

### 4.6. `GameDetail.razor` (Ficha Detallada del Juego)
- **Estado:** La carátula principal del hero utiliza `width="320"` y `height="320"` con `fetchpriority="high"` (óptimo para LCP de la vista de detalle) y `CoverImageUrl`. Conforme.

---

## 5. Auditoría de Infraestructura y Caché HTTP (`CloudflareR2StorageService`)

En `CloudflareR2StorageService.cs`:
```csharp
Headers =
{
    CacheControl = "public, max-age=31536000, immutable"
}
```

- **Diagnóstico:**
  La directiva `immutable` combinada con `max-age=31536000` (1 año) es una excelente práctica para optimizar el TTFB y eliminar revalidaciones 304 en CDN Zero-Egress.
  Sin embargo, debido a que las claves de almacenamiento son deterministas fijas (`games/{bggId}/cover.webp` y `games/{bggId}/cover_thumb.webp`), si un moderador o proceso nocturno actualiza la carátula de un juego, los clientes que ya tengan la versión en caché local no percibirán la nueva imagen.
- **Dictamen Técnico:**
  Se debe mantener `public, max-age=31536000, immutable` por ser el estándar de máxima eficiencia para recursos estáticos inmutables en R2, estableciendo en la arquitectura que cualquier sustitución forzada debe acompañarse de purga en CDN o versionado en querystring (`?v={timestamp}`).

---

## 6. Conclusiones y Plan de Acción Justificado por los Datos

El diagnóstico empírico evidencia tres focos de actuación inmediata:
1. **Reducción Inmediata de Payload:** Cambiar `GameCard` y `HomeGameCard` para que consuman `ThumbnailUrl` en lugar de `CoverImageUrl`.
2. **Erradicación de CLS:** Dotar de `width` y `height` a `HomeGiveawayCard` y `HomeReleaseCard`.
3. **Optimización de Assets Locales:** Convertir `patchwork.png` y carátulas pesadas de `wwwroot/images/games` a WebP, reduciendo los 7,8 MB de assets locales a menos de 2 MB.
