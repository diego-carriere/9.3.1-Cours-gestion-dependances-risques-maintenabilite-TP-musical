using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Domain.Abstractions;

/// <summary>
/// Recherche de morceaux par mot-clé. Une liste vide n'est pas un échec (rien ne correspond) ;
/// un fournisseur injoignable l'est (<see cref="ErrorKind.ProviderUnavailable"/>,
/// <see cref="ErrorKind.Timeout"/>). Ne lève jamais pour un échec attendu.
/// </summary>
public interface IMusicCatalog
{
    public Task<Result<IReadOnlyList<Track>>> SearchAsync(Keyword keyword, CancellationToken cancellationToken);
}
