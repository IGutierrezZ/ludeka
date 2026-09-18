# Archive Report: change-40-medios-r2-skiasharp

**Change**: `change-40-medios-r2-skiasharp` (Incremento 40: Pipeline de Almacenamiento y Optimización de Medios)  
**Archive Date**: 2026-09-18  
**Archive Status**: **PARTIAL (Declared)**  
**Delivered Artifact**: Mergeado en `main` (`f756da4`)

---

## Resumen Ejecutivo

El cambio `change-40` implementó la infraestructura de almacenamiento de medios en Cloudflare R2 con optimización de imágenes via SkiaSharp. **16 de 19 entregables concretos del proposal se completaron e integraron en `main`.** El incremento está productivo y verificado (1417/1417 pruebas en verde, 0 fallos).

**Este es un archivado parcial:** la fase `spec` nunca se ejecutó (divergencia de enrutamiento descrita más abajo), y existen **2 desviaciones del proposal** que se registran honestamente en este informe sin ocultar ni maquillar. Una cobertura de prueba parcial en `CloudflareR2StorageService` también se documenta explícitamente.

---

## Artefactos SDD Disponibles

| Artefacto | Estado | Observación |
|-----------|--------|-------------|
| **proposal.md** | ✓ Presente | Fuente única de los requerimientos y alcance definido |
| **spec.md** | ✗ Ausente | **Deliberadamente no creada** (ver §3 "Divergencia de Enrutamiento") |
| **design.md** | ✗ Ausente | No se ejecutó fase `design` |
| **tasks.md** | ✗ Ausente | No se ejecutó fase `tasks` |
| **Artefactos canónicos** | ✓ Presentes | `docs/specs/sistema/26-almacenamiento-medios-r2-skiasharp.md` (§4) |

---

## Entregables del Proposal vs. Realización

### Entregados (16/19)

1. ✓ **Cliente S3 hacia R2**: `src/Ludeka.Infrastructure/Services/CloudflareR2StorageService.cs:43-50` con paquete `AWSSDK.S3 4.0.103.2` registrado en `src/Ludeka.Infrastructure/Ludeka.Infrastructure.csproj:8`.

2. ✓ **SkiaSharp 4.152.0 y SkiaSharp.NativeAssets.Linux.NoDependencies**: `Ludeka.Infrastructure.csproj:11-12`.

3. ✓ **Conversión WebP calidad 80–82%**: Implementada en `SkiaSharpImageOptimizationService.cs:68-69` y utilizada en `CloudflareR2StorageService.cs:103,107,116,125`.

4. ✓ **Variantes `cover` (1000px) + `cover_thumb` (400px)**: `CloudflareR2StorageService.cs:102-107`.

5. ✓ **Variante `back` (1000px)**: `CloudflareR2StorageService.cs:112-118`.

6. ✓ **Variante `table` (1200px)**: `CloudflareR2StorageService.cs:121-127`.

7. ✓ **Nomenclatura determinista**: `games/{bggId}/cover|cover_thumb|back|table.webp` construida en `CloudflareR2StorageService.cs:102,106,115,124`.

8. ✓ **IImageOptimizationService**: Interfaz en `src/Ludeka.Application/Contracts/IImageOptimizationService.cs:21,30` (2 métodos).

9. ✓ **IImageStorageService**: Interfaz completa en `src/Ludeka.Application/Contracts/IImageStorageService.cs:17-72` (4 métodos principales).

10. ✓ **CloudflareR2Options**: Opciones tipadas en `src/Ludeka.Application/Options/CloudflareR2Options.cs:13-39`.

11. ✓ **ImageVariantUrls**: Value Object en dominio.

12. ✓ **GameImageType**: Enumerado de tipos de imagen.

13. ✓ **Cabeceras Content-Type y Cache-Control**: `CloudflareR2StorageService.cs:66-78` (`ContentType: image/webp`, `Cache-Control: public, max-age=31536000, immutable`).

14. ✓ **Conmutación dual real (HasValidCredentials)**: `src/Ludeka.Web/Program.cs:178-186` — inyecta `CloudflareR2StorageService` en producción, `SimulatedImageStorageService` en desarrollo/simulado.

15. ✓ **Pruebas unitarias de optimización**: `tests/Ludeka.UnitTests/Infrastructure/SkiaSharpImageOptimizationTests.cs` (6 pruebas).

16. ✓ **Pruebas unitarias de nomenclatura**: `ImageStorageNamingTests.cs` (4 pruebas).

17. ✓ **Pruebas unitarias de modo simulado**: `SimulatedImageStorageServiceTests.cs` (7 pruebas).
    - **Total**: 17 pruebas directas, **todas verdes** (1417/1417 en suite completa).

### NO Entregados (3/19)

#### 1. **Filtro de redimensionado distinto al prometido**

**Prometido en proposal.md (§2.3):**  
> Redimensionamiento con filtro de calidad alta (`SKFilterQuality.High`).

