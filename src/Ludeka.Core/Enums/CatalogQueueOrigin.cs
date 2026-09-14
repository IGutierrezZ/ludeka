namespace Ludeka.Core.Enums;

/// <summary>
/// Especifica el origen por el cual un juego fue registrado en la cola de auto-catalogación.
/// </summary>
public enum CatalogQueueOrigin
{
    /// <summary>
    /// Solicitado por un usuario comunitario al importar su ludoteca desde BGG.
    /// </summary>
    UserImport = 0,

    /// <summary>
    /// Descubierto y extraído automáticamente a partir de una publicación o novedad editorial.
    /// </summary>
    NewsDiscovery = 1,

    /// <summary>
    /// Incorporado durante la ejecución nocturna para completar el cupo diario con los mejores juegos de BGG.
    /// </summary>
    TopBggBackfill = 2,

    /// <summary>
    /// Descubierto automáticamente a partir de los lanzamientos y novedades del año en BoardGameGeek.
    /// </summary>
    BggNewReleases = 3,

    /// <summary>
    /// Descubierto automáticamente a partir de la lista de tendencias mundiales (Hotness) de BoardGameGeek.
    /// </summary>
    BggHotness = 4
}

