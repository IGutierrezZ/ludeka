using System;
using System.Linq;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Options;
using Ludeka.Infrastructure.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class AdminUserSeederTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LudekaDbContext _context;

    public AdminUserSeederTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task EnsureAdminUserAsync_WhenDatabaseIsEmpty_CreatesAdminUserWithAllPermissions()
    {
        var options = new AdminUserOptions
        {
            Id = "super-admin",
            UserName = "Super Admin Fundador",
            Email = "fundador@ludeka.es",
            Country = "España"
        };

        await AdminUserSeeder.EnsureAdminUserAsync(_context, options);

        var admin = await _context.AppUsers.FirstOrDefaultAsync(u => u.Id == "super-admin");
        Assert.NotNull(admin);
        Assert.Equal("Super Admin Fundador", admin.UserName);
        Assert.Equal("fundador@ludeka.es", admin.Email);
        Assert.Equal(UserRole.FoundingTeam, admin.Role);
        Assert.Equal(UserStatus.Active, admin.Status);
        Assert.Equal(ModeratorPermission.All, admin.Permissions);
        Assert.True(admin.HasPermission(ModeratorPermission.CanEditGames));
        Assert.True(admin.HasPermission(ModeratorPermission.CanUploadImages));
        Assert.True(admin.HasPermission(ModeratorPermission.CanPublishInstagram));
    }

    [Fact]
    public async Task EnsureAdminUserAsync_WhenFoundingUserAlreadyExists_DoesNotDuplicateOrOverwrite()
    {
        // Añadir usuario fundador previo
        var existingFounder = new AppUser(
            id: "carlos_existente",
            userName: "Carlos Fundador Existente",
            email: "carlos@ludeka.es",
            role: UserRole.FoundingTeam
        );
        _context.AppUsers.Add(existingFounder);
        await _context.SaveChangesAsync();

        var options = new AdminUserOptions
        {
            Id = "nuevo_admin",
            UserName = "Otro Admin",
            Email = "otro@ludeka.es"
        };

        await AdminUserSeeder.EnsureAdminUserAsync(_context, options);

        // El usuario existente se conserva y no se crea 'nuevo_admin'
        var users = await _context.AppUsers.ToListAsync();
        Assert.Single(users);
        Assert.Equal("carlos_existente", users[0].Id);
    }

    [Fact]
    public async Task EnsureAdminUserAsync_WhenUserWithSameIdExistsAsCommunityUser_PromotesToFoundingTeam()
    {
        // Usuario comunitario previo con el mismo ID
        var standardUser = new AppUser(
            id: "admin-fundador",
            userName: "Usuario Previo",
            email: "previo@ludeka.es",
            role: UserRole.CommunityUser
        );
        _context.AppUsers.Add(standardUser);
        await _context.SaveChangesAsync();

        var options = new AdminUserOptions
        {
            Id = "admin-fundador",
            UserName = "Administrador Ludeka",
            Email = "admin@ludeka.es"
        };

        await AdminUserSeeder.EnsureAdminUserAsync(_context, options);

        var promoted = await _context.AppUsers.FirstOrDefaultAsync(u => u.Id == "admin-fundador");
        Assert.NotNull(promoted);
        Assert.Equal(UserRole.FoundingTeam, promoted.Role);
        Assert.Equal(ModeratorPermission.All, promoted.Permissions);
        Assert.Equal(UserStatus.Active, promoted.Status);
    }
}
