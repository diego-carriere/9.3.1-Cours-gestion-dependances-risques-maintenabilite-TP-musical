using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Infrastructure.Music;

/// <summary>
/// Composite : le seul <see cref="IMusicCatalog"/> exposé au conteneur. Il essaie les fournisseurs
/// dans l'ordre de <c>Music:Providers</c>, relu sur <see cref="IConfiguration"/> à chaque appel :
/// éditer la configuration change de source sans recompiler ni redémarrer. Une liste vide est une
/// réponse (rien ne correspond), pas une panne : seule une panne passe au fournisseur suivant.
/// </summary>
internal sealed partial class FailoverMusicCatalog : IMusicCatalog
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FailoverMusicCatalog> _logger;

    public FailoverMusicCatalog(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<FailoverMusicCatalog> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<Track>>> SearchAsync(TrackRequest request, CancellationToken cancellationToken)
    {
        var failures = new List<string>();

        foreach (var key in ConfiguredProviders())
        {
            var provider = _serviceProvider.GetKeyedService<IMusicCatalog>(key);
            if (provider is null)
            {
                LogUnknownProvider(key);
                continue;
            }

            var result = await provider.SearchAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                return result;
            }

            LogProviderFailed(key, result.Error.Message);
            failures.Add($"{key} ({result.Error.Message})");
        }

        return Result.Failure<IReadOnlyList<Track>>(
            ErrorKind.ProviderUnavailable,
            failures.Count == 0
                ? "Aucun fournisseur musical configuré."
                : $"Aucun fournisseur musical n'a répondu : {string.Join(", ", failures)}.");
    }

    private IEnumerable<string> ConfiguredProviders() =>
        from child in _configuration.GetSection($"{MusicProvidersOptions.SectionName}:{nameof(MusicProvidersOptions.Providers)}").GetChildren()
        let key = ProviderKeys.Normalize(child.Value)
        where key.Length > 0
        select key;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Music:Providers names unknown provider '{Key}'; skipped.")]
    private partial void LogUnknownProvider(string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Music provider '{Key}' failed ({Reason}); trying the next one.")]
    private partial void LogProviderFailed(string key, string reason);
}
