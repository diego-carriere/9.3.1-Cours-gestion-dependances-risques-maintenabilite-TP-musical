using Microsoft.Extensions.Options;
using ReveilMusical.Domain.Model;

namespace ReveilMusical.Application.Options;

/// <summary>Une politique invalide empêche l'application de démarrer (ValidateOnStart).</summary>
public sealed class WakeUpOptionsValidator : IValidateOptions<WakeUpOptions>
{
    public ValidateOptionsResult Validate(string? name, WakeUpOptions options)
    {
        var failures = new List<string>();

        failures.AddRange(
            from channel in options.FallbackChannels
            where ChannelId.Create(channel).IsFailure
            select $"{WakeUpOptions.SectionName}:FallbackChannels contient un identifiant invalide : '{channel}'.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
