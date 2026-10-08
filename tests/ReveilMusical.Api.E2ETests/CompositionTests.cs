using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ReveilMusical.Application.WakeUp;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Api.E2ETests;

public sealed class CompositionTests
{
    [Fact]
    public async Task The_DI_container_builds_without_any_captive_dependency()
    {
        // Program.cs active ValidateScopes et ValidateOnBuild : la construction elle-même est le test.
        await using var factory = new ReveilApiFactory();
        using var scope = factory.Services.CreateScope();

        Assert.IsType<TriggerWakeUpUseCase>(scope.ServiceProvider.GetRequiredService<ITriggerWakeUpUseCase>());
    }

    [Theory]
    [InlineData("Music:MusicBrainz:UserAgent", "", "UserAgent")]
    [InlineData("Music:Providers:0", "spotify", "spotify")]
    [InlineData("Wakeup:FallbackChannels:0", "push notif", "push notif")]
    [InlineData("UserService:Users:0:PreferredChannel", "pigeon voyageur", "Users:0")]
    [InlineData("UserService:Users:0:PreferredChannel", "emial", "emial")]
    [InlineData("Wakeup:FallbackChannels:1", "fax", "fax")]
    [InlineData("Music:Cache:Freshness", "00:00:00", "Freshness")]
    [InlineData("Resilience:Music:ITunes:AttemptTimeout", "00:00:00", "AttemptTimeout")]
    [InlineData("Resilience:Music:MusicBrainz:CircuitBreakerMinimumThroughput", "1", "CircuitBreakerMinimumThroughput")]
    [InlineData("Resilience:Channels:RetryCount", "-1", "RetryCount")]
    [InlineData("Vendors:Mail:FromAddress", "pas une adresse", "Vendors:Mail")]
    [InlineData("Vendors:Sms:OutboxDirectory", " ", "Vendors:Sms")]
    public async Task The_application_refuses_to_start_on_invalid_configuration(string key, string value, string expectedInMessage)
    {
        await using var factory = new ReveilApiFactory(new Dictionary<string, string?> { [key] = value });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(expectedInMessage, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_host_holds_no_mutable_static_state()
    {
        var types = Assembly.Load("ReveilMusical.Api").GetTypes();

        Assert.Empty(StaticStateInspector.FindMutableStaticFields(types));
    }
}
