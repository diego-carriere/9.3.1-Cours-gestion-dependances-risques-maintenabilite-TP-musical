using ReveilMusical.Domain.Results;

namespace ReveilMusical.Domain.Model;

/// <summary>Liste non vide et sans doublon de mots-clés, dans l'ordre donné par l'utilisateur.</summary>
public sealed class KeywordSet
{
    private KeywordSet(IReadOnlyList<Keyword> keywords) => Keywords = keywords;

    public IReadOnlyList<Keyword> Keywords { get; }

    public static Result<KeywordSet> Create(IEnumerable<string?> rawKeywords)
    {
        var keywords = new List<Keyword>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in rawKeywords)
        {
            var keyword = Keyword.Create(raw);
            if (keyword.IsFailure)
            {
                return Result.Failure<KeywordSet>(keyword.Error);
            }

            if (seen.Add(keyword.Value.Normalized))
            {
                keywords.Add(keyword.Value);
            }
        }

        return keywords.Count == 0
            ? Result.Failure<KeywordSet>(ErrorKind.InvalidRequest, "Un jeu de mots-clés ne peut pas être vide.")
            : Result.Success(new KeywordSet(keywords));
    }
}
