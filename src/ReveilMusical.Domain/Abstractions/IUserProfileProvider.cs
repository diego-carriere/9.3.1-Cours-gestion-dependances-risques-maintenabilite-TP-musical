using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Domain.Abstractions;

/// <summary>
/// Le service utilisateur interne : un fournisseur comme un autre, derrière une interface.
/// Échecs attendus : <see cref="ErrorKind.UserNotFound"/>, <see cref="ErrorKind.UserServiceUnavailable"/>.
/// </summary>
public interface IUserProfileProvider
{
    public Task<Result<UserProfile>> GetAsync(UserId id, CancellationToken cancellationToken);
}
