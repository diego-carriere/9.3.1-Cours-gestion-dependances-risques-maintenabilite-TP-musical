using ReveilMusical.Domain.Model;

namespace ReveilMusical.Application.WakeUp;

/// <summary>L'appel déclenché à l'heure du réveil : tout est fourni en entrée, aucun appel météo.</summary>
public sealed record WakeUpRequest(UserId UserId, DayOfWeek Day, WeatherCondition Weather);
