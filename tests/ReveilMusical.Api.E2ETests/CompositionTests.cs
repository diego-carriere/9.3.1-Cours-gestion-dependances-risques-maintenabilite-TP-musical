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
    [InlineData("Wakeup:MaxSearchAttempts", "0", "MaxSearchAttempts")]
    [InlineData("UserService:Users:0:PreferredChannel", "pigeon voyageur", "Users:0")]
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
