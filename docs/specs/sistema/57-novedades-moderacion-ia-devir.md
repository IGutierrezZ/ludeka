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

### 3.1 Exclusión de Secciones «En Desarrollo», Juegos de Rol y Meses Pasados
- **Descarte de secciones preliminares y de rol:** Se excluyen los bloques clasificados bajo `EN DESARROLLO`, `JUEGOS DE ROL`, `ROL`, `RPG`, suplementos o manuales (`Libro básico`, `Pantalla`).
- **Filtro de meses pasados:** Se descartan secciones cuyo mes sea anterior al mes en curso (`< DateOnly(currentYear, currentMonth, 1)` en UTC), garantizando que solo se procesen lanzamientos vigentes o futuros.
- **Prevención de duplicados espurios:** Si una sección incluye productos detallados con precio, se omite el análisis secundario de tarjetas de cuadrícula para evitar capturar fragmentos de texto como títulos (`Autor:`, `Ilustrador:`).
- **Lista negra estricta de tokens de título:** Validación que descarta cadenas que no representan títulos de juegos de mesa.

### 3.2 Captura de Enlaces de Producto y Galería Completa
- Si la tarjeta del juego en Devir incluye un enlace a su ficha detallada (`https://devir.es/<slug>`), se captura como `SourceUrl`.
- **Galería oficial enriquecida:** El extractor descarga e inspecciona la ficha de producto analizando el bloque JSON `mage/gallery/gallery` del script Magento 2:
  - Carátula 3D (`face3d`): carátula principal.
  - Fotografía en mesa y componentes (`components1`): `TableImageUrl`.
  - Contraportada oficial (`backflat`): `BackCoverImageUrl`.
  - Portada plana 2D (`frontflat`): recurso secundario.
  - EAN-13 y PVP oficial del producto.

### 3.3 Extracción del Catálogo General de Devir
- Métodos `ExtractCatalogPageAsync(page)` y `ParseCatalogPageHtml(html)` para paginar el catálogo (`https://devir.es/catalogo/juegos-de-mesa?p={page}`), extrayendo productos con enlace, título, carátula e indicador de siguiente página.

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
- **Novedades Enlazadas:** Si existe coincidencia local, la novedad se guarda como `Status = Published`. Además, si la novedad incluye carátula 3D (`face3d`), fotografía en mesa o contraportada oficial, se actualizan los medios del juego en catálogo (`matchedGame.UpdateImages(...)` y `matchedGame.UpdateMediaUrls(...)`).
- **Novedades Sin Enlace Inmediato:** Se consultan en paralelo o en lote mediante `IReleaseAiMatcherService` con un timeout defensivo de 4 segundos por llamada (con fallback inmediato al emparejador heurístico) para prevenir bloqueos de circuito o desconexiones de WebSocket en Cloud Run, y se persisten inmediatamente como `Status = PendingModeration` con su propuesta IA en la bandeja de moderación.
- **Fuentes Oficiales Estrictas e Independientes:** La sincronización de editoriales se circunscribe a Devir y Maldito Games, ejecutándose de forma aislada para que eventuales errores en una no impidan la ejecución de las demás.

### 5.2 Barrido Autónomo de Galería de Catálogo (`DevirImagesBackfillJobRunner`)
- Job autónomo registrado en `Ludeka.Jobs` (`devir-images-backfill`).
- Recorre sistemáticamente las páginas de `https://devir.es/catalogo/juegos-de-mesa?p={page}` bajo concesión de ventana.
- Cruza en memoria contra el catálogo completo de Ludeka por EAN y título normalizado.
- Si el juego coincide y carece de alguna de sus tres vistas principales (caja 3D, mesa o contraportada), descarga su ficha oficial, extrae la galería de alta resolución y actualiza la entidad `Game`.
- Si el juego ya cuenta con todas sus imágenes completas, omite la consulta de red a la ficha individual.
- **Ritmo Cortés y Tolerancia a Fallos (INC-138):** Incorpora un retardo preventivo de 750 ms entre páginas consecutivas para evitar la activación del WAF/rate-limiting de Cloudflare en entornos de centros de datos (GCP Cloud Run), y tolera fallos HTTP aislados (permitiendo hasta 2 páginas fallidas consecutivas antes de detener el recorrido).

