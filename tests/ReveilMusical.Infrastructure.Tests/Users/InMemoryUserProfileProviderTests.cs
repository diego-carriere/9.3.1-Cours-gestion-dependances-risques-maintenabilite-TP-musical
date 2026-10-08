using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using ReveilMusical.Infrastructure.Users;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Infrastructure.Tests.Users;

public sealed class InMemoryUserProfileProviderTests : UserProfileProviderContractTests
{
    internal static UserRecord Alice() => new()
    {
        Id = "42",
        DisplayName = "Alice",
        PreferredChannel = "push",
        Contacts = new() { ["push"] = "device-token-1234", ["SMS"] = "+33612345678" },
        TracksByWeather = new()
        {
            ["SOLEIL"] = new() { Title = "Here Comes the Sun", Artist = "The Beatles" },
            ["pluie"] = new() { Title = "Set Fire to the Rain" },
        },
        TracksByDayAndWeather = new() { ["LUNDI+PLUIE"] = new() { Title = "Manic Monday", Artist = "The Bangles" } },
        FallbackTrack = new() { Title = "Wake Me Up", Artist = "Avicii" },
    };

    [Fact]
    public async Task A_record_is_mapped_to_the_domain_profile()
    {
        var profile = (await Create(Alice()).GetAsync(UserId.Create("42").Value, TestContext.Current.CancellationToken)).Value;

        Assert.Equal("Alice", profile.DisplayName);
        Assert.Equal("push", profile.PreferredChannel.Value);
        Assert.Equal("+33612345678", profile.ContactFor(ChannelId.Create("sms").Value)?.Value);
        Assert.Equal("Manic Monday — The Bangles", FirstChoice(profile, DayOfWeek.Monday, WeatherCondition.Rainy));
        Assert.Equal("Set Fire to the Rain", FirstChoice(profile, DayOfWeek.Tuesday, WeatherCondition.Rainy));
        Assert.Equal("Here Comes the Sun — The Beatles", FirstChoice(profile, DayOfWeek.Monday, WeatherCondition.Sunny));
        Assert.Equal("Wake Me Up — Avicii", FirstChoice(profile, DayOfWeek.Monday, WeatherCondition.Snowy));
    }

    [Fact]
    public async Task A_simulated_outage_is_an_unavailable_user_service()
    {
        var sut = new InMemoryUserProfileProvider(Microsoft.Extensions.Options.Options.Create(
            new UserDirectoryOptions { Users = [Alice()], SimulateOutage = true }));

        var result = await sut.GetAsync(UserId.Create("42").Value, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.UserServiceUnavailable, result.Error.Kind);
    }

    protected override IUserProfileProvider CreateSutKnowingUser42() => Create(Alice());

    private static InMemoryUserProfileProvider Create(params UserRecord[] users) =>
        new(Microsoft.Extensions.Options.Options.Create(new UserDirectoryOptions { Users = [.. users] }));

    private static string FirstChoice(UserProfile profile, DayOfWeek day, WeatherCondition weather) =>
        profile.CandidatesFor(day, weather)[0].Request.ToString();
}
