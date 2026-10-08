using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.TestSupport;

public sealed class FakeUserProfileProvider : IUserProfileProvider
{
    private readonly Dictionary<UserId, UserProfile> _profiles = [];
    private DomainError? _outage;

    public FakeUserProfileProvider With(UserProfile profile)
    {
        _profiles[profile.Id] = profile;
        return this;
    }

    public FakeUserProfileProvider IsDown()
    {
        _outage = new DomainError(ErrorKind.UserServiceUnavailable, "Service utilisateur en panne (scripté).");
        return this;
    }

    public Task<Result<UserProfile>> GetAsync(UserId id, CancellationToken cancellationToken)
    {
        if (_outage is not null)
        {
            return Task.FromResult(Result.Failure<UserProfile>(_outage));
        }

        return Task.FromResult(_profiles.TryGetValue(id, out var profile)
            ? Result.Success(profile)
            : Result.Failure<UserProfile>(ErrorKind.UserNotFound, $"Utilisateur '{id}' inconnu."));
    }
}
