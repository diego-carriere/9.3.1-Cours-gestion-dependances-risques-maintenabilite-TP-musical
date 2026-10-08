using ReveilMusical.Domain.Model;

namespace ReveilMusical.Application.Music;

public enum TrackSource
{
    /// <summary>Trouvé chez un fournisseur musical (en direct ou depuis son cache).</summary>
    Catalog,

    /// <summary>Aucun fournisseur n'a répondu : playlist locale, mode dégradé.</summary>
    LocalPlaylist,
}

/// <summary>
/// Le morceau retenu, d'où il vient, le niveau de préférence de l'utilisateur qui l'a donné, et le
/// morceau demandé. Niveau et morceau demandé sont <c>null</c> pour la playlist locale : ce morceau,
/// l'utilisateur ne l'a pas choisi.
/// </summary>
public sealed record TrackChoice(Track Track, TrackSource Source, PreferenceLevel? Level, TrackRequest? Request);
