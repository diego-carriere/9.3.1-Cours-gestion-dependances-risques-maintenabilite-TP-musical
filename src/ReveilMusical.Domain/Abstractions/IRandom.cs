namespace ReveilMusical.Domain.Abstractions;

/// <summary>Le hasard, dépendance cachée rendue explicite : scripté en test, donc déterministe.</summary>
public interface IRandom
{
    /// <summary>Un entier dans [0, <paramref name="maxExclusive"/>[.</summary>
    public int NextIndex(int maxExclusive);
}
