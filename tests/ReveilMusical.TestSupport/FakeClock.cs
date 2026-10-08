using ReveilMusical.Domain.Abstractions;

namespace ReveilMusical.TestSupport;

/// <summary>Remplace l'horloge en test : la péremption s'écrit en avançant <see cref="UtcNow"/>, jamais avec un sleep.</summary>
public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset initial) => UtcNow = initial;

    public DateTimeOffset UtcNow { get; set; }

    public void Advance(TimeSpan by) => UtcNow += by;
}
