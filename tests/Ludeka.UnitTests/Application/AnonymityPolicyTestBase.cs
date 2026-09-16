using System;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// Base de las pruebas de la política de anonimia (INC-46, F4): contexto SQLite en memoria con un
/// juego sembrado, identidad de sesión sustituible y utilidades para exigir que una operación
/// denegada no deja rastro alguno en la base de datos.
/// </summary>
public abstract class AnonymityPolicyTestBase : IAsyncLifetime
{
    protected SqliteConnection Connection { get; private set; } = null!;

    protected LudekaDbContext Context { get; private set; } = null!;

    /// <summary>Juego sembrado en el catálogo al que apuntan las escrituras con identidad.</summary>
    protected Game Game { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Connection = new SqliteConnection("Filename=:memory:");
        await Connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(Connection)
            .Options;

        Context = new LudekaDbContext(options);
        await Context.Database.EnsureCreatedAsync();

        Game = CreateGame(bggId: 9001, spanishTitle: "Juego de Prueba");
        Context.Games.Add(Game);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    public async Task DisposeAsync()
    {
        await Context.DisposeAsync();
        await Connection.DisposeAsync();
    }

    protected static Game CreateGame(int bggId, string spanishTitle) => new(
        bggId: bggId,
        originalTitle: spanishTitle,
        spanishTitle: spanishTitle,
        designer: "Diseñadora de Prueba",
        publisher: "Editorial Local",
        yearPublished: 2024,
        coverImageUrl: "https://example.test/portada.jpg",
        thumbnailUrl: "https://example.test/miniatura.jpg",
        description: "Juego sembrado para las pruebas de anonimia.",
        bggRating: 7.5,
        bggRank: 120,
        ludistRating: 0.0,
        confrontation: ConfrontationType.Competitive,
        style: GameStyle.Eurogame,
        isOfficialSolo: false,
        age: new AgeRating(10, 10),
        language: LanguageDependence.None,
        footprint: TableFootprint.StandardTable,
        duration: new GameDuration(30, 60, 15),
        scalability: [new ScalabilityEntry(2, "2", ScalabilityStatus.Recommended)]);

    /// <summary>Identidad sin sesión: sin usuario, sin roles y sin permisos.</summary>
    protected static StubCurrentUserService Anonymous() => StubCurrentUserService.Anonymous();

    /// <summary>Identidad con sesión iniciada y el identificador indicado.</summary>
    protected static StubCurrentUserService WithSession(
        string userId = "sesion-real-1",
        string userName = "Usuario con Sesión") => StubCurrentUserService.WithSession(userId, userName);

    /// <summary>Identidad con privilegios declarados pero sin sesión.</summary>
    protected static StubCurrentUserService PrivilegedWithoutSession() => StubCurrentUserService.PrivilegedWithoutSession();
}
