# Incremento 89: Saneamiento Integral de Consultas SQL Restantes, Eliminación de Egress O(N) y Blindaje de Caché de Fichas Públicas

> **ID:** INC-89  
> **Slug:** `opt-consultas-egress-fase2`  
> **Rama:** `inc/opt-consultas-egress-fase2`  
> **Estado:** ⏳ En progreso  
> **Módulos Impactados:** Módulo 01 (`docs/specs/sistema/01-catalogo-base.md`), Módulo 19 (`docs/specs/sistema/19-directorio-editoriales-creadores-tiendas.md`), Módulo 46 (`docs/specs/sistema/46-optimizacion-consultas-egress-cache.md`)  
> **Dependencias:** INC-88.  

---

## 1. Contexto y Diagnóstico

Tras la auditoría forense posterior a INC-88, se identificaron las últimas consultas restantes que incurrían en descargas de tablas completas o colecciones no paginadas:
1. `SqliteGameRepository.GetByPublisherAsync` descargaba incondicionalmente todos los juegos de la base de datos a memoria (`ToListAsync()`) para filtrar por editorial en LINQ to Objects en cada visita a `/editoriales/{slug}`.
2. `SqliteGameRepository.GetGamesPendingQualityBackfillAsync` y `GetGamesPendingQualityBackfillCountAsync` descargaban el catálogo completo para evaluar vacíos de escalabilidad en RAM.
3. `SqliteFoundingVerdictRepository.GetAllAsync` ordenaba en RAM en lugar de en SQL.
4. `CachedPublisherService` no disponía de caché L1 en `GetBySlugAsync` ni `GetByIdAsync`, permitiendo que ráfagas de tráfico o bots golpearan la base de datos en fichas individuales de editorial.

---

## 2. Objetivos Técnicos

1. **Filtrado SQL Nativo en `GetByPublisherAsync`:** Empujar la condición `WHERE` a base de datos mediante `EF.Functions.Like(g.Publisher, ...)` y `EF.Functions.Like(g.SpanishPublisher, ...)`, transfiriendo únicamente los juegos coincidentes.
2. **Acotación de Consultas de Calidad:** Evitar el `ToListAsync` indiscriminado en comprobaciones de calidad del catálogo.
3. **Blindaje de Caché L1 en Fichas de Editorial:** Implementar en `CachedPublisherService` almacenamiento en `IMemoryCache` con TTL de 30 minutos e invalidación reactiva.
4. **Ordenación SQL en Veredictos Fundadores:** Ordenar en base de datos en PostgreSQL.
5. **Preservación del 100% de Pruebas Verdes:** Batería de pruebas automatizadas sin regresiones.
