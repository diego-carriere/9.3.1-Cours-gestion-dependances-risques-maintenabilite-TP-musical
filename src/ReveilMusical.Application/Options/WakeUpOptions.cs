namespace ReveilMusical.Application.Options;

/// <summary>Politique du réveil, liée à la section <see cref="SectionName"/> par le composition root.</summary>
public sealed class WakeUpOptions
{
    public const string SectionName = "Wakeup";

    /// <summary>
    /// Ordre de la cascade après le canal préféré. Un tableau, pas une liste : le binder de
    /// configuration remplace un tableau, alors qu'il complète une liste déjà remplie.
    /// </summary>
    public string[] FallbackChannels { get; set; } = [];
}
