using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Infrastructure.Notifications;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Infrastructure.Tests.Notifications;

public sealed class NotificationRegistrationTests : IDisposable
{
    private readonly TempDirectory _outbox = new();

    [Theory]
    [InlineData("email", typeof(EmailChannelAdapter))]
    [InlineData("sms", typeof(SmsChannelAdapter))]
    [InlineData("push", typeof(PushChannelAdapter))]
    public void The_three_channels_are_registered_behind_the_resilience_decorator(string channel, Type adapter)
    {
        using var provider = BuildProvider();

        var resolved = provider.GetRequiredService<INotificationChannelResolver>().Resolve(ChannelId.Create(channel).Value);

        var decorator = Assert.IsType<ResilientNotificationChannel>(resolved);
        Assert.IsType(adapter, decorator.Inner);
    }

    [Fact]
    public void An_unknown_channel_resolves_to_null()
    {
        using var provider = BuildProvider();

        Assert.Null(provider.GetRequiredService<INotificationChannelResolver>().Resolve(ChannelId.Create("pigeon").Value));
    }

    [Fact]
    public void A_new_channel_is_one_adapter_and_one_registration()
    {
        using var provider = BuildProvider(services => services.AddNotificationChannel<FakeNotificationChannel>("WhatsApp"));

        var resolved = provider.GetRequiredService<INotificationChannelResolver>().Resolve(ChannelId.Create("whatsapp").Value);

        Assert.IsType<FakeNotificationChannel>(Assert.IsType<ResilientNotificationChannel>(resolved).Inner);
    }

    [Fact]
    public void An_invalid_channel_identifier_is_refused_at_registration()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddNotificationChannel<FakeNotificationChannel>("pas valide"));
    }

    [Fact]
    public async Task Vendor_settings_come_from_configuration()
    {
        using var provider = BuildProvider();
        var push = provider.GetRequiredService<INotificationChannelResolver>().Resolve(ChannelId.Create("push").Value)!;

        var result = await push.SendAsync(ContactAddress.Create("device-token-1234").Value,
            WakeUpMessage.Compose("Alice", new Track("A", "B"), DayOfWeek.Monday, WeatherCondition.Sunny),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Contains("device-token-1234", _outbox.Read("push.log"), StringComparison.Ordinal);
    }

    public void Dispose() => _outbox.Dispose();

    private ServiceProvider BuildProvider(Action<IServiceCollection>? extra = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Vendors:Mail:OutboxDirectory"] = _outbox.Path,
            ["Vendors:Mail:WriteToConsole"] = "false",
            ["Vendors:Sms:OutboxDirectory"] = _outbox.Path,
            ["Vendors:Sms:WriteToConsole"] = "false",
            ["Vendors:Push:OutboxDirectory"] = _outbox.Path,
            ["Vendors:Push:WriteToConsole"] = "false",
        }).Build();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddNotifications(configuration);
        extra?.Invoke(services);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
