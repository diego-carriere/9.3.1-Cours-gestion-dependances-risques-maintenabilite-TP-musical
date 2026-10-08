namespace ReveilMusical.Api.Contracts;

/// <summary>Corps de <c>POST /wake-ups</c>, tel que l'ordonnanceur l'envoie : codes du brief, en français.</summary>
internal sealed record WakeUpHttpRequest(string? UserId, string? Day, string? Weather);
