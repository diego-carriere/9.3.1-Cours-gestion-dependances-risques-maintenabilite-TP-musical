using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.TestSupport;

/// <summary>
/// Catalogue scripté par morceau demandé. Un morceau non scripté renvoie une liste vide (rien ne
/// correspond), ce qui n'est pas un échec : voir <see cref="IMusicCatalog"/>.
/// </summary>
public sealed class FakeMusicCatalog : IMusicCatalog
{
    private readonly Dictionary<string, Result<IReadOnlyList<Track>>> _scripted = new(StringComparer.Ordinal);
    private DomainError? _outage;
    private Exception? _exception;

    public List<TrackRequest> Searches { get; } = [];

    /// <summary>Le morceau demandé par son seul titre, sans artiste.</summary>
    public FakeMusicCatalog Returns(string title, params Track[] tracks) => Returns(Request(title), tracks);

    public FakeMusicCatalog Returns(TrackRequest request, params Track[] tracks)
    {
        _scripted[request.Normalized] = Result.Success<IReadOnlyList<Track>>(tracks);
        return this;
    }

    public FakeMusicCatalog FailsFor(string title, ErrorKind kind)
    {
        _scripted[Request(title).Normalized] = Result.Failure<IReadOnlyList<Track>>(kind, $"Échec scripté pour '{title}'.");
        return this;
    }

    /// <summary>Toute recherche échoue : le fournisseur est en panne.</summary>
    public FakeMusicCatalog IsDown(ErrorKind kind = ErrorKind.ProviderUnavailable)
    {
        _outage = new DomainError(kind, "Catalogue en panne (scripté).");
        return this;
    }

    /// <summary>Simule un catalogue qui viole son contrat : il lève au lieu de renvoyer un échec.</summary>
    public FakeMusicCatalog Throws(Exception exception)
    {
        _exception = exception;
        return this;
    }

    public Task<Result<IReadOnlyList<Track>>> SearchAsync(TrackRequest request, CancellationToken cancellationToken)
    {
        Searches.Add(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (_exception is not null)
        {
            throw _exception;
        }

        if (_outage is not null)
        {
            return Task.FromResult(Result.Failure<IReadOnlyList<Track>>(_outage));
        }

        return Task.FromResult(_scripted.TryGetValue(request.Normalized, out var result)
            ? result
            : Result.Success<IReadOnlyList<Track>>([]));
    }

    private static TrackRequest Request(string title) => TrackRequest.Create(title).Value;
}
