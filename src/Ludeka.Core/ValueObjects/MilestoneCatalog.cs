using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Ludeka.Core.Enums;

namespace Ludeka.Core.ValueObjects;

/// <summary>
/// Definición inmutable de metadatos de un hito en el catálogo oficial de Ludeka.
/// </summary>
public record MilestoneDefinition(
    MilestoneType Type,
    MilestoneCategory Category,
    string Title,
    string Description,
    string IconEmoji,
    int SortOrder
);

/// <summary>
/// Catálogo canónico de hitos y logros de Ludeka.
/// </summary>
public static class MilestoneCatalog
{
    private static readonly Dictionary<MilestoneType, MilestoneDefinition> Definitions = new()
    {
        [MilestoneType.FirstGameInCollection] = new MilestoneDefinition(
            MilestoneType.FirstGameInCollection,
            MilestoneCategory.Collection,
            "Primera Piedra",
            "Añadir el primer juego a tu colección física.",
            "📦",
            1
        ),
        [MilestoneType.TenGamesInCollection] = new MilestoneDefinition(
            MilestoneType.TenGamesInCollection,
            MilestoneCategory.Collection,
            "Estantería Viva",
            "Alcanzar 10 títulos en propiedad en tu ludoteca.",
            "📚",
            2
        ),
        [MilestoneType.FirstGamePlayed] = new MilestoneDefinition(
            MilestoneType.FirstGamePlayed,
            MilestoneCategory.Collection,
            "Estrenando Tableros",
            "Marcar tu primer juego como jugado.",
            "🎲",
            3
        ),
        [MilestoneType.TenGamesPlayed] = new MilestoneDefinition(
            MilestoneType.TenGamesPlayed,
            MilestoneCategory.Collection,
            "Curtido en Mesa",
            "Marcar al menos 10 juegos diferentes como jugados.",
            "✨",
            4
        ),
        [MilestoneType.FirstGameLoaned] = new MilestoneDefinition(
            MilestoneType.FirstGameLoaned,
            MilestoneCategory.Collection,
            "Biblioteca Amiga",
            "Prestar un juego de tu ludoteca a un amigo o asociación.",
            "🤝",
            5
        ),
        [MilestoneType.FirstPlayLogged] = new MilestoneDefinition(
            MilestoneType.FirstPlayLogged,
            MilestoneCategory.Plays,
            "Cuaderno de Bitácora",
            "Registrar tu primera partida en el diario de sesiones.",
            "📝",
            6
        ),
        [MilestoneType.FivePlaysLogged] = new MilestoneDefinition(
            MilestoneType.FivePlaysLogged,
            MilestoneCategory.Plays,
            "Mesa Frecuente",
            "Registrar al menos 5 partidas en tu diario.",
            "🔥",
            7
        ),
        [MilestoneType.LargeGroupPlay] = new MilestoneDefinition(
            MilestoneType.LargeGroupPlay,
            MilestoneCategory.Plays,
            "Mesa Llena",
            "Registrar una sesión de juego con 5 o más comensales.",
            "🎉",
            8
        ),
        [MilestoneType.FirstReviewSubmitted] = new MilestoneDefinition(
            MilestoneType.FirstReviewSubmitted,
            MilestoneCategory.Community,
            "Voz en la Mesa",
            "Publicar tu primera micro-reseña y semáforo.",
            "💬",
            9
        ),
        [MilestoneType.FirstLikeGiven] = new MilestoneDefinition(
            MilestoneType.FirstLikeGiven,
            MilestoneCategory.Community,
            "Buen Ojo",
            "Apoyar con un «me gusta» a una editorial, tienda, creador o vídeo.",
            "❤️",
            10
        )
    };

    public static IReadOnlyList<MilestoneDefinition> All { get; } =
        new ReadOnlyCollection<MilestoneDefinition>(Definitions.Values.OrderBy(d => d.SortOrder).ToList());

    public static MilestoneDefinition Get(MilestoneType type)
    {
        if (Definitions.TryGetValue(type, out var def))
            return def;

        throw new KeyNotFoundException($"No existe definición en el catálogo para el hito {type}.");
    }

    public static bool TryGet(MilestoneType type, out MilestoneDefinition? definition)
    {
        if (Definitions.TryGetValue(type, out var def))
        {
            definition = def;
            return true;
        }

        definition = null;
        return false;
    }
}
