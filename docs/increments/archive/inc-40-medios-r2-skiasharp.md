# Incremento 40: Pipeline de Almacenamiento y Optimización de Medios (Cloudflare R2 + SkiaSharp + WebP)

- **Identificador SDD:** `change-40-medios-r2-skiasharp`
- **Estado:** ✅ **Completado y Archivado** (906 tests en verde al 100%)
- **Rama:** `inc/medios-r2-skiasharp` (worktree en `C:\repos\ludeka-wt\medios-r2-skiasharp`)
- **Objetivo Principal:** Dotar a Ludeka de almacenamiento soberano de imágenes en Cloudflare R2 con cero costes de salida (*zero egress fees*) y pipeline en memoria (*zero-disk*) con SkiaSharp para compresión y generación de variantes WebP deterministas.

---

## 1. Alcance Funcional y Técnico

1. **Almacenamiento S3-Compatible en Cloudflare R2:**
   - Bucket `ludeka-media` o configurable por `Cloudflare:BucketName`.
   - Dominio CDN público configurable (`Cloudflare:PublicCdnBaseUrl`, ej. `https://cdn.ludeka.com`).
   - Cabeceras inmutables de caché en navegador y CDN: `Cache-Control: public, max-age=31536000, immutable`.
2. **Motor de Optimización Zero-Disk con SkiaSharp:**
   - Sin escritura a disco físico; procesamiento exclusivo en streams en memoria.
   - Redimensionado bicúbico respetando el aspect ratio original.
   - Codificación nativa a WebP (calidad 80–82%).
3. **Nomenclatura Determinista de Variantes:**
   - Portada principal: `games/{bggId}/cover.webp` (máx. 1000px).
   - Portada miniatura: `games/{bggId}/cover_thumb.webp` (máx. 400px).
   - Contraportada / trasera: `games/{bggId}/back.webp` (máx. 1000px).
   - Foto en mesa / componentes: `games/{bggId}/table.webp` (máx. 1200px).
   - Medios sociales / moderación: `social/{year}/{month}/{guid}.webp`.
4. **Modo Simulado para Tests y Desarrollo:**
   - Implementación `SimulatedImageStorageService` para pruebas sin credenciales de Cloudflare.

---

## 2. Dependencias NuGet
- `AWSSDK.S3` (cliente liviano para llamadas S3 contra Cloudflare R2).
- `SkiaSharp` (motor gráfico multiplataforma de Google con licencia MIT libre).
- `SkiaSharp.NativeAssets.Linux.NoDependencies` (para soporte nativo en Docker / Cloud Run).
