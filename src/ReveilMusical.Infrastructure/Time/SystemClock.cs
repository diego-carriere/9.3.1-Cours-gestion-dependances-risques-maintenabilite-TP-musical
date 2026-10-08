using ReveilMusical.Domain.Abstractions;

namespace ReveilMusical.Infrastructure.Time;

/// <summary>Le seul endroit du code qui lit l'horloge système.</summary>
internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
