using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Infrastructure.Music;
using ReveilMusical.Infrastructure.Notifications;
using ReveilMusical.Infrastructure.Time;
using ReveilMusical.Infrastructure.Users;

namespace ReveilMusical.Infrastructure.Tests;

public sealed class InfrastructureRegistrationTests
{
    [Fact]
    public void AddInfrastructure_wires_every_port_without_captive_dependency()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Music:Providers:0"] = "itunes",
            ["Music:MusicBrainz:UserAgent"] = "ReveilMusical/1.0 ( https://example.test )",
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();

        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        var resolve = scope.ServiceProvider;

        Assert.IsType<FailoverMusicCatalog>(resolve.GetRequiredService<IMusicCatalog>());
        Assert.IsType<BuiltInFallbackPlaylist>(resolve.GetRequiredService<IFallbackPlaylist>());
        Assert.IsType<KeyedNotificationChannelResolver>(resolve.GetRequiredService<INotificationChannelResolver>());
        Assert.IsType<LoggingOperatorAlerter>(resolve.GetRequiredService<IOperatorAlerter>());
        Assert.IsType<InMemoryUserProfileProvider>(resolve.GetRequiredService<IUserProfileProvider>());
        Assert.IsType<SystemClock>(resolve.GetRequiredService<IClock>());
        Assert.IsType<SystemRandom>(resolve.GetRequiredService<IRandom>());
    }

    [Fact]
    public void The_system_clock_reads_the_current_utc_time()
    {
        var before = DateTimeOffset.UtcNow;

        var now = new SystemClock().UtcNow;

        Assert.InRange(now, before, DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, now.Offset);
    }

    [Fact]
    public void The_system_random_stays_within_bounds()
    {
        var random = new SystemRandom();

        Assert.All(Enumerable.Range(0, 200), _ => Assert.InRange(random.NextIndex(3), 0, 2));
    }
}
