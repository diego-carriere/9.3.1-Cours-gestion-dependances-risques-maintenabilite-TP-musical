using ReveilMusical.Domain.Results;

namespace ReveilMusical.Domain.Model;

public sealed record UserId
{
    private UserId(string value) => Value = value;

    public string Value { get; }

    public static Result<UserId> Create(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? Result.Failure<UserId>(ErrorKind.InvalidRequest, "L'identifiant utilisateur est obligatoire.")
            : Result.Success(new UserId(raw.Trim()));

    public override string ToString() => Value;
}
