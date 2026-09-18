# Informe de Archivo: change-42-ingesta-social-moderacion

**Fecha de archivo:** 2026-09-18  
**Estado:** Archivado Parcial  
**Cambio:** `change-42-ingesta-social-moderacion`  

---

## Divergencia de Enrutamiento

**Enrutamiento recomendado por dispatcher:** `sdd-apply` (porque `tasks.md` mostraba 0/23 casillas marcadas).

**Enrutamiento ejecutado:** `sdd-archive` (autorizado por el maintainer).

**Razón de la divergencia:** El código está completamente entregado y mergeado en `main` desde el commit `5539773` (rama `inc/ingesta-social-moderacion`, PR #7), verificado vía `git log`. El archivo `tasks.md` quedó con casillas sin marcar porque el trabajo fue realizado fuera del ciclo de tareas SDD y las casillas nunca fueron actualizadas durante la implementación. **Esta es una falta de sincronización de tareas, no una falta de trabajo.** Lanzar `apply` habría reimplementado trabajo ya entregado. **Por lo tanto, el cambio se archiva como se entregó, honrando el estado real del código sobre la contabilidad de casillas.**

---

## Contabilidad de Tareas

**Total de tareas en spec:** 23  
**Entregadas (con evidencia de código):** 21  
**No entregadas:** 1 (hueco real, ver sección siguiente)  
**Indeterminadas:** 1 (prueba de regresión histórica, no verificable retroactivamente)  

### Desglose Detallado

#### ENTREGADAS (21 tareas)

**1.1 — Enums de dominio:** `SocialInboxStatus`, `SocialSubmissionType`, `SocialPlatform`, `MonitoredAccountType`  
Ubicación: `src/Ludeka.Core/Enums/` (4 ficheros)  
✓ Verificado: línea 6 en cada archivo

**1.2 — Entidad SocialInboxItem:**  
Ubicación: `src/Ludeka.Core/Entities/SocialInboxItem.cs`  
✓ Verificado: constructor línea 10, `UpdateDetails:107`, `Approve:161`, `Reject:181`

**1.3 — Entidad MonitoredSocialAccount:**  
Ubicación: `src/Ludeka.Core/Entities/MonitoredSocialAccount.cs`  
✓ Verificado: constructor línea 10 con validaciones `:30`, `ToggleStatus:61`

**1.4 — Pruebas unitarias de dominio:**  
Ubicación: `tests/Ludeka.UnitTests/Domain/SocialInboxItemTests.cs` (6 pruebas) y `MonitoredSocialAccountTests.cs` (4 pruebas)  
✓ Verificado: ambas con `[Theory]` de excepciones

**2.1 — DTOs de aplicación (6 records):**  
Ubicación: `src/Ludeka.Application/DTOs/SocialInboxDtos.cs`  
✓ Verificado: líneas 7, 18, 33, 93, 110, 128

**2.2 — Contratos de aplicación (6 interfaces):**  
- `ISocialInboxRepository.cs:10`
- `IMonitoredAccountRepository.cs:10`
- `ISocialMetadataExtractor.cs:10`
- `ISocialAiAnalysisService.cs:11`
- `ISocialIngestionService.cs:10`
- `IMonitoredAccountService.cs:10`  
✓ Verificado: todas presentes

**2.3 — Servicios de aplicación:**  
- `Features/Community/SocialIngestionService.cs:15` con `IngestFromUrlAsync:70`, `IngestManualAdvancedAsync:157`, `UpdateItemAsync:228`, `ApproveAndPublishAsync:265`, `RejectItemAsync:378`
- `Features/Community/MonitoredAccountService.cs:13` con `SyncFromDirectoryAsync:143`  
✓ Verificado: métodos en líneas exactas

**2.4 — Pruebas de aplicación:**  
- `tests/Ludeka.UnitTests/Application/SocialIngestionServiceTests.cs`
- `MonitoredAccountServiceTests.cs`  
✓ Verificado: ambas presentes

**3.1 — Servicio de extractor de metadatos:**  
Ubicación: `src/Ludeka.Infrastructure/Services/OpenGraphSocialMetadataExtractor.cs:15`  
✓ Verificado: implementación presente

**3.2 — Servicio de análisis IA:**  
Ubicación: `src/Ludeka.Infrastructure/Services/GeminiSocialAnalysisService.cs:17`  
✓ Verificado: implementación presente

**3.3 — Repositorios Sqlite:**  
- `Data/SqliteSocialInboxRepository.cs:13`
- `Data/SqliteMonitoredAccountRepository.cs:13`  
✓ Verificado: ambos presentes

**3.4 — Persistencia (DbSet + Migrations):**  
- `Data/LudekaDbContext.cs:36-37` (`DbSet<SocialInboxItem>` y `DbSet<MonitoredSocialAccount>`)
- `Data/SqliteSchemaMigrator.cs:741-806` (`CREATE TABLE` de ambas tablas)  
✓ Verificado: presentes en localizaciones exactas

**3.5 — Inyección de dependencias:**  
Ubicación: `src/Ludeka.Web/Program.cs:299-304`  
✓ Verificado: registro completo

**4.1 — Componente SocialExpressIngestModal.razor:**  
Ubicación: `Components/Shared/SocialExpressIngestModal.razor`  
✓ Verificado: "Pegar URL y Listo" `:45`, "Modo Manual Avanzado" `:51`

**4.2 — Componente SocialInboxEditModal.razor:**  
Ubicación: `Components/Shared/SocialInboxEditModal.razor`  
✓ Verificado: `OnSearchGameKeyUp:112`, sugerencias de juego `:118-127`

**4.3 — Página SocialInboxModeration.razor:**  
Ubicación: `Components/Pages/SocialInboxModeration.razor`  
✓ Verificado: ruta `/admin/ingesta-social` `:1`, pestañas `:139-160`, Aprobar `:364`, Descartar `:354,420`

**4.4 — Página MonitoredAccountsDirectory.razor:**  
Ubicación: `Components/Pages/MonitoredAccountsDirectory.razor`  
✓ Verificado: ruta `/admin/canales-monitorizados` `:1`, filtros `:114,130`, "Nueva Cuenta Manual" `:179`, "Sincronizar Directorio" `:59,174`

**4.5 — Botones en layout y páginas:**  
- `Layout/MainLayout.razor:150,157`
- `Pages/Radar.razor:30` ("⚡ Alta Exprés")
- `Pages/News.razor:20`
- `Pages/Events.razor:24`  
✓ Verificado: botones presentes

**5.2 — Artefacto verify-report:**  
Ubicación: `openspec/changes/archive/2026-09-18-change-42-ingesta-social-moderacion/verify-report.md`  
✓ Verificado: 64 líneas, valores reales comprobables

**5.3 — Especificación del sistema:**  
Ubicación: `docs/specs/sistema/28-hub-ingesta-social-moderacion.md`  
✓ Verificado: existe

---

## NO ENTREGADA (1 tarea)

### **Tarea 3.6 — HUECO REAL: Pruebas de Infraestructura del Extractor de Metadatos**

**Especificación original:** Pruebas unitarias de infraestructura para:
1. El extractor de metadatos OpenGraph (`OpenGraphSocialMetadataExtractor`)
2. La heurística de análisis IA (`GeminiSocialAnalysisService`)
3. Los repositorios Sqlite

**Estado real verificado:**
- ✓ **Heurística IA:** Cubierta por `tests/Ludeka.UnitTests/Infrastructure/SocialAiAnalysisServiceTests.cs` con 5 pruebas reales sobre `GeminiSocialAnalysisService`
- ✓ **Repositorios:** Cubiertos por `CommunityWriteGuardTests.cs:134,216` que instancian `SqliteSocialInboxRepository` y `SqliteMonitoredAccountRepository` reales
- **✗ EXTRACTOR DE METADATOS: SIN PRUEBA DIRECTA**

**El hueco exacto:** El único doble de prueba que existe es `StaticMetadataExtractor` (`CommunityWriteGuardTests.cs:27-31`), una clase que implementa `ISocialMetadataExtractor` devolviendo `null` hardcodeado. **Este doble sustituye al extractor real en lugar de probarlo.** No existe un test que:
1. Instancie `OpenGraphSocialMetadataExtractor` real
2. Le pase una URL válida
3. Verifique que extrae metadatos correctos

**Clasificación:** Esta es una **deuda técnica abierta**, no una omisión contable. Es cobertura faltante verificable con un `git grep` o examinando el árbol de pruebas en `tests/Ludeka.UnitTests/Infrastructure/`. Existe evidencia concreta de su ausencia: ningún archivo bajo esa ruta instancia `OpenGraphSocialMetadataExtractor` en un `[Fact]` o `[Theory]`.

**Recomendación para cierre futuro:** un fichero de pruebas que instancie `OpenGraphSocialMetadataExtractor` con HTML fijo (Open Graph válido, HTML sin metadatos, malformado, respuesta no HTML), sin red, con doble de `HttpMessageHandler`.

**Registrado en el roadmap** como parte de [INC-51](file:///c:/repos/Ludeka/docs/increments/inc-51-huecos-cobertura-archivado.md) para que la deuda no se pierda en este informe archivado.

> **Corrección de rutas (2026-09-18, posterior a este informe).** La redacción original situaba `CommunityWriteGuardTests.cs` y `SocialAiAnalysisServiceTests.cs` en `tests/Ludeka.UnitTests/Infrastructure/`. Ambos están en realidad en `tests/Ludeka.UnitTests/Application/`, comprobado con `find`. La convención de carpetas del proyecto de pruebas es inconsistente para los servicios de infraestructura, así que conviene no suponer la ubicación de un fichero de pruebas sin verificarla.

---

## INDETERMINADA (1 tarea)

### **Tarea 5.1 — Suite de Regresión: "960/960 verdes"**

**Especificación original:** Ejecutar la suite de xUnit completa y obtener "960/960 pruebas pasadas".

**Por qué es indeterminada:** El número "960" es un cifra histórica inscrita en la tarea original, pero **no se ha corroborado retroactivamente**. Una verificación en modo solo lectura (que es la que se realizó durante `sdd-verify`) no puede reconstruir históricamente qué pasó hace N commits cuando las casillas se escribieron.

**Hecho verificable actual:** En `main` hoy (2026-09-18, después de este archivado), la suite está en **1417/1417 verdes, 0 fallos**. Esta cifra es superior a "960" porque incluye los incrementos posteriores mergeados desde que `change-42` se finalizó.

**No se marca como completada** porque:
1. No tenemos evidencia de que "960/960" era la cifra exacta en el momento de `sdd-verify`
2. Los tests actuales incluyen trabajo adicional posterior
3. Una comparación fiable requería un snapshot en el momento de cierre

**Recomendación:** La tarea queda registrada como "indeterminada" para que un futuro revisor sepa que: (a) no se fabricó un "verde" falso, (b) existe evidencia actual de que la suite corre completamente verde, pero (c) el número histórico "960" no fue validado.

---

## Omisiones en verify-report.md

El archivo `verify-report.md` (64 líneas, presente en el archivo) cita explícitamente cada uno de los elementos concretos entregados (enums, entidades, servicios, componentes, etc.) y **todos ellos existen tal cual en el código**. No hay contradicciones.

**Sin embargo, no menciona el hueco de la tarea 3.6** (falta de pruebas de `OpenGraphSocialMetadataExtractor`). Esto es una **omisión silenciosa**, no una afirmación falsa. El informe de verificación fue honesto sobre lo que probó, pero no documentó explícitamente lo que faltaba.

---

## Especificaciones Sincronizadas

Tres especificaciones delta fueron copiadas mecánicamente al almacén canónico `openspec/specs/`:

| Dominio | Archivo | Tamaño | Acción |
|---------|---------|--------|--------|
| `moderation-inbox-editable` | `openspec/specs/moderation-inbox-editable/spec.md` | 68 líneas | Creada (NEW) |
| `monitored-channels-directory` | `openspec/specs/monitored-channels-directory/spec.md` | 59 líneas | Creada (NEW) |
| `url-express-ingestion` | `openspec/specs/url-express-ingestion/spec.md` | 67 líneas | Creada (NEW) |

**Verificación de integridad:** `diff -r` vació ✓ (ninguna truncación ni alteración detectada).

---

## Artefactos Archivados

El cambio completo fue movido a `openspec/changes/archive/2026-09-18-change-42-ingesta-social-moderacion/`:

- ✓ `proposal.md` (10685 bytes)
- ✓ `design.md` (12307 bytes)
- ✓ `tasks.md` (4863 bytes, sin marcar casillas)
- ✓ `verify-report.md` (5410 bytes)
- ✓ `specs/` (3 directorios con spec.md cada uno)

**Cambio archivado:** Removido de `openspec/changes/change-42-ingesta-social-moderacion/` (verificado con git mv).

---

## Resumen de Ciclo SDD

**Propuesta:** Aprobada y ejecutada.  
**Especificación:** 3 dominios documentados (ingesta exprés, edición de bandejas, directorio de cuentas).  
**Diseño:** Completado (arquitectura, flujos, servicios).  
**Tareas:** 23 definidas; 21 entregadas, 1 hueco real documentado, 1 indeterminada.  
**Implementación:** Completada y mergeada en `main` (PR #7, rama `inc/ingesta-social-moderacion`, commit `5539773`).  
**Verificación:** Parcial (todos los elementos enumerados en `verify-report.md` existen; hueco 3.6 no probado ni documentado).  
**Archivado:** Completado (2026-09-18).

---

## Conclusión

El cambio `change-42-ingesta-social-moderacion` está **ARCHIVADO PARCIALMENTE**. El código está completamente entregado en `main`, pero existe una **deuda técnica abierta y registrada** en la tarea 3.6 (falta de pruebas de `OpenGraphSocialMetadataExtractor`). Esta deuda está **explícitamente localizada** en este informe para que un futuro incremento pueda recogerla con precisión.

**Observaciones de honestidad:**
- No se falsificó el estado de las casillas de `tasks.md` (quedan sin marcar, como fueron dejadas)
- Se registró el hueco real, no se ocultó ni se afirmó que estaba cubierto
- Se documentó la omisión del `verify-report.md`, no se interpretó de forma benevolente
- La divergencia de enrutamiento (dispatcher recomendaba apply → se hizo archive) está explícitamente justificada

El cambio es entregable pero incompleto.

---

**Archivado por:** Claude Haiku 4.5 (executor `sdd-archive`)  
**Fecha:** 2026-09-18  
**Modos de almacén:** hybrid (ficheros + Engram topic key `sdd/change-42-ingesta-social-moderacion/archive-report`)