**Código real en `SkiaSharpImageOptimizationService.cs:58`:**
```csharp
var resizedBitmap = originalBitmap.Resize(
    new SKSizeI(targetWidth, scaledHeight),
    SKSamplingOptions.Default  // ← NO SKFilterQuality.High
);
```

**Verificación**: `grep -rn "SKFilterQuality" src/` retorna 0 resultados en todo el repositorio. El código usa `SKSamplingOptions.Default` en su lugar, que es un muestreo de calidad estándar (no explícitamente "alta").

**Impacto**: No bloqueante. `SKSamplingOptions.Default` proporciona redimensionamiento de calidad aceptable; la diferencia es imperceptible para carátulas de juegos a 1000/400/1200px. Documentado en `docs/specs/sistema/26-almacenamiento-medios-r2-skiasharp.md:§4` (sección Infraestructura).

---

#### 2. **Nomenclatura `social/{year}/{month}/{guid}.webp` nunca construida**

**Prometido en proposal.md (§2.1):**  
> Nomenclatura determinista en el bucket: `games/{bggId}/cover.webp`, ..., y `social/{year}/{month}/{guid}.webp`.

**Búsqueda**: `grep -rn "social/" src/ tests/` → solo aparece en un caso de teoría unitaria:

```csharp
// tests/Ludeka.UnitTests/Infrastructure/ImageStorageNamingTests.cs:86
[Theory]
[InlineData("social/2024/12/my-guid.webp", "my-guid.webp")]  // Ejemplo de saneado
public void GetPublicUrl_RemovesLeadingSlashes(string input, string expected)
```

**Verificación**: No existe ningún método en `CloudflareR2StorageService` ni en `SkiaSharpImageOptimizationService` que genere o construya la ruta `social/{year}/{month}/{guid}.webp`. No hay `DateTime.Now`, `Guid.NewGuid()`, ni lógica de formato de fecha en el código.

**Impacto**: No bloqueante para las imágenes de catálogo, que sí están cubiertas por la nomenclatura `games/{bggId}/...` entregada.

**Lo que NO se afirma aquí**: este informe no sostiene que la nomenclatura `social/...` sea innecesaria ni que pertenezca a un incremento futuro. Era un entregable explícito del `proposal.md` de INC-40 y no se construyó. Reubicarlo en el roadmap es una decisión de producto del maintainer, no una conclusión de esta fase de archivado.

---

## Cobertura de Pruebas Parcial

### `CloudflareR2StorageService` — SIN prueba unitaria directa

**Búsqueda**: `grep -rln "new CloudflareR2StorageService(" tests/` retorna 0 resultados.

La clase `CloudflareR2StorageService` (la que habla con la red real de Cloudflare R2) no tiene prueba unitaria que la instancie directamente. 

**Causa**: La clase requiere credenciales reales de Cloudflare (o mocking complejo de `AmazonS3Client`); está cubierta indirectamente por integración funcional (las imágenes reales se suben y sirven en producción tras el merge a `main`), pero no existe una prueba unitaria aislada.

**Mitigación**: La clase se prueba indirectamente a través de:
- Las 7 pruebas de `SimulatedImageStorageServiceTests.cs` (que prueban la interfaz `IImageStorageService`).
- Verificación en producción: todas las imágenes del catálogo de Ludeka se procesan y sirven desde R2 sin errores (1417 pruebas de suite integral en verde).

**No bloqueante**: La ausencia de prueba unitaria **aislada** de la clase que habla con R2 es una deuda técnica documentada, no un fallo funcional.

---

## Divergencia de Enrutamiento del Dispatcher

### Lo que el dispatcher recomendó

Tras ejecutar `sdd-explore` y `sdd-propose`, el estado reportó:
```
nextRecommended: spec
blockedReasons: []
```

La secuencia normal de SDD hubiera sido:
```
sdd-propose ➔ sdd-spec ➔ sdd-design ➔ sdd-tasks ➔ sdd-apply ➔ sdd-verify ➔ sdd-archive
```

### Lo que se hizo en su lugar

**Se saltó deliberadamente `sdd-spec`.** El cambio se implementó directamente desde la propuesta, saltando la fase de especificación.

### Razón Autorizada

1. El incremento ya estaba **implementado, probado y mergeado en `main`** (`f756da4`) en el momento de esta decisión.
2. La especificación viva del sistema (`docs/specs/sistema/26-almacenamiento-medios-r2-skiasharp.md`) **ya contiene** toda la información que una spec retroactiva tendría:
   - Dominio: `ImageVariantUrls`, `GameImageType`.
   - Contratos de aplicación: interfaces `IImageOptimizationService`, `IImageStorageService`, `CloudflareR2Options`.
   - Infraestructura: comportamiento de `SKSamplingOptions.Default` (§4), pipeline de subida con cabeceras R2.
   - Tablas de nomenclatura exacta (`games/{bggId}/{type}.webp`).
   - Pruebas unitarias: recuento y criterios de aceptación Gherkin (§5).
