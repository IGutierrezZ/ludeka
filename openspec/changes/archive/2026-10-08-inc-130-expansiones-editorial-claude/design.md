# Diseño Técnico: INC-130 Pestaña de Expansiones Editorial Limpia

## 1. Arquitectura de Componentes

### 1.1 `GameDetail.razor`
En la sección `_activeTab == "expansiones"`, se simplifica el markup:
```razor
<section id="bloque-04-expansiones-tiendas" class="flex flex-col gap-6" aria-label="Expansiones &amp; Dónde Comprar">
    <div class="flex items-center justify-between gap-3">
        <h2 class="text-xl sm:text-2xl font-black text-[var(--ink)] tracking-tight">Expansiones</h2>
        @if (!Game.IsExpansion && _expansions.Count > 0)
        {
            <span class="py-1 px-3 rounded-full bg-[var(--paper-2)] text-[var(--ink)] font-extrabold text-xs flex items-center gap-1">
                @_expansions.Count @(_expansions.Count == 1 ? "expansión" : "expansiones")
            </span>
        }
        else if (Game.IsExpansion && _sisterExpansions.Count > 0)
        {
            <span class="py-1 px-3 rounded-full bg-[var(--paper-2)] text-[var(--ink)] font-extrabold text-xs flex items-center gap-1">
                @_sisterExpansions.Count @(_sisterExpansions.Count == 1 ? "expansión hermana" : "expansiones hermanas")
            </span>
        }
    </div>

    @if (Game.IsExpansion)
    {
        <ExpansionAporteCard
            ExpansionId="@Game.Id"
            Necessity="@Game.ExpansionNecessity"
            ImpactTags="@Game.ImpactTags"
            WhatItBringsSummary="@Game.WhatItBringsSummary"
            ExtraPlayerCount="@Game.ExtraPlayerCount"
            ExtraDurationMinutes="@Game.ExtraDurationMinutes"
            OnAporteGenerated="HandleExpansionAporteUpdated" />

        @if (_sisterExpansions.Count > 0)
        {
            <div class="pt-2">
                <ExpansionSisterList
                    CurrentExpansionId="@Game.Id"
                    BaseGameTitle="@(_parentGame?.SpanishTitle ?? "Juego Base")"
                    Sisters="@_sisterExpansions"
                    Synergies="@_sisterSynergies" />
            </div>
        }
    }
    else
    {
        <ExpansionEcosystemSection
            BaseGameId="@Game.Id"
            Expansions="@_expansions"
            BaseGameMaxPlayers="@Game.Players.MaxPlayers" />
    }
</section>
```

### 1.2 `ExpansionEcosystemSection.razor`
El componente pasa de ser un sistema de pestañas y mezclador a un presentador editorial limpio:
- Parámetros:
  - `BaseGameId`: identificador del juego base.
  - `Expansions`: lista de `ExpansionSummaryDto`.
  - `BaseGameMaxPlayers`: número máximo de jugadores del juego base para calcular el incremento «hasta X jugadores».
- Estado:
  - `_userInCollectionIds`: conjunto de identificadores de juegos/expansiones en la ludoteca del usuario.
- Interacción con la ludoteca:
  - `HandleToggleLibrary(Guid expansionId)`: añade o retira de la colección sin recargar la página; si no hay sesión, redirige a login preservando la URL.

## 2. Pruebas de Contrato
- Actualización de `GameDetailEditorialBlocksContractTests.cs` para validar la cabecera limpia sin el número 04.
- Actualización de `ExpansionAndMediaCarouselUiContractTests.cs` para verificar la cuadrícula responsive y la retirada del mezclador.
- Actualización de `WebMarkupContractTests.cs` preservando la iconografía Lucide estricta (cero emojis).
