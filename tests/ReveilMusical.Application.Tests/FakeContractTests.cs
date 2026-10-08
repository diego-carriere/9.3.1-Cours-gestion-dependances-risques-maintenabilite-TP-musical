using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Application.Tests;

// Les fakes sur lesquels reposent les tests du cas d'usage respectent le même contrat que les
// vrais adaptateurs : un test vert sur fake ne ment pas sur le comportement réel.

public sealed class FakeMusicCatalogContractTests : MusicCatalogContractTests
{
    protected override IMusicCatalog CreateSutWhoseProviderIsDown() => new FakeMusicCatalog().IsDown();

    protected override IMusicCatalog CreateSutWithNoMatch() => new FakeMusicCatalog();
}

public sealed class FakeNotificationChannelContractTests : NotificationChannelContractTests
{
    protected override ContactAddress ValidContact => ContactAddress.Create("ok").Value;

    protected override ContactAddress InvalidContact => ContactAddress.Create("rejected").Value;

    protected override INotificationChannel CreateWorkingSut() => new FakeNotificationChannel().Rejects("rejected");

    protected override INotificationChannel CreateSutWhoseServiceIsDown() =>
        new FakeNotificationChannel().FailsWith(ErrorKind.ChannelUnavailable);
}

public sealed class FakeUserProfileProviderContractTests : UserProfileProviderContractTests
{
    protected override IUserProfileProvider CreateSutKnowingUser42() =>
        new FakeUserProfileProvider().With(new ProfileBuilder("42").Build());
}

public sealed class FakeFallbackPlaylistContractTests : FallbackPlaylistContractTests
{
    protected override IFallbackPlaylist CreateSut() => new FakeFallbackPlaylist();
}
