using System;
using Microsoft.AspNetCore.Authentication;

namespace Ludeka.Web.Authentication;

/// <summary>
/// Intención de un desafío externo, transportada en <see cref="AuthenticationProperties.Items"/> y
/// por tanto dentro del parámetro <c>state</c> que el manejador remoto protege con Data Protection
/// (INC-49, diseño §D1). Nunca se lee del formulario ni de la cadena de consulta: ahí sería
/// alterable por el navegador. Clase estática y pura, sin dependencias de HTTP, para que sea
/// unitariamente comprobable en aislamiento.
/// </summary>
public static class ExternalLoginIntent
{
    /// <summary>Clave de <see cref="AuthenticationProperties.Items"/> para la intención del desafío.</summary>
    public const string IntentKey = "ludeka:intent";

    /// <summary>Valor de <see cref="IntentKey"/> que marca un desafío de vinculación (no de acceso).</summary>
    public const string LinkValue = "link";

    /// <summary>Clave de <see cref="AuthenticationProperties.Items"/> con el UserId capturado en servidor.</summary>
    public const string UserIdKey = "ludeka:link_user_id";

    /// <summary>
    /// Marca las propiedades del desafío con la intención de vinculación y el <paramref name="userId"/>
    /// de la sesión que lo emite, siempre capturado en servidor.
    /// </summary>
    /// <param name="properties">Propiedades del desafío OAuth que se está emitiendo.</param>
    /// <param name="userId">Identificador de la cuenta de la sesión activa.</param>
    public static void MarkLink(AuthenticationProperties properties, string userId)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        properties.Items[IntentKey] = LinkValue;
        properties.Items[UserIdKey] = userId;
    }

    /// <summary>
    /// Lee la intención de vinculación exclusivamente de <see cref="AuthenticationProperties.Items"/>.
    /// Nunca se consulta un formulario, la cadena de consulta ni ningún otro canal.
    /// </summary>
    /// <param name="properties">Propiedades recibidas en el retorno del proveedor, o <c>null</c>.</param>
    /// <param name="userId">Identificador marcado en el desafío, o vacío si no hay intención de vinculación.</param>
    /// <returns><c>true</c> si <paramref name="properties"/> marca una intención de vinculación válida.</returns>
    public static bool TryReadLink(AuthenticationProperties? properties, out string userId)
    {
        userId = string.Empty;

        if (properties is null) return false;
        if (!properties.Items.TryGetValue(IntentKey, out var intent) || intent != LinkValue) return false;
        if (!properties.Items.TryGetValue(UserIdKey, out var value) || string.IsNullOrWhiteSpace(value)) return false;

        userId = value;
        return true;
    }
}
