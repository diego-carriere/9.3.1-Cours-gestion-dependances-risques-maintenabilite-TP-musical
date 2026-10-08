using System.ComponentModel.DataAnnotations;

namespace ReveilMusical.Infrastructure.Notifications;

/// <summary>
/// Seuils de résilience communs aux canaux (section <see cref="SectionName"/>). Bornés comme ceux
/// des fournisseurs musicaux : une valeur hors bornes empêche le démarrage.
/// </summary>
public sealed record ChannelResilienceOptions
{
    public const string SectionName = "Resilience:Channels";

    /// <summary>Au-delà, la tentative est abandonnée : un réveil ne doit pas attendre un canal figé.</summary>
    [Range(typeof(TimeSpan), "00:00:00.010", "00:01:00", ParseLimitsInInvariantCulture = true)]
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(3);

    [Range(0, 5)]
    public int RetryCount { get; init; } = 1;

    [Range(typeof(TimeSpan), "00:00:00", "00:00:30", ParseLimitsInInvariantCulture = true)]
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    [Range(0.0, 1.0)]
    public double CircuitBreakerFailureRatio { get; init; } = 0.5;

    [Range(2, 1000)]
    public int CircuitBreakerMinimumThroughput { get; init; } = 4;

    [Range(typeof(TimeSpan), "00:00:00.500", "1.00:00:00", ParseLimitsInInvariantCulture = true)]
    public TimeSpan CircuitBreakerSamplingDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Pendant ce délai, un canal tombé est sauté immédiatement : la cascade passe au suivant.</summary>
    [Range(typeof(TimeSpan), "00:00:00.500", "1.00:00:00", ParseLimitsInInvariantCulture = true)]
    public TimeSpan CircuitBreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(30);
}
