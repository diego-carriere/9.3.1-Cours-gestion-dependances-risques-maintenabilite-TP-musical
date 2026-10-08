using ReveilMusical.Application.Music;
using ReveilMusical.Application.Notifications;
using ReveilMusical.Domain.Model;

namespace ReveilMusical.Application.WakeUp;

public sealed record WakeUpReport(
    UserId UserId,
    ChannelId PreferredChannel,
    TrackChoice Track,
    DispatchOutcome Dispatch,
    DateTimeOffset TriggeredAtUtc)
{
    public bool Delivered => Dispatch.DeliveredOn is not null;

    /// <summary>Playlist locale, autre canal que le préféré, ou réveil non remis.</summary>
    public bool Degraded => Track.Source == TrackSource.LocalPlaylist || Dispatch.DeliveredOn != PreferredChannel;
}
