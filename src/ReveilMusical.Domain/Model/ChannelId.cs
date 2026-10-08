using ReveilMusical.Domain.Results;

namespace ReveilMusical.Domain.Model;

/// <summary>
/// Identifiant d'un canal de notification (« email », « sms », « push »...). Volontairement une
/// chaîne et pas une enum : ajouter WhatsApp ou l'appel vocal ne touche ni le Domaine ni
/// l'Application, seulement l'Infrastructure (un adaptateur et son enregistrement).
/// </summary>
public sealed record ChannelId
{
    public const int MaxLength = 32;

    private ChannelId(string value) => Value = value;

    public string Value { get; }

    public static Result<ChannelId> Create(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();

        if (value.Length == 0 || value.Length > MaxLength || !value.All(IsAllowed))
        {
            return Result.Failure<ChannelId>(
                ErrorKind.InvalidRequest, $"Identifiant de canal invalide : '{raw}' (lettres, chiffres, tirets).");
        }

        return Result.Success(new ChannelId(value));
    }

    public override string ToString() => Value;

    private static bool IsAllowed(char c) => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-';
}
