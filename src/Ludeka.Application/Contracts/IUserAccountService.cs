using System.Threading;
using System.Threading.Tasks;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Resultado del proceso de baja o cierre de cuenta.
/// </summary>
public record AccountClosureResult(bool Success, string Message);

/// <summary>
/// Servicio de autoservicio para la gestión y cierre de la propia cuenta del usuario en sesión (RGPD art. 17).
/// </summary>
public interface IUserAccountService
{
    /// <summary>
    /// Tramita la baja y supresión de la cuenta del usuario autenticado en sesión mediante
    /// borrado lógico y anonimización irreversible de su PII, purgando credenciales OAuth.
    /// </summary>
    Task<AccountClosureResult> CloseOwnAccountAsync(CancellationToken ct = default);
}
