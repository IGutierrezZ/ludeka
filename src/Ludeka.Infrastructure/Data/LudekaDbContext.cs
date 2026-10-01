using Ludeka.Core.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Data;

public class LudekaDbContext : DbContext, IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<UserCollectionItem> CollectionItems => Set<UserCollectionItem>();
    public DbSet<GameLoan> Loans => Set<GameLoan>();
    public DbSet<UserGameReview> Reviews => Set<UserGameReview>();
    public DbSet<FoundingVerdict> FoundingVerdicts => Set<FoundingVerdict>();
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<PendingBggImport> PendingBggImports => Set<PendingBggImport>();
    public DbSet<Giveaway> Giveaways => Set<Giveaway>();
    public DbSet<WeeklyRelease> WeeklyReleases => Set<WeeklyRelease>();
    public DbSet<RuleQuestion> RuleQuestions => Set<RuleQuestion>();
    public DbSet<RuleAnswer> RuleAnswers => Set<RuleAnswer>();
    public DbSet<RuleVote> RuleVotes => Set<RuleVote>();
    public DbSet<ExpansionSynergy> ExpansionSynergies => Set<ExpansionSynergy>();
    public DbSet<ExpansionRecipe> ExpansionRecipes => Set<ExpansionRecipe>();
    public DbSet<CommunityNotificationLog> NotificationLogs => Set<CommunityNotificationLog>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<GameIssueReport> IssueReports => Set<GameIssueReport>();
    public DbSet<GameEditLog> GameEditLogs => Set<GameEditLog>();
    public DbSet<Publisher> Publishers => Set<Publisher>();
    public DbSet<Creator> Creators => Set<Creator>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();
    public DbSet<GamePlayLog> GamePlayLogs => Set<GamePlayLog>();
    public DbSet<BoardGameEvent> BoardGameEvents => Set<BoardGameEvent>();
    public DbSet<NightlyCatalogingExecutionLog> NightlyCatalogingExecutionLogs => Set<NightlyCatalogingExecutionLog>();
    public DbSet<InstagramPostDraft> InstagramPostDrafts => Set<InstagramPostDraft>();
    public DbSet<BggCatalogStagingItem> BggCatalogStaging => Set<BggCatalogStagingItem>();
    public DbSet<SocialInboxItem> SocialInboxItems => Set<SocialInboxItem>();
    public DbSet<MonitoredSocialAccount> MonitoredSocialAccounts => Set<MonitoredSocialAccount>();
    public DbSet<GamePriceSnapshot> GamePriceSnapshots => Set<GamePriceSnapshot>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<NotificationOutboxMessage> NotificationOutboxMessages => Set<NotificationOutboxMessage>();
    public DbSet<JobExecutionLease> JobExecutionLeases => Set<JobExecutionLease>();
    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();
    public DbSet<UserLike> UserLikes => Set<UserLike>();
    public DbSet<UserMilestone> UserMilestones => Set<UserMilestone>();
    public DbSet<BggRawSnapshot> BggRawSnapshots => Set<BggRawSnapshot>();
    public DbSet<DailyTrendingGame> DailyTrendingGames => Set<DailyTrendingGame>();

    public LudekaDbContext(DbContextOptions<LudekaDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Configuración de Game ---
        var game = modelBuilder.Entity<Game>();
        game.ToTable("Games");

        game.HasKey(g => g.Id);

        game.HasIndex(g => g.Slug).IsUnique();
        game.HasIndex(g => g.BggId).IsUnique();
        game.HasIndex(g => g.SpanishTitle);
        game.HasIndex(g => g.OriginalTitle);
        game.HasIndex(g => g.BaseGameId);
        game.HasIndex(g => g.Type);

        game.ComplexProperty(g => g.Age);
        game.ComplexProperty(g => g.Duration);

        // Mapeo JSON nativo en EF Core 10 para colecciones de Value Objects y primitivas
        game.OwnsMany(g => g.Scalability, b => b.ToJson());
        game.OwnsMany(g => g.Sleeves, b => b.ToJson());
        game.OwnsMany(g => g.PurchaseLinks, b => b.ToJson());
        game.OwnsMany(g => g.RegionalPublishers, b => b.ToJson());
        game.OwnsMany(g => g.LocalizedTitles, b => b.ToJson());
        game.PrimitiveCollection(g => g.ImpactTags);

        game.Property(g => g.SpanishPublisher).HasMaxLength(200);
        game.HasIndex(g => g.SpanishPublisher);

        // Mapeo de Síntesis Inteligente con IA (Incremento 13)
        game.OwnsOne(g => g.AiSummary, b => b.ToJson());

        // Relación reflexiva para juego base y expansiones
        game.HasOne(g => g.BaseGame)
            .WithMany(g => g.Expansions)
            .HasForeignKey(g => g.BaseGameId)
            .OnDelete(DeleteBehavior.Restrict);

        // --- Configuración de UserCollectionItem ---
        var collection = modelBuilder.Entity<UserCollectionItem>();
        collection.ToTable("UserCollectionItems");
        collection.HasKey(c => c.Id);

        collection.HasIndex(c => new { c.UserId, c.GameId });
        collection.HasIndex(c => new { c.UserId, c.BggId });
        collection.HasIndex(c => new { c.UserId, c.Status });
        collection.HasIndex(c => new { c.UserId, c.IsPlayed });

        collection.Property(c => c.IsPlayed).HasDefaultValue(false);
        collection.Property(c => c.Status).IsRequired(false);

        collection.HasOne(c => c.Game)
            .WithMany()
            .HasForeignKey(c => c.GameId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Configuración de GamePlayLog (Incremento 30) ---
        var playLog = modelBuilder.Entity<GamePlayLog>();
        playLog.ToTable("GamePlayLogs");
        playLog.HasKey(p => p.Id);

        playLog.HasIndex(p => new { p.UserId, p.PlayDate });
        playLog.HasIndex(p => p.GameId);

        playLog.Property(p => p.UserId).IsRequired().HasMaxLength(100);
        playLog.Property(p => p.Location).IsRequired().HasMaxLength(150);
        playLog.Property(p => p.Comment).HasMaxLength(1000);

        playLog.HasOne(p => p.Game)
            .WithMany()
            .HasForeignKey(p => p.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Configuración de PendingBggImport ---
        var pending = modelBuilder.Entity<PendingBggImport>();
        pending.ToTable("PendingBggImports");
        pending.HasKey(p => p.Id);

        pending.HasIndex(p => p.BggId).IsUnique();
        pending.HasIndex(p => new { p.Status, p.RequestedCount });
        pending.HasIndex(p => p.CreatedAt);

        // --- Configuración de GameLoan ---
        var loan = modelBuilder.Entity<GameLoan>();
        loan.ToTable("GameLoans");
        loan.HasKey(l => l.Id);

        loan.HasIndex(l => new { l.UserId, l.IsReturned });
        loan.HasIndex(l => new { l.UserId, l.GameId });

        loan.HasOne(l => l.Game)
            .WithMany()
            .HasForeignKey(l => l.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Configuración de UserGameReview ---
        var review = modelBuilder.Entity<UserGameReview>();
        review.ToTable("UserGameReviews");
        review.HasKey(r => r.Id);

        review.HasIndex(r => new { r.UserId, r.GameId }).IsUnique();
        review.HasIndex(r => r.GameId);

        review.HasOne(r => r.Game)
            .WithMany()
            .HasForeignKey(r => r.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        // Mapeo JSON para votos de comensales y experiencia familiar
        review.OwnsMany(r => r.PlayerCountRatings, b => b.ToJson());
        review.OwnsOne(r => r.FamilyExperience, b => b.ToJson());

        // --- Configuración de FoundingVerdict ---
        var verdict = modelBuilder.Entity<FoundingVerdict>();
        verdict.ToTable("FoundingVerdicts");
        verdict.HasKey(v => v.Id);

        verdict.HasIndex(v => v.GameId).IsUnique();

        verdict.OwnsMany(v => v.Photos, b => b.ToJson());

        // --- Configuración de MediaItem ---
        var media = modelBuilder.Entity<MediaItem>();
        media.ToTable("MediaItems");
        media.HasKey(m => m.Id);

        media.HasIndex(m => m.GameId);
        media.HasIndex(m => m.Status);
        media.HasIndex(m => m.Type);
        media.HasIndex(m => m.Category);
        media.HasIndex(m => m.Platform);
        media.HasIndex(m => new { m.GameId, m.Status });

        media.HasOne(m => m.Game)
            .WithMany()
            .HasForeignKey(m => m.GameId)
            .OnDelete(DeleteBehavior.SetNull);

        // --- Configuración de Giveaway ---
        var giveaway = modelBuilder.Entity<Giveaway>();
        giveaway.ToTable("Giveaways");
        giveaway.HasKey(g => g.Id);

        giveaway.HasIndex(g => g.DeadlineAt);
        giveaway.HasIndex(g => g.Platform);
        giveaway.HasIndex(g => g.IsCommunityExclusive);
        giveaway.HasIndex(g => g.Country);
        giveaway.Property(g => g.Country).HasMaxLength(100).IsRequired();

        giveaway.HasOne(g => g.Game)
            .WithMany()
            .HasForeignKey(g => g.GameId)
            .OnDelete(DeleteBehavior.SetNull);

        // --- Configuración de WeeklyRelease ---
        var release = modelBuilder.Entity<WeeklyRelease>();
        release.ToTable("WeeklyReleases");
        release.HasKey(r => r.Id);

        release.HasIndex(r => r.ReleaseDate);

        release.Property(r => r.SourceUrl)
            .HasMaxLength(1000);

        release.HasOne(r => r.Game)
            .WithMany()
            .HasForeignKey(r => r.GameId)
            .OnDelete(DeleteBehavior.SetNull);

        // --- Configuración de RuleQuestion ---
        var question = modelBuilder.Entity<RuleQuestion>();
        question.ToTable("RuleQuestions");
        question.HasKey(q => q.Id);

        question.HasIndex(q => q.GameId);
        question.HasIndex(q => q.CreatedAt);

        question.HasOne(q => q.Game)
            .WithMany()
            .HasForeignKey(q => q.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        question.HasMany(q => q.Answers)
            .WithOne(a => a.Question)
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Configuración de RuleAnswer ---
        var answer = modelBuilder.Entity<RuleAnswer>();
        answer.ToTable("RuleAnswers");
        answer.HasKey(a => a.Id);

        answer.HasIndex(a => a.QuestionId);
        answer.HasIndex(a => a.IsAccepted);

        // --- Configuración de RuleVote ---
        var vote = modelBuilder.Entity<RuleVote>();
        vote.ToTable("RuleVotes");
        vote.HasKey(v => v.Id);

        vote.HasIndex(v => new { v.UserId, v.QuestionId });
        vote.HasIndex(v => new { v.UserId, v.AnswerId });

        // --- Configuración de ExpansionSynergy ---
        var synergy = modelBuilder.Entity<ExpansionSynergy>();
        synergy.ToTable("ExpansionSynergies");
        synergy.HasKey(s => s.Id);

        synergy.HasIndex(s => s.BaseGameId);
        synergy.HasIndex(s => new { s.ExpansionAId, s.ExpansionBId });

        synergy.HasOne(s => s.BaseGame)
            .WithMany()
            .HasForeignKey(s => s.BaseGameId)
            .OnDelete(DeleteBehavior.Cascade);

        synergy.HasOne(s => s.ExpansionA)
            .WithMany()
            .HasForeignKey(s => s.ExpansionAId)
            .OnDelete(DeleteBehavior.Cascade);

        synergy.HasOne(s => s.ExpansionB)
            .WithMany()
            .HasForeignKey(s => s.ExpansionBId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Configuración de ExpansionRecipe ---
        var recipe = modelBuilder.Entity<ExpansionRecipe>();
        recipe.ToTable("ExpansionRecipes");
        recipe.HasKey(r => r.Id);

        recipe.HasIndex(r => r.BaseGameId);
        recipe.PrimitiveCollection(r => r.IncludedExpansionIds);

        recipe.HasOne(r => r.BaseGame)
            .WithMany()
            .HasForeignKey(r => r.BaseGameId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Configuración de CommunityNotificationLog ---
        var notificationLog = modelBuilder.Entity<CommunityNotificationLog>();
        notificationLog.ToTable("NotificationLogs");
        notificationLog.HasKey(n => n.Id);
        notificationLog.HasIndex(n => n.Status);
        notificationLog.HasIndex(n => n.Channel);
        notificationLog.HasIndex(n => n.CreatedAt);

        // INC-47 (R3a, diseño §5.3): idempotencia de la creación de sub-entregas. Un mensaje
        // reclamado por segunda vez no puede duplicar una sub-entrega existente. En PostgreSQL y
        // en SQLite los NULL se consideran distintos en un índice único, así que las filas
        // históricas con MessageId = NULL convivirán sin violar la restricción (verificado por
        // la prueba que siembra una fila histórica antes de reconciliar el esquema).
        notificationLog.HasIndex(n => new { n.MessageId, n.Channel }).IsUnique();

        // --- Configuración de UserPreference ---
        var userPref = modelBuilder.Entity<UserPreference>();
        userPref.ToTable("UserPreferences");
        userPref.HasKey(u => u.UserId);
        userPref.Property(u => u.PreferredTheme).HasMaxLength(32).IsRequired();
        userPref.Property(u => u.Country).HasMaxLength(100);
        userPref.Property(u => u.HidePublicProfile).HasDefaultValue(false);
        userPref.Property(u => u.UpdatedAt).IsRequired();

        // --- Configuración de GameIssueReport (Incremento 17) ---
        var report = modelBuilder.Entity<GameIssueReport>();
        report.ToTable("GameIssueReports");
        report.HasKey(r => r.Id);

        report.HasIndex(r => r.GameId);
        report.HasIndex(r => r.Status);
        report.HasIndex(r => r.IssueType);
        report.HasIndex(r => r.CreatedAt);
        report.HasIndex(r => new { r.Status, r.CreatedAt });

        report.Property(r => r.GameSlug).IsRequired().HasMaxLength(200);
        report.Property(r => r.GameTitle).IsRequired().HasMaxLength(250);
        report.Property(r => r.Details).HasMaxLength(1000);
        report.Property(r => r.ReporterNameOrAlias).HasMaxLength(100);
        report.Property(r => r.ModeratorNotes).HasMaxLength(1000);
        report.Property(r => r.ResolvedByUserId).HasMaxLength(100);
        report.Property(r => r.ReportedByUserId).HasMaxLength(100);

        // --- Configuración de GameEditLog (Incremento 18) ---
        var editLog = modelBuilder.Entity<GameEditLog>();
        editLog.ToTable("GameEditLogs");
        editLog.HasKey(l => l.Id);

        editLog.HasIndex(l => l.GameId);
        editLog.HasIndex(l => l.EditedAt);

        editLog.Property(l => l.EditorUserId).IsRequired().HasMaxLength(100);
        editLog.Property(l => l.EditorName).IsRequired().HasMaxLength(100);
        editLog.Property(l => l.SummaryOfChanges).IsRequired().HasMaxLength(1000);

        // --- Configuración de Publisher (Incremento 19) ---
        var publisher = modelBuilder.Entity<Publisher>();
        publisher.ToTable("Publishers");
        publisher.HasKey(p => p.Id);
        publisher.HasIndex(p => p.Slug).IsUnique();
        publisher.HasIndex(p => p.Name);
        publisher.Property(p => p.Name).IsRequired().HasMaxLength(200);
        publisher.Property(p => p.Slug).IsRequired().HasMaxLength(200);
        publisher.OwnsMany(p => p.SocialLinks, b => b.ToJson());

        // --- Configuración de Creator (Incremento 19) ---
        var creator = modelBuilder.Entity<Creator>();
        creator.ToTable("Creators");
        creator.HasKey(c => c.Id);
        creator.HasIndex(c => c.Slug).IsUnique();
        creator.HasIndex(c => c.Name);
        creator.Property(c => c.Name).IsRequired().HasMaxLength(200);
        creator.Property(c => c.Slug).IsRequired().HasMaxLength(200);
        creator.OwnsMany(c => c.SocialLinks, b => b.ToJson());

        // --- Configuración de Store (Incremento 19) ---
        var store = modelBuilder.Entity<Store>();
        store.ToTable("Stores");
        store.HasKey(s => s.Id);
        store.HasIndex(s => s.Slug).IsUnique();
        store.HasIndex(s => s.Name);
        store.HasIndex(s => s.Country);
        store.Property(s => s.Name).IsRequired().HasMaxLength(200);
        store.Property(s => s.Slug).IsRequired().HasMaxLength(200);
        store.Property(s => s.Country).IsRequired().HasMaxLength(100);
        store.PrimitiveCollection(s => s.ShippingCountries);
        store.OwnsMany(s => s.SocialLinks, b => b.ToJson());

        // --- Configuración de AppUser (Incremento 20) ---
        var user = modelBuilder.Entity<AppUser>();
        user.ToTable("AppUsers");
        user.HasKey(u => u.Id);
        user.HasIndex(u => u.Email).IsUnique();
        user.HasIndex(u => u.Role);
        user.HasIndex(u => u.Status);
        user.Property(u => u.Id).IsRequired().HasMaxLength(100);
        user.Property(u => u.UserName).IsRequired().HasMaxLength(150);
        user.Property(u => u.Email).IsRequired().HasMaxLength(200);
        user.Property(u => u.Country).HasMaxLength(100);

        // --- Configuración de AuditLogEntry (Incremento 20) ---
        var audit = modelBuilder.Entity<AuditLogEntry>();
        audit.ToTable("AuditLogs");
        audit.HasKey(a => a.Id);
        audit.HasIndex(a => a.UserId);
        audit.HasIndex(a => a.Timestamp);
        audit.HasIndex(a => a.EntityType);
        audit.HasIndex(a => a.Action);
        audit.HasIndex(a => new { a.EntityType, a.EntityId });
        audit.Property(a => a.UserId).IsRequired().HasMaxLength(100);
        audit.Property(a => a.UserName).IsRequired().HasMaxLength(150);
        audit.Property(a => a.EntityId).IsRequired().HasMaxLength(100);
        audit.Property(a => a.EntityName).IsRequired().HasMaxLength(200);
        audit.Property(a => a.Summary).IsRequired().HasMaxLength(500);
        audit.OwnsMany(a => a.Changes, b => b.ToJson());

        // --- Configuración de BoardGameEvent (Incremento 21) ---
        var evt = modelBuilder.Entity<BoardGameEvent>();
        evt.ToTable("BoardGameEvents");
        evt.HasKey(e => e.Id);
        evt.HasIndex(e => e.StartDate);
        evt.HasIndex(e => e.IsOfficial);
        evt.HasIndex(e => e.Country);
        evt.Property(e => e.Title).IsRequired().HasMaxLength(200);
        evt.Property(e => e.ImageUrl).IsRequired().HasMaxLength(500);
        evt.Property(e => e.Location).IsRequired().HasMaxLength(200);
        evt.Property(e => e.Country).IsRequired().HasMaxLength(100);

        // --- Configuración de NightlyCatalogingExecutionLog (Incremento 24) ---
        var nightlyLog = modelBuilder.Entity<NightlyCatalogingExecutionLog>();
        nightlyLog.ToTable("NightlyCatalogingExecutionLogs");
        nightlyLog.HasKey(l => l.Id);
        nightlyLog.HasIndex(l => l.StartedAt);
        nightlyLog.Property(l => l.Status).IsRequired().HasMaxLength(50);
        nightlyLog.Property(l => l.CatalogedTitlesJson).IsRequired();

        // --- Configuración de InstagramPostDraft (Incremento 28) ---
        var instagramDraft = modelBuilder.Entity<InstagramPostDraft>();
        instagramDraft.ToTable("InstagramPostDrafts");
        instagramDraft.HasKey(d => d.Id);
        instagramDraft.HasIndex(d => d.Status);
        instagramDraft.HasIndex(d => new { d.SourceType, d.SourceId });
        instagramDraft.HasIndex(d => d.CreatedAt);
        instagramDraft.Property(d => d.Title).IsRequired().HasMaxLength(250);
        instagramDraft.Property(d => d.Caption).IsRequired().HasMaxLength(4000);
        instagramDraft.Property(d => d.Theme).IsRequired().HasMaxLength(20);
        instagramDraft.Property(d => d.CreatedByUserId).IsRequired().HasMaxLength(100);

        // --- Configuración de BggCatalogStagingItem (Incremento 41) ---
        var staging = modelBuilder.Entity<BggCatalogStagingItem>();
        staging.ToTable("BggCatalogStaging");
        staging.HasKey(s => s.BggId);

        staging.HasIndex(s => s.OriginalTitle);
        staging.HasIndex(s => s.UsersRated);
        staging.HasIndex(s => s.FetchStatus);
        staging.HasIndex(s => s.ImagesStatus);
        staging.HasIndex(s => s.AiStatus);
        staging.HasIndex(s => s.PromotionStatus);
        staging.HasIndex(s => s.CreatedAt);

        staging.Property(s => s.OriginalTitle).IsRequired().HasMaxLength(250);
        staging.Property(s => s.SpanishTitle).HasMaxLength(250);
        staging.Property(s => s.Designer).HasMaxLength(200);
        staging.Property(s => s.Publisher).HasMaxLength(200);
        staging.Property(s => s.SpanishPublisher).HasMaxLength(200);
        staging.Property(s => s.ErrorMessage).HasMaxLength(1000);

        // --- Configuración de SocialInboxItem (Incremento 42) ---
        var socialInbox = modelBuilder.Entity<SocialInboxItem>();
        socialInbox.ToTable("SocialInboxItems");
        socialInbox.HasKey(i => i.Id);
        socialInbox.HasIndex(i => i.Status);
        socialInbox.HasIndex(i => i.DetectedType);
        socialInbox.HasIndex(i => i.CreatedAt);
        socialInbox.Property(i => i.SourceUrl).IsRequired().HasMaxLength(500);
        socialInbox.Property(i => i.Title).IsRequired().HasMaxLength(250);
        socialInbox.Property(i => i.OrganizerOrAuthor).IsRequired().HasMaxLength(200);
        socialInbox.Property(i => i.Collaborator).HasMaxLength(200);
        socialInbox.Property(i => i.Location).HasMaxLength(200);
        socialInbox.Property(i => i.PlayerCountBadge).HasMaxLength(50);
        socialInbox.Property(i => i.ThumbnailUrl).HasMaxLength(1000);

        socialInbox.HasOne(i => i.Game)
            .WithMany()
            .HasForeignKey(i => i.GameId)
            .OnDelete(DeleteBehavior.SetNull);

        // --- Configuración de MonitoredSocialAccount (Incremento 42) ---
        var monitoredAccount = modelBuilder.Entity<MonitoredSocialAccount>();
        monitoredAccount.ToTable("MonitoredSocialAccounts");
        monitoredAccount.HasKey(a => a.Id);
        monitoredAccount.HasIndex(a => new { a.Platform, a.HandleOrChannelId });
        monitoredAccount.HasIndex(a => a.IsEnabled);
        monitoredAccount.Property(a => a.Name).IsRequired().HasMaxLength(200);
        monitoredAccount.Property(a => a.HandleOrChannelId).IsRequired().HasMaxLength(150);
        monitoredAccount.Property(a => a.ProfileUrl).IsRequired().HasMaxLength(500);
        monitoredAccount.Property(a => a.ResolvedFeedUrl).HasMaxLength(500);
        monitoredAccount.Property(a => a.Notes).HasMaxLength(1000);

        // --- Configuración de GamePriceSnapshot (Incremento 45) ---
        var priceSnapshot = modelBuilder.Entity<GamePriceSnapshot>();
        priceSnapshot.ToTable("GamePriceSnapshots");
        priceSnapshot.HasKey(s => s.Id);
        priceSnapshot.HasIndex(s => new { s.GameId, s.RecordedAtUtc });
        priceSnapshot.HasIndex(s => s.Price);
        priceSnapshot.HasIndex(s => new { s.GameId, s.StoreName });
        priceSnapshot.Property(s => s.StoreName).IsRequired().HasMaxLength(150);
        priceSnapshot.Property(s => s.AffiliateUrl).IsRequired().HasMaxLength(500);
        priceSnapshot.Property(s => s.Currency).IsRequired().HasMaxLength(10);

        // --- Configuración de ExternalLogin (Incremento 46) ---
        var externalLogin = modelBuilder.Entity<ExternalLogin>();
        externalLogin.ToTable("ExternalLogins");
        externalLogin.HasKey(l => l.Id);
        externalLogin.HasIndex(l => new { l.Provider, l.ProviderKey }).IsUnique();
        externalLogin.Property(l => l.UserId).IsRequired().HasMaxLength(100);
        externalLogin.Property(l => l.Provider).IsRequired().HasMaxLength(50);
        externalLogin.Property(l => l.ProviderKey).IsRequired().HasMaxLength(255);
        externalLogin.Property(l => l.ProviderEmail).HasMaxLength(200);
        externalLogin.Property(l => l.ProviderEmailVerifiedAt);   // DateTimeOffset? anulable (INC-49)

        externalLogin.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Configuración de NotificationOutboxMessage (INC-47, R3a, diseño §5.3) ---
        var outbox = modelBuilder.Entity<NotificationOutboxMessage>();
        outbox.ToTable("NotificationOutboxMessages");
        outbox.HasKey(m => m.Id);
        outbox.Property(m => m.Title).IsRequired().HasMaxLength(250);
        outbox.Property(m => m.Summary).IsRequired();
        outbox.Property(m => m.FieldsJson).IsRequired();
        outbox.Property(m => m.ClaimedBy).HasMaxLength(128);

        // Índice de reclamación: cubre el filtro y la ordenación completos de la sentencia de
        // reclamación (WHERE Status = 0 AND NextAttemptAt <= ahora ORDER BY NextAttemptAt, CreatedAt),
        // que R4a implementa contra esta misma tabla.
        outbox.HasIndex(m => new { m.Status, m.NextAttemptAt, m.CreatedAt });

        // Índice del chequeo de salud: COUNT(*) y MIN(CreatedAt) sobre Status = Pending (diseño §11).
        outbox.HasIndex(m => new { m.Status, m.CreatedAt });

        // --- Configuración de JobExecutionLease (INC-47, R3b, diseño §7.1) ---
        var jobLease = modelBuilder.Entity<JobExecutionLease>();
        jobLease.ToTable("JobExecutionLeases");
        jobLease.HasKey(l => l.Id);
        jobLease.Property(l => l.JobName).IsRequired().HasMaxLength(64);
        jobLease.Property(l => l.WindowKey).IsRequired().HasMaxLength(32);
        jobLease.Property(l => l.Status).IsRequired().HasMaxLength(20);
        jobLease.Property(l => l.HostIdentifier).HasMaxLength(128);

        // LA restricción. Es lo que hace que un reintento de Cloud Scheduler choque contra la
        // base de datos y no contra un if en memoria (diseño §7.1).
        jobLease.HasIndex(l => new { l.JobName, l.WindowKey }).IsUnique();

        // Lectura operativa: últimas ejecuciones de un trabajo.
        jobLease.HasIndex(l => new { l.JobName, l.StartedAt });

        // --- Configuración de MagicLinkToken (INC-64) ---
        var magicLink = modelBuilder.Entity<MagicLinkToken>();
        magicLink.ToTable("MagicLinkTokens");
        magicLink.HasKey(t => t.Id);
        magicLink.Property(t => t.Email).IsRequired().HasMaxLength(200);
        magicLink.Property(t => t.TokenHash).IsRequired().HasMaxLength(128);
        magicLink.Property(t => t.TargetUserId).HasMaxLength(100);
        magicLink.HasIndex(t => t.TokenHash).IsUnique();
        magicLink.HasIndex(t => new { t.Email, t.CreatedAt });

        // --- Configuración de UserLike (INC-65: «Me gusta» en Editoriales, Tiendas, Creadores y Vídeos) ---
        var userLike = modelBuilder.Entity<UserLike>();
        userLike.ToTable("UserLikes");
        userLike.HasKey(l => l.Id);
        userLike.Property(l => l.UserId).IsRequired();
        userLike.Property(l => l.TargetType).IsRequired();
        userLike.Property(l => l.TargetId).IsRequired();
        userLike.Property(l => l.CreatedAt).IsRequired();
        userLike.HasIndex(l => new { l.UserId, l.TargetType, l.TargetId }).IsUnique();
        userLike.HasIndex(l => new { l.TargetType, l.TargetId });

        // --- Configuración de UserMilestone (INC-67: Gamificación — Hitos y Logros del Jugador) ---
        var userMilestone = modelBuilder.Entity<UserMilestone>();
        userMilestone.ToTable("UserMilestones");
        userMilestone.HasKey(m => m.Id);
        userMilestone.Property(m => m.UserId).IsRequired().HasMaxLength(128);
        userMilestone.Property(m => m.Type).IsRequired();
        userMilestone.Property(m => m.UnlockedAt).IsRequired();
        userMilestone.HasIndex(m => new { m.UserId, m.Type }).IsUnique();
        userMilestone.HasIndex(m => m.UserId);

        // --- Configuración de BggRawSnapshot (INC-90: Snapshots crudos BGG desacoplados) ---
        var snapshot = modelBuilder.Entity<BggRawSnapshot>();
        snapshot.ToTable("BggRawSnapshots");
        snapshot.HasKey(s => s.BggId);
        snapshot.Property(s => s.BggId).ValueGeneratedNever();
        snapshot.Property(s => s.RawJson).IsRequired();
        if (Database.IsNpgsql())
        {
            snapshot.Property(s => s.RawJson).HasColumnType("jsonb");
        }
        snapshot.Property(s => s.ApiVersion).IsRequired();
        snapshot.Property(s => s.FetchedAtUtc).IsRequired();
        snapshot.HasIndex(s => s.FetchedAtUtc);

        // --- Configuración de DailyTrendingGame (INC-92: Tendencias BGG diarias) ---
        var trending = modelBuilder.Entity<DailyTrendingGame>();
        trending.ToTable("DailyTrendingGames");
        trending.HasKey(t => t.Id);
        trending.Property(t => t.Title).IsRequired().HasMaxLength(250);
        trending.Property(t => t.ThumbnailUrl).HasMaxLength(500);
        trending.HasIndex(t => new { t.DateUtc, t.Rank }).IsUnique();
        trending.HasIndex(t => new { t.DateUtc, t.BggId }).IsUnique();
        trending.HasIndex(t => t.DateUtc);
        trending.HasIndex(t => t.BggId);
        trending.HasIndex(t => t.GameId);
        trending.HasOne(t => t.Game)
            .WithMany()
            .HasForeignKey(t => t.GameId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
