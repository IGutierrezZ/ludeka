# Incremento 132: Dureza Numérica BGG, Extrapolación de Complejidad y Ordenación Precisa

- **ID del Incremento:** `INC-132`
- **Slug:** `dureza-numerica-bgg`
- **Rama:** `inc/dureza-numerica-bgg`
- **Fecha:** 2026-10-08
- **Estado:** ⏳ En progreso (ODD / SDD)
- **Épica / Contexto:** Calidad de Datos, Dominio y Experiencia de Usuario en Catálogo.

---

## 1. Descripción del Problema y Objetivos
Actualmente, Ludeka calcula la dureza de los juegos en tiempo de ejecución mediante una heurística basada en duración máxima, edad recomendada y estilo de juego, clasificándolos de forma tosca en un enum de tres valores (`Light`, `Medium`, `Heavy`). El valor real y continuo de peso/complejidad de BoardGameGeek (`averageweight`, escala de 1.00 a 5.00) no se encuentra persistido en la entidad `Game` ni en la base de datos operativa, impidiendo una ordenación por dureza matemáticamente precisa tanto en el catálogo como en la ficha técnica.

### Objetivos Principales:
1. **Persistencia en Dominio (`Ludeka.Core`):** Añadir la propiedad `BggWeight` (`double?`, 1.00 a 5.00) a la entidad `Game`, con validaciones y métodos de actualización.
2. **Función Canónica de Extrapolación:** Extrapolar `BggWeight` a `GameComplexity` (`Light`, `Medium`, `Heavy`) según los umbrales estándar de la afición (< 2.20 Ligero, 2.20 - 3.25 Medio, ≥ 3.25 Duro) con fallback a la heurística previa para títulos sin votos en BGG.
3. **Ingesta y Snapshots (`Ludeka.Infrastructure`):**
   - Extraer `<averageweight value="X.XX" />` dentro de `<statistics><ratings>` en `BggXmlParser`.
   - Generar migración EF Core dual (SQLite y PostgreSQL) incorporando la columna `BggWeight`.
   - Crear un procedimiento/runner de backfill para poblar `BggWeight` en los juegos existentes a partir de los datos crudos ya almacenados en `BggRawSnapshot`.
4. **Ordenación y Filtrado Preciso en Repositorio:** Actualizar `SqliteGameRepository` para ordenar por `BggWeight` continuo en `ComplexityAsc` y `ComplexityDesc`, adaptando las consultas de filtrado.
5. **Presentación Editorial (`Ludeka.Web`):** Mostrar el valor numérico decimal junto a la etiqueta cualitativa en `GameDetail.razor` y componentes relacionados (ej. `2.4 / 5 · Ligero`).
6. **Batería de Pruebas TDD:** Pruebas unitarias de dominio, repositorio, parser y contratos web para garantizar cero regresiones.
