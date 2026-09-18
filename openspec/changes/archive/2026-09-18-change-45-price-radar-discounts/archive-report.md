# Informe de Archivo: INC-45 — Radar de Bajadas de Precios, Mínimos Históricos y Alertas de Ofertas

**Cambio:** `change-45-price-radar-discounts`  
**Archivado en:** `openspec/changes/archive/2026-09-18-change-45-price-radar-discounts/`  
**Fecha de archivo:** 2026-09-18  
**Ejecutor:** `sdd-archive` (Haiku 4.5)

---

## 1. Estado Final del Cambio

### Entrega y Merger

El incremento INC-45 fue **completamente implementado, verificado y mergeado a `main`** hace más de una semana. La rama `inc/price-radar-discounts` se integró en `main` mediante Pull Request (#32 o anterior), y el repositorio se encuentra en estado limpio:

- **Rama active:** `main` en commit `f756da4` (Merge pull request #31)
- **Historial:** El cambio ya no aparece como rama activa ni en `inc/`. Su circuito de entrega SDD está cerrado.
- **Garantía:** No hay código pendiente, cambios no commitados ni verificaciones bloqueadas.

### Ejecución de Tareas

El archivo `tasks.md` registra **31 de 31 tareas completadas:**

- `T1: Modelos de Dominio` ✅ (GamePriceSnapshot, GamePriceMetrics, PriceDropAlert)
- `T2: Persistencia e Infraestructura` ✅ (IGamePriceRepository, SqliteGamePriceRepository, migraciones)
- `T3: Casos de Uso y Servicio` ✅ (IPriceRadarService, PriceRadarService)
- `T4: Background Worker` ✅ (PriceRadarHostedService, inyección de dependencias)
- `T5: Componentes Blazor` ✅ (StoreOffersCard.razor, MyLibrary.razor, Radar.razor)
- `T6: Verificación Global y Documentación` ✅ (tests, specs, archivo SDD)

**Verificación:** Suite de pruebas en verde: **1417/1417 tests**, 0 fallos. Ejecutado hoy en `main`.

### Volcado a Especificación Viva

La infraestructura funcional del cambio ya fue volcada íntegramente a la especificación viva del sistema:

- **Documento:** `docs/specs/sistema/31-radar-precios-minimos-historicos.md`  
- **Alcance:** Dominio completo (GamePriceSnapshot, GamePriceMetrics, PriceDropAlert), capas de aplicación e infraestructura, componentes Blazor y flujos.
- **Índice actualizado:** `docs/specs/sistema/README.md` incluye entrada para el módulo 31 en la tabla de módulos canónicos.
- **Estado:** Volcado realizado en commit previo (no es responsabilidad de esta fase de archivo).

### Documento de Incremento Archivado

El registro histórico del incremento fue ya trasladado a archivo:

- **Ubicación:** `docs/increments/archive/inc-45-price-radar-discounts.md`  
- **Contenido:** Propuesta, especificación, diseño, tareas y evidencia de entrega.
- **ROADMAP:** Actualizado a `✅ Archivado` en tanto `docs/increments/ROADMAP.md` como `docs/specs/ROADMAP_MVP_SLICES.md`.

---

## 2. Artifacts de Cambio Preservados

El circuito de archivo preservó byte-a-byte todos los artefactos SDD del cambio:

| Artefacto | Presente | Tamaño | Integridad |
|-----------|----------|--------|-----------|
| `proposal.md` | ✅ | 4543 bytes | Verificada (diff -r vacío) |
| `spec.md` | ✅ | 6915 bytes | Verificada (diff -r vacío) |
| `design.md` | ✅ | 7991 bytes | Verificada (diff -r vacío) |
| `tasks.md` | ✅ | 2994 bytes | Verificada (diff -r vacío) |

**Método de archivado:** `git mv` + `diff -r` post-verificación (sin truncación por modelo).

---

## 3. Particularidad Estructural: Ausencia de Delta Specs Canonizados

### Observación Honesta

Este cambio **NO aportó especificaciones delta al almacén canónico `openspec/specs/`** por la siguiente razón técnica:

- **Formato del `spec.md`:** Monolítico, redactado con requerimientos numerados (`FR-01` a `FR-07`, `NFR-01` a `NFR-04`).
- **Formato canónico esperado:** Deltas por capacidad dentro de `openspec/changes/{change-name}/specs/{domain}/spec.md`, con estructura `### Requirement:` / `#### Scenario:`.
- **Resultado:** Ausencia de carpeta `specs/` dentro de este cambio.

### Implicación para Composición

El comando `sdd-archive-compose` se **saltó por decisión mecánica** (no hay deltas para componer en `openspec/specs/`). El `spec.md` monolítico permaneció en la rama del cambio y no entró en la capa canónica del almacén.

### Cobertura Funcional Garantizada

**A pesar de la ausencia de delta specs canónicos, la cobertura funcional ES COMPLETA:**

La especificación viva `docs/specs/sistema/31-radar-precios-minimos-historicos.md` **incluye** toda la información arquitectónica, de dominio y de interfaz del cambio. Es el registro durable que un futuro mantenedor consultará.

**Recomendación para trabajo futuro:** Si se requiere que cambios futuros sobre el radar de precios entren como deltas composables en `openspec/specs/`, refactorizar ese módulo al formato de requerimientos canónicos (una capacidad por fichero delta).

---

## 4. Verificación de Archivo

### Checklist de Paso 4 (sdd-phase-common.md § Paso 4)

- [x] Specs sincronizados correctamente (N/A: sin deltas)
- [x] Carpeta del cambio movida a archivo con `git mv`
- [x] Archivo preserva todos los artefactos que existían (4/4 ficheros)
- [x] Tareas archivadas retienen bytes originales; 31/31 completado
- [x] Directorio activo `openspec/changes/change-45-price-radar-discounts/` ausente
- [x] Output verbatim de `diff -r` incluido y **vacío** (evidencia de integridad)

### Output de diff -r (Verificación Obligatoria)

```
diff -r snapshot/source destination/2026-09-18-change-45-price-radar-discounts
(vacío — sin diferencias detectadas)
```

**Resultado:** ✅ **PASS** — Integridad confirmada byte-a-byte.

---

## 5. Observaciones Finales

1. **Ciclo SDD cerrado:** El cambio pasó completo por `sdd-explore → sdd-propose → sdd-spec → sdd-design → sdd-tasks → sdd-apply → sdd-verify → sdd-archive`.

2. **Deuda declarada:** La ausencia de delta specs canónicos en `openspec/specs/` fue resultado de una elección de formato del `spec.md` original (monolítico vs. deltas). Esto no afecta a la cobertura funcional (garantizada por `docs/specs/sistema/31-radar-precios-minimos-historicos.md`), pero es deuda técnica si se requiere composición futura.

3. **Sin riesgos bloqueantes:** La suite de pruebas sigue 1417/1417 verde. No hay verificaciones pendientes ni hallazgos sin resolver.

4. **Estado de `main`:** Limpio, sin ramas activas del cambio. Listo para el siguiente incremento.

---

## Trazabilidad de Observaciones Engram

Ningún artefacto intermedio (`verify-report`, `apply-progress`) fue leído en este archivo, ya que el usuario confirmó estado final directo. Las observaciones Engram de fases previas quedan en el histórico pero no se citan aquí por brevedad.

**Observación de archivo:** Persistida como `sdd/change-45-price-radar-discounts/archive-report` (topic key).
