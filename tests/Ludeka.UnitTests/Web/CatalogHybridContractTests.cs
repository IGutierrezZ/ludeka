using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ludeka.Application.DTOs;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas unitarias y de contrato para el catálogo híbrido (INC-116).
/// Valida la frase conversacional mad-lib reactiva, la serialización bidireccional de parámetros URL,
/// la gestión de chips activos de filtros, y el marcado accesible del panel drawer y controles del catálogo.
/// </summary>
public class CatalogHybridContractTests
{
    private const string HomeCatalogPath = "src/Ludeka.Web/Components/Pages/Home.razor";
    private const string CatalogSentenceFilterPath = "src/Ludeka.Web/Components/Shared/CatalogSentenceFilter.razor";

    #region CatalogFilterState Unit Tests

    [Fact]
    public void CatalogFilterState_PlayersSentenceLabel_ShouldFormatCorrectly()
    {
        var state = new CatalogFilterState();
        Assert.Equal("cualquier número", state.PlayersSentenceLabel);

        state.TogglePlayerCount(1);
        Assert.Equal("1 jugador", state.PlayersSentenceLabel);

        state.TogglePlayerCount(1); // Desmarcar
        state.TogglePlayerCount(4);
        Assert.Equal("4 jugadores", state.PlayersSentenceLabel);

        state.TogglePlayerCount(2); // Ahora 2 y 4
        Assert.Equal("2 o 4 jugadores", state.PlayersSentenceLabel);

        state.TogglePlayerCount(7); // Ahora 2, 4 y 7+
        Assert.Equal("2, 4 o 7+ jugadores", state.PlayersSentenceLabel);
    }

    [Fact]
    public void CatalogFilterState_WeightSentenceLabel_ShouldFormatCorrectly()
    {
        var state = new CatalogFilterState();
        Assert.Equal("cualquiera", state.WeightSentenceLabel);

        state.ToggleComplexity(GameComplexity.Light);
        Assert.Equal("ligera", state.WeightSentenceLabel);

        state.ToggleComplexity(GameComplexity.Heavy);
        Assert.Equal("ligera o dura", state.WeightSentenceLabel);

        state.ToggleComplexity(GameComplexity.Medium);
        Assert.Equal("ligera, media o dura", state.WeightSentenceLabel);
    }

    [Fact]
    public void CatalogFilterState_StyleSentenceLabel_ShouldFormatCorrectly()
    {
        var state = new CatalogFilterState();
        Assert.Equal("cualquiera", state.StyleSentenceLabel);

        state.ToggleStyle(GameStyle.Eurogame);
        Assert.Equal("euro", state.StyleSentenceLabel);

        state.ToggleStyle(GameStyle.Ameritrash);
        Assert.Equal("euro o temático", state.StyleSentenceLabel);

        state.ToggleStyle(GameStyle.PartyGame);
        Assert.Equal("euro, temático o party", state.StyleSentenceLabel);
    }

    [Fact]
    public void CatalogFilterState_TimeSentenceLabel_ShouldFormatCorrectly()
    {
        var state = new CatalogFilterState();
        Assert.Equal("lo que haga falta", state.TimeSentenceLabel);

        state.ToggleMaxDuration(45);
        Assert.Equal("hasta 45 min", state.TimeSentenceLabel);

        state.ToggleMaxDuration(90); // 45 y 90 -> debe tomar el mayor
        Assert.Equal("hasta 90 min", state.TimeSentenceLabel);
    }

    [Fact]
    public void CatalogFilterState_Toggles_ShouldModifySetsAndResetPage()
    {
        var state = new CatalogFilterState { CurrentPage = 5 };

        Assert.True(state.TogglePlayerCount(3));
        Assert.Equal(1, state.CurrentPage);
        Assert.Contains(3, state.PlayerCounts);

        Assert.False(state.TogglePlayerCount(3)); // Alternar de nuevo para quitar
        Assert.DoesNotContain(3, state.PlayerCounts);

        state.CurrentPage = 3;
        Assert.True(state.ToggleComplexity(GameComplexity.Medium));
        Assert.Equal(1, state.CurrentPage);
        Assert.Contains(GameComplexity.Medium, state.Complexities);

        state.CurrentPage = 4;
        Assert.True(state.ToggleType(GameType.Expansion));
        Assert.Equal(1, state.CurrentPage);
        Assert.Contains(GameType.Expansion, state.Types);

        state.CurrentPage = 2;
        Assert.True(state.ToggleFootprint(TableFootprint.SmallTable));
        Assert.Equal(1, state.CurrentPage);
        Assert.Contains(TableFootprint.SmallTable, state.Footprints);

        state.CurrentPage = 2;
        Assert.True(state.ToggleConfrontation(ConfrontationType.Cooperative));
        Assert.Equal(1, state.CurrentPage);
        Assert.Contains(ConfrontationType.Cooperative, state.Confrontations);

        state.CurrentPage = 3;
        Assert.True(state.ToggleLanguage(LanguageDependence.None));
        Assert.Equal(1, state.CurrentPage);
        Assert.Contains(LanguageDependence.None, state.Languages);
    }

