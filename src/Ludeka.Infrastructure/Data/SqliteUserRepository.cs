using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Identity;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Data;

public class SqliteUserRepository : IUserRepository
{
    private readonly LudekaDbContext _db;

    public SqliteUserRepository(LudekaDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<AppUser?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var cleanId = id.Trim().ToLowerInvariant();

        // AsNoTracking: la identidad se relee en cada comprobación (políticas, circuito e
        // invalidación en caliente) y una entidad rastreada devolvería permisos obsoletos.
        return await _db.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == cleanId, ct);
    }

    public async Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var cleanEmail = email.Trim().ToLowerInvariant();
        return await _db.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Email == cleanEmail, ct);
    }

    public async Task<IReadOnlyList<AppUser>> GetAllAsync(string? search = null, UserRole? role = null, UserStatus? status = null, CancellationToken ct = default)
    {
        var query = _db.AppUsers.AsNoTracking().AsQueryable();

        if (role.HasValue)
        {
            query = query.Where(u => u.Role == role.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(u => u.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var clean = search.Trim().ToLower();
            query = query.Where(u =>
                u.UserName.ToLower().Contains(clean) ||
                u.Email.ToLower().Contains(clean) ||
                u.Id.ToLower().Contains(clean));
        }

        return await query
            .OrderBy(u => u.Role)
            .ThenBy(u => u.UserName)
            .ToListAsync(ct);
    }

    public async Task AddAsync(AppUser user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await _db.AppUsers.AddAsync(user, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AppUser user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        // Corrección INC-49 (PR #7): si esta cuenta ya está bajo seguimiento por otra instancia en
        // el mismo DbContext (por ejemplo, tras un alta o una lectura con tracking anteriores en el
        // mismo ámbito), se vuelcan los valores nuevos sobre esa instancia rastreada en vez de
        // adjuntar una segunda con la misma clave. Mismo patrón que
        // ExternalLoginRepository.RemoveAsync (PR #2), que corrigió el mismo conflicto del
        // ChangeTracker para ExternalLogin.
        var trackedEntry = _db.ChangeTracker.Entries<AppUser>()
            .FirstOrDefault(e => e.Entity.Id == user.Id);

        if (trackedEntry is not null && !ReferenceEquals(trackedEntry.Entity, user))
        {
            trackedEntry.CurrentValues.SetValues(user);
        }
        else
        {
            _db.AppUsers.Update(user);
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Reemplazo del correo sintético (INC-49, diseño §3.4): traduce la excepción del ORM a
            // lenguaje de dominio aquí, en Infrastructure, para que Ludeka.Application no necesite
            // conocer EF Core. Mismo patrón que ExternalLoginRepository.AddAsync.
            throw new DuplicateUserEmailException($"El correo '{user.Email}' ya pertenece a otra cuenta.", ex);
        }
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        var cleanId = id.Trim().ToLowerInvariant();
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == cleanId, ct);
        if (user != null)
        {
            _db.AppUsers.Remove(user);
            await _db.SaveChangesAsync(ct);
        }
    }
}
