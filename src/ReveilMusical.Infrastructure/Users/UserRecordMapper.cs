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

        var byWeather = new Dictionary<WeatherCondition, KeywordSet>();
        foreach (var (code, keywords) in record.KeywordsByWeather)
        {
            var set = KeywordSet.Create(keywords);
            if (!FrenchCodes.TryParseWeather(code, out var weather) || set.IsFailure)
            {
                return Fail(record, $"préférence météo invalide '{code}'.");
            }

            byWeather[weather] = set.Value;
        }

        var byDayAndWeather = new Dictionary<DayAndWeather, KeywordSet>();
        foreach (var (code, keywords) in record.KeywordsByDayAndWeather)
        {
            var parts = code.Split('+');
            var set = KeywordSet.Create(keywords);
            if (parts.Length != 2
                || !FrenchCodes.TryParseDay(parts[0], out var day)
                || !FrenchCodes.TryParseWeather(parts[1], out var weather)
                || set.IsFailure)
            {
                return Fail(record, $"surcharge jour+météo invalide '{code}' (attendu « LUNDI+PLUIE »).");
            }

            byDayAndWeather[new DayAndWeather(day, weather)] = set.Value;
        }

        var fallback = KeywordSet.Create(record.FallbackKeywords);
        if (fallback.IsFailure)
        {
            return Fail(record, "mots-clés de secours absents.");
        }

        return Result.Success(new UserProfile(
            id.Value, record.DisplayName, preferred.Value, contacts, byWeather, byDayAndWeather, fallback.Value));
    }

    private static Result<UserProfile> Fail(UserRecord record, string reason) =>
        Result.Failure<UserProfile>(ErrorKind.InvalidRequest, $"Utilisateur '{record.Id}' : {reason}");
}
