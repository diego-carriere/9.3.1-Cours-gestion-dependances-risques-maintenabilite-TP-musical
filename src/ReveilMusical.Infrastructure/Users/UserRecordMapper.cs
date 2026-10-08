using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Infrastructure.Users;

/// <summary>Traduit le format du service utilisateur vers le modèle du Domaine, en validant chaque champ.</summary>
internal static class UserRecordMapper
{
    public static Result<UserProfile> Map(UserRecord record)
    {
        var id = UserId.Create(record.Id);
        if (id.IsFailure)
        {
            return Fail(record, id.Error.Message);
        }

        var preferred = ChannelId.Create(record.PreferredChannel);
        if (preferred.IsFailure)
        {
            return Fail(record, preferred.Error.Message);
        }

        var contacts = new Dictionary<ChannelId, ContactAddress>();
        foreach (var (channel, address) in record.Contacts)
        {
            var channelId = ChannelId.Create(channel);
            var contact = ContactAddress.Create(address);
            if (channelId.IsFailure || contact.IsFailure)
            {
                return Fail(record, $"contact invalide '{channel}' → '{address}'.");
            }

            contacts[channelId.Value] = contact.Value;
        }

        var byWeather = new Dictionary<WeatherCondition, TrackRequest>();
        foreach (var (code, track) in record.TracksByWeather)
        {
            var request = ToRequest(track);
            if (!FrenchCodes.TryParseWeather(code, out var weather) || request.IsFailure)
            {
                return Fail(record, $"morceau par météo invalide '{code}'.");
            }

            byWeather[weather] = request.Value;
        }

        var byDayAndWeather = new Dictionary<DayAndWeather, TrackRequest>();
        foreach (var (code, track) in record.TracksByDayAndWeather)
        {
            var parts = code.Split('+');
            var request = ToRequest(track);
            if (parts.Length != 2
                || !FrenchCodes.TryParseDay(parts[0], out var day)
                || !FrenchCodes.TryParseWeather(parts[1], out var weather)
                || request.IsFailure)
            {
                return Fail(record, $"surcharge jour+météo invalide '{code}' (attendu « LUNDI+PLUIE » et un titre).");
            }

            byDayAndWeather[new DayAndWeather(day, weather)] = request.Value;
        }

        var fallback = ToRequest(record.FallbackTrack);
        if (fallback.IsFailure)
        {
            return Fail(record, "morceau de secours absent ou sans titre.");
        }

        return Result.Success(new UserProfile(
            id.Value, record.DisplayName, preferred.Value, contacts, byWeather, byDayAndWeather, fallback.Value));
    }

    private static Result<TrackRequest> ToRequest(TrackRecord? track) => TrackRequest.Create(track?.Title, track?.Artist);

    private static Result<UserProfile> Fail(UserRecord record, string reason) =>
        Result.Failure<UserProfile>(ErrorKind.InvalidRequest, $"Utilisateur '{record.Id}' : {reason}");
}
