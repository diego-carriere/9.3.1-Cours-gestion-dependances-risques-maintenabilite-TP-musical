using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;

namespace ReveilMusical.TestSupport;

public sealed class FakeNotificationChannelResolver : INotificationChannelResolver
{
    private readonly Dictionary<ChannelId, INotificationChannel> _channels = [];

    public FakeNotificationChannelResolver With(string channel, INotificationChannel implementation)
    {
        _channels[ChannelId.Create(channel).Value] = implementation;
        return this;
    }

    /// <summary>Enregistre chaque canal avec un <see cref="FakeNotificationChannel"/> neuf.</summary>
    public static FakeNotificationChannelResolver Knowing(params string[] channels)
    {
        var resolver = new FakeNotificationChannelResolver();
        foreach (var channel in channels)
        {
            resolver.With(channel, new FakeNotificationChannel());
        }

        return resolver;
    }

    public INotificationChannel? Resolve(ChannelId channel) => _channels.GetValueOrDefault(channel);
}
