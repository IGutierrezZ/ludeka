namespace Ludeka.Application.Options;

/// <summary>
/// Configuración para el servicio de almacenamiento de medios en Cloudflare R2 (compatible con API S3).
/// </summary>
public class CloudflareR2Options
{
    public const string SectionName = "Cloudflare";

    /// <summary>
    /// ID de la cuenta de Cloudflare (usado en el endpoint S3 https://{AccountId}.r2.cloudflarestorage.com).
    /// </summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// Clave de acceso R2 (Access Key ID).
    /// </summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>
    /// Clave secreta R2 (Secret Access Key).
    /// </summary>
    public string SecretAccessKey { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del bucket de almacenamiento (ej. "ludeka-media").
    /// </summary>
    public string BucketName { get; set; } = "ludeka-media";

    /// <summary>
    /// URL base pública del CDN para servir los activos multimedia (ej. "https://cdn.ludeka.com").
    /// </summary>
    public string PublicCdnBaseUrl { get; set; } = "https://cdn.ludeka.com";

    /// <summary>
    /// Indica si el servicio debe ejecutarse en modo simulado/local sin conectar a Cloudflare.
    /// Si las credenciales están vacías o Simulate es true, se usará el almacenamiento simulado.
    /// </summary>
    public bool Simulate { get; set; } = true;

    /// <summary>
    /// Determina si las credenciales mínimas para operar contra Cloudflare R2 están configuradas.
    /// </summary>
    public bool HasValidCredentials =>
        !Simulate &&
        !string.IsNullOrWhiteSpace(AccountId) &&
        !string.IsNullOrWhiteSpace(AccessKeyId) &&
        !string.IsNullOrWhiteSpace(SecretAccessKey) &&
        !string.IsNullOrWhiteSpace(BucketName);
}
