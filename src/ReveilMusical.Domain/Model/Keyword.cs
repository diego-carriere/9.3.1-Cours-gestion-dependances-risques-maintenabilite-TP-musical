using ReveilMusical.Domain.Results;

namespace ReveilMusical.Domain.Model;

/// <summary>Un terme de recherche musicale choisi par l'utilisateur (ex. « beau temps »).</summary>
public sealed record Keyword
{
    public const int MaxLength = 100;

    private Keyword(string value) => Value = value;

    public string Value { get; }

    /// <summary>Forme insensible à la casse : deux graphies d'un même mot partagent une entrée de cache.</summary>
    public string Normalized => Value.ToUpperInvariant();

    public static Result<Keyword> Create(string? raw)
    {
        var words = (raw ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var value = string.Join(' ', words);

        if (value.Length == 0)
        {
            return Result.Failure<Keyword>(ErrorKind.InvalidRequest, "Un mot-clé ne peut pas être vide.");
        }

        if (value.Length > MaxLength)
        {
            return Result.Failure<Keyword>(
                ErrorKind.InvalidRequest, $"Un mot-clé ne peut pas dépasser {MaxLength} caractères.");
        }

        return Result.Success(new Keyword(value));
    }

    public override string ToString() => Value;
}
