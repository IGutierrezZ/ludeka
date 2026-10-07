# Diseño Técnico: INC-121 — Alineación Editorial de Claude (Parte 1)

## Arquitectura de Componentes

### 1. `StaffBar.razor` Contextual
Para que `StaffBar` sea reactiva ante la página en curso y admita comunicación bidireccional con las páginas (por ejemplo para abrir modales específicos de la página como BGG o Edición de Sorteo), implementamos:
- Detección de ruta mediante `NavigationManager.ToBaseRelativePath(Navigation.Uri)`.
- Disparo de eventos globales o servicio de mediación ligera (`IStaffActionService` o EventCallback en cascading state) si es necesario, o enlaces directos/modales embebidos en el shell.
- Para modales globales de staff (ej. BGG o Alta Exprés), `StaffBar` puede invocar directamente los componentes modales existentes (`BggSearchModal`, `SocialExpressIngestModal`) o emitir un evento que la página activa escuche.

### 2. Formato de Cuentas Atrás (`GiveawayService`)
Modificación del cálculo en `CalculateRemainingTime`:
```csharp
private static string CalculateRemainingTime(DateTimeOffset deadline, bool isExpired)
{
    if (isExpired)
        return "Finalizado";

    var diff = deadline - DateTimeOffset.UtcNow;
    if (diff.TotalHours < 24)
        return "Finaliza hoy";

    var days = (int)Math.Ceiling(diff.TotalDays);
    return days == 1 ? "1 día" : $"{days} días";
}
```

### 3. Numerales en `GameCard.razor`
Estructura DOM:
```html
<div class="relative w-full">
    @if (PositionNumber.HasValue)
    {
        <span class="absolute -top-4 -left-2 z-20 text-3xl sm:text-[42px] font-black tracking-[-0.06em] select-none pointer-events-none leading-none"
              style="color: @colorVar; text-shadow: 0 2px 0 var(--paper, #FFF8EE);">
            @PositionNumber.Value.ToString("00")
        </span>
    }
    <div class="aspect-square w-full rounded overflow-hidden bg-[var(--paper-2)] shadow-[0_12px_24px_var(--shadow)]">
        <!-- img tag -->
    </div>
</div>
```
De este modo, el recorte `overflow-hidden` se aplica estrictamente a la imagen, permitiendo que el número `01`, `02` sobresalga limpiamente con el estilo de Claude.

### 4. Paginación en `Radar.razor`
Tanto para `_giveaways` como para `_topDeals`:
- Introducir `_giveawayPage` y `_giveawayPageSize = 10`.
- Introducir `_dealsPage` y `_dealsPageSize = 10`.
- Renderizar los controles de paginación (`NavPills` de 44px) idénticos al patrón de `Home.razor`.

### 5. Reporte de Sorteos en `GiveawayDetail.razor`
- Modal `ReportModal` con motivo (`Sorteo falso o spam`, `Ya ha terminado`, `Datos incorrectos`, `Otro`) y textarea de comentario.
- Persistencia mediante `IGameReportService` o notificación a moderación con registro de auditoría.
