using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludeka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSupabasePostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Permissions = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Changes = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BoardGameEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WebsiteUrl = table.Column<string>(type: "text", nullable: true),
                    Organizer = table.Column<string>(type: "text", nullable: false),
                    IsOfficial = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardGameEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Creators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Nationality = table.Column<string>(type: "text", nullable: true),
                    Bio = table.Column<string>(type: "text", nullable: true),
                    AvatarUrl = table.Column<string>(type: "text", nullable: true),
                    BggPersonId = table.Column<int>(type: "integer", nullable: true),
                    WebsiteUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SocialLinks = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Creators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FoundingVerdicts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<string>(type: "text", nullable: false),
                    AuthorName = table.Column<string>(type: "text", nullable: false),
                    Recommendation = table.Column<int>(type: "integer", nullable: false),
                    OverallVerdict = table.Column<string>(type: "text", nullable: false),
                    TwoPlayerVerdict = table.Column<string>(type: "text", nullable: false),
                    FamilyVerdict = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Photos = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoundingVerdicts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameEditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditorUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EditorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SummaryOfChanges = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AssociatedReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    EditedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameEditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameIssueReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameSlug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GameTitle = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    IssueType = table.Column<int>(type: "integer", nullable: false),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReportedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReporterNameOrAlias = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ModeratorNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolvedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameIssueReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BggId = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    OriginalTitle = table.Column<string>(type: "text", nullable: false),
                    SpanishTitle = table.Column<string>(type: "text", nullable: false),
                    Designer = table.Column<string>(type: "text", nullable: false),
                    Publisher = table.Column<string>(type: "text", nullable: false),
                    YearPublished = table.Column<int>(type: "integer", nullable: false),
                    CoverImageUrl = table.Column<string>(type: "text", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    BggRating = table.Column<double>(type: "double precision", nullable: false),
                    BggRank = table.Column<int>(type: "integer", nullable: true),
                    LudistRating = table.Column<double>(type: "double precision", nullable: false),
                    Confrontation = table.Column<int>(type: "integer", nullable: false),
                    Style = table.Column<int>(type: "integer", nullable: false),
                    IsOfficialSolo = table.Column<bool>(type: "boolean", nullable: false),
                    Language = table.Column<int>(type: "integer", nullable: false),
                    Footprint = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    BaseGameId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpansionNecessity = table.Column<int>(type: "integer", nullable: true),
                    ImpactTags = table.Column<int[]>(type: "integer[]", nullable: false),
                    WhatItBringsSummary = table.Column<string>(type: "text", nullable: true),
                    ExtraPlayerCount = table.Column<int>(type: "integer", nullable: true),
                    ExtraDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    Age_BoxAge = table.Column<int>(type: "integer", nullable: false),
                    Age_CommunityAge = table.Column<int>(type: "integer", nullable: false),
                    Duration_EstimatedPerPlayerMinutes = table.Column<int>(type: "integer", nullable: false),
                    Duration_MaxMinutes = table.Column<int>(type: "integer", nullable: false),
                    Duration_MinMinutes = table.Column<int>(type: "integer", nullable: false),
                    AiSummary = table.Column<string>(type: "jsonb", nullable: true),
                    PurchaseLinks = table.Column<string>(type: "jsonb", nullable: true),
                    Scalability = table.Column<string>(type: "jsonb", nullable: true),
                    Sleeves = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Games_Games_BaseGameId",
                        column: x => x.BaseGameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstagramPostDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Caption = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    SvgContent = table.Column<string>(type: "text", nullable: true),
                    Theme = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InstagramMediaId = table.Column<string>(type: "text", nullable: true),
                    InstagramPermalink = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedByUserName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstagramPostDrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NightlyCatalogingExecutionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    QueueProcessedCount = table.Column<int>(type: "integer", nullable: false),
                    NewsDiscoveryCount = table.Column<int>(type: "integer", nullable: false),
                    TopBackfillCount = table.Column<int>(type: "integer", nullable: false),
                    TotalCatalogedCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    CatalogedTitlesJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NightlyCatalogingExecutionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    TargetUrl = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorDetails = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingBggImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BggId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    YearPublished = table.Column<int>(type: "integer", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "text", nullable: true),
                    CoverImageUrl = table.Column<string>(type: "text", nullable: true),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    ExtractedTitle = table.Column<string>(type: "text", nullable: true),
                    RequestedCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingBggImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Publishers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    WebsiteUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SocialLinks = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Publishers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuleVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnswerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleVotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    WebsiteUrl = table.Column<string>(type: "text", nullable: true),
                    AffiliateCode = table.Column<string>(type: "text", nullable: true),
                    HasLoyaltyProgram = table.Column<bool>(type: "boolean", nullable: false),
                    ShippingCountries = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SocialLinks = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    PreferredTheme = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "ExpansionRecipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseGameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IdealFor = table.Column<string>(type: "text", nullable: false),
                    IncludedExpansionIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpansionRecipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpansionRecipes_Games_BaseGameId",
                        column: x => x.BaseGameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpansionSynergies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseGameId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpansionAId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpansionBId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpansionSynergies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpansionSynergies_Games_BaseGameId",
                        column: x => x.BaseGameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExpansionSynergies_Games_ExpansionAId",
                        column: x => x.ExpansionAId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExpansionSynergies_Games_ExpansionBId",
                        column: x => x.ExpansionBId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameLoans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    BorrowerName = table.Column<string>(type: "text", nullable: false),
                    LoanDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    IsReturned = table.Column<bool>(type: "boolean", nullable: false),
                    ReturnedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameLoans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameLoans_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GamePlayLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PlayerCount = table.Column<int>(type: "integer", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GamePlayLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GamePlayLogs_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Giveaways",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Organizer = table.Column<string>(type: "text", nullable: false),
                    Collaborator = table.Column<string>(type: "text", nullable: true),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeadlineAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true),
                    GameTitle = table.Column<string>(type: "text", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "text", nullable: true),
                    IsCommunityExclusive = table.Column<bool>(type: "boolean", nullable: false),
                    IsPromoted = table.Column<bool>(type: "boolean", nullable: false),
                    InstagramMediaId = table.Column<string>(type: "text", nullable: true),
                    InstagramPermalink = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Giveaways", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Giveaways_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MediaItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    EmbedUrl = table.Column<string>(type: "text", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "text", nullable: false),
                    AuthorChannel = table.Column<string>(type: "text", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    PlayerCountBadge = table.Column<string>(type: "text", nullable: true),
                    LikesCount = table.Column<int>(type: "integer", nullable: true),
                    Excerpt = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsBroken = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaItems_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RuleQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    UserName = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    VotesCount = table.Column<int>(type: "integer", nullable: false),
                    AcceptedAnswerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleQuestions_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserCollectionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: true),
                    IsPlayed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BggId = table.Column<int>(type: "integer", nullable: true),
                    PendingTitle = table.Column<string>(type: "text", nullable: true),
                    PendingThumbnailUrl = table.Column<string>(type: "text", nullable: true),
                    PendingYearPublished = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCollectionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCollectionItems_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserGameReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    MicroReview = table.Column<string>(type: "text", nullable: true),
                    PlayContext = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FamilyExperience = table.Column<string>(type: "jsonb", nullable: true),
                    PlayerCountRatings = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGameReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserGameReviews_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyReleases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Publisher = table.Column<string>(type: "text", nullable: false),
                    ReleaseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true),
                    CoverImageUrl = table.Column<string>(type: "text", nullable: true),
                    EstimatedPvp = table.Column<decimal>(type: "numeric", nullable: true),
                    IsReprint = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    InstagramMediaId = table.Column<string>(type: "text", nullable: true),
                    InstagramPermalink = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyReleases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeeklyReleases_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RuleAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    UserName = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    OfficialRuleReference = table.Column<string>(type: "text", nullable: true),
                    VotesCount = table.Column<int>(type: "integer", nullable: false),
                    IsAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleAnswers_RuleQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "RuleQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_Email",
                table: "AppUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_Role",
                table: "AppUsers",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_Status",
                table: "AppUsers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType",
                table: "AuditLogs",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardGameEvents_Country",
                table: "BoardGameEvents",
                column: "Country");

            migrationBuilder.CreateIndex(
                name: "IX_BoardGameEvents_IsOfficial",
                table: "BoardGameEvents",
                column: "IsOfficial");

            migrationBuilder.CreateIndex(
                name: "IX_BoardGameEvents_StartDate",
                table: "BoardGameEvents",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_Creators_Name",
                table: "Creators",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Creators_Slug",
                table: "Creators",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpansionRecipes_BaseGameId",
                table: "ExpansionRecipes",
                column: "BaseGameId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpansionSynergies_BaseGameId",
                table: "ExpansionSynergies",
                column: "BaseGameId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpansionSynergies_ExpansionAId_ExpansionBId",
                table: "ExpansionSynergies",
                columns: new[] { "ExpansionAId", "ExpansionBId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpansionSynergies_ExpansionBId",
                table: "ExpansionSynergies",
                column: "ExpansionBId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundingVerdicts_GameId",
                table: "FoundingVerdicts",
                column: "GameId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameEditLogs_EditedAt",
                table: "GameEditLogs",
                column: "EditedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GameEditLogs_GameId",
                table: "GameEditLogs",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameIssueReports_CreatedAt",
                table: "GameIssueReports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GameIssueReports_GameId",
                table: "GameIssueReports",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameIssueReports_IssueType",
                table: "GameIssueReports",
                column: "IssueType");

            migrationBuilder.CreateIndex(
                name: "IX_GameIssueReports_Status",
                table: "GameIssueReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GameIssueReports_Status_CreatedAt",
                table: "GameIssueReports",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GameLoans_GameId",
                table: "GameLoans",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameLoans_UserId_GameId",
                table: "GameLoans",
                columns: new[] { "UserId", "GameId" });

            migrationBuilder.CreateIndex(
                name: "IX_GameLoans_UserId_IsReturned",
                table: "GameLoans",
                columns: new[] { "UserId", "IsReturned" });

            migrationBuilder.CreateIndex(
                name: "IX_GamePlayLogs_GameId",
                table: "GamePlayLogs",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GamePlayLogs_UserId_PlayDate",
                table: "GamePlayLogs",
                columns: new[] { "UserId", "PlayDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Games_BaseGameId",
                table: "Games",
                column: "BaseGameId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_BggId",
                table: "Games",
                column: "BggId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_OriginalTitle",
                table: "Games",
                column: "OriginalTitle");

            migrationBuilder.CreateIndex(
                name: "IX_Games_Slug",
                table: "Games",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_SpanishTitle",
                table: "Games",
                column: "SpanishTitle");

            migrationBuilder.CreateIndex(
                name: "IX_Games_Type",
                table: "Games",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Giveaways_Country",
                table: "Giveaways",
                column: "Country");

            migrationBuilder.CreateIndex(
                name: "IX_Giveaways_DeadlineAt",
                table: "Giveaways",
                column: "DeadlineAt");

            migrationBuilder.CreateIndex(
                name: "IX_Giveaways_GameId",
                table: "Giveaways",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Giveaways_IsCommunityExclusive",
                table: "Giveaways",
                column: "IsCommunityExclusive");

            migrationBuilder.CreateIndex(
                name: "IX_Giveaways_Platform",
                table: "Giveaways",
                column: "Platform");

            migrationBuilder.CreateIndex(
                name: "IX_InstagramPostDrafts_CreatedAt",
                table: "InstagramPostDrafts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InstagramPostDrafts_SourceType_SourceId",
                table: "InstagramPostDrafts",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_InstagramPostDrafts_Status",
                table: "InstagramPostDrafts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_Category",
                table: "MediaItems",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_GameId",
                table: "MediaItems",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_GameId_Status",
                table: "MediaItems",
                columns: new[] { "GameId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_Platform",
                table: "MediaItems",
                column: "Platform");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_Status",
                table: "MediaItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_Type",
                table: "MediaItems",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_NightlyCatalogingExecutionLogs_StartedAt",
                table: "NightlyCatalogingExecutionLogs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_Channel",
                table: "NotificationLogs",
                column: "Channel");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_CreatedAt",
                table: "NotificationLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_Status",
                table: "NotificationLogs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PendingBggImports_BggId",
                table: "PendingBggImports",
                column: "BggId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingBggImports_CreatedAt",
                table: "PendingBggImports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PendingBggImports_Status_RequestedCount",
                table: "PendingBggImports",
                columns: new[] { "Status", "RequestedCount" });

            migrationBuilder.CreateIndex(
                name: "IX_Publishers_Name",
                table: "Publishers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Publishers_Slug",
                table: "Publishers",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleAnswers_IsAccepted",
                table: "RuleAnswers",
                column: "IsAccepted");

            migrationBuilder.CreateIndex(
                name: "IX_RuleAnswers_QuestionId",
                table: "RuleAnswers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleQuestions_CreatedAt",
                table: "RuleQuestions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RuleQuestions_GameId",
                table: "RuleQuestions",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleVotes_UserId_AnswerId",
                table: "RuleVotes",
                columns: new[] { "UserId", "AnswerId" });

            migrationBuilder.CreateIndex(
                name: "IX_RuleVotes_UserId_QuestionId",
                table: "RuleVotes",
                columns: new[] { "UserId", "QuestionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Stores_Country",
                table: "Stores",
                column: "Country");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_Name",
                table: "Stores",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_Slug",
                table: "Stores",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCollectionItems_GameId",
                table: "UserCollectionItems",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCollectionItems_UserId_BggId",
                table: "UserCollectionItems",
                columns: new[] { "UserId", "BggId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserCollectionItems_UserId_GameId",
                table: "UserCollectionItems",
                columns: new[] { "UserId", "GameId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserCollectionItems_UserId_IsPlayed",
                table: "UserCollectionItems",
                columns: new[] { "UserId", "IsPlayed" });

            migrationBuilder.CreateIndex(
                name: "IX_UserCollectionItems_UserId_Status",
                table: "UserCollectionItems",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UserGameReviews_GameId",
                table: "UserGameReviews",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGameReviews_UserId_GameId",
                table: "UserGameReviews",
                columns: new[] { "UserId", "GameId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyReleases_GameId",
                table: "WeeklyReleases",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyReleases_ReleaseDate",
                table: "WeeklyReleases",
                column: "ReleaseDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUsers");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BoardGameEvents");

            migrationBuilder.DropTable(
                name: "Creators");

            migrationBuilder.DropTable(
                name: "ExpansionRecipes");

            migrationBuilder.DropTable(
                name: "ExpansionSynergies");

            migrationBuilder.DropTable(
                name: "FoundingVerdicts");

            migrationBuilder.DropTable(
                name: "GameEditLogs");

            migrationBuilder.DropTable(
                name: "GameIssueReports");

            migrationBuilder.DropTable(
                name: "GameLoans");

            migrationBuilder.DropTable(
                name: "GamePlayLogs");

            migrationBuilder.DropTable(
                name: "Giveaways");

            migrationBuilder.DropTable(
                name: "InstagramPostDrafts");

            migrationBuilder.DropTable(
                name: "MediaItems");

            migrationBuilder.DropTable(
                name: "NightlyCatalogingExecutionLogs");

            migrationBuilder.DropTable(
                name: "NotificationLogs");

            migrationBuilder.DropTable(
                name: "PendingBggImports");

            migrationBuilder.DropTable(
                name: "Publishers");

            migrationBuilder.DropTable(
                name: "RuleAnswers");

            migrationBuilder.DropTable(
                name: "RuleVotes");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropTable(
                name: "UserCollectionItems");

            migrationBuilder.DropTable(
                name: "UserGameReviews");

            migrationBuilder.DropTable(
                name: "UserPreferences");

            migrationBuilder.DropTable(
                name: "WeeklyReleases");

            migrationBuilder.DropTable(
                name: "RuleQuestions");

            migrationBuilder.DropTable(
                name: "Games");
        }
    }
}
