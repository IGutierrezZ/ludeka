# 57. Sincronización No Bloqueante de Editoriales, Bandeja de Moderación con IA y Jerarquía de Cajas 3D (Devir / Maldito)

## 1. Propósito y Resumen

Este módulo establece el flujo desacoplado y tolerante a fallos para la sincronización de novedades oficiales de editoriales (Maldito Games y Devir), eliminando los bloqueos por consultas directas a la API de BGG durante la extracción. Introduce una bandeja de moderación comparativa asistida por IA (Gemini) para aquellas novedades que no coinciden de forma inmediata con el catálogo local de Ludeka, implementa filtros deterministas en Devir contra lanzamientos tentativos «En desarrollo», captura enlaces canónicos a fichas de producto y eleva las imágenes de caja 3D (`face3d`) como carátulas prioritarias en catálogo y fichas de juego.

---

## 2. Dominio y Estados de Novedades (`WeeklyRelease`)

### 2.1 Enumeración de Estados (`WeeklyReleaseStatus`)
Se formaliza el ciclo de vida de una novedad editorial para garantizar que el público general solo visualice novedades confirmadas:

```csharp
public enum WeeklyReleaseStatus
{
    Published = 0,
    PendingModeration = 1,
    Rejected = 2
}
```

### 2.2 Propiedades de Dominio en `WeeklyRelease`
Se añaden a la entidad `WeeklyRelease` los campos necesarios para la moderación y el enriquecimiento asistido por IA:
- `WeeklyReleaseStatus Status`: Estado actual de la novedad.
- `int? SuggestedBggId`: Identificador BGG inferido por el asistente de IA.
- `string? SuggestedTitle`: Nombre canónico inferido por la IA para su búsqueda.
- `string? AiReasoning`: Explicación sintética y fundamentada del asistente sobre la propuesta.
- `DateTime? ModeratedAt`: Marca temporal de resolución de la novedad.
- `string? ModeratedBy`: Identificador o nombre del moderador que resolvió el registro.

### 2.3 Métodos de Transición de Dominio
- `MarkPendingModeration(int? suggestedBggId, string? suggestedTitle, string? aiReasoning)`: Envía la novedad a moderación con la propuesta de la IA.
- `Approve(int? bggGameId, string? moderatedBy)`: Publica la novedad, vinculándola opcionalmente a un juego local importado o existente.
- `Reject(string? moderatedBy)`: Descarta la novedad e impide su visibilidad pública.

---

## 3. Extractor Oficial de Devir (`DevirReleasesExtractor`)

### 3.1 Exclusión de Secciones «En Desarrollo» y Validación de Fecha
- **Descarte de secciones preliminares:** Se excluyen los bloques clasificados bajo `EN DESARROLLO JUEGOS DE MESA` o `EN DESARROLLO JUEGOS DE ROL`.
- **Exigencia de mes de calendario:** Para evitar anuncios etéreos, solo se aceptan títulos adscritos a una sección con mes definido en castellano (ej. «ABRIL», «MAYO») o fecha concreta. Si el encabezado carece de mes o es indefinido, el lanzamiento se ignora.

### 3.2 Captura de Enlaces de Producto
- Si la tarjeta del juego en Devir incluye un enlace a su ficha detallada (`https://devir.es/<slug>`), se captura como `productUrl`, evitando enlaces genéricos a la página de próximos lanzamientos.

### 3.3 Priorización de Caja 3D (`face3d`)
- El extractor detecta imágenes en perspectiva 3D (`face3d` o con indicadores 3D) en una ventana de proximidad acotada ($\le 400$ caracteres de distancia del producto).
- La URL de la caja 3D se establece como `CoverImageUrl` principal, manteniendo el resto de capturas fotográficas (en mesa, 2D, contraportada) como imágenes secundarias.

---

## 4. Asistente IA de Enlace BGG (`IReleaseAiMatcherService`)

### 4.1 Contrato del Asistente
```csharp
public interface IReleaseAiMatcherService
{
    Task<AiReleaseMatchResultDto> MatchReleaseAsync(
        string title,
        string publisher,
        string? description = null,
        string? ean = null,
        CancellationToken cancellationToken = default);
}
```

### 4.2 Implementación `GeminiReleaseMatcherService` con Fallback Determinista
- Utiliza Google Gemini API con structured outputs o prompting estricto para predecir:
  1. Si se trata efectivamente de un juego de mesa o expansión (descartando merchandising o películas).
  2. El BGG ID más probable si se conoce con alta certidumbre.
  3. El título canónico normalizado para búsquedas.
  4. El razonamiento técnico de la propuesta.
