using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;

namespace ReveilMusical.Infrastructure.Notifications;

/// <summary>
/// Pipeline Polly d'un canal : retry (englobant), circuit breaker, timeout par tentative
/// (englobé). Il agit sur des <see cref="Result{T}"/>, pas sur des exceptions : les adaptateurs
/// ne lèvent pas pour un échec attendu.
/// </summary>
internal static class ChannelResiliencePipelines
{
    public static void Configure(ResiliencePipelineBuilder<Result<DeliveryReceipt>> builder, ChannelResilienceOptions options)
    {
        if (options.RetryCount > 0)
        {
            builder.AddRetry(new RetryStrategyOptions<Result<DeliveryReceipt>>
            {
                MaxRetryAttempts = options.RetryCount,
                Delay = options.RetryDelay,
                BackoffType = DelayBackoffType.Constant,
                ShouldHandle = static args => ValueTask.FromResult(
                    args.Outcome.Exception is not BrokenCircuitException && IsTransient(args.Outcome)),
            });
        }

        builder
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<Result<DeliveryReceipt>>
            {
                FailureRatio = options.CircuitBreakerFailureRatio,
                MinimumThroughput = options.CircuitBreakerMinimumThroughput,
                SamplingDuration = options.CircuitBreakerSamplingDuration,
                BreakDuration = options.CircuitBreakerBreakDuration,
                ShouldHandle = static args => ValueTask.FromResult(IsTransient(args.Outcome)),
            })
            .AddTimeout(options.AttemptTimeout);
    }

    // Une coordonnée invalide n'est pas transitoire : ni réessai, ni impact sur le disjoncteur.
    private static bool IsTransient(Outcome<Result<DeliveryReceipt>> outcome) =>
        outcome.Exception is TimeoutRejectedException
        || (outcome.Exception is null
            && outcome.Result.IsFailure
            && outcome.Result.Error.Kind is ErrorKind.ChannelUnavailable or ErrorKind.Timeout);
}
