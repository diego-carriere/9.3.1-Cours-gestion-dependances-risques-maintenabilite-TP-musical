using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Infrastructure.Music;

/// <summary>Traduction commune aux deux clients HTTP : exception de transport ou de Polly → échec attendu.</summary>
internal static class ProviderCallErrors
{
    public static bool IsExpected(Exception ex, CancellationToken callerToken) =>
        IsTimeout(ex, callerToken) || ex is HttpRequestException or BrokenCircuitException or RateLimiterRejectedException;

    public static Result<IReadOnlyList<Track>> ToFailure(Exception ex, string provider, CancellationToken callerToken) => ex switch
    {
        _ when IsTimeout(ex, callerToken) => Result.Failure<IReadOnlyList<Track>>(ErrorKind.Timeout, $"{provider} a dépassé le délai imparti."),
        RateLimiterRejectedException => Result.Failure<IReadOnlyList<Track>>(
            ErrorKind.ProviderUnavailable, $"{provider} : quota de requêtes atteint, appel non émis."),
        BrokenCircuitException => Result.Failure<IReadOnlyList<Track>>(ErrorKind.ProviderUnavailable, $"{provider} : circuit ouvert."),
        _ => Result.Failure<IReadOnlyList<Track>>(ErrorKind.ProviderUnavailable, $"{provider} est injoignable."),
    };

    // Un OperationCanceledException que l'appelant n'a pas demandé est un délai (HttpClient.Timeout).
    private static bool IsTimeout(Exception ex, CancellationToken callerToken) =>
        ex is TimeoutRejectedException || (ex is OperationCanceledException && !callerToken.IsCancellationRequested);
}