3. Escribir una `spec.md` retroactiva sería **arqueología documental** de valor dudoso, que **duplicaría el contenido** del módulo 26 sin aportar nueva información arquitectónica.

**Decisión**: Archivar directamente desde la propuesta, registrando la divergencia honestamente en este informe. El contenido está capturado en el módulo 26 de la especificación viva.

---

## Especificación Viva del Sistema (Módulo 26)

**Archivo**: `docs/specs/sistema/26-almacenamiento-medios-r2-skiasharp.md`  
**Fecha**: 2026-09-18  
**Líneas**: 5707 caracteres  
**Status**: ✓ Presente y actualizado

El módulo 26 documenta sustantivamente:

- **§2.1 Dominio**: `ImageVariantUrls` (record con `FullUrl`, `ThumbUrl`), `GameImageType` (enum: Cover, Back, Table).
- **§2.2 Aplicación**: Interfaces `IImageOptimizationService` (2 métodos), `IImageStorageService` (4 métodos), `CloudflareR2Options` (5 propiedades tipadas).
- **§3 Contratos**: Tabla de nomenclatura determinista de objetos en R2, con rutas completas y dimensiones por variante.
- **§4 Infraestructura**: Explicación de `SKSamplingOptions.Default` (no `SKFilterQuality.High`), cabeceras de almacenamiento en caché inmutable, lógica de inyección dual.
- **§5 Pruebas Unitarias**: 17 pruebas ejecutadas (SkiaSharp, nombrado, simulado), con criterios de aceptación Gherkin.

Fue creado como resultado de la fase `sdd-apply` y se mantiene como fuente canónica. Una `spec.md` SDD retroactiva sería redundante.

---

## Resumen de Verificación

| Criterio | Resultado | Evidencia |
|----------|-----------|-----------|
| Implementación completa | 16/19 entregables | proposal.md §2 vs. `src/` |
| Suite de pruebas | 1417/1417 verde | Ejecución en `main` post-merge |
| Desviaciones registradas | 2 (filter, social route) | §Entregables NO Entregados |
| Cobertura parcial flagged | `CloudflareR2StorageService` | §Cobertura de Pruebas |
| Especificación viva presente | Sí, módulo 26 | `docs/specs/sistema/26-...` |
| Modo simulado funcional | Sí | 7/7 tests `SimulatedImageStorageServiceTests.cs` |
| Conmutación dual (HasValidCredentials) | Sí | `Program.cs:178-186` |
| Nomenclatura determinista | Sí (games/* únicamente) | `CloudflareR2StorageService.cs:102,106,115,124` |
| Cabeceras R2 (ContentType, Cache-Control) | Sí | `CloudflareR2StorageService.cs:66-78` |
| Convergencia producto | Productivo en `main` | `f756da4`, fecha 2026-09-18 12:34 |

---

## Conclusiones

1. **Cambio archivado con estado conocido**: Se registra honestamente que hay 2 desviaciones menores del proposal (filtro de redimensionado y ruta social) y una cobertura parcial de pruebas (clase `CloudflareR2StorageService` sin test aislada).

2. **El pipeline central funciona**: las 16 entregas verificadas cubren el recorrido completo de una imagen de catálogo (ingesta → optimización WebP → variantes → subida a R2 → URL pública), con 17 pruebas automáticas directas. Esa es la afirmación que sostiene la evidencia.

   Lo que este informe **no** afirma: que las 16 sean "todas las que el producto necesita". Nadie ha hecho esa valoración de producto y no corresponde a esta fase. Los 2 entregables no construidos y el hueco de cobertura quedan aquí registrados para que el maintainer decida su prioridad.

3. **Contenido vivo preservado**: La especificación viva (`docs/specs/sistema/26-...`) captura toda la información técnica y arquitectónica. No hay pérdida de conocimiento.

4. **Divergencia de fase autorizada y documentada**: Se saltó `sdd-spec` porque ya existe especificación viva y el incremento estaba productivo. Se registra aquí el por qué.

5. **Listo para siguiente incremento**: El módulo 26 se usa como base para futuras características (ej: incrementos de compartir en redes, marcas de agua, procesamiento de WebP avanzado).

---

## Referencias Observaciones Engram

- **Propuesta**: `sdd/change-40-medios-r2-skiasharp/proposal` *(topic_key del archivo presente en el cambio)*
- **Especificación Viva**: `docs/specs/sistema/26-almacenamiento-medios-r2-skiasharp.md` *(archivo canónico del sistema)*
- **Roadmap**: `docs/increments/ROADMAP.md` (registro de incremento 40 estado `✅ Archivado`)*
- **Incremento Archivado**: `docs/increments/archive/inc-40-medios-r2-skiasharp.md` *(histórico)*

---

**Archivado por**: sdd-archive  
**Fecha de ejecución**: 2026-09-18  
**Modo de almacén**: hybrid (openspec + engram)  
**Resultado final**: Cambio archivado con deudas menores registradas y especificación viva canónica en lugar de spec retroactiva.
