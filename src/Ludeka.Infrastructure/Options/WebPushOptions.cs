namespace Ludeka.Infrastructure.Options;

/// <summary>
/// Configuración VAPID (Voluntary Application Server Identification) para notificaciones Web Push en PWA y navegador (INC-149).
/// Incluye un par de claves ECDSA P-256 válidas de desarrollo por defecto cuando no se configuran secretos externos.
/// </summary>
public class WebPushOptions
{
    public const string SectionName = "WebPush";

    /// <summary>
    /// Clave pública VAPID en formato Base64Url sin relleno (65 bytes descomprimidos P-256).
    /// </summary>
    public string PublicKey { get; set; } = "BEl62iUYgUivxIkv69yViEuiBIa-Ib9-SkvMeAtA3LFgDzkrxZJjSgSnfckjBJuBkr3qBUYIHBQFLXYp5Nksh8U";

    /// <summary>
    /// Clave privada VAPID en formato Base64Url sin relleno (32 bytes P-256).
    /// </summary>
    public string PrivateKey { get; set; } = "UUxI4O8-FbRouAevSmBQ6o18hgE4nSG3qwvJTfKc-ls";

    /// <summary>
    /// Sujeto de contacto VAPID (mailto: o https://).
    /// </summary>
    public string Subject { get; set; } = "mailto:hola@ludeka.es";
}
