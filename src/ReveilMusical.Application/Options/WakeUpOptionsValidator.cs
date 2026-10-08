using Microsoft.Extensions.Options;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;

namespace ReveilMusical.Application.Options;

/// <summary>
/// Une politique invalide empêche l'application de démarrer (ValidateOnStart). Un canal de repli
/// doit être enregistré : une faute de frappe (« emial ») se voit au démarrage, pas à 6 h du matin.
/// La liste des canaux vient du conteneur, pas d'une liste en dur : ajouter WhatsApp reste un
/// adaptateur et un enregistrement.
/// </summary>
public sealed class WakeUpOptionsValidator : IValidateOptions<WakeUpOptions>
{
    private readonly INotificationChannelResolver _channels;

    public WakeUpOptionsValidator(INotificationChannelResolver channels) => _channels = channels;

    public ValidateOptionsResult Validate(string? name, WakeUpOptions options)
    {
        var failures = new List<string>();

        foreach (var raw in options.FallbackChannels)
        {
            var channel = ChannelId.Create(raw);
            if (channel.IsFailure)
            {
                failures.Add($"{WakeUpOptions.SectionName}:FallbackChannels contient un identifiant invalide : '{raw}'.");
            }
            else if (_channels.Resolve(channel.Value) is null)
            {
                failures.Add($"{WakeUpOptions.SectionName}:FallbackChannels : aucun canal n'est enregistré sous '{raw}'.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
