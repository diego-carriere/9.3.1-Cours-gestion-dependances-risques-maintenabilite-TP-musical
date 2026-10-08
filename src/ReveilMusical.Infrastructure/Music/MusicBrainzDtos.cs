using System.Text.Json.Serialization;

namespace ReveilMusical.Infrastructure.Music;

/// <summary>Forme brute d'une recherche MusicBrainz. Ne sort jamais de <see cref="MusicBrainzCatalog"/>.</summary>
internal sealed class MusicBrainzRecordingSearchDto
{
    public List<MusicBrainzRecordingDto>? Recordings { get; init; }
}

internal sealed class MusicBrainzRecordingDto
{
    public string? Title { get; init; }

    [JsonPropertyName("artist-credit")]
    public List<MusicBrainzArtistCreditDto>? ArtistCredit { get; init; }
}

internal sealed class MusicBrainzArtistCreditDto
{
    public string? Name { get; init; }

    [JsonPropertyName("joinphrase")]
    public string? JoinPhrase { get; init; }
}