### 5.3 Resiliencia HTTP y Desacoplamiento de Portada en Maldito Games (`MalditoReleasesExtractor`, INC-138)
- **Cabeceras de Navegación Humana:** Configura peticiones HTTP enriquecidas (`Accept`, `Accept-Language`, `sec-ch-ua`, `User-Agent` de navegador de escritorio) y erradica colisiones o duplicidades en cabeceras.
- **Reintento Educado:** Aplica un único reintento diferido (1.500 ms) ante respuestas HTTP 403, 429 o fallos 5xx transitorios.
- **Aislamiento de Portada frente a Catálogo Magento:** La descarga de lanzamientos de la portada y del catálogo cronológico se ejecuta de forma desacoplada; si el catálogo pesado falla o agota el tiempo de espera, la portada se procesa íntegramente y sus más de 40 novedades son enviadas con éxito al sincronizador y la bandeja de moderación.

### 5.4 Despliegue Automatizado de Jobs en CI/CD (INC-138)
- El job `devir-images-backfill` está integrado formalmente en la matriz de despliegue continuo de Cloud Run Jobs en `.github/workflows/ci-cd.yml`, garantizando su actualización y aprovisionamiento en Google Cloud en cada entrega a `main`.

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
- **Clasificación en Galería:** En el carrusel polaroid del héroe, las imágenes se identifican contextualmente como «Caja 3D», «Portada», «En mesa» o «Contraportada».
- **Fotos Personales de Análisis:** Se integran automáticamente en la galería las fotografías personales asociadas al veredicto fundador (`_foundingVerdict.Photos`), permitiendo acceder a imágenes reales de partidas y análisis propios sin alterar las imágenes de catálogo.

---

## 8. Verificación y Cobertura de Pruebas

La arquitectura ha sido verificada mediante pruebas automáticas exhaustivas en `tests/Ludeka.UnitTests`:
- `WeeklyReleaseDomainTests.cs`: 5 pruebas que verifican las transiciones de estado, invariantes y asignación de propuestas IA.
- `DevirReleasesExtractorTests.cs`: 10 pruebas de exclusión de secciones en desarrollo, rol, meses pasados, captura de enlaces de producto, detección de cajas 3D, galería completa, paginación de catálogo y reintentos ante 403.
- `MalditoReleasesExtractorTests.cs`: 8 pruebas que cubren extracción de portada, catálogo Magento, reintentos y tolerancia a fallos desacoplados en catálogo sin pérdida de lanzamientos de portada.
- `GeminiReleaseMatcherServiceTests.cs`: 9 pruebas que validan el asistente IA, parsing de respuestas estructuradas y fallback determinista ante errores.
- `EditorialReleasesSyncServiceTests.cs`: 21 pruebas de sincronización instantánea, detección de cajas 3D, enriquecimiento de mesa/contraportada, aislamiento por editorial y envío a moderación.
- `WeeklyReleaseServiceTests.cs`: 14 pruebas de filtrado público, consulta de pendientes, aprobación con/sin BGG ID y rechazo.
- `DevirImagesBackfillJobRunnerTests.cs`: 5 pruebas del trabajo autónomo de barrido de imágenes, omisión de fichas ya completas, recorrido multipágina, ritmo cortés y tolerancia a fallos transitorios.
- `LudekaJobsCompositionTests.cs`: 1 prueba de composición del contenedor de trabajos verificando la exposición de los 17 runners oficiales.
- `NewsPageContractTests.cs`: 6 pruebas de contrato UI para la visibilidad de la pestaña de moderación según roles y botones de acción.

**Total de la suite tras la incorporación del módulo y robustecimiento (INC-138):** 2.749 pruebas unitarias verificadas al 100% en verde (2.759 totales con integración).