- **Fallback Determinista Seguro:** Ante fallos de conectividad, cuotas de API o claves no configuradas, el servicio nunca interrumpe el flujo; genera un resultado determinista a partir del título limpio de la editorial para permitir la revisión humana en moderación.

---

## 5. Sincronización No Bloqueante (`EditorialReleasesSyncService`)

### 5.1 Desacoplamiento de BGG en Tiempo Real
- Para eliminar la latencia acumulada (hasta 140 llamadas secuenciales a BGG con demoras de 1.200 ms), el sincronizador realiza únicamente cruces locales instantáneos en base de datos:
  1. Coincidencia directa por EAN-13.
  2. Coincidencia directa por slug normalizado (`NormalizeTitle`).
  3. Coincidencia por título base limpio (`ExtractBaseTitle`).
- **Novedades Enlazadas:** Si existe coincidencia local, la novedad se guarda como `Status = Published`. Además, si la novedad incluye una caja 3D (`face3d`), se actualiza la carátula principal del juego existente en catálogo (`matchedGame.UpdateImages(...)`).
- **Novedades Sin Enlace Inmediato:** Se consultan en paralelo o en lote mediante `IReleaseAiMatcherService` y se persisten inmediatamente como `Status = PendingModeration` con su propuesta IA, dejándolas listas en la bandeja de moderación sin bloquear el hilo de sincronización.
- **Fuentes Oficiales Estrictas:** La sincronización de editoriales se circunscribe exclusivamente a los extractores de Devir y Maldito Games.

---

## 6. Bandeja de Moderación y Visualización Pública (`WeeklyReleaseService` y `News.razor`)

### 6.1 Filtrado Público
- Todas las consultas de novedades destinadas a los usuarios (`GetReleasesAsync`, vistas públicas) filtran de forma estricta por `Status == WeeklyReleaseStatus.Published`.

### 6.2 Bandeja de Moderación en `/novedades`
- Accesible únicamente a usuarios autenticados con permiso `CanApproveMedia` (administradores y moderadores).
- Presenta una pestaña interactiva «Pendientes (N)» que muestra tarjetas comparativas:
  - Título, editorial, fecha estimada y carátula del producto propuesto por la editorial.
  - Título propuesto por la IA, BGG ID sugerido con enlace directo a BoardGameGeek y razonamiento de la IA en caja estilizada.
  - Input interactivo editable para que el moderador confirme o modifique el BGG ID antes de aprobar.
  - Botón «Aprobar y publicar»: Si se especifica BGG ID y el juego no existe localmente, se importa automáticamente en segundo plano mediante `IBggImportService` y se asocia la novedad al juego, pasando a `Published`.
  - Botón «Descartar»: Pasa la novedad a `Rejected`, archivándola fuera de la vista de moderación y del público.

---

## 7. Jerarquía de Imágenes y Fotos Personales en Ficha (`GameDetail.razor`)

- **Prioridad Visual:** Las imágenes con caja 3D (`face3d`) se priorizan como carátula principal del juego tanto en catálogo como en el héroe de la ficha de detalle.
- **Clasificación en Galería:** En el carrusel polaroid del héroe, las imágenes se identifican contextualmente como «Caja 3D», «Portada» o «En mesa».
- **Fotos Personales de Análisis:** Se integran automáticamente en la galería las fotografías personales asociadas al veredicto fundador (`_foundingVerdict.Photos`), permitiendo acceder a imágenes reales de partidas y análisis propios sin alterar las imágenes de catálogo.

---

## 8. Verificación y Cobertura de Pruebas

La arquitectura ha sido verificada mediante pruebas automáticas exhaustivas en `tests/Ludeka.UnitTests`:
- `WeeklyReleaseDomainTests.cs`: 5 pruebas que verifican las transiciones de estado, invariantes y asignación de propuestas IA.
- `DevirReleasesExtractorTests.cs`: Pruebas de exclusión de secciones en desarrollo, meses no definidos, captura de enlaces de producto y detección de cajas 3D.
- `GeminiReleaseMatcherServiceTests.cs`: 9 pruebas que validan el asistente IA, parsing de respuestas estructuradas y fallback determinista ante errores.
- `EditorialReleasesSyncServiceTests.cs`: 19 pruebas de sincronización instantánea, detección de cajas 3D, actualización de carátulas en catálogo y envío a moderación.
- `WeeklyReleaseServiceTests.cs`: 14 pruebas de filtrado público, consulta de pendientes, aprobación con/sin BGG ID y rechazo.
- `NewsPageContractTests.cs`: 6 pruebas de contrato UI para la visibilidad de la pestaña de moderación según roles y botones de acción.

**Total de la suite tras la incorporación del módulo:** 2.675 pruebas unitarias verificadas al 100% en verde.
