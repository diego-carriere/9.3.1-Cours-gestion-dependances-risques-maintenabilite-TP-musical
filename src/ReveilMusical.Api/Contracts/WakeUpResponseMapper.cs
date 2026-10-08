using System.Text;
using ReveilMusical.Application.WakeUp;

namespace ReveilMusical.Api.Contracts;

internal static class WakeUpResponseMapper
{
    public static WakeUpHttpResponse ToHttp(WakeUpReport report) => new(
        report.UserId.Value,
        report.Delivered,
        report.Degraded,
        report.TriggeredAtUtc,
        new TrackHttpResponse(
            report.Track.Track.Title,
            report.Track.Track.Artist,
            Kebab(report.Track.Source),
            Kebab(report.Track.Level),
            report.Track.Keyword?.Value),
        new NotificationHttpResponse(
            report.PreferredChannel.Value,
            report.Dispatch.DeliveredOn?.Value,
            report.Dispatch.OperatorAlerted,
            [.. report.Dispatch.Attempts.Select(a => new DeliveryAttemptHttpResponse(a.Channel.Value, Kebab(a.Status), a.Detail))]));

    /// <summary><c>LocalPlaylist</c> → <c>local-playlist</c> : le contrat HTTP ne suit pas le nommage C#.</summary>
    internal static string Kebab(Enum value)
    {
        var name = value.ToString();
        var kebab = new StringBuilder(name.Length + 4);

        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
            {
                kebab.Append('-');
            }

            kebab.Append(char.ToLowerInvariant(name[i]));
        }

        return kebab.ToString();
    }
}
