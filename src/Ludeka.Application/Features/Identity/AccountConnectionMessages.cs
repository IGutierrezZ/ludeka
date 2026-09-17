namespace Ludeka.Application.Features.Identity;

/// <summary>
/// Textos exactos de los mensajes de vinculación y desvinculación de proveedores (INC-49, diseño
/// §3.1 y §D5). Centralizados aquí para que `Web` los reutilice sin duplicar literales y para que
/// la auditoría de honestidad de <see cref="AccountConnectionMessagesTests"/> los fije por prueba.
/// </summary>
public static class AccountConnectionMessages
{
    /// <summary>
    /// Titular del rechazo cuando el proveedor ya pertenece a otra cuenta (diseño §3.1). No
    /// promete ninguna fusión ni transferencia automática: solo describe el invariante de esquema.
    /// </summary>
    public static string RejectedOwnedByAnotherAccountHeadline(string provider)
        => $"Esa cuenta de {provider} ya está vinculada a otra cuenta de Ludeka.";

    /// <summary>
    /// Detalle del rechazo (diseño §3.1). Las dos únicas vías de resolución que ofrece —entrar
    /// directamente con el proveedor, o desvincularlo primero en la otra cuenta— son alcanzables
    /// por el propio usuario, sin soporte ni traspaso de por medio.
    /// </summary>
    public static string RejectedOwnedByAnotherAccountDetail(string provider)
        => $"Un mismo acceso de {provider} solo puede pertenecer a una cuenta de Ludeka. " +
           $"Si esa otra cuenta es tuya, cierra sesión y entra directamente con {provider}. " +
           "Si prefieres usar este acceso desde la cuenta actual, desvincúlalo primero en la pantalla de conexiones de esa otra cuenta.";

    /// <summary>
    /// Guarda del último método de acceso (diseño §D5): se deniega desvincular la única identidad
    /// externa vinculada a una cuenta sin ningún otro método de acceso.
    /// </summary>
    public const string LastAccessMethodDenied =
        "No puedes desvincular tu único método de acceso: te quedarías sin ninguna forma de volver a entrar. Vincula antes otro proveedor.";
}
