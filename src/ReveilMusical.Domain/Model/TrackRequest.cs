using ReveilMusical.Domain.Results;

namespace ReveilMusical.Domain.Model;

/// <summary>
/// Un morceau choisi par l'utilisateur (le brief : « le morceau choisi par l'utilisateur pour
/// chaque type de météo »), tel qu'on le demande à un fournisseur : un titre, et l'artiste s'il est
/// connu. Le fournisseur répond par les <see cref="Track"/> qui y correspondent.
/// </summary>
public sealed record TrackRequest
{
    public const int MaxLength = 100;

    private TrackRequest(string title, string? artist)
    {
        Title = title;
        Artist = artist;
    }

    public string Title { get; }

    public string? Artist { get; }

    /// <summary>
    /// Forme insensible à la casse : deux graphies d'un même morceau partagent une entrée de cache.
    /// Le saut de ligne ne peut apparaître ni dans le titre ni dans l'artiste (espaces repliés).
    /// </summary>
    public string Normalized => $"{Title}\n{Artist}".ToUpperInvariant();

    public static Result<TrackRequest> Create(string? title, string? artist = null)
    {
        var cleanTitle = Collapse(title);
        var cleanArtist = Collapse(artist);

        if (cleanTitle.Length == 0)
        {
            return Result.Failure<TrackRequest>(ErrorKind.InvalidRequest, "Un morceau demandé a un titre.");
        }

        if (cleanTitle.Length > MaxLength || cleanArtist.Length > MaxLength)
        {
            return Result.Failure<TrackRequest>(
                ErrorKind.InvalidRequest, $"Un titre ou un artiste ne peut pas dépasser {MaxLength} caractères.");
        }

        return Result.Success(new TrackRequest(cleanTitle, cleanArtist.Length == 0 ? null : cleanArtist));
    }

    public override string ToString() => Artist is null ? Title : $"{Title} — {Artist}";

    private static string Collapse(string? raw) =>
        string.Join(' ', (raw ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
