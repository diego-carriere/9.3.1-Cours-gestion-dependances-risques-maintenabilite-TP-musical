using System.Net;
using System.Threading.RateLimiting;
using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Retry;

namespace ReveilMusical.Infrastructure.Music;

/// <summary>
/// Pipeline Polly d'un client HTTP de fournisseur, de l'extérieur vers l'intérieur : timeout total,
/// retry, circuit breaker, limiteur de débit, timeout par tentative. Le limiteur est placé au plus
/// près de l'appel : chaque tentative, réessais compris, consomme un jeton du quota.
/// </summary>
internal static class HttpResiliencePipelines
{
    public static void Configure(
        ResiliencePipelineBuilder<HttpResponseMessage> builder, ProviderResilienceOptions options, RateLimiter limiter)
    {
        builder.AddTimeout(options.TotalTimeout);

        if (options.RetryCount > 0)
        {
            builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = options.RetryCount,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = options.RetryBaseDelay,
                ShouldHandle = static args => ValueTask.FromResult(ShouldRetry(args.Outcome)),
            });
        }

        builder
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                SamplingDuration = options.CircuitBreakerSamplingDuration,
                FailureRatio = options.CircuitBreakerFailureRatio,
                MinimumThroughput = options.CircuitBreakerMinimumThroughput,
                BreakDuration = options.CircuitBreakerBreakDuration,
                ShouldHandle = static args => ValueTask.FromResult(IsFailure(args.Outcome)),
            })
            .AddRateLimiter(limiter)
            .AddTimeout(options.AttemptTimeout);
    }

    /// <summary>iTunes : ~20 requêtes/minute. Pas de file d'attente : au-delà, on rejette, et le failover prend le relais.</summary>
    public static RateLimiter ITunesLimiter(int requestsPerMinute) => new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
    {
        PermitLimit = requestsPerMinute,
        Window = TimeSpan.FromMinutes(1),
        SegmentsPerWindow = 6,
        QueueLimit = 0,
        AutoReplenishment = true,
    });

    /// <summary>MusicBrainz : 1 requête/seconde. Une petite file absorbe un léger pic en patientant.</summary>
    public static RateLimiter MusicBrainzLimiter(int requestsPerSecond) => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
    {
        PermitLimit = requestsPerSecond,
        Window = TimeSpan.FromSeconds(1),
        QueueLimit = 2,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = true,
    });

    // Ne réessaie jamais un circuit ouvert, un quota épuisé ni un 4xx : aucun n'est transitoire
    // à l'échelle d'un réessai, et réessayer brûlerait le quota.
    private static bool ShouldRetry(Outcome<HttpResponseMessage> outcome) =>
        outcome.Exception is not (BrokenCircuitException or RateLimiterRejectedException) && IsFailure(outcome);

    private static bool IsFailure(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is RateLimiterRejectedException)
        {
            return false; // Notre propre quota local : le fournisseur n'y est pour rien.
        }

        if (outcome.Exception is not null)
        {
            return true;
        }

        var status = outcome.Result?.StatusCode;
        return status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || (status is not null && (int)status >= 500);
    }
}
