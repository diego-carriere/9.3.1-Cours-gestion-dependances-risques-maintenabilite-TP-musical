using ReveilMusical.Application.WakeUp;
using ReveilMusical.Domain.Model;

namespace ReveilMusical.Api.Contracts;

/// <summary>Valide le corps HTTP et le traduit en <see cref="WakeUpRequest"/> ; chaque champ fautif est nommé.</summary>
internal static class WakeUpRequestParser
{
    public static bool TryParse(WakeUpHttpRequest? body, out WakeUpRequest? request, out Dictionary<string, string[]> errors)
    {
        errors = [];
        request = null;

        var userId = UserId.Create(body?.UserId);
        if (userId.IsFailure)
        {
            errors["userId"] = [userId.Error.Message];
        }

        if (!FrenchCodes.TryParseDay(body?.Day, out var day))
        {
            errors["day"] = ["Jour attendu : LUNDI, MARDI, MERCREDI, JEUDI, VENDREDI, SAMEDI ou DIMANCHE."];
        }

        if (!FrenchCodes.TryParseWeather(body?.Weather, out var weather))
        {
            errors["weather"] = ["Météo attendue : SOLEIL, PLUIE, NEIGE ou NUAGEUX."];
        }

        if (errors.Count > 0)
        {
            return false;
        }

        request = new WakeUpRequest(userId.Value, day, weather);
        return true;
    }
}
