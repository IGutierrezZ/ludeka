using Ludeka.Application.Features.Identity;

namespace Ludeka.Web.Authentication;

/// <summary>
/// Rutas cerradas de la vinculación de cuentas (INC-49, diseño §D1/§D4). Todos los destinos son
/// literales fijos elegidos en servidor: nunca se transporta texto libre ni una URL arbitraria por
/// la cadena de consulta, para no abrir una redirección abierta.
/// </summary>
public static class AccountConnectionRoutes
{
    /// <summary>Pantalla de conexiones de la propia cuenta.</summary>
    public const string Page = "/cuenta/conexiones";

    /// <summary>
    /// La sesión que emitió el desafío de vinculación ya no existe al volver del proveedor.
    /// No se vincula nada.
    /// </summary>
    public const string LoginWithLinkWithoutSession = "/login?aviso=vinculacion-sin-sesion";

    /// <summary>
    /// La sesión que vuelve del proveedor es de un usuario distinto al que emitió el desafío.
    /// No se vincula nada.
    /// </summary>
    public const string PageWithSessionChanged = "/cuenta/conexiones?resultado=sesion-cambiada";

    /// <summary>
    /// El correo verificado del acceso normal (no de vinculación) coincide con una cuenta que ya
    /// tiene al menos un proveedor vinculado (rama 2b de <c>ResolveAsync</c>, INC-49). Nunca se
    /// fusiona en silencio: se informa del conflicto y se dirige a iniciar sesión con el método ya
    /// usado. Mismo código cerrado que interpreta <see cref="Ludeka.Web.Services.LoginRedirect.ResolveAccountCollisionNotice(string?)"/>.
    /// </summary>
    public const string LoginWithAccountCollision = "/login?aviso=cuenta-existente";

    /// <summary>
    /// Traduce el resultado de <see cref="IExternalLoginService.LinkAsync"/> al código cerrado de
    /// <c>?resultado=</c> que <c>AccountConnections.razor</c> (PR #4) interpreta en servidor.
    /// </summary>
    public static string PageWithResult(ExternalLoginLinkOutcome outcome) => outcome switch
    {
        ExternalLoginLinkOutcome.Linked => $"{Page}?resultado=vinculado",
        ExternalLoginLinkOutcome.AlreadyLinkedToThisAccount => $"{Page}?resultado=ya-vinculado",
        ExternalLoginLinkOutcome.RejectedOwnedByAnotherAccount => $"{Page}?resultado=en-uso",
        _ => Page
    };
}
