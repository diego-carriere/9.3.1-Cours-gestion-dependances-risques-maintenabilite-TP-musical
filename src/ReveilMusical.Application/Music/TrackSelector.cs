using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReveilMusical.Application.Options;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;

namespace ReveilMusical.Application.Music;

/// <summary>
/// Choisit le morceau (README, « Règle de choix du morceau ») : tirage d'un mot-clé, recherche,
/// tirage d'un résultat ; puis les mots-clés de secours ; puis la playlist locale. Ne peut pas
/// échouer : c'est l'exigence « le silence n'est jamais acceptable ».
/// </summary>
public sealed partial class TrackSelector
{
    private readonly IMusicCatalog _catalog;
    private readonly IFallbackPlaylist _fallbackPlaylist;
    private readonly IRandom _random;
    private readonly WakeUpOptions _options;
    private readonly ILogger<TrackSelector> _logger;

    public TrackSelector(
        IMusicCatalog catalog,
        IFallbackPlaylist fallbackPlaylist,
        IRandom random,
        IOptions<WakeUpOptions> options,
        ILogger<TrackSelector> logger)
    {
        _catalog = catalog;
        _fallbackPlaylist = fallbackPlaylist;
        _random = random;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TrackChoice> SelectAsync(
        UserProfile profile, DayOfWeek day, WeatherCondition weather, CancellationToken cancellationToken)
    {
        var selection = profile.KeywordsFor(day, weather);
        var budget = _options.MaxSearchAttempts;

        foreach (var (keywords, level) in Stages(profile, selection))
        {
            var remaining = keywords.Keywords.ToList();

            while (remaining.Count > 0 && budget > 0)
            {
                var keyword = Draw(remaining);
                budget--;

                var search = await _catalog.SearchAsync(keyword, cancellationToken).ConfigureAwait(false);

                if (search.IsFailure)
                {
                    // Un fournisseur en panne ne répondra pas mieux au mot-clé suivant : on ne
                    // brûle pas le quota, on passe directement au dernier recours.
                    LogCatalogUnavailable(search.Error.Message);
                    return FromLocalPlaylist(weather, selection.Level);
                }

                if (search.Value.Count > 0)
                {
                    var track = search.Value[_random.NextIndex(search.Value.Count)];
                    return new TrackChoice(track, TrackSource.Catalog, level, keyword);
                }
            }
        }

        LogNothingFound(profile.Id.Value);
        return FromLocalPlaylist(weather, selection.Level);
    }

    private static IEnumerable<(KeywordSet Keywords, PreferenceLevel Level)> Stages(UserProfile profile, KeywordSelection selection)
    {
        yield return (selection.Keywords, selection.Level);

        if (selection.Level != PreferenceLevel.Fallback)
        {
            yield return (profile.FallbackKeywords, PreferenceLevel.Fallback);
        }
    }

    /// <summary>Tirage sans remise : un mot-clé déjà essayé ne l'est pas deux fois.</summary>
    private Keyword Draw(List<Keyword> remaining)
    {
        var index = _random.NextIndex(remaining.Count);
        var keyword = remaining[index];
        remaining.RemoveAt(index);
        return keyword;
    }

    private TrackChoice FromLocalPlaylist(WeatherCondition weather, PreferenceLevel level) =>
        new(_fallbackPlaylist.Pick(weather), TrackSource.LocalPlaylist, level, null);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Music catalog unavailable ({Reason}); using the local playlist.")]
    private partial void LogCatalogUnavailable(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "No catalog track matched the keywords of user {UserId}; using the local playlist.")]
    private partial void LogNothingFound(string userId);
}
