namespace Ludeka.Infrastructure.Bgg;

public class BggOptions
{
    public const string SectionName = "Bgg";

    private string? _token;

    /// <summary>
    /// Clave de API de BoardGameGeek (parámetro o cabecera X-BGG-API-KEY).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Token de autorización Bearer emitido por BoardGameGeek (Applications).
    /// </summary>
    public string? ApiToken
    {
        get => _token;
        set => _token = value;
    }

    /// <summary>
    /// Alias de ApiToken alineado con el estándar de autorización Bearer de BGG.
    /// </summary>
    public string? BearerToken
    {
        get => _token;
        set => _token = value;
    }

    /// <summary>
    /// URL base para la API XMLAPI2.
    /// </summary>
    public string BaseUrl { get; set; } = "https://boardgamegeek.com/xmlapi2/";

    /// <summary>
    /// Cabecera User-Agent requerida por las directrices de BGG.
    /// </summary>
    public string UserAgent { get; set; } = "LudekaApp/1.0 (https://ludeka.es; contacto@ludeka.es)";

    /// <summary>
    /// Activa el modo simulado/mock para la API de BGG.
    /// Si es true, o si no hay credenciales (ApiToken/ApiKey), se utiliza SimulatedBggClient.
    /// </summary>
    public bool SimulateApi { get; set; } = true;

    /// <summary>
    /// Determina si debe utilizarse el cliente simulado en lugar del cliente HTTP real.
    /// En producción (SimulateApi=false) se conecta a la API real aunque no haya token configurado.
    /// </summary>
    public bool ShouldSimulate => SimulateApi;

    /// <summary>
    /// Número máximo de reintentos en el ciclo de sondeo ante respuestas HTTP 202 Accepted.
    /// </summary>
    public int MaxPollingRetries { get; set; } = 6;

    /// <summary>
    /// Tiempo máximo total en segundos para el sondeo de una colección antes de finalizar por timeout.
    /// </summary>
    public int PollingTimeoutSeconds { get; set; } = 45;

    /// <summary>
    /// Pausa inicial en segundos para el primer reintento de sondeo tras un HTTP 202.
    /// </summary>
    public int InitialPollingDelaySeconds { get; set; } = 3;

    /// <summary>
    /// Número máximo de intentos de reintento ante código HTTP 429 Too Many Requests o 503.
    /// </summary>
    public int MaxRateLimitRetries { get; set; } = 3;
}
