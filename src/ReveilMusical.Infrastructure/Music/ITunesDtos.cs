namespace ReveilMusical.Infrastructure.Music;

/// <summary>Forme brute d'une réponse iTunes Search. Ne sort jamais de <see cref="ITunesCatalog"/>.</summary>
internal sealed class ITunesSearchResponseDto
{
    public int ResultCount { get; init; }

    public List<ITunesTrackDto>? Results { get; init; }
}

internal sealed class ITunesTrackDto
{
    public string? TrackName { get; init; }

    public string? ArtistName { get; init; }

    /// <summary>
    /// Champ propre à iTunes : le brief exige qu'il ne fuite pas dans le métier. Il est désérialisé
    /// ici, et s'arrête ici (vérifié par AdapterIsolationTests et ArchitectureTests).
    /// </summary>
    public string? TrackViewUrl { get; init; }
}
