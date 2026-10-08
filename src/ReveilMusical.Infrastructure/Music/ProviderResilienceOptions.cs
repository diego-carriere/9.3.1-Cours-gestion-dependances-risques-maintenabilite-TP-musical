namespace ReveilMusical.Infrastructure.Music;

public sealed class MusicResilienceOptions
{
    public const string SectionName = "Resilience:Music";

    public ProviderResilienceOptions ITunes { get; set; } = new();

    public ProviderResilienceOptions MusicBrainz { get; set; } = new();
}

/// <summary>Seuils du pipeline HTTP d'un fournisseur musical.</summary>
public sealed class ProviderResilienceOptions
{
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(8);

    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Volontairement bas : chaque réessai consomme le quota du fournisseur.</summary>
    public int RetryCount { get; set; } = 1;

    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    public int CircuitBreakerMinimumThroughput { get; set; } = 4;

    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}
