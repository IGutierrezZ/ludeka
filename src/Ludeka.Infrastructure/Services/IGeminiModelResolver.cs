using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Ludeka.Infrastructure.Services;

/// <summary>
/// Resolutor dinámico de modelos de Google Gemini que descubre los modelos disponibles en la API,
/// los filtra y los ordena automáticamente de menor a mayor precio manteniendo un estado de enfriamiento (cooldown) ante errores.
/// </summary>
public interface IGeminiModelResolver
{
    /// <summary>
    /// Obtiene la lista ordenada de modelos candidatos a intentar en cascada, empezando por el más económico disponible.
    /// </summary>
    Task<IReadOnlyList<string>> GetCandidateModelsAsync(CancellationToken ct = default);

    /// <summary>
    /// Registra un fallo HTTP al invocar un modelo concreto para aplicar exclusión (404/400) o enfriamiento temporal (429/503).
    /// </summary>
    void ReportModelFailure(string model, HttpStatusCode? statusCode);

    /// <summary>
    /// Registra una invocación exitosa para limpiar cualquier penalización temporal previa sobre el modelo.
    /// </summary>
    void ReportModelSuccess(string model);
}
