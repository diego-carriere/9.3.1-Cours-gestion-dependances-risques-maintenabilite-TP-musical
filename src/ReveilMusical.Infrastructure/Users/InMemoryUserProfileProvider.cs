using Microsoft.Extensions.Options;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Infrastructure.Users;

/// <summary>
/// Le service utilisateur interne, simulé (le brief le demande « à mocker ») : profils lus en
/// configuration, validés au démarrage. Un vrai client HTTP le remplacerait derrière le même port.
/// </summary>
internal sealed class InMemoryUserProfileProvider : IUserProfileProvider
{
    private readonly bool _simulateOutage;
    private readonly Dictionary<UserId, UserProfile> _profiles;

    public InMemoryUserProfileProvider(IOptions<UserDirectoryOptions> options)
    {
        _simulateOutage = options.Value.SimulateOutage;
        _profiles = options.Value.Users
            .Select(UserRecordMapper.Map)
            .Where(result => result.IsSuccess)
            .Select(result => result.Value)
            .ToDictionary(profile => profile.Id);
    }

    public Task<Result<UserProfile>> GetAsync(UserId id, CancellationToken cancellationToken)
    {
        if (_simulateOutage)
        {
            return Task.FromResult(Result.Failure<UserProfile>(ErrorKind.UserServiceUnavailable, "Service utilisateur indisponible (simulé)."));
        }

        return Task.FromResult(_profiles.TryGetValue(id, out var profile)
            ? Result.Success(profile)
            : Result.Failure<UserProfile>(ErrorKind.UserNotFound, $"Utilisateur '{id}' inconnu."));
    }
}
