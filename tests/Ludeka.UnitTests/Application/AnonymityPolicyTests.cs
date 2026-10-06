using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Admin;
using Ludeka.Application.Features.Bgg;
using Ludeka.Application.Features.Community;
using Ludeka.Application.Features.Library;
using Ludeka.Application.Features.Plays;
using Ludeka.Application.Features.Reports;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Repositories;
using Ludeka.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// Frontera entre navegación pública y acciones con identidad (INC-46, spec anonymity-policy, F4):
/// sin sesión ninguna escritura con identidad llega a la base de datos, la denegación es controlada
/// (<see cref="UnauthorizedAccessException"/>) y las nueve entidades de dominio que exigen
/// <c>UserId</c> rechazan identificadores vacíos.
/// </summary>
public class IdentityInvariantEntityTests
{
    private static readonly Guid AnyGameId = Guid.NewGuid();
    private static readonly Guid AnyQuestionId = Guid.NewGuid();
    private static readonly DateTimeOffset AnyDate = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GamePlayLog_ShouldRejectEmptyIdentity(string? userId)
        => Assert.Throws<ArgumentException>(() => new GamePlayLog(userId!, AnyGameId, AnyDate, "Casa", playerCount: 2));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UserCollectionItem_ShouldRejectEmptyIdentity(string? userId)
        => Assert.Throws<ArgumentException>(() => new UserCollectionItem(userId!, AnyGameId, CollectionStatus.InCollection));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UserGameReview_ShouldRejectEmptyIdentity(string? userId)
        => Assert.Throws<ArgumentException>(() => new UserGameReview(userId!, AnyGameId, score: 8.0));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GameLoan_ShouldRejectEmptyIdentity(string? userId)
        => Assert.Throws<ArgumentException>(() => new GameLoan(userId!, AnyGameId, "Ana", AnyDate));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RuleQuestion_ShouldRejectEmptyIdentity(string? userId)
        => Assert.Throws<ArgumentException>(() => new RuleQuestion(AnyGameId, userId!, "Ana", "¿Título?", "Cuerpo de la duda."));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RuleAnswer_ShouldRejectEmptyIdentity(string? userId)
        => Assert.Throws<ArgumentException>(() => new RuleAnswer(AnyQuestionId, userId!, "Ana", "Cuerpo de la respuesta."));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RuleVote_ShouldRejectEmptyIdentity(string? userId)
        => Assert.Throws<ArgumentException>(() => new RuleVote(userId!, questionId: AnyQuestionId));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AuditLogEntry_ShouldRejectEmptyIdentity(string? userId)
        => Assert.Throws<ArgumentException>(() => new AuditLogEntry(
            userId: userId!,
            userName: "Ana",
            action: AuditAction.Created,
            entityType: AuditEntityType.User,
            entityId: "ana",
            entityName: "Ana",
            summary: "Alta de usuario"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UserPreference_ShouldRejectEmptyIdentity(string? userId)
        => Assert.Throws<ArgumentException>(() => new UserPreference(userId!, "wood"));
}

/// <summary>Colección, préstamos y reseñas: ninguna escritura sin sesión.</summary>
public class LibraryWriteAnonymityTests : AnonymityPolicyTestBase
{
    private UserLibraryService CreateService(ICurrentUserService currentUser) => new(
        new SqliteUserCollectionRepository(Context),
        new SqliteGameLoanRepository(Context),
        new SqliteUserReviewRepository(Context),
        new SqliteGameRepository(Context),
        currentUser);

    [Fact]
    public async Task SetCollectionStateAsync_WithoutSession_DeniesAndPersistsNothing()
    {
        var service = CreateService(Anonymous());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.SetCollectionStateAsync(Game.Id, CollectionStatus.InCollection));

        Assert.Empty(await Context.CollectionItems.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task TogglePlayedStateAsync_WithoutSession_DeniesAndPersistsNothing()
    {
        var service = CreateService(Anonymous());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.TogglePlayedStateAsync(Game.Id));

        Assert.Empty(await Context.CollectionItems.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task SetPlayedStateAsync_WithoutSession_DeniesAndPersistsNothing()
    {
        var service = CreateService(Anonymous());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.SetPlayedStateAsync(Game.Id, isPlayed: true));

        Assert.Empty(await Context.CollectionItems.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateLoanAsync_WithoutSession_DeniesAndPersistsNothing()
    {
        Context.CollectionItems.Add(new UserCollectionItem("dueno-real", Game.Id, CollectionStatus.InCollection));
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var service = CreateService(Anonymous());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateLoanAsync(
            new CreateLoanRequest(Game.Id, "Ana", DateTimeOffset.UtcNow)));

        Assert.Empty(await Context.Loans.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ReturnLoanAsync_WithoutSession_DeniesAndLeavesTheLoanUntouched()
    {
        var loan = new GameLoan("dueno-real", Game.Id, "Ana", DateTimeOffset.UtcNow);
        Context.Loans.Add(loan);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var service = CreateService(Anonymous());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReturnLoanAsync(loan.Id));

        var stored = await Context.Loans.AsNoTracking().SingleAsync(l => l.Id == loan.Id);
        Assert.False(stored.IsReturned);
    }

    [Fact]
    public async Task SubmitReviewAsync_WithoutSession_DeniesAndLeavesTheReviewUntouched()
    {
        var review = new UserGameReview("dueno-real", Game.Id, score: 9.0, microReview: "Muy bueno");
        Context.Reviews.Add(review);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var service = CreateService(Anonymous());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SubmitReviewAsync(
            new SubmitReviewRequest(Game.Id, Score: 1.0, MicroReview: "Cambio anónimo")));

        var stored = await Context.Reviews.AsNoTracking().SingleAsync(r => r.Id == review.Id);
        Assert.Equal(9.0, stored.Score);
        Assert.Empty(await Context.CollectionItems.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task SetCollectionStateAsync_WithSession_PersistsItemWithTheSessionIdentity()
    {
        var service = CreateService(WithSession("jugadora-real"));

        var item = await service.SetCollectionStateAsync(Game.Id, CollectionStatus.InCollection);

        Assert.NotNull(item);
        var stored = await Context.CollectionItems.AsNoTracking().SingleAsync();
        Assert.Equal("jugadora-real", stored.UserId);
        Assert.Equal(CollectionStatus.InCollection, stored.Status);
    }

    [Fact]
    public async Task SubmitReviewAsync_WithSession_PersistsReviewWithTheSessionIdentity()
    {
        var service = CreateService(WithSession("jugadora-real"));

        await service.SubmitReviewAsync(new SubmitReviewRequest(Game.Id, Score: 8.0, MicroReview: "Gran juego"));

        var stored = await Context.Reviews.AsNoTracking().SingleAsync();
        Assert.Equal("jugadora-real", stored.UserId);
        Assert.Equal(8.0, stored.Score);
    }
}

/// <summary>Diario de partidas: registrar y borrar exigen sesión.</summary>
public class PlayLogAnonymityTests : AnonymityPolicyTestBase
{
    private GamePlayLogService CreateService(ICurrentUserService currentUser) => new(
        new SqliteGamePlayLogRepository(Context),
        new SqliteGameRepository(Context),
        new SqliteUserCollectionRepository(Context),
        currentUser);

    [Fact]
    public async Task RecordPlayAsync_WithoutSession_DeniesAndPersistsNothing()
    {
        var service = CreateService(Anonymous());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RecordPlayAsync(
            new RecordPlayRequest(Game.Id, DateTimeOffset.UtcNow, "Casa", PlayerCount: 3)));

        Assert.Empty(await Context.GamePlayLogs.AsNoTracking().ToListAsync());
        Assert.Empty(await Context.CollectionItems.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task DeletePlayAsync_WithoutSession_DeniesAndKeepsThePlay()
    {
        var play = new GamePlayLog("dueno-real", Game.Id, DateTimeOffset.UtcNow, "Casa", playerCount: 3);
        Context.GamePlayLogs.Add(play);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var service = CreateService(Anonymous());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeletePlayAsync(play.Id));

        Assert.True(await Context.GamePlayLogs.AsNoTracking().AnyAsync(p => p.Id == play.Id));
    }

    [Fact]
    public async Task RecordPlayAsync_WithSession_PersistsPlayAndPlayedFlagForTheSessionIdentity()
    {
        var service = CreateService(WithSession("jugadora-real"));

        var dto = await service.RecordPlayAsync(new RecordPlayRequest(
            Game.Id,
            DateTimeOffset.UtcNow,
            "Casa",
            PlayerCount: 3,
            DurationMinutes: 45,
            Comment: "Primera partida"));

        Assert.Equal(Game.Id, dto.GameId);
        var storedPlay = await Context.GamePlayLogs.AsNoTracking().SingleAsync();
        Assert.Equal("jugadora-real", storedPlay.UserId);

        var storedItem = await Context.CollectionItems.AsNoTracking().SingleAsync();
        Assert.Equal("jugadora-real", storedItem.UserId);
        Assert.True(storedItem.IsPlayed);
    }
}

/// <summary>Reportes de incidencia: exigen sesión y la identidad del reporte es la de la sesión.</summary>
public class GameReportAnonymityTests : AnonymityPolicyTestBase
{
    private GameIssueReportService CreateService(ICurrentUserService currentUser) => new(
        new SqliteGameIssueReportRepository(Context),
        currentUser);

    [Fact]
    public async Task CreateReportAsync_WithoutSession_DeniesAndPersistsNothing()
    {
        var service = CreateService(Anonymous());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateReportAsync(
            new CreateGameReportCommand(
                GameId: Game.Id,
                GameSlug: "juego-de-prueba",
                GameTitle: "Juego de Prueba",
                IssueType: GameIssueType.WrongImage,
                Details: "La imagen no corresponde")).AsTask());

        Assert.Empty(await Context.IssueReports.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateReportAsync_WithSession_UsesTheSessionIdentityEvenIfTheCommandCarriesAnother()
    {
        var service = CreateService(WithSession("reportadora-real"));

        var report = await service.CreateReportAsync(new CreateGameReportCommand(
            GameId: Game.Id,
            GameSlug: "juego-de-prueba",
            GameTitle: "Juego de Prueba",
            IssueType: GameIssueType.ErroneousMetadata,
            Details: "El año no es correcto",
            ReporterNameOrAlias: "Alias inventado",
            UserId: "identidad-ajena")).AsTask();

        Assert.Equal("reportadora-real", report.ReportedByUserId);
        var stored = await Context.IssueReports.AsNoTracking().SingleAsync();
        Assert.Equal("reportadora-real", stored.ReportedByUserId);
    }
}

/// <summary>Preguntas de reglas, respuestas y votos: exigen sesión y no cuentan votos anónimos.</summary>
public class RuleQaAnonymityTests : AnonymityPolicyTestBase
{
    private RuleQAService CreateService() => new(new SqliteRuleQARepository(Context));

    private async Task<RuleQuestionDto> SeedQuestionAsync()
    {
        var service = CreateService();
        return await service.CreateQuestionAsync(
            new CreateRuleQuestionRequest(Game.Id, "¿Se puede pasar?", "Duda sobre el turno de paso."),
            userId: "autor-real",
            userName: "Autor Real");
    }

    [Fact]
    public async Task CreateQuestionAsync_WithoutSession_DeniesAndPersistsNothing()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateQuestionAsync(
            new CreateRuleQuestionRequest(Game.Id, "¿Se puede pasar?", "Duda sobre el turno de paso."),
            userId: string.Empty,
            userName: string.Empty));

        Assert.Empty(await Context.RuleQuestions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AddAnswerAsync_WithoutSession_DeniesAndPersistsNothing()
    {
        var question = await SeedQuestionAsync();
        var service = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AddAnswerAsync(
            new CreateRuleAnswerRequest(question.Id, "Sí, se puede pasar."),
            userId: "   ",
            userName: string.Empty));

        Assert.Empty(await Context.RuleAnswers.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ToggleVoteQuestionAsync_WithoutSession_DeniesAndDoesNotCountVotes()
    {
        var question = await SeedQuestionAsync();
        var service = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ToggleVoteQuestionAsync(question.Id, userId: string.Empty));

        Assert.Empty(await Context.RuleVotes.AsNoTracking().ToListAsync());
        var stored = await Context.RuleQuestions.AsNoTracking().SingleAsync(q => q.Id == question.Id);
        Assert.Equal(0, stored.VotesCount);
    }

    [Fact]
    public async Task ToggleVoteAnswerAsync_WithoutSession_DeniesAndDoesNotCountVotes()
    {
        var question = await SeedQuestionAsync();
        var service = CreateService();
        var answer = await service.AddAnswerAsync(
            new CreateRuleAnswerRequest(question.Id, "Sí, se puede pasar."),
            userId: "respondedor-real",
            userName: "Respondedor Real");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ToggleVoteAnswerAsync(answer.Id, userId: string.Empty));

        Assert.Empty(await Context.RuleVotes.AsNoTracking().ToListAsync());
        var stored = await Context.RuleAnswers.AsNoTracking().SingleAsync(a => a.Id == answer.Id);
        Assert.Equal(0, stored.VotesCount);
    }

    [Fact]
    public async Task CreateQuestionAsync_WithSession_PersistsQuestionWithTheSessionIdentity()
    {
        var service = CreateService();

        var question = await service.CreateQuestionAsync(
            new CreateRuleQuestionRequest(Game.Id, "¿Puedo robar?", "Duda sobre el robo."),
            userId: "preguntona-real",
            userName: "Preguntona Real");

        Assert.NotEqual(Guid.Empty, question.Id);
        var stored = await Context.RuleQuestions.AsNoTracking().SingleAsync();
        Assert.Equal("preguntona-real", stored.UserId);
        Assert.Equal("¿Puedo robar?", stored.Title);
    }
}

/// <summary>Bitácora de auditoría: solo entidades reales, nunca identidades vacías.</summary>
public class AuditTrailAnonymityTests : AnonymityPolicyTestBase
{
    [Fact]
    public async Task RecordChangeAsync_WithEmptyIdentity_DeniesAndPersistsNothing()
    {
        var service = new AuditService(new SqliteAuditLogRepository(Context), Anonymous());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RecordChangeAsync(new RecordAuditCommand(
            UserId: string.Empty,
            UserName: string.Empty,
            Action: AuditAction.Created,
            EntityType: AuditEntityType.Game,
            EntityId: Game.Id.ToString(),
            EntityName: Game.SpanishTitle,
            Summary: "Intento anónimo")));

        Assert.Empty(await Context.AuditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task RecordChangeAsync_WithSessionIdentity_PersistsTheRealActor()
    {
        var service = new AuditService(new SqliteAuditLogRepository(Context), WithSession("auditora-real", "Auditora Real"));

        await service.RecordChangeAsync(new RecordAuditCommand(
            UserId: "auditora-real",
            UserName: "Auditora Real",
            Action: AuditAction.Updated,
            EntityType: AuditEntityType.Game,
            EntityId: Game.Id.ToString(),
            EntityName: Game.SpanishTitle,
            Summary: "Edición real"));

        var stored = await Context.AuditLogs.AsNoTracking().SingleAsync();
        Assert.Equal("auditora-real", stored.UserId);
        Assert.Equal("Auditora Real", stored.UserName);
    }
}

/// <summary>Preferencias: ni tema ni país se persisten sin sesión.</summary>
public class UserPreferenceAnonymityTests : AnonymityPolicyTestBase
{
    [Fact]
    public async Task SetUserThemeAsync_WithEmptyIdentity_DeniesAndPersistsNothing()
    {
        var service = new SqliteUserPreferenceService(Context);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.SetUserThemeAsync(string.Empty, "wood"));

        Assert.Empty(await Context.UserPreferences.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task SetUserCountryAsync_WithEmptyIdentity_DeniesAndPersistsNothing()
    {
        var service = new SqliteUserPreferenceService(Context);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.SetUserCountryAsync("   ", "España"));

        Assert.Empty(await Context.UserPreferences.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task SetUserThemeAsync_WithSession_PersistsPreferenceForThatIdentity()
    {
        var service = new SqliteUserPreferenceService(Context);

        await service.SetUserThemeAsync("jugadora-real", "editorial");

        var stored = await Context.UserPreferences.AsNoTracking().SingleAsync();
        Assert.Equal("jugadora-real", stored.UserId);
        Assert.Equal("light", stored.PreferredTheme);
    }
}

/// <summary>Importación y alta desde BGG: sin sesión no se consulta BGG ni se escribe colección.</summary>
public class BggImportAnonymityTests : AnonymityPolicyTestBase
{
    private sealed class RecordingBggClient : IBggClient
    {
        public int CollectionCalls { get; private set; }

        public int FetchCalls { get; private set; }

        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default)
        {
            FetchCalls++;
            return Task.FromResult<Game?>(null);
        }

        public Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(
            string username,
            IProgress<BggImportProgressReport>? progress,
            CancellationToken ct = default)
        {
            CollectionCalls++;
            return Task.FromResult<IReadOnlyList<BggCollectionItemDto>>([]);
        }

        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggSearchResultDto>>([]);

        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggTopGameDto>>([]);
    }

    [Fact]
    public async Task ImportUserCollectionAsync_WithoutSession_DeniesWithoutCallingBggNorPersisting()
    {
        var bgg = new RecordingBggClient();
        var service = new BggImportService(
            bgg,
            new SqliteGameRepository(Context),
            new SqliteUserCollectionRepository(Context),
            new SqlitePendingBggImportRepository(Context),
            Anonymous());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ImportUserCollectionAsync(new BggImportRequest("jugador-bgg")));

        Assert.Equal(0, bgg.CollectionCalls);
        Assert.Empty(await Context.CollectionItems.AsNoTracking().ToListAsync());
        Assert.Empty(await Context.PendingBggImports.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AddGameToCollectionAsync_WithoutSession_DeniesWithoutCallingBggNorPersisting()
    {
        var bgg = new RecordingBggClient();
        var service = new BggSearchAssistedService(
            bgg,
            new SqliteGameRepository(Context),
            new SqliteUserCollectionRepository(Context),
            new SqlitePendingBggImportRepository(Context),
            Anonymous());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.AddGameToCollectionAsync(bggId: 4321, status: CollectionStatus.InCollection));

        Assert.Equal(0, bgg.FetchCalls);
        Assert.Empty(await Context.CollectionItems.AsNoTracking().ToListAsync());
    }
}
