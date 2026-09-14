namespace Ludeka.Core.Enums;

/// <summary>
/// Tipología de contenido detectada o asignada en la ingesta social.
/// Determina la entidad de destino final al aprobarse en la moderación.
/// </summary>
public enum SocialSubmissionType
{
    Giveaway = 0,
    WeeklyRelease = 1,
    BoardGameEvent = 2,
    MediaItem = 3
}
