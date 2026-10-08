using ReveilMusical.Domain.Results;

namespace ReveilMusical.Domain.Model;

/// <summary>
/// Coordonnée de l'utilisateur sur un canal : adresse email, numéro, jeton d'appareil... Opaque
/// pour le métier ; c'est l'adaptateur du canal qui en valide le format.
/// </summary>
public sealed record ContactAddress
{
    private ContactAddress(string value) => Value = value;

    public string Value { get; }

    public static Result<ContactAddress> Create(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? Result.Failure<ContactAddress>(ErrorKind.InvalidContact, "Une coordonnée ne peut pas être vide.")
            : Result.Success(new ContactAddress(raw.Trim()));

    public override string ToString() => Value;
}
