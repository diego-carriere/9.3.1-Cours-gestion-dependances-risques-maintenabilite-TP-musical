using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Infrastructure.Music;

/// <summary>
/// Adapter : <see cref="IMusicCatalog"/> par l'API de recherche de MusicBrainz. Deux traits propres
/// à ce fournisseur : le User-Agent identifiable est obligatoire (voir <see cref="MusicBrainzOptions"/>),
/// et l'artiste est une liste de crédits à recoller avec leurs « joinphrase ».
/// </summary>
internal sealed partial class MusicBrainzCatalog : IMusicCatalog
{
    private const string Provider = "MusicBrainz";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly MusicBrainzOptions _options;
    private readonly ILogger<MusicBrainzCatalog> _logger;

    public MusicBrainzCatalog(HttpClient httpClient, IOptions<MusicBrainzOptions> options, ILogger<MusicBrainzCatalog> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<Track>>> SearchAsync(Keyword keyword, CancellationToken cancellationToken)
    {
        try
        {
            using var request = BuildRequest(keyword);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                LogNonSuccessStatus((int)response.StatusCode);
                return Result.Failure<IReadOnlyList<Track>>(ErrorKind.ProviderUnavailable, $"{Provider} a répondu {(int)response.StatusCode}.");
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var payload = await JsonSerializer.DeserializeAsync<MusicBrainzRecordingSearchDto>(body, JsonOptions, cancellationToken).ConfigureAwait(false);

            if (payload?.Recordings is null)
            {
                LogUnexpectedPayload();
                return Result.Failure<IReadOnlyList<Track>>(ErrorKind.ProviderUnavailable, $"Réponse {Provider} inattendue.");
            }

            return Result.Success<IReadOnlyList<Track>>(
            [
                .. from recording in payload.Recordings
                   let track = Track.Create(recording.Title, ArtistOf(recording))
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

    private HttpRequestMessage BuildRequest(Keyword keyword)
    {
        var uri = FormattableString.Invariant(
            $"ws/2/recording?query={Uri.EscapeDataString(keyword.Value)}&fmt=json&limit={_options.Limit}");
        var request = new HttpRequestMessage(HttpMethod.Get, uri);

        // Sans validation : la forme « Application/Version ( contact ) » est validée au démarrage,
        // et le parseur strict d'en-têtes refuserait certains contacts (URL, adresse email).
        request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);
        return request;
    }

    private static string? ArtistOf(MusicBrainzRecordingDto recording)
    {
        if (recording.ArtistCredit is null or [])
        {
            return null;
        }

        var artist = new StringBuilder();
        foreach (var credit in recording.ArtistCredit)
        {
            artist.Append(credit.Name).Append(credit.JoinPhrase);
        }

        return artist.ToString();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "MusicBrainz returned {StatusCode}.")]
    private partial void LogNonSuccessStatus(int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MusicBrainz returned an unexpected payload shape.")]
    private partial void LogUnexpectedPayload();

    [LoggerMessage(Level = LogLevel.Warning, Message = "MusicBrainz returned a malformed body.")]
    private partial void LogMalformedBody(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MusicBrainz call failed.")]
    private partial void LogUnavailable(Exception exception);
}
