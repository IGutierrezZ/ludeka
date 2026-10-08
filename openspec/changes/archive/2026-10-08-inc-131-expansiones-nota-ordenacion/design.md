# Diseño Técnico: INC-131 Calificación en Tarjetas de Expansión y Ordenación por Nota

## 1. Capa de Aplicación (`ExpansionService.cs`)

En el método `GetExpansionsForBaseGameAsync`:
```csharp
public async Task<IReadOnlyList<ExpansionSummaryDto>> GetExpansionsForBaseGameAsync(Guid baseGameId, CancellationToken ct = default)
{
    var expansions = await _expansionRepository.GetExpansionsByBaseGameIdAsync(baseGameId, ct);
    return expansions
        .Select(ExpansionSummaryDto.FromEntity)
        .OrderByDescending(e => e.LudistRating > 0 ? e.LudistRating : e.BggRating)
        .ThenByDescending(e => e.BggRating)
        .ThenBy(e => e.SpanishTitle)
        .ToList();
}
```

## 2. Componente Blazor (`ExpansionEcosystemSection.razor`)

En la vista, cada tarjeta incorpora en la fila superior:
```razor
<div class="flex items-center justify-between w-full">
    <span class="text-[11px] font-bold uppercase tracking-wider text-[var(--muted)]">
        @GetNecessitySimpleLabel(exp.Necessity)
    </span>
    @if (GetEffectiveRating(exp) > 0)
    {
        <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-[var(--paper-3)] border border-[var(--rule)]/20 text-xs font-black text-[var(--ink)]">
            <span class="text-[var(--mustard)]">★</span> @GetEffectiveRating(exp).ToString("0.0")
        </span>
    }
</div>
```

Métodos auxiliares:
```csharp
private static double GetEffectiveRating(ExpansionSummaryDto exp) =>
    exp.LudistRating > 0 ? exp.LudistRating : exp.BggRating;

private IEnumerable<ExpansionSummaryDto> SortedExpansions =>
    Expansions?.OrderByDescending(GetEffectiveRating).ThenBy(e => e.SpanishTitle) ?? [];
```

## 3. Pruebas y Validación
- Pruebas unitarias para `ExpansionService` verificando que devuelve las expansiones ordenadas por nota de forma descendente.
- Pruebas de interfaz en `ExpansionAndMediaCarouselUiContractTests` verificando el renderizado de la nota cuantitativa y la ordenación.
