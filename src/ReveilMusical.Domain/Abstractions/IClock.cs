namespace ReveilMusical.Domain.Abstractions;

/// <summary>L'horloge système, dépendance cachée rendue explicite (Support J1).</summary>
public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
