using ReveilMusical.Domain.Abstractions;

namespace ReveilMusical.Infrastructure.Time;

/// <summary>Le seul endroit du code qui tire au hasard. <see cref="Random.Shared"/> est thread-safe.</summary>
internal sealed class SystemRandom : IRandom
{
    public int NextIndex(int maxExclusive) => Random.Shared.Next(maxExclusive);
}
