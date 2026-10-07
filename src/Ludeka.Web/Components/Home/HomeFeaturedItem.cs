namespace Ludeka.Web.Components.Home;

public enum HomeFeaturedSlotType
{
    Sorteo,
    Novedad,
    Evento,
    Tendencia
}

/// <summary>
/// Modelo de elemento para la cinta de 3 destacados diarios de la portada (INC-121).
/// </summary>
public sealed class HomeFeaturedItem
{
    public HomeFeaturedSlotType Type { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Icon { get; set; } = "sparkles";
}
