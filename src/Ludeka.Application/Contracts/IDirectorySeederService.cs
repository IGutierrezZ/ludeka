using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Servicio de siembra y sincronización del directorio de Editoriales, Tiendas y Creadores (INC-54).
/// </summary>
public interface IDirectorySeederService
{
    Task<DirectorySeedResultDto> SeedDirectoryAsync(CancellationToken ct = default);
}
