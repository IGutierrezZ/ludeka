using System;
using System.Collections.Generic;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class UserManagementDomainTests
{
    [Fact]
    public void AppUser_Creation_ShouldSetPropertiesProperly()
    {
        var user = new AppUser(
            id: "laura_mod",
            userName: "Laura Moderadora",
            email: "laura@ludeka.es",
            role: UserRole.Moderator,
            permissions: ModeratorPermission.CanEditGames | ModeratorPermission.CanUploadImages
        );

        Assert.Equal("laura_mod", user.Id);
        Assert.Equal("Laura Moderadora", user.UserName);
        Assert.Equal("laura@ludeka.es", user.Email);
        Assert.Equal(UserRole.Moderator, user.Role);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.True(user.HasPermission(ModeratorPermission.CanEditGames));
        Assert.True(user.HasPermission(ModeratorPermission.CanUploadImages));
        Assert.False(user.HasPermission(ModeratorPermission.CanManagePublishers));
        Assert.False(user.HasPermission(ModeratorPermission.CanResolveReports));
    }

    [Fact]
    public void AppUser_FoundingTeam_ShouldHaveAllPermissionsInherently()
    {
        var founder = new AppUser(
            id: "carlos_fundador",
            userName: "Carlos Fundador",
            email: "carlos@ludeka.es",
            role: UserRole.FoundingTeam,
            permissions: ModeratorPermission.None
        );

        Assert.Equal(UserRole.FoundingTeam, founder.Role);
        Assert.True(founder.HasPermission(ModeratorPermission.CanEditGames));
        Assert.True(founder.HasPermission(ModeratorPermission.CanUploadImages));
        Assert.True(founder.HasPermission(ModeratorPermission.CanManagePublishers));
        Assert.True(founder.HasPermission(ModeratorPermission.CanManageCreators));
        Assert.True(founder.HasPermission(ModeratorPermission.CanApproveMedia));
        Assert.True(founder.HasPermission(ModeratorPermission.CanResolveReports));
        Assert.True(founder.HasPermission(ModeratorPermission.CanManageStoreLinks));
        Assert.True(founder.HasPermission(ModeratorPermission.All));
    }

    [Fact]
    public void AppUser_SuspendedUser_ShouldDenyAllPermissionsEvenIfAssigned()
    {
        var user = new AppUser(
            id: "troll_mod",
            userName: "Usuario Problemático",
            email: "troll@ludeka.es",
            role: UserRole.Moderator,
            permissions: ModeratorPermission.All,
            status: UserStatus.Suspended
        );

        Assert.Equal(UserStatus.Suspended, user.Status);
        Assert.False(user.HasPermission(ModeratorPermission.CanEditGames));
        Assert.False(user.HasPermission(ModeratorPermission.CanManagePublishers));
        Assert.False(user.HasPermission(ModeratorPermission.CanResolveReports));
    }

    [Fact]
    public void AppUser_CommunityUser_ShouldHaveNoPermissions()
    {
        var user = new AppUser(
            id: "david_jugador",
            userName: "David Jugador",
            email: "david@comunidad.es",
            role: UserRole.CommunityUser
        );

        Assert.False(user.HasPermission(ModeratorPermission.CanEditGames));
        Assert.False(user.HasPermission(ModeratorPermission.CanManagePublishers));
    }

    [Fact]
    public void AppUser_GrantAndRevokePermission_ShouldWorkBitwise()
    {
        var user = new AppUser(
            id: "mod_flexible",
            userName: "Mod Flexible",
            email: "mod@ludeka.es",
            role: UserRole.Moderator,
            permissions: ModeratorPermission.CanEditGames
        );

        Assert.True(user.HasPermission(ModeratorPermission.CanEditGames));
        Assert.False(user.HasPermission(ModeratorPermission.CanManageCreators));

        user.GrantPermission(ModeratorPermission.CanManageCreators);
        Assert.True(user.HasPermission(ModeratorPermission.CanManageCreators));
        Assert.True(user.HasPermission(ModeratorPermission.CanEditGames));

        user.RevokePermission(ModeratorPermission.CanEditGames);
        Assert.False(user.HasPermission(ModeratorPermission.CanEditGames));
        Assert.True(user.HasPermission(ModeratorPermission.CanManageCreators));
    }

    [Fact]
    public void AppUser_UpdateRoleAndPermissions_ShouldApplyChanges()
    {
        var user = new AppUser(
            id: "pedro",
            userName: "Pedro",
            email: "pedro@test.es",
            role: UserRole.CommunityUser
        );

        Assert.Equal(UserRole.CommunityUser, user.Role);

        user.UpdateRoleAndPermissions(UserRole.Moderator, ModeratorPermission.CanResolveReports);
        Assert.Equal(UserRole.Moderator, user.Role);
        Assert.True(user.HasPermission(ModeratorPermission.CanResolveReports));
        Assert.False(user.HasPermission(ModeratorPermission.CanEditGames));

        user.UpdateStatus(UserStatus.Suspended);
        Assert.Equal(UserStatus.Suspended, user.Status);
        Assert.False(user.HasPermission(ModeratorPermission.CanResolveReports));
    }

    [Fact]
    public void AppUser_Validation_ThrowsOnInvalidArguments()
    {
        Assert.Throws<ArgumentException>(() => new AppUser("", "User", "email@test.com"));
        Assert.Throws<ArgumentException>(() => new AppUser("id", "", "email@test.com"));
        Assert.Throws<ArgumentException>(() => new AppUser("id", "User", ""));
    }

    [Fact]
    public void AuditLogEntry_Creation_ShouldStoreDiffCorrectly()
    {
        var changes = new List<AuditFieldChange>
        {
            new("SpanishTitle", "Catan Antiguo", "Catan: El Juego"),
            new("Duration", "60-90", "45-75")
        };

        var entry = new AuditLogEntry(
            userId: "laura_mod",
            userName: "Laura Moderadora",
            action: AuditAction.Updated,
            entityType: AuditEntityType.Game,
            entityId: "catan",
            entityName: "Catan",
            summary: "Actualizado título y duración",
            changes: changes
        );

        Assert.Equal("laura_mod", entry.UserId);
        Assert.Equal("Laura Moderadora", entry.UserName);
        Assert.Equal(AuditAction.Updated, entry.Action);
        Assert.Equal(AuditEntityType.Game, entry.EntityType);
        Assert.Equal("catan", entry.EntityId);
        Assert.Equal("Catan", entry.EntityName);
        Assert.Equal(2, entry.Changes.Count);
        Assert.Equal("SpanishTitle", entry.Changes[0].FieldName);
        Assert.Equal("Catan Antiguo", entry.Changes[0].OldValue);
        Assert.Equal("Catan: El Juego", entry.Changes[0].NewValue);
    }

    [Fact]
    public void AuditAction_LinkedFounderIdentity_IsAppendedWithoutRenumbering()
    {
        // La acción se persiste como entero: la nueva entrada DEBE añadirse al
        // final para no reinterpretar las filas de auditoría ya almacenadas.
        Assert.Equal(7, (int)AuditAction.LinkedFounderIdentity);
        Assert.Equal(6, (int)AuditAction.Published);
        Assert.Equal(0, (int)AuditAction.Created);
    }
}
