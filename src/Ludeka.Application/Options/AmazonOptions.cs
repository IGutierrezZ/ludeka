namespace Ludeka.Application.Options;

/// <summary>
/// Opciones de configuración para la integración de datos y precios de Amazon.
/// </summary>
public class AmazonOptions
{
    public const string SectionName = "Amazon";

    /// <summary>
    /// Proveedor de catálogo activo: "Bridge" (API puente como Rainforest), "Official" (PA-API 5.0) o "None".
    /// </summary>
    public string Provider { get; set; } = "None";

    /// <summary>
    /// Tag de afiliado para el marketplace (ej. "ludeka-21").
    /// </summary>
    public string AssociateTag { get; set; } = "ludeka-21";

    /// <summary>
    /// Dominio del marketplace de Amazon (por defecto "amazon.es").
    /// </summary>
    public string DomainMatch { get; set; } = "amazon.es";

    /// <summary>
    /// Opciones del proveedor puente (Rainforest API).
    /// </summary>
    public AmazonBridgeOptions Bridge { get; set; } = new();

    /// <summary>
    /// Opciones del proveedor oficial PA-API 5.0.
    /// </summary>
    public AmazonPaApiOptions PaApi { get; set; } = new();
}

public class AmazonBridgeOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.rainforestapi.com";
    public string AmazonDomain { get; set; } = "amazon.es";
}

public class AmazonPaApiOptions
{
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string AssociateTag { get; set; } = "ludeka-21";
    public string Region { get; set; } = "eu-west-1";
    public string Host { get; set; } = "webservices.amazon.es";
}
