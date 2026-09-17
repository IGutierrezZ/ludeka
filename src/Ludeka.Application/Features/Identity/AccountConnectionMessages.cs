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

    /// <summary>
    /// Confirmación fija tras completar una vinculación (código <c>vinculado</c> de la tabla de
    /// resultados del diseño §D4, INC-49 PR #4). <c>AccountConnectionRoutes.PageWithResult</c>
    /// (PR #3) solo transporta el código del resultado, no el nombre del proveedor: por eso este
    /// texto es intencionadamente genérico en vez de interpolar <c>{Proveedor}</c> como sugiere la
    /// tabla ilustrativa del diseño.
    /// </summary>
    public const string LinkedSuccessfully =
        "Listo: acabas de sumar un método de acceso más a tu cuenta.";

    /// <summary>
    /// Aviso fijo cuando el proveedor ya estaba vinculado a la misma cuenta (código
    /// <c>ya-vinculado</c> de §D4). Misma salvedad de genericidad que <see cref="LinkedSuccessfully"/>.
    /// </summary>
    public const string AlreadyLinkedToThisAccountNotice =
        "Ese acceso ya estaba vinculado a tu cuenta: no hacía falta repetirlo.";

    /// <summary>
    /// Aviso fijo cuando la vinculación se rechaza porque el proveedor ya pertenece a otra cuenta
    /// (código <c>en-uso</c> de §D4). Mismo invariante y las mismas dos vías de resolución que
    /// <see cref="RejectedOwnedByAnotherAccountHeadline"/>/<see cref="RejectedOwnedByAnotherAccountDetail"/>,
    /// pero sin el nombre del proveedor: ver la salvedad de genericidad de <see cref="LinkedSuccessfully"/>.
    /// </summary>
    public const string RejectedOwnedByAnotherAccountGenericNotice =
        "Ese acceso ya está vinculado a otra cuenta de Ludeka. Si es tuya, cierra sesión y entra directamente con él; si no, desvincúlalo primero desde esa otra cuenta.";

    /// <summary>
    /// Aviso fijo cuando la sesión cambia mientras se completaba el desafío de vinculación (código
    /// <c>sesion-cambiada</c> de §D4, diseño §D1).
    /// </summary>
    public const string SessionChangedDuringLink =
        "La vinculación se canceló porque tu sesión cambió mientras completabas el acceso. No se ha vinculado nada.";

    /// <summary>
    /// Texto del aviso de correo no verificado (diseño §D6/§3.2), reutilizado por la pantalla de
    /// conexiones (PR #4) y, cuando llegue, por el aviso descartable de cabecera (PR #6): un único
    /// literal centralizado, sin duplicar.
    /// </summary>
    public const string UnverifiedProviderEmailNotice =
        "Tu cuenta no tiene ningún correo verificado por un proveedor de acceso. Vincula otro método para no perderla si el actual deja de estar disponible.";
}
