using System;
using System.Collections.Generic;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class GameGalleryImageTests
{
    private static Game CreateSampleGame()
    {
        return new Game(
            bggId: 1001,
            originalTitle: "Sample Board Game",
            spanishTitle: "Juego de Mesa Ejemplo",
            designer: "Test Designer",
            publisher: "Test Publisher",
            yearPublished: 2024,
            coverImageUrl: "https://cdn.ludeka.es/sample-cover.webp",
            thumbnailUrl: "https://cdn.ludeka.es/sample-thumb.webp",
            description: "Descripción de prueba",
            bggRating: 8.5,
            bggRank: 42,
            ludistRating: 9.0,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new AgeRating(12, 12),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(60, 90, 20)
        );
    }

    [Fact]
    public void Constructor_ConValoresValidos_CreaObjetoCorrectamente()
    {
        var img = new GameGalleryImage("https://cdn.ludeka.es/games/ark-nova/gallery-1.webp", "Despliegue de losetas");

        Assert.Equal("https://cdn.ludeka.es/games/ark-nova/gallery-1.webp", img.Url);
        Assert.Equal("Despliegue de losetas", img.Title);
    }

    [Fact]
    public void Constructor_SinTitulo_AsignaNuloATitulo()
    {
        var img = new GameGalleryImage("https://cdn.ludeka.es/games/ark-nova/gallery-2.webp", "  ");

        Assert.Equal("https://cdn.ludeka.es/games/ark-nova/gallery-2.webp", img.Url);
        Assert.Null(img.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_ConUrlInvalida_LanzaExcepcion(string? invalidUrl)
    {
        Assert.Throws<ArgumentException>(() => new GameGalleryImage(invalidUrl!, "Foto"));
    }

    [Fact]
    public void Constructor_ConTituloDemasiadoLargo_LanzaExcepcion()
    {
        var longTitle = new string('A', 151);
        Assert.Throws<ArgumentException>(() => new GameGalleryImage("https://cdn.ludeka.es/img.webp", longTitle));
    }

    [Fact]
    public void Game_AddGalleryImage_AnadeImagenALaColeccion()
    {
        var game = CreateSampleGame();
        Assert.Empty(game.AdditionalImages);

        game.AddGalleryImage("https://cdn.ludeka.es/extra-1.webp", "Componentes");

        Assert.Single(game.AdditionalImages);
        Assert.Equal("https://cdn.ludeka.es/extra-1.webp", game.AdditionalImages[0].Url);
        Assert.Equal("Componentes", game.AdditionalImages[0].Title);
    }

    [Fact]
    public void Game_RemoveGalleryImage_EliminaPorIndice()
    {
        var game = CreateSampleGame();
        game.AddGalleryImage("https://cdn.ludeka.es/1.webp", "1");
        game.AddGalleryImage("https://cdn.ludeka.es/2.webp", "2");

        Assert.Equal(2, game.AdditionalImages.Count);

        game.RemoveGalleryImage(0);

        Assert.Single(game.AdditionalImages);
        Assert.Equal("https://cdn.ludeka.es/2.webp", game.AdditionalImages[0].Url);
    }

    [Fact]
    public void Game_UpdateAdditionalImages_ReemplazaColeccionFiltrandoElementosVacios()
    {
        var game = CreateSampleGame();
        game.AddGalleryImage("https://cdn.ludeka.es/old.webp", "Vieja");

        var newImages = new List<GameGalleryImage>
        {
            new("https://cdn.ludeka.es/new1.webp", "Nueva 1"),
            new("https://cdn.ludeka.es/new2.webp", "Nueva 2")
        };

        game.UpdateAdditionalImages(newImages);

        Assert.Equal(2, game.AdditionalImages.Count);
        Assert.Equal("https://cdn.ludeka.es/new1.webp", game.AdditionalImages[0].Url);
        Assert.Equal("https://cdn.ludeka.es/new2.webp", game.AdditionalImages[1].Url);
    }

    [Fact]
    public void Game_UpdateAdditionalImages_ConNulo_LimpiaColeccion()
    {
        var game = CreateSampleGame();
        game.AddGalleryImage("https://cdn.ludeka.es/photo.webp");
        Assert.NotEmpty(game.AdditionalImages);

        game.UpdateAdditionalImages(null);
        Assert.Empty(game.AdditionalImages);
    }
}
