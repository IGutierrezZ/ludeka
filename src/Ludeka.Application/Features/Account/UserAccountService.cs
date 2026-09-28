using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Features.Account;

public class UserAccountService : IUserAccountService
{
    private readonly IUserRepository _userRepository;
    private readonly IExternalLoginRepository _externalLoginRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserSessionInvalidator? _sessionInvalidator;
    private readonly IAuditService? _auditService;

    public UserAccountService(
        IUserRepository userRepository,
        IExternalLoginRepository externalLoginRepository,
        ICurrentUserService currentUserService,
        IUserSessionInvalidator? sessionInvalidator = null,
        IAuditService? auditService = null)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _externalLoginRepository = externalLoginRepository ?? throw new ArgumentNullException(nameof(externalLoginRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _sessionInvalidator = sessionInvalidator;
        _auditService = auditService;
    }

    public async Task<AccountClosureResult> CloseOwnAccountAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return new AccountClosureResult(false, "No hay una sesión de usuario activa para tramitar la baja.");
        }

        var userId = _currentUserService.UserId.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByIdAsync(userId, ct);

        if (user == null)
        {
            return new AccountClosureResult(false, "No se encontró el registro de la cuenta de usuario.");
        }

        if (user.Status == UserStatus.Deleted)
        {
            return new AccountClosureResult(true, "La cuenta ya se encuentra dada de baja y anonimizada.");
        }

        if (user.Role == UserRole.FoundingTeam)
        {
            return new AccountClosureResult(false, "Las cuentas pertenecientes a la Mesa Fundadora no pueden darse de baja por autoservicio.");
        }

        var oldUserName = user.UserName;
        var oldRole = user.Role;
        var oldStatus = user.Status;

        user.AnonymizeAndClose("Baja voluntaria solicitada por el propio usuario (autoservicio RGPD art. 17)");
        await _userRepository.UpdateAsync(user, ct);

        // Purgar credenciales e identidades OAuth asociadas para liberar los proveedores
        await _externalLoginRepository.DeleteByUserIdAsync(user.Id, ct);

        // Registrar auditoría si el servicio está disponible
        if (_auditService != null)
        {
            await _auditService.RecordChangeAsync(new RecordAuditCommand(
                UserId: user.Id,
                UserName: "Usuario eliminado",
                Action: AuditAction.Deleted,
                EntityType: AuditEntityType.User,
                EntityId: user.Id,
                EntityName: "Usuario eliminado",
                Summary: $"Baja voluntaria y supresión de cuenta tramitada por el usuario '{oldUserName}' (RGPD art. 17).",
                Changes: [
                    new FieldChangeDto("Status", oldStatus.ToString(), UserStatus.Deleted.ToString()),
                    new FieldChangeDto("Role", oldRole.ToString(), UserRole.CommunityUser.ToString())
                ]
            ), ct);
        }

        // Expulsar sesiones y circuitos activos
        _sessionInvalidator?.Invalidate(user.Id);

        return new AccountClosureResult(true, "Tu cuenta ha sido dada de baja y tus datos personales han sido anonimizados irreversiblemente.");
    }
}
