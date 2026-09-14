namespace Ludeka.Core.Enums;

/// <summary>
/// Tipología canónica de imágenes asociadas a un juego en catálogo y comunidad.
/// </summary>
public enum GameImageType
{
    /// <summary>
    /// Portada frontal de la caja (boxartfront).
    /// </summary>
    Cover = 1,

    /// <summary>
    /// Contraportada / trasera de la caja (boxartback).
    /// </summary>
    Back = 2,

    /// <summary>
    /// Fotografía de componentes o partida desplegada en mesa (gameplay/creative).
    /// </summary>
    Table = 3,

    /// <summary>
    /// Imagen social para moderación, sorteos, novedades o eventos.
    /// </summary>
    Social = 4
}
