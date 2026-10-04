# INC-107: Ingesta Masiva en Lotes BGG (x20) e IA (x10), Filtrado Inteligente de Expansiones y Saneamiento de Cola Administrativa

> **Estado:** ⏳ En progreso  
> **Fecha:** 2026-10-05  
> **Rama:** `inc/ingesta-expansiones-lotes-ia`  
> **Worktree:** `F:\repos\ludeka-wt\ingesta-expansiones-lotes-ia`  
> **Documento Vivo ODD:** `odd/tasks/inc-107-ingesta-expansiones-lotes-ia.md`  

---

## 1. Contexto y Objetivos

1. **Eficiencia en Ingesta Nocturna:**
   - La catalogación nocturna procesaba hasta ahora los títulos pendientes uno a uno.
   - Migración a lotes de 20 para llamadas HTTP a BGG (`/xmlapi2/thing?id=...&versions=1`) y lotes de 10 para síntesis estructurada con Gemini Flash.
   - Aumento del cupo diario a 400 juegos/noche.
2. **Cribado de Expansiones Anti-Promos:**
   - Filtrado en 2 niveles: descarte léxico de promos/accesorios y validación de umbral de tracción comunitaria (`usersrated >= 30` / `owned >= 100`) o comercial (EAN / editorial española).
3. **Saneamiento Editorial de `/admin/cola-catalogacion`:**
   - Retirada de botones de un solo uso o parches de incrementos anteriores.
   - Claridad en el flujo operativo de administración.
