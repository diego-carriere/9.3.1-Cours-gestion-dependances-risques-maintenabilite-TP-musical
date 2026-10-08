using ReveilMusical.Domain.Results;

namespace ReveilMusical.Domain.Model;

/// <summary>
/// Un morceau, tel que le métier le connaît : un titre et un artiste. Aucun identifiant ni lien
/// propre à un fournisseur (le trackViewUrl d'iTunes reste dans son adaptateur).
/// </summary>
public sealed record Track(string Title, string Artist)
{
    public static Result<Track> Create(string? title, string? artist)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
        {
            return Result.Failure<Track>(ErrorKind.InvalidRequest, "Un morceau a un titre et un artiste.");
        }

        return Result.Success(new Track(title.Trim(), artist.Trim()));
    }
}
