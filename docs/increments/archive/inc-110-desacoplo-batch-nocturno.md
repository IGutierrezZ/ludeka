# INC-110: Desacoplo en Segundo Plano del Batch Nocturno y Auto-Recuperación de Bloqueos en Cola

> **Estado:** ⏳ En progreso  
> **Fecha de Inicio:** 2026-10-05  
> **Tipo:** Resiliencia / Arquitectura / Background Tasks  
> **Rama:** `inc/desacoplo-batch-nocturno`  
> **Worktree:** `F:\repos\ludeka-wt\desacoplo-batch-nocturno`

---

## 1. Contexto y Diagnóstico del Problema

Al ejecutar el lote nocturno de 400 títulos desde el panel de administración (`/admin/cola-catalogacion`), el proceso se ejecutaba síncronamente en el hilo del circuito interactivo de Blazor Server. Al requerir más de 5 minutos, la conexión alcanzaba el timeout de 300 segundos de Google Cloud Run / SignalR, matando el proceso de forma abrupta.

Como consecuencia:
1. La bitácora en base de datos quedaba permanentemente con `Status = "Running"`.
2. El último bloque de títulos encolados quedaba congelado en `Status = "Processing"`, impidiendo que consultas posteriores los retomasen.
3. El moderador no podía elegir el tamaño del lote (ej. 40 títulos) para completar cuotas parciales sin disparar otros 400.

---

## 2. Solución

1. **Auto-recuperación de bloqueos huérfanos:**
   - Incorporación de `RecoverStaleProcessingToPendingAsync` en `IPendingBggImportRepository` y `SqlitePendingBggImportRepository`.
   - Limpieza automática de logs atascados en `Running` al arrancar un nuevo ciclo.
2. **Desacoplo del botón a segundo plano (`Task.Run`):**
   - El botón web delega el trabajo a una tarea de fondo con un `IServiceScope` independiente y `CancellationTokenSource`, liberando el hilo web inmediatamente.
   - Sondeo periódico (`Timer`) que actualiza la UI sin bloquear el circuito.
3. **Selector interactivo de cupo:**
   - Control de tamaño de lote en el panel para procesar exactamente el número deseado (40, 50, 100, 200, 400 títulos).
