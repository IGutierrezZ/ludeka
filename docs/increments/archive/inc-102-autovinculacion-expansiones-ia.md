# INC-102: Auto-vinculación Inmediata de Expansiones en Búsqueda Asistida y Síntesis Asistida de Aporte con IA

> **Estado:** ✅ Archivado  
> **Fecha:** 2026-10-02  
> **Pruebas:** 2.300 unitarias (100% pasando) + 10 integración  
> **Rama:** `inc/autovinculacion-expansiones-ia`  
> **Worktree:** `F:\repos\ludeka-wt\autovinculacion-expansiones-ia`  

---

## 1. Contexto y Diagnóstico

Al añadir una expansión desde el buscador asistido de BGG en la ludoteca personal (`BggSearchModal` / `MyLibrary`), se identificaron dos carencias:
1. **Ausencia de auto-vinculación en ingesta bajo demanda:** `BggSearchAssistedService.AddGameToCollectionAsync` descarga el juego y lo clasifica como `GameType.Expansion`, pero no consulta el snapshot satélite para resolver `inboundBaseBggId` ni asigna `BaseGameId`, dejando la expansión huérfana hasta que se ejecute una reconciliación masiva posterior. Tampoco vincula expansiones huérfanas en catálogo cuando se añade su juego base.
2. **Tarjeta de aporte vacía por falta de estado vacío y metadatos editoriales:** La sección *«¿Qué aporta al Juego Base?»* en `ExpansionAporteCard.razor` consume metadatos editoriales de Ludeka (`WhatItBringsSummary`, `ExpansionNecessity`, `ImpactTags`). Al ser importada directamente desde BGG (que no ofrece estos datos), el componente se renderiza en blanco sin advertir que el contenido está pendiente de curación.

---

## 2. Solución Arquitectónica

1. **Auto-vinculación bidireccional en `BggSearchAssistedService`:**
   - Inyección de `IBggRawSnapshotRepository`.
   - Si se añade una expansión: extracción del ID entrante de su snapshot crudo y asignación inmediata de `SetBaseGameId(baseGame.Id)` si el juego base ya está en el catálogo.
   - Si se añade un juego base: extracción de enlaces salientes de expansiones y vinculación de expansiones huérfanas existentes en catálogo.
   - Si se asocia a la colección una expansión ya catalogada pero huérfana, reintentar oportunistamente su vinculación.
2. **Síntesis Asistida de Aporte con IA (`IAiGameSummaryService`):**
   - Nuevo DTO `ExpansionAporteAiDto` (`WhatItBringsSummary`, `Necessity`, `ImpactTags`, deltas de jugadores y duración).
   - Métodos `GenerateExpansionAporteAsync` y `EnsureExpansionAporteAsync` en `GeminiGameSummaryService` con salida estructurada JSON de Gemini Flash.
   - Motor de respaldo `HeuristicExpansionAporteGenerator` (Zero-Crash Fallback) para modo simulación y resiliencia ante contingencias de red/API.
   - Generación automática durante la ingesta asistida de nuevas expansiones.
3. **Estado Vacío y Acción Interactiva en `ExpansionAporteCard.razor`:**
   - Estado vacío cuidado estéticamente con icono `sparkles`, aviso pedagógico de contenido pendiente de curación y botón interactivo «✨ Generar Aporte con IA».
   - Refresco reactivo en `GameDetail.razor` sin recarga de página.

---

## 3. Pruebas y Criterios de Aceptación

- Batería de pruebas unitarias en `BggSearchAssistedServiceTests` cubriendo auto-vinculación entrante, saliente y resiliencia sin juego base.
- Pruebas para `HeuristicExpansionAporteGenerator` y `GeminiGameSummaryService`.
- Pruebas de componente para `ExpansionAporteCard.razor` verificando estado vacío y botón IA.
- Verificación completa de la suite de pruebas unitarias en verde.
