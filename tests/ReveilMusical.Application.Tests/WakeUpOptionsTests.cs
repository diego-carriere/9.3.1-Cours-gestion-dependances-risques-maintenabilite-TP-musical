using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReveilMusical.Application.Options;
using ReveilMusical.Application.WakeUp;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Application.Tests;

public sealed class WakeUpOptionsTests
{
    [Fact]
    public void Valid_options_pass_validation()
    {
        var result = new WakeUpOptionsValidator().Validate(null, new WakeUpOptions { FallbackChannels = ["push", "sms"] });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void An_invalid_fallback_channel_identifier_is_rejected()
    {
        var result = new WakeUpOptionsValidator().Validate(null, new WakeUpOptions { FallbackChannels = ["sms", "push notif"] });

        Assert.True(result.Failed);
        Assert.Contains("push notif", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void AddApplication_resolves_the_use_case_without_captive_dependency()
    {
        using var provider = BuildProvider(o => o.FallbackChannels = ["email"]);
        using var scope = provider.CreateScope();

        Assert.IsType<TriggerWakeUpUseCase>(scope.ServiceProvider.GetRequiredService<ITriggerWakeUpUseCase>());
        Assert.Equal(["email"], scope.ServiceProvider.GetRequiredService<IOptions<WakeUpOptions>>().Value.FallbackChannels);
    }

    [Fact]
    public void AddApplication_refuses_invalid_options()
    {
        using var provider = BuildProvider(o => o.FallbackChannels = ["push notif"]);

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<WakeUpOptions>>().Value);
    }

    private static ServiceProvider BuildProvider(Action<WakeUpOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IUserProfileProvider, FakeUserProfileProvider>();
        services.AddSingleton<IMusicCatalog, FakeMusicCatalog>();
        services.AddSingleton<IFallbackPlaylist, FakeFallbackPlaylist>();
        services.AddSingleton<INotificationChannelResolver, FakeNotificationChannelResolver>();
        services.AddSingleton<IOperatorAlerter, RecordingOperatorAlerter>();
        services.AddSingleton<IRandom>(new FakeRandom());
        services.AddSingleton<IClock>(new FakeClock(DateTimeOffset.UnixEpoch));
        services.AddApplication(configure);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
