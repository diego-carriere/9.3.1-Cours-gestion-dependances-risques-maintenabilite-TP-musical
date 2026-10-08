using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Domain.Abstractions;

/// <summary>
/// Recherche d'un morceau demandé chez un fournisseur. Les résultats viennent dans l'ordre de
/// pertinence du fournisseur. Une liste vide n'est pas un échec (rien ne correspond) ;
/// un fournisseur injoignable l'est (<see cref="ErrorKind.ProviderUnavailable"/>,
/// <see cref="ErrorKind.Timeout"/>). Ne lève jamais pour un échec attendu.
/// </summary>
public interface IMusicCatalog
{
    public Task<Result<IReadOnlyList<Track>>> SearchAsync(TrackRequest request, CancellationToken cancellationToken);
}
