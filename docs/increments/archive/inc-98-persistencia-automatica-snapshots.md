# Incremento 98: Persistencia Automática de Snapshots Satélite en Llamadas BGG XMLAPI2 y Métrica de Fallidos en Staging

> **Slug:** `persistencia-automatica-snapshots`  
> **Rama:** `inc/persistencia-automatica-snapshots`  
> **Estado:** ⏳ En progreso  
> **Fecha:** 2026-10-02  

---

## 1. Contexto y Diagnóstico

### 1.1 El problema de la doble consulta a BGG
Durante la inspección de `/admin/cola-catalogacion`, se identificó que cuando el batch nocturno (`NightlyCatalogingService`) cataloga juegos procedentes de la cola comunitaria o del relleno Top de BGG mediante `_bggClient.FetchGameByBggIdAsync`, construye la entidad `Game` y la guarda en la base de datos, pero **no persiste el snapshot satélite en `BggRawSnapshots`**.

Como consecuencia:
- La tabla satélite de payloads crudos quedaba desfasada tras cada ciclo de catalogación nocturna o importación comunitaria.
- Para llenar esos huecos se requería una sincronización secundaria posterior (`BggRawSnapshotSyncService` / `BggRawBackfillJobRunner`) que volvía a consultar a la API de BGG (`/xmlapi2/thing`), consumiendo cuota HTTP de forma redundante y demorando el volcado.

### 1.2 El descuadre visual en métricas de Staging
En la tarjeta de Staging de `/admin/cola-catalogacion`, la cuadrícula muestra:
- `Total Staging`: 17.603
- `Pend. Thing`: 0
- `Pend. Fotos R2`: 0
- `Pend. IA Lote`: 0
- `Listos Promo`: 0
- `Promovidos`: 17.393

La diferencia de 210 títulos corresponde a registros con fallos (`FailedCount` en `BggStagingMetricsDto`), pero la interfaz omite dicha tarjeta, dando la falsa impresión de números que no cuadran ($17.393 + 210 = 17.603$).

---

## 2. Objetivos Técnicos

1. **Auto-Persistencia Transparente en `BggXmlApiClient`:**
   - Inyectar de forma desacoplada y opcional `IBggRawSnapshotRepository` en `BggXmlApiClient`.
   - Cada vez que se obtenga un XML de Thing (individual o multi-ID), convertir cada `<item>` con `BggXmlToJsonConverter.ConvertToJson(item)` y guardarlo de forma idempotente en `BggRawSnapshots` vía `_snapshotRepo.UpsertAsync`.
   - Manejar cualquier error de persistencia de forma no bloqueante (resiliencia) para que un problema de base de datos satélite no impida devolver el juego al llamador.

2. **Registro en Inyección de Dependencias:**
   - Configurar `LudekaServiceCollectionExtensions.cs` para inyectar `IBggRawSnapshotRepository` en el scoped `IBggClient`.
   - Dar soporte a `SimulatedBggClient` para pruebas simuladas si procede.

3. **Métrica de Fallidos en UI de Staging:**
   - Añadir la tarjeta «Fallidos» en la cuadrícula de métricas de Staging en `CatalogQueueAdmin.razor`, visualizando `_stagingMetrics.FailedCount` con contraste de color apropiado (resaltado solo si es > 0).

4. **Suite de Pruebas Automáticas:**
   - Pruebas unitarias de auto-persistencia en `BggXmlApiClient`.
   - Pruebas de resiliencia no bloqueante ante excepciones en el repositorio satélite.
   - Verificación de no-regresión en la suite completa de pruebas.

---

## 3. Criterios de Aceptación

- [ ] Toda llamada HTTP a BGG XMLAPI2 que descargue datos de `thing` almacena automáticamente su `BggRawSnapshot` en la tabla satélite.
- [ ] La catalogación nocturna y la cola comunitaria dejan los snapshots creados sin necesidad de sincronización posterior.
- [ ] La interfaz de moderación de catálogo refleja explícitamente los títulos fallidos de staging cuadrando la suma total ($17.393 + 210 = 17.603$).
- [ ] Todas las pruebas unitarias pasan en verde al 100%.
