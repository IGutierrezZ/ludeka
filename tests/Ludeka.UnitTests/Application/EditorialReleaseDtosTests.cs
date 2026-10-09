using System;
using System.Collections.Generic;
using Ludeka.Application.DTOs;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class EditorialReleaseDtosTests
{
    [Fact]
    public void EditorialReleaseItem_InitializesPropertiesCorrectly()
    {
        var item = new EditorialReleaseItem(
            Title: "Daggerheart",
            Publisher: "Devir",
            ReleaseDate: new DateOnly(2027, 1, 1),
            TargetDateText: "2027",
            EstimatedPvp: 49.99m,
            Ean: "8436625615992",
            CoverImageUrl: "https://devir.es/img/dagger.jpg",
            Notes: "En desarrollo",
            SourceUrl: "https://devir.es/proximos-lanzamientos",
            IsReprint: false);

        Assert.Equal("Daggerheart", item.Title);
        Assert.Equal("Devir", item.Publisher);
        Assert.Equal(new DateOnly(2027, 1, 1), item.ReleaseDate);
        Assert.Equal("2027", item.TargetDateText);
        Assert.Equal(49.99m, item.EstimatedPvp);
        Assert.Equal("8436625615992", item.Ean);
        Assert.Equal("https://devir.es/img/dagger.jpg", item.CoverImageUrl);
        Assert.Equal("En desarrollo", item.Notes);
        Assert.Equal("https://devir.es/proximos-lanzamientos", item.SourceUrl);
        Assert.False(item.IsReprint);
    }

    [Fact]
    public void EditorialSyncSummaryDto_InitializesTotalsAndCollections()
    {
        var resultDevir = new EditorialSyncResultDto(
            Publisher: "Devir",
            Success: true,
            ItemsFound: 42,
            ReleasesCreated: 10,
            ReleasesUpdated: 32,
            GamesLinked: 15,
            GamesImported: 5);

        var summary = new EditorialSyncSummaryDto(
            TotalFound: 42,
            CreatedCount: 10,
            UpdatedCount: 32,
            GamesLinkedCount: 15,
            GamesImportedFromBggCount: 5,
            PublisherResults: new List<EditorialSyncResultDto> { resultDevir },
            Errors: Array.Empty<string>());

        Assert.Equal(42, summary.TotalFound);
        Assert.Equal(10, summary.CreatedCount);
        Assert.Equal(32, summary.UpdatedCount);
        Assert.Equal(15, summary.GamesLinkedCount);
        Assert.Equal(5, summary.GamesImportedFromBggCount);
        Assert.Single(summary.PublisherResults);
        Assert.Empty(summary.Errors);
    }

    [Fact]
    public void ArrakisDtos_InitializePropertiesCorrectly()
    {
        var gallery = new ArrakisProductGalleryDto(
            CoverImageUrl: "https://arrakisgames.com/wp-content/uploads/spirit.jpg",
            TableImageUrl: "https://arrakisgames.com/wp-content/uploads/spirit-table.jpg",
            BackCoverImageUrl: null,
            FrontFlatImageUrl: null,
            Ean: "8421005001106",
            Pvp: 84.95m,
            BggId: 162886,
            BggUrl: "https://boardgamegeek.com/boardgame/162886/spirit-island",
            Title: "Spirit Island",
            StatusText: "Reimpresión: Noviembre 2026");

        Assert.Equal("https://arrakisgames.com/wp-content/uploads/spirit.jpg", gallery.CoverImageUrl);
        Assert.Equal("8421005001106", gallery.Ean);
        Assert.Equal(84.95m, gallery.Pvp);
        Assert.Equal(162886, gallery.BggId);
        Assert.Equal("https://boardgamegeek.com/boardgame/162886/spirit-island", gallery.BggUrl);
        Assert.Equal("Spirit Island", gallery.Title);

        var item = new ArrakisCatalogItemDto(
            ProductUrl: "https://arrakisgames.com/spirit-island/",
            Title: "Spirit Island",
            Ean: "8421005001106",
            CoverImageUrl: "https://arrakisgames.com/wp-content/uploads/spirit.jpg",
            BggId: 162886,
            Pvp: 84.95m);

        Assert.Equal("https://arrakisgames.com/spirit-island/", item.ProductUrl);
        Assert.Equal("Spirit Island", item.Title);
        Assert.Equal("8421005001106", item.Ean);
        Assert.Equal(162886, item.BggId);
        Assert.Equal(84.95m, item.Pvp);
    }
}
