using ReveilMusical.Domain.Abstractions;

namespace ReveilMusical.TestSupport;

/// <summary>
/// Hasard scripté : renvoie les valeurs données, dans l'ordre, puis 0. Une valeur hors de
/// l'intervalle demandé est une erreur de test, signalée plutôt que corrigée en silence.
/// </summary>
public sealed class FakeRandom : IRandom
{
    private readonly Queue<int> _values;

    public FakeRandom(params int[] values) => _values = new Queue<int>(values);

    /// <summary>Les bornes demandées, dans l'ordre des appels.</summary>
    public List<int> Requests { get; } = [];

    public int NextIndex(int maxExclusive)
    {
        Requests.Add(maxExclusive);
        var value = _values.Count > 0 ? _values.Dequeue() : 0;

        return value >= 0 && value < maxExclusive
            ? value
            : throw new InvalidOperationException($"Valeur scriptée {value} hors de [0, {maxExclusive}[.");
    }
}
