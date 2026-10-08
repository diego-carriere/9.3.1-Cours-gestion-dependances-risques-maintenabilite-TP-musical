using Microsoft.Extensions.Logging;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Application.Music;

/// <summary>
/// Choisit le morceau (README, « Règle de choix du morceau ») : le morceau que l'utilisateur a
/// choisi pour ce jour et cette météo, cherché chez le fournisseur ; s'il est introuvable, le niveau
/// de préférence suivant ; si le fournisseur est en panne, la playlist locale. Ne peut pas échouer :
/// c'est l'exigence « le silence n'est jamais acceptable ».
/// </summary>
public sealed partial class TrackSelector
{
    private readonly IMusicCatalog _catalog;
    private readonly IFallbackPlaylist _fallbackPlaylist;
    private readonly ILogger<TrackSelector> _logger;

    public TrackSelector(IMusicCatalog catalog, IFallbackPlaylist fallbackPlaylist, ILogger<TrackSelector> logger)
    {
        _catalog = catalog;
        _fallbackPlaylist = fallbackPlaylist;
        _logger = logger;
    }

    public async Task<TrackChoice> SelectAsync(
        UserProfile profile, DayOfWeek day, WeatherCondition weather, CancellationToken cancellationToken)
    {
        // Au plus trois recherches (jour + météo, météo, secours) : le quota des fournisseurs est
        // borné par construction.
        var candidates = profile.CandidatesFor(day, weather);

        foreach (var (request, level) in candidates)
        {
            var search = await SearchAsync(request, cancellationToken).ConfigureAwait(false);

            if (search.IsFailure)
            {
                // Un fournisseur en panne ne répondra pas mieux au morceau suivant : on ne brûle pas
                // le quota, on passe directement au dernier recours.
                LogCatalogUnavailable(search.Error.Message);
                return FromLocalPlaylist(weather);
            }

            if (search.Value.Count > 0)
            {
                // Le premier résultat : le fournisseur les classe par pertinence.
                return new TrackChoice(search.Value[0], TrackSource.Catalog, level, request);
            }
        }

        LogNothingFound(profile.Id.Value);
        return FromLocalPlaylist(weather);
    }

    /// <summary>
    /// Filet du métier : un catalogue qui lève malgré son contrat compte comme une panne, ce qui
    /// mène à la playlist locale. Seule l'annulation demandée par l'appelant se propage.
    /// </summary>
    private async Task<Result<IReadOnlyList<Track>>> SearchAsync(TrackRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await _catalog.SearchAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogCatalogThrew(ex);
            return Result.Failure<IReadOnlyList<Track>>(ErrorKind.ProviderUnavailable, "Erreur inattendue du catalogue musical.");
        }
    }

    private TrackChoice FromLocalPlaylist(WeatherCondition weather) =>
        new(_fallbackPlaylist.Pick(weather), TrackSource.LocalPlaylist, Level: null, Request: null);

    [LoggerMessage(Level = LogLevel.Error, Message = "Music catalog threw instead of returning a result.")]
    private partial void LogCatalogThrew(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Music catalog unavailable ({Reason}); using the local playlist.")]
    private partial void LogCatalogUnavailable(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "No catalog track matched the tracks chosen by user {UserId}; using the local playlist.")]
    private partial void LogNothingFound(string userId);
}
