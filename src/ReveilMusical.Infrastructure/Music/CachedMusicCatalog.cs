using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Infrastructure.Music;

/// <summary>
/// Decorator : met en cache les recherches d'un fournisseur (le brief : « ~20 requêtes/minute, à
/// respecter côté cache »). Une entrée fraîche épargne l'appel ; une entrée périmée est resservie
/// si le fournisseur tombe. La clé contient le fournisseur : deux fournisseurs ne partagent rien, et
/// l'Application n'a pas à le savoir. Le tirage au hasard a lieu après, dans l'Application : les
/// réveils restent variés sans rappeler le fournisseur.
/// </summary>
internal sealed partial class CachedMusicCatalog : IMusicCatalog
{
    private readonly IMusicCatalog _inner;
    private readonly string _providerKey;
    private readonly IMemoryCache _cache;
    private readonly IClock _clock;
    private readonly MusicCacheOptions _options;
    private readonly ILogger<CachedMusicCatalog> _logger;

    public CachedMusicCatalog(
        IMusicCatalog inner,
        string providerKey,
        IMemoryCache cache,
        IClock clock,
        IOptions<MusicCacheOptions> options,
        ILogger<CachedMusicCatalog> logger)
    {
        _inner = inner;
        _providerKey = providerKey;
        _cache = cache;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<Track>>> SearchAsync(Keyword keyword, CancellationToken cancellationToken)
    {
        var key = $"music:{_providerKey}:{keyword.Normalized}";
        var cached = _cache.TryGetValue(key, out CachedSearch? entry) ? entry : null;

        if (cached is not null && _clock.UtcNow - cached.StoredAtUtc < _options.Freshness)
        {
            return Result.Success(cached.Tracks);
        }

        var fresh = await _inner.SearchAsync(keyword, cancellationToken).ConfigureAwait(false);

        if (fresh.IsSuccess)
        {
            _cache.Set(key, new CachedSearch(fresh.Value, _clock.UtcNow), _options.StaleRetention);
            return fresh;
        }

        if (cached is not null)
        {
            LogServingStale(_providerKey, keyword.Value);
            return Result.Success(cached.Tracks);
        }

        return fresh;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Provider '{Provider}' failed; serving a stale search for '{Keyword}'.")]
    private partial void LogServingStale(string provider, string keyword);

    private sealed record CachedSearch(IReadOnlyList<Track> Tracks, DateTimeOffset StoredAtUtc);
}
