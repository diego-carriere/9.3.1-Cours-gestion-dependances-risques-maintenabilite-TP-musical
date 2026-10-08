using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Infrastructure.Music;

/// <summary>
/// Adapter : <see cref="IMusicCatalog"/> par iTunes Search API. Seuls le titre et l'artiste
/// franchissent la frontière ; <c>trackViewUrl</c> reste dans le DTO. Jamais d'exception pour un
/// échec attendu : panne, quota, délai et corps illisible deviennent des <see cref="Result{T}"/>.
/// </summary>
internal sealed partial class ITunesCatalog : IMusicCatalog
{
    private const string Provider = "iTunes";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ITunesOptions _options;
    private readonly ILogger<ITunesCatalog> _logger;

    public ITunesCatalog(HttpClient httpClient, IOptions<ITunesOptions> options, ILogger<ITunesCatalog> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<Track>>> SearchAsync(Keyword keyword, CancellationToken cancellationToken)
    {
        var uri = FormattableString.Invariant(
            $"search?term={Uri.EscapeDataString(keyword.Value)}&media=music&entity=song&limit={_options.Limit}&country={_options.Country}");

        try
        {
            using var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                LogNonSuccessStatus((int)response.StatusCode);
                return Result.Failure<IReadOnlyList<Track>>(ErrorKind.ProviderUnavailable, $"{Provider} a répondu {(int)response.StatusCode}.");
            }

            // iTunes sert du JSON en « text/javascript » : lu en flux, sans se fier au Content-Type.
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var payload = await JsonSerializer.DeserializeAsync<ITunesSearchResponseDto>(body, JsonOptions, cancellationToken).ConfigureAwait(false);

            if (payload?.Results is null)
            {
                LogUnexpectedPayload();
                return Result.Failure<IReadOnlyList<Track>>(ErrorKind.ProviderUnavailable, $"Réponse {Provider} inattendue.");
            }

            return Result.Success<IReadOnlyList<Track>>(
            [
                .. from result in payload.Results
                   let track = Track.Create(result.TrackName, result.ArtistName)
                   where track.IsSuccess
                   select track.Value,
            ]);
        }
        catch (JsonException ex)
        {
            LogMalformedBody(ex);
            return Result.Failure<IReadOnlyList<Track>>(ErrorKind.ProviderUnavailable, $"Réponse {Provider} illisible.");
        }
        catch (Exception ex) when (ProviderCallErrors.IsExpected(ex, cancellationToken))
        {
            LogUnavailable(ex);
            return ProviderCallErrors.ToFailure(ex, Provider, cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "iTunes returned {StatusCode}.")]
    private partial void LogNonSuccessStatus(int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "iTunes returned an unexpected payload shape.")]
    private partial void LogUnexpectedPayload();

    [LoggerMessage(Level = LogLevel.Warning, Message = "iTunes returned a malformed body.")]
    private partial void LogMalformedBody(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "iTunes call failed.")]
    private partial void LogUnavailable(Exception exception);
}