    [Fact]
    public void CatalogFilterState_ChipsManagement_ShouldGenerateAndRemoveAccurately()
    {
        var state = new CatalogFilterState();
        state.TogglePlayerCount(2);
        state.ToggleComplexity(GameComplexity.Heavy);
        state.ToggleStyle(GameStyle.Eurogame);
        state.ToggleMaxDuration(60);
        state.ToggleType(GameType.BaseGame);
        state.ToggleFootprint(TableFootprint.SmallTable);
        state.ToggleConfrontation(ConfrontationType.Competitive);
        state.ToggleLanguage(LanguageDependence.None);
        state.SetYearRange(2020, 2024);

        var chips = state.GetActiveChips();
        Assert.Equal(10, chips.Count);

        // Remover un chip de jugadores
        var playerChip = chips.First(c => c.Category == "players");
        state.RemoveChip(playerChip);
        Assert.Empty(state.PlayerCounts);

        // Remover chip de dureza
        var weightChip = chips.First(c => c.Category == "weight");
        state.RemoveChip(weightChip);
        Assert.Empty(state.Complexities);

        // Remover chip de año
        var yearFromChip = chips.First(c => c.Key == "ano_desde");
        state.RemoveChip(yearFromChip);
        Assert.Null(state.MinYear);
        Assert.Equal(2024, state.MaxYear);
    }

    [Fact]
    public void CatalogFilterState_BidirectionalQuerySerialization_ShouldPreserveValues()
    {
        var original = new CatalogFilterState
        {
            SearchTerm = "Dune",
            CurrentPage = 2,
            ViewMode = "list",
            SortBy = GameSortOrder.RatingDesc,
            MinYear = 2018,
            MaxYear = 2023,
            ActivePreset = "familiar"
        };
        original.PlayerCounts.Add(3);
        original.PlayerCounts.Add(4);
        original.Complexities.Add(GameComplexity.Medium);
        original.Styles.Add(GameStyle.Eurogame);
        original.MaxDurations.Add(60);
        original.Types.Add(GameType.BaseGame);
        original.Footprints.Add(TableFootprint.StandardTable);
        original.Confrontations.Add(ConfrontationType.Competitive);
        original.Languages.Add(LanguageDependence.Low);

        var queryDict = original.ToQueryDictionary();

        // Convertir a Dictionary<string, StringValues> para simular QueryHelpers.ParseQuery
        var stringValuesDict = queryDict
            .Where(kvp => kvp.Value != null)
            .ToDictionary(kvp => kvp.Key, kvp => new StringValues(kvp.Value!));

        var reconstructed = new CatalogFilterState();
        reconstructed.FromQueryDictionary(stringValuesDict);

        Assert.Equal("Dune", reconstructed.SearchTerm);
        Assert.Equal(2, reconstructed.CurrentPage);
        Assert.Equal("list", reconstructed.ViewMode);
        Assert.Equal(GameSortOrder.RatingDesc, reconstructed.SortBy);
        Assert.Equal(2018, reconstructed.MinYear);
        Assert.Equal(2023, reconstructed.MaxYear);
        Assert.Equal("familiar", reconstructed.ActivePreset);

        Assert.Equal(new[] { 3, 4 }, reconstructed.PlayerCounts.OrderBy(x => x));
        Assert.Equal(new[] { GameComplexity.Medium }, reconstructed.Complexities);
        Assert.Equal(new[] { GameStyle.Eurogame }, reconstructed.Styles);
        Assert.Equal(new[] { 60 }, reconstructed.MaxDurations);
        Assert.Equal(new[] { GameType.BaseGame }, reconstructed.Types);
        Assert.Equal(new[] { TableFootprint.StandardTable }, reconstructed.Footprints);
        Assert.Equal(new[] { ConfrontationType.Competitive }, reconstructed.Confrontations);
        Assert.Equal(new[] { LanguageDependence.Low }, reconstructed.Languages);
    }

    [Theory]
    [InlineData("got ")]
    [InlineData("Ark Nova ")]
    [InlineData(" The Mind ")]
    public void CatalogFilterState_ToQueryDictionary_ShouldPreserveSpacesInSearchTerm(string termWithSpaces)
    {
        var state = new CatalogFilterState { SearchTerm = termWithSpaces };
        var query = state.ToQueryDictionary();

        Assert.True(query.ContainsKey("q"));
        Assert.Equal(termWithSpaces, query["q"]);

        // Simular deserialización de QueryHelpers
        var stringValuesDict = new Dictionary<string, StringValues>
        {
            ["q"] = new StringValues(query["q"]!)
        };
        var reconstructed = new CatalogFilterState();
        reconstructed.FromQueryDictionary(stringValuesDict);

        Assert.Equal(termWithSpaces, reconstructed.SearchTerm);
    }

