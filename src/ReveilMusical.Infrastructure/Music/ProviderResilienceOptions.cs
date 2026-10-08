using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace ReveilMusical.Infrastructure.Music;

public sealed class MusicResilienceOptions
{
    public const string SectionName = "Resilience:Music";

    [ValidateObjectMembers]
    public ProviderResilienceOptions ITunes { get; set; } = new();

    [ValidateObjectMembers]
    public ProviderResilienceOptions MusicBrainz { get; set; } = new();
}

/// <summary>
/// Seuils du pipeline HTTP d'un fournisseur musical. Les bornes reprennent celles de Polly (un
/// disjoncteur échantillonne sur 500 ms au moins) : une valeur hors bornes empêche le démarrage,
/// au lieu de faire échouer la construction du pipeline au premier réveil.
/// </summary>
public sealed class ProviderResilienceOptions
{
    [Range(typeof(TimeSpan), "00:00:00.010", "00:01:00", ParseLimitsInInvariantCulture = true)]
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(8);

    [Range(typeof(TimeSpan), "00:00:00.010", "00:01:00", ParseLimitsInInvariantCulture = true)]
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Volontairement bas : chaque réessai consomme le quota du fournisseur.</summary>
    [Range(0, 5)]
    public int RetryCount { get; set; } = 1;

    [Range(typeof(TimeSpan), "00:00:00", "00:00:30", ParseLimitsInInvariantCulture = true)]
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    [Range(0.0, 1.0)]
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    [Range(2, 1000)]
    public int CircuitBreakerMinimumThroughput { get; set; } = 4;

    [Range(typeof(TimeSpan), "00:00:00.500", "1.00:00:00", ParseLimitsInInvariantCulture = true)]
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    [Range(typeof(TimeSpan), "00:00:00.500", "1.00:00:00", ParseLimitsInInvariantCulture = true)]
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}
