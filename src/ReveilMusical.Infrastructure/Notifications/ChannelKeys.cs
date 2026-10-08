namespace ReveilMusical.Infrastructure.Notifications;

/// <summary>Les identifiants des canaux livrés. Ils n'existent qu'ici, jamais dans le Domaine.</summary>
internal static class ChannelKeys
{
    public const string Email = "email";
    public const string Sms = "sms";
    public const string Push = "push";
}
