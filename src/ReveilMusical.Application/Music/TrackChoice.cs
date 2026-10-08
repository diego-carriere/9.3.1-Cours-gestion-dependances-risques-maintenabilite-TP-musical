using ReveilMusical.Domain.Model;

namespace ReveilMusical.Application.Music;

public enum TrackSource
{
    /// <summary>Trouvé chez un fournisseur musical (en direct ou depuis son cache).</summary>
    Catalog,

    /// <summary>Aucun fournisseur n'a répondu : playlist locale, mode dégradé.</summary>
    LocalPlaylist,
}

/// <summary>Le morceau retenu, d'où il vient, et le mot-clé qui l'a donné (<c>null</c> pour la playlist locale).</summary>
public sealed record TrackChoice(Track Track, TrackSource Source, PreferenceLevel Level, Keyword? Keyword);
