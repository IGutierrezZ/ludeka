# INC-51: Huecos de Cobertura y Desviaciones Destapados al Archivar INC-40, INC-42 e INC-43

> **Estado:** ✅ Archivado (completado y verificado el 2026-09-26)
> **Fecha de Inicio:** 2026-09-26
> **Rama de Trabajo:** `inc/huecos-cobertura`
> **Worktree:** `C:\repos\ludeka-wt\huecos-cobertura`
> **Dependencias:** INC-40, INC-42 e INC-43 (los tres archivados el 2026-09-18 como *archivado parcial declarado*)
> **Especificación Viva:** [26. Almacenamiento de Medios R2 y SkiaSharp](file:///c:/repos/Ludeka/docs/specs/sistema/26-almacenamiento-medios-r2-skiasharp.md) · [28. Hub de Ingesta Social y Moderación](file:///c:/repos/Ludeka/docs/specs/sistema/28-hub-ingesta-social-moderacion.md) · [29. Ingesta Continua de Novedades BGG](file:///c:/repos/Ludeka/docs/specs/sistema/29-ingesta-continua-novedades-bgg.md)

---

## 1. Cómo se descubrieron

El 2026-09-18 se cerró el residuo de `openspec/changes/`, que conservaba seis incrementos ya entregados y mergeados en `main` sin archivar. Antes de archivarlos se encargó una verificación independiente tarea por tarea **contra el código real del árbol**, no contra los documentos.

Esa verificación desmintió la hipótesis de partida. Se asumía que el desfase era puro retraso de contabilidad —casillas de `tasks.md` sin marcar sobre trabajo hecho— y en cuatro de los seis lo era. En tres casos apareció trabajo que **nunca se hizo**.

El detalle completo, con su evidencia, vive en los informes de archivado:

- `openspec/changes/archive/2026-09-18-change-40-medios-r2-skiasharp/archive-report.md`
- `openspec/changes/archive/2026-09-18-change-42-ingesta-social-moderacion/archive-report.md`
- `openspec/changes/archive/2026-09-18-change-43-ingesta-continua-bgg/archive-report.md`

Este documento existe para que esos tres huecos **tengan dueño en el roadmap** en lugar de quedar sepultados en informes de cambios archivados. Ninguno es urgente y ninguno bloquea la salida a producción. Pero fueron invisibles durante semanas precisamente por no estar aquí.

## 2. Por qué la verificación original no los vio

Los tres huecos sobrevivieron a una fase `sdd-verify` que se declaró aprobada. El motivo importa más que los huecos:

**Los `verify-report.md` de INC-42 e INC-43 definen sus propios criterios y los aprueban al 100%, pero su alcance es menor que el de la `tasks.md` que verifican.**

- El de INC-43 enumera los criterios RF-01 a RF-07. Su veredicto «100% aprobado» es **cierto para esos siete criterios**, y ninguno de ellos corresponde a la tarea 5.1. La tarea no aparece: no se reivindica como hecha ni se señala como omitida.
- El de INC-42 cita elementos concretos que sí existen en el código, sin contradicción alguna. Simplemente no menciona el hueco de la tarea 3.6. Es una omisión silenciosa, no una afirmación falsa.

Ninguno de los dos miente. Los dos son incompletos por construcción. La lección operativa para futuras verificaciones: **un informe de verificación debe declarar explícitamente su cobertura respecto a la `tasks.md`**, incluyendo qué tareas quedan fuera de sus criterios y por qué.

---

## 3. Hueco 1 — INC-42: `OpenGraphSocialMetadataExtractor` sin ninguna prueba

**Tarea de origen:** INC-42, tarea 3.6. Pedía pruebas de infraestructura para el extractor de metadatos, la heurística de IA y los repositorios.

**Estado real verificado:**

| Componente de la tarea 3.6 | Estado |
|---|---|
| Heurística de IA | **Cubierta** — `tests/Ludeka.UnitTests/Application/SocialAiAnalysisServiceTests.cs`, 5 pruebas reales sobre `GeminiSocialAnalysisService` |
| Repositorios | **Cubiertos** — `tests/Ludeka.UnitTests/Application/CommunityWriteGuardTests.cs:134,216` instancian `SqliteSocialInboxRepository` y `SqliteMonitoredAccountRepository` reales |
| Extractor de metadatos | **SIN PRUEBA** |

El extractor real vive en `src/Ludeka.Infrastructure/Services/OpenGraphSocialMetadataExtractor.cs:15`. Ningún test del repositorio lo instancia.

Lo único que existe es `StaticMetadataExtractor` (`tests/Ludeka.UnitTests/Application/CommunityWriteGuardTests.cs:27-31`), un doble de prueba que devuelve `null` y **sustituye** al extractor en lugar de probarlo. Es exactamente lo contrario de cobertura: garantiza que el código de parseo de Open Graph nunca se ejecuta durante la suite.

**Riesgo concreto:** el extractor es el punto de entrada del flujo «Pegar URL y Listo» del alta exprés. Un cambio en su parseo de etiquetas Open Graph —o una regresión al tocar `HttpClient`, la codificación o el manejo de respuestas parciales— no rompería ninguna prueba.

**Trabajo propuesto:** un fichero de pruebas del extractor con HTML fijo como entrada. Casos mínimos a cubrir: etiquetas Open Graph completas, etiquetas ausentes o parciales, `og:title` sin `og:image`, HTML malformado, y respuesta no HTML. Sin red: con doble de `HttpMessageHandler`, como ya hacen los recolectores de INC-44.

Sobre dónde colocarlo, ojo: **la convención de carpetas del proyecto de pruebas es inconsistente para los servicios de infraestructura.** `SkiaSharpImageOptimizationService` vive en `Infrastructure/` y sus pruebas también, pero `GeminiSocialAnalysisService` vive igualmente en `Infrastructure/` y sus pruebas están en `Application/`. Conviene decidir la ubicación al proponer, no darla por supuesta.

**Decisión abierta:** ninguna. Es cobertura que falta y que la tarea 3.6 ya pedía.

---

## 4. Hueco 2 — INC-43: el dataset simulado de BGG no llega a 2025/2026

**Tarea de origen:** INC-43, tarea 5.1. Pedía actualizar el dataset simulado de BGG con lanzamientos de 2025/2026, para que el descubrimiento de novedades tuviera datos que encontrar en modo simulado.

**Estado real verificado:**

- `grep` de «2025» y «2026» en todo `src/Ludeka.Infrastructure/Bgg`: **cero coincidencias**.
- El año de publicación más alto de todo `src/Ludeka.Infrastructure/Bgg/BggSimulationDataset.cs` (952 líneas) es **2022**.
- `git log` confirma que el último commit que tocó `SimulatedBggClient.cs` y `BggSimulationDataset.cs` es `a9b61bb`, de **INC-28**: anterior y ajeno a INC-43.

**Consecuencia concreta:** `BggDiscoveryService` clasifica cada hallazgo comparando su año de publicación con el actual y el inmediatamente anterior (`src/Ludeka.Application/Features/Bgg/BggDiscoveryService.cs:174-175`). Si el año es reciente asigna `CatalogQueueOrigin.BggNewReleases`; si no, `BggHotness`.

Con el dataset parado en 2022, **la rama `BggNewReleases` no se activa nunca en modo simulado**. La lógica existe y está probada con dobles en `tests/Ludeka.UnitTests/Bgg/BggDiscoveryServiceTests.cs`, pero el recorrido de punta a punta —lote nocturno, descubrimiento, clasificación, badge en la cola de catalogación— no se puede ejercitar sin conexión real a BGG.

**Trabajo propuesto:** añadir al dataset simulado títulos con año de publicación del ejercicio en curso y el anterior. Conviene hacerlo con años **relativos a la fecha de ejecución**, no fijos: un dataset con «2025» codificado a mano reproduce el mismo problema en 2028. Merece una prueba que recorra el flujo completo en modo simulado y compruebe que aparece al menos un elemento con origen `BggNewReleases`.

**Decisión abierta:** si el dataset debe calcular los años de forma relativa o si se acepta fijarlos y revisarlos por temporada.

---

## 5. Hueco 3 — INC-40: dos desviaciones del proposal y una clase sin prueba

De los 19 entregables concretos que promete el `proposal.md` de INC-40, la verificación confirmó **16 entregados íntegros**. Los otros tres son estos.

### 5.1 Filtro de redimensionado distinto al prometido

El `proposal.md` enuncia `SKFilterQuality.High`. El código usa `SKSamplingOptions.Default` (`src/Ludeka.Infrastructure/Services/SkiaSharpImageOptimizationService.cs:58`); `grep -rn "SKFilterQuality" src/` devuelve cero resultados.

Merece verificarse si es una decisión deliberada —`SKFilterQuality` está marcado como obsoleto en versiones recientes de SkiaSharp, y `SKSamplingOptions` es su sustituto— o un descuido. Si fue deliberada, **lo que falta es actualizar el `proposal.md` archivado o dejar constancia del cambio**, no tocar el código. El módulo 26 de la especificación viva ya documenta el valor real, así que la deriva está solo entre el proposal y el código.

**Decisión abierta:** confirmar si `SKSamplingOptions.Default` es la elección correcta en calidad de reescalado, o si conviene un modo de muestreo explícito de mayor calidad para las variantes grandes.

### 5.2 Nomenclatura `social/{year}/{month}/{guid}.webp` nunca construida

El `proposal.md` la promete como convención del bucket. No existe ningún generador de esa ruta en `src/`. La única aparición del literal en el repositorio es un valor de ejemplo dentro de un caso de `Theory` que prueba el saneado de barras de `GetPublicUrl` (`tests/Ludeka.UnitTests/Infrastructure/ImageStorageNamingTests.cs:86`), no una convención producida por ningún servicio.

El módulo 26 de la especificación viva tampoco la menciona, lo que es coherente con que nunca se construyera.

**Decisión abierta, y es de producto:** era un entregable explícito de INC-40 y no se hizo. Hay que decidir si se implementa, si se reubica en un incremento posterior cuando exista una funcionalidad que suba imágenes de origen social, o si se retira como alcance descartado. Lo que no debe pasar es quedarse sin decidir: la nomenclatura del bucket es difícil de cambiar una vez hay objetos subidos en producción.

### 5.3 `CloudflareR2StorageService` sin prueba unitaria propia

`grep -rln "new CloudflareR2StorageService(" tests/` devuelve cero resultados. La clase que habla con la red real de Cloudflare (`src/Ludeka.Infrastructure/Services/CloudflareR2StorageService.cs`) no tiene ninguna prueba.

Sí están cubiertos su hermano simulado (`SimulatedImageStorageServiceTests.cs`, 7 pruebas), la optimización con SkiaSharp (`SkiaSharpImageOptimizationTests.cs`, 6) y el nombrado determinista (`ImageStorageNamingTests.cs`, 4). Lo que queda sin probar es la composición concreta de esa clase: las claves del bucket que construye, las cabeceras `ContentType` y `Cache-Control` que fija (`:66-78`) y los anchos por variante que pasa al optimizador (`:102-127`).

**Trabajo propuesto:** probarla con un doble de `IAmazonS3`, verificando la clave generada, las cabeceras y el ancho por variante. Sin tocar la red.

**Decisión abierta:** ninguna. Es cobertura que falta.

---

## 6. Alcance y criterios de aceptación

Este documento **no** decide el diseño ni el orden. Es un registro para que los tres huecos no se pierdan.

Antes de ejecutarlo hay que resolver, en la fase de propuesta:

1. **Si se parte.** Los huecos 1 y 5.3 son cobertura mecánica sin decisiones. El 2 y el 5.2 tienen decisiones abiertas. Puede convenir entregar primero la cobertura y dejar las decisiones de producto para un incremento aparte.
2. **Qué hacer con 5.2**, que es la única decisión con coste de reversión real.
3. **Si la lección de la sección 2** —que un `verify-report.md` declare su cobertura respecto a `tasks.md`— se recoge aquí o en una mejora aparte del flujo SDD.

Criterios de aceptación, para lo que se decida entregar:

- Los componentes que hoy no tienen prueba pasan a tenerla, sin red, con dobles.
- Ninguna prueba nueva sustituye al componente que dice probar. El patrón `StaticMetadataExtractor` es el antipatrón exacto a no repetir.
- La suite completa sigue en verde. Punto de partida: **1417/1417, 0 fallos** (`dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj`, verificado el 2026-09-18 sobre `main` en `aee1685`).
- Toda desviación que se acepte en lugar de corregirse queda declarada por escrito, con su motivo.

## 7. Fuera de alcance

- **La deuda de formato de las specs canónicas.** `openspec/specs/` tiene 40 de 54 specs en prosa numerada sin encabezados `### Requirement:`, y esa es la causa que bloqueó el archivado de INC-49 y que volverá a bloquear cualquier delta futura que toque una de ellas. Conviene abordarla de forma perezosa —una spec por incremento, cuando una delta la toque— y no en un barrido mecánico. No pertenece a este incremento.
- **Las deudas nominales de INC-49**: el smoke test manual (tarea 4.5, que arrastra INC-50) y el WARNING de `ExternalLoginEvents.HandleTicketReceivedAsync`, que no captura `UnauthorizedAccessException` en la rama de vinculación.
- **La deriva de `AuditAction`** en el módulo 14 de `docs/specs/sistema/`, heredada de INC-46.
- **`RunScheduledCollectionAsync`** de INC-44: existe en el contrato real pero no en su `design.md` ni en el módulo 30 de la especificación viva. Es deriva documental menor, sin hueco de código.

---

## 8. Resolución Final y Cierre de INC-51 (2026-09-26)

Tras el análisis arquitectónico del estado actual del repositorio y código fuente, se dictaminó la resolución de los puntos pendientes, completando la cobertura de pruebas unitarias faltantes y declarando cerradas las desviaciones históricas obsoletas:

1. **Hueco 1 (`OpenGraphSocialMetadataExtractor`):**
   - **Resuelto con código y pruebas:** Se implementó la suite completa en `tests/Ludeka.UnitTests/Infrastructure/OpenGraphSocialMetadataExtractorTests.cs` (19 pruebas en verde).
   - **Corrección de defecto descubierta:** Se corrigió el patrón regex de atributos OpenGraph para soportar de forma segura comillas anidadas (evitando truncados de títulos que contenían comillas simples en atributos delimitados por comillas dobles, habituales en publicaciones de Instagram) y la extracción robusta del autor ignorando el sufijo `on/en Instagram`.

2. **Hueco 2 (`BggSimulationDataset` sin lanzamientos 2025/2026):**
   - **Cerrado formalmente sin cambio de código:** La lógica de clasificación según fecha (`CatalogQueueOrigin.BggNewReleases` vs `BggHotness`) ya se encuentra 100% cubierta con dobles en `BggDiscoveryServiceTests.cs`. En staging y producción (INC-53), la ingesta utiliza el volcado diario real de BGG (`bg_ranks.csv`), por lo que alterar los 40 juegos clásicos del mock falsearía datos sin aportar valor real.

3. **Hueco 3.1 (`SKSamplingOptions` vs `SKFilterQuality`):**
   - **Cerrado como obsoleto:** `SKFilterQuality` es una API obsoleta en versiones recientes de SkiaSharp; el código en `SkiaSharpImageOptimizationService.cs` ya utiliza la API canónica moderna `SKSamplingOptions.Default` (adecuadamente documentada en el módulo 26 de la especificación viva).

4. **Hueco 3.2 (Nomenclatura de bucket `social/{year}/...`):**
   - **Cerrado como superado por diseño superior:** INC-42 implementó un patrón determinista y trazable ligado al agregado (`$"social-inbox/{itemId:N}/thumbnail.webp"` en `SocialIngestionService.cs:449`), haciendo innecesaria y contraproducente la ruta propuesta originalmente en INC-40.

5. **Hueco 3.3 (`CloudflareR2StorageService` sin prueba unitaria propia):**
   - **Resuelto con pruebas:** Se implementó la suite completa en `tests/Ludeka.UnitTests/Infrastructure/CloudflareR2StorageServiceTests.cs` (28 pruebas en verde), cubriendo subidas optimizadas, verificación de cabeceras S3 (`Cache-Control: public, max-age=31536000, immutable`), variantes de carátula/trasera/mesa, borrado, rutas de CDN y manejo de excepciones.

**Veredicto de Verificación:** 1.923 pruebas unitarias al 100% en verde (47 pruebas nuevas agregadas, 0 fallos). INC-51 queda formalmente **✅ Archivado**.

