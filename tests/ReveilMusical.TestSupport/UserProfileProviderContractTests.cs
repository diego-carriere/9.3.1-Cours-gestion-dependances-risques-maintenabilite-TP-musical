using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using Xunit;

namespace ReveilMusical.TestSupport;

public abstract class UserProfileProviderContractTests
{
    /// <summary>Une instance qui connaît l'utilisateur « 42 ».</summary>
    protected abstract IUserProfileProvider CreateSutKnowingUser42();

    /// <summary>Une instance dont le service est en panne.</summary>
    protected abstract IUserProfileProvider CreateSutWhoseServiceIsDown();

    [Fact]
    public async Task A_service_outage_is_an_expected_failure_never_an_exception()
    {
        var result = await CreateSutWhoseServiceIsDown().GetAsync(UserId.Create("42").Value, CancellationToken.None);

        Assert.Equal(ErrorKind.UserServiceUnavailable, result.Error.Kind);
    }

    [Fact]
    public async Task A_known_user_returns_his_profile()
    {
        var result = await CreateSutKnowingUser42().GetAsync(UserId.Create("42").Value, CancellationToken.None);

        Assert.Equal("42", result.Value.Id.Value);
    }

    [Fact]
    public async Task An_unknown_user_is_an_expected_failure_never_an_exception()
    {
        var result = await CreateSutKnowingUser42().GetAsync(UserId.Create("inconnu").Value, CancellationToken.None);

        Assert.Equal(ErrorKind.UserNotFound, result.Error.Kind);
    }
}
