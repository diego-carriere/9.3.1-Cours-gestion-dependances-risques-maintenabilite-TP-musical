namespace ReveilMusical.Infrastructure.Notifications;

/// <summary>Seuils de résilience communs aux canaux (section <see cref="SectionName"/>).</summary>
public sealed record ChannelResilienceOptions
{
    public const string SectionName = "Resilience:Channels";

    /// <summary>Au-delà, la tentative est abandonnée : un réveil ne doit pas attendre un canal figé.</summary>
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(3);

    public int RetryCount { get; init; } = 1;

    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    public double CircuitBreakerFailureRatio { get; init; } = 0.5;

    public int CircuitBreakerMinimumThroughput { get; init; } = 4;

    public TimeSpan CircuitBreakerSamplingDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Pendant ce délai, un canal tombé est sauté immédiatement : la cascade passe au suivant.</summary>
    public TimeSpan CircuitBreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(30);
}