    [Fact]
    public void CatalogFilterState_ToCriteria_ShouldMapAllFacets()
    {
        var state = new CatalogFilterState
        {
            SearchTerm = "Terraforming",
            MinYear = 2016,
            MaxYear = 2022,
            SortBy = GameSortOrder.ComplexityDesc
        };
        state.PlayerCounts.Add(1);
        state.Complexities.Add(GameComplexity.Heavy);
        state.Styles.Add(GameStyle.Eurogame);
        state.MaxDurations.Add(120);
        state.Types.Add(GameType.BaseGame);
        state.Footprints.Add(TableFootprint.TableMonster);
        state.Confrontations.Add(ConfrontationType.Competitive);
        state.Languages.Add(LanguageDependence.High);

        var criteria = state.ToCriteria();

        Assert.Equal("Terraforming", criteria.SearchTerm);
        Assert.Equal(new[] { 1 }, criteria.PlayerCounts);
        Assert.True(criteria.PlayerCountsMatchAll);
        Assert.Equal(new[] { GameComplexity.Heavy }, criteria.Complexities);
        Assert.Equal(new[] { GameStyle.Eurogame }, criteria.Styles);
        Assert.Equal(120, criteria.MaxDurationMinutes);
        Assert.Equal(new[] { GameType.BaseGame }, criteria.Types);
        Assert.Equal(new[] { TableFootprint.TableMonster }, criteria.Footprints);
        Assert.Equal(new[] { ConfrontationType.Competitive }, criteria.Confrontations);
        Assert.Equal(new[] { LanguageDependence.High }, criteria.Languages);
        Assert.Equal(2016, criteria.MinYear);
        Assert.Equal(2022, criteria.MaxYear);
        Assert.Equal(GameSortOrder.ComplexityDesc, criteria.SortBy);
    }

    #endregion

    #region CatalogSentenceFilter Markup Contract Tests

    [Fact]
    public void CatalogSentenceFilter_MarkupContract_ShouldIncludeMadLibPillsAndAccessibility()
    {
        var source = ReadSource(CatalogSentenceFilterPath);

        // Frase interactiva mad-lib
        Assert.Contains("Busco un juego para", source, StringComparison.Ordinal);
        Assert.Contains("de dureza", source, StringComparison.Ordinal);
        Assert.Contains("estilo", source, StringComparison.Ordinal);
        Assert.Contains("y que dure", source, StringComparison.Ordinal);

        // Atributos de accesibilidad y foco visible (WCAG 2.2 AA)
        Assert.Contains("aria-label=\"Filtrar por número de jugadores\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Filtrar por dureza\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Filtrar por categoría o estilo\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Filtrar por duración máxima\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-expanded=", source, StringComparison.Ordinal);
        Assert.Contains("focus-visible:ring-2", source, StringComparison.Ordinal);

        // Estilos de píldora interactiva
        Assert.Contains("#FFC145", source, StringComparison.Ordinal);
        Assert.Contains("#FFF8EE", source, StringComparison.Ordinal);
    }

    #endregion

    #region Home Catalog Markup Contract Tests

    [Fact]
    public void HomeCatalog_MarkupContract_ShouldContainGreenHeaderAndHybridComponents()
    {
        var source = ReadSource(HomeCatalogPath);

        // Banda verde (#1B7D52) y etiqueta de pantalla
        Assert.Contains("data-screen-label=\"Catálogo\"", source, StringComparison.Ordinal);
        Assert.Contains("bg-[#1B7D52]", source, StringComparison.Ordinal);

        // Componente de frase conversacional integrado
        Assert.Contains("<CatalogSentenceFilter", source, StringComparison.Ordinal);

        // Botón desplegable '+ Más filtros'
        Assert.Contains("aria-controls=\"panel-filtros-avanzados\"", source, StringComparison.Ordinal);
        Assert.Contains("ActiveFilterCount", source, StringComparison.Ordinal);

        // Panel drawer con las 8 taxonomías completas
        Assert.Contains("id=\"panel-filtros-avanzados\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Panel de filtros avanzados\"", source, StringComparison.Ordinal);

        // Fila de chips de filtros activos con botón para limpiar
        Assert.Contains("GetActiveChips", source, StringComparison.Ordinal);
        Assert.Contains("RemoveChip", source, StringComparison.Ordinal);
        Assert.Contains("Limpiar filtros", source, StringComparison.Ordinal);

        // Modos de vista y paginación con tamaño táctil de 44px
        Assert.Contains("min-h-[44px]", source, StringComparison.Ordinal);
        Assert.Contains("min-w-[44px]", source, StringComparison.Ordinal);
    }

    #endregion

    private static string ReadSource(string relativePath)
    {
        var path = Path.Combine(GetRepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"No se encontró el archivo fuente: {relativePath}");
        return File.ReadAllText(path);
    }

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Ludeka.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
