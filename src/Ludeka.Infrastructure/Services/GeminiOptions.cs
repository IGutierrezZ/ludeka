using System;

namespace Ludeka.Infrastructure.Services;

/// <summary>
/// Opciones de configuración para la integración con la API de Google Gemini y su modo simulado.
/// </summary>
public class GeminiOptions
{
    public const string SectionName = "Gemini";
    public const string DefaultModel = "gemini-3.8-flash";

    /// <summary>
    /// Clave de API de Google Gemini (Google AI Studio).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Modelo de Gemini a emplear. Por defecto "gemini-3.8-flash".
    /// Si se deja vacío o se especifica "auto", el sistema autoselecciona automáticamente el modelo recomendado.
    /// </summary>
    public string Model { get; set; } = DefaultModel;

    /// <summary>
    /// URL base de la API REST de Google Gemini.
    /// </summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";

    /// <summary>
    /// Indica si se debe forzar el modo de simulación heurística offline (true por defecto para desarrollo seguro).
    /// </summary>
    public bool Simulate { get; set; } = true;

    /// <summary>
    /// Propiedad evaluada: conmuta a simulación heurística si Simulate es true o si no hay ApiKey configurada.
    /// </summary>
    public bool ShouldSimulate => Simulate || string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    /// Obtiene el modelo efectivo a utilizar, autoseleccionando el predeterminado si es nulo, vacío o "auto".
    /// </summary>
    public string GetEffectiveModel()
    {
        if (string.IsNullOrWhiteSpace(Model) || Model.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultModel;
        }

        return Model.Trim();
    }
}
