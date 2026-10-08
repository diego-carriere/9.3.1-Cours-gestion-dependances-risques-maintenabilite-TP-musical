using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.TestSupport;

/// <summary>
/// Catalogue scripté par mot-clé. Un mot-clé non scripté renvoie une liste vide (rien ne
/// correspond), ce qui n'est pas un échec : voir <see cref="IMusicCatalog"/>.
/// </summary>
public sealed class FakeMusicCatalog : IMusicCatalog
{
    private readonly Dictionary<string, Result<IReadOnlyList<Track>>> _scripted = new(StringComparer.Ordinal);
    private DomainError? _outage;

    public List<Keyword> Searches { get; } = [];

    public FakeMusicCatalog Returns(string keyword, params Track[] tracks)
    {
        _scripted[Keyword.Create(keyword).Value.Normalized] = Result.Success<IReadOnlyList<Track>>(tracks);
        return this;
    }

    public FakeMusicCatalog FailsFor(string keyword, ErrorKind kind)
    {
        _scripted[Keyword.Create(keyword).Value.Normalized] = Result.Failure<IReadOnlyList<Track>>(kind, $"Échec scripté pour '{keyword}'.");
        return this;
    }

    /// <summary>Toute recherche échoue : le fournisseur est en panne.</summary>
    public FakeMusicCatalog IsDown(ErrorKind kind = ErrorKind.ProviderUnavailable)
    {
        _outage = new DomainError(kind, "Catalogue en panne (scripté).");
        return this;
    }

    public Task<Result<IReadOnlyList<Track>>> SearchAsync(Keyword keyword, CancellationToken cancellationToken)
    {
        Searches.Add(keyword);

        if (_outage is not null)
        {
            return Task.FromResult(Result.Failure<IReadOnlyList<Track>>(_outage));
        }

        return Task.FromResult(_scripted.TryGetValue(keyword.Normalized, out var result)
            ? result
            : Result.Success<IReadOnlyList<Track>>([]));
    }
}
