# INC-102: Barrido y Auditoría Integral de Calidad de Catálogo desde Snapshots Locales de BGG

> **Incremento:** INC-102  
> **Estado:** ⏳ En progreso  
> **Rama:** `inc/barrido-calidad-snapshots-locales`  
> **Worktree:** `F:\repos\ludeka-wt\barrido-calidad-snapshots-locales`  
> **Artefactos SDD:** `openspec/changes/2026-10-02-change-102-barrido-calidad-snapshots-locales/`

---

## 1. Motivación y Diagnóstico

1. **Bucle Infinito en «Pendientes Sin Votos»:** La acción en `/admin/cola-catalogacion` selecciona repetidamente los mismos títulos que no tienen votos comunitarios en BGG, porque la consulta en repositorio carece de cursor y no avanza monotónicamente.
2. **Desconexión con Snapshots Locales:** A pesar de contar con 17.505 snapshots de BGG almacenados en base de datos (100% de cobertura), el enriquecedor de catálogo sigue recurriendo a llamadas HTTP individuales con retardos de 1,5 segundos.

---

## 2. Solución de Arquitectura

1. **Reconstitución XML desde JSON:** Conversor determinista `BggJsonToXmlConverter` para alimentar directamente a `BggXmlParser` en memoria.
2. **Estrategia Snapshot-First en `BggMassIngestionService`:** Evaluar snapshots satélite antes de salir a internet, permitiendo auditar y sanear ADN lúdico, tiempos y escalabilidad a velocidad de CPU/memoria.
3. **Paginación por Cursor en Pendientes:** Integrar cursor `afterBggId` en `GetGamesPendingQualityBackfillAsync` para evitar bucles infinitos.
4. **Ejecución en CLI (`Ludeka.Jobs`):** Runner desatendido para ejecutar el barrido completo de ~17.505 títulos en minutos.
