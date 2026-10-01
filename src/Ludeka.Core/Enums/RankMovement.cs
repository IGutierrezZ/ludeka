namespace Ludeka.Core.Enums;

/// <summary>
/// Representa la variación de posición de un juego en el ranking diario de tendencias respecto a la fecha anterior.
/// </summary>
public enum RankMovement
{
    /// <summary>Conserva la misma posición respecto al día anterior.</summary>
    Same = 0,

    /// <summary>Ha subido puestos en el ranking (posición numérica menor).</summary>
    Up = 1,

    /// <summary>Ha bajado puestos en el ranking (posición numérica mayor).</summary>
    Down = 2,

    /// <summary>Nueva entrada en el Top 50 que no figuraba en el día anterior.</summary>
    New = 3
}
