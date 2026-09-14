using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ludeka.Infrastructure.Seeding;

/// <summary>
/// Sembrador defensivo que garantiza la existencia obligatoria de al menos un usuario
/// Administrador de la Mesa Fundadora (FoundingTeam) con permisos completos en la base de datos.
/// </summary>
public static class AdminUserSeeder
{
    public static async Task EnsureAdminUserAsync(
        LudekaDbContext db,
        AdminUserOptions options,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        options ??= new AdminUserOptions();

        // 1. Comprobar si ya existe algún usuario con rol FoundingTeam
        bool hasFoundingUser = await db.AppUsers
            .AnyAsync(u => u.Role == UserRole.FoundingTeam, ct);

        if (hasFoundingUser)
        {
            logger?.LogInformation("AdminUserSeeder: Ya existe al menos un usuario con rol FoundingTeam. Omitiendo creación de administrador inicial.");
            return;
        }

        // 2. Si no existe ningún administrador fundador, creamos el administrador permanente garantizado
        string adminId = string.IsNullOrWhiteSpace(options.Id) ? "admin-fundador" : options.Id.Trim().ToLowerInvariant();
        string adminUserName = string.IsNullOrWhiteSpace(options.UserName) ? "Administrador Ludeka" : options.UserName.Trim();
        string adminEmail = string.IsNullOrWhiteSpace(options.Email) ? "admin@ludeka.es" : options.Email.Trim().ToLowerInvariant();
        string? adminCountry = string.IsNullOrWhiteSpace(options.Country) ? "España" : options.Country.Trim();

        // Verificar si existe un usuario con ese mismo ID o Email para promoverlo si procede
        var existingUser = await db.AppUsers
            .FirstOrDefaultAsync(u => u.Id == adminId || u.Email == adminEmail, ct);

        if (existingUser != null)
        {
            logger?.LogInformation("AdminUserSeeder: Promoviendo usuario existente '{Id}' a FoundingTeam con permisos completos.", existingUser.Id);
            existingUser.UpdateRoleAndPermissions(UserRole.FoundingTeam, ModeratorPermission.All);
            existingUser.UpdateStatus(UserStatus.Active);
        }
        else
        {
            logger?.LogInformation("AdminUserSeeder: Creando usuario Administrador Fundador inicial garantizado '{Id}' ({Email}).", adminId, adminEmail);
            var newAdmin = new AppUser(
                id: adminId,
                userName: adminUserName,
                email: adminEmail,
                role: UserRole.FoundingTeam,
                permissions: ModeratorPermission.All,
                status: UserStatus.Active,
                createdAt: DateTimeOffset.UtcNow,
                country: adminCountry
            );

            await db.AppUsers.AddAsync(newAdmin, ct);
        }

        await db.SaveChangesAsync(ct);
        logger?.LogInformation("AdminUserSeeder: Administrador inicial garantizado y persistido con éxito.");
    }
}
