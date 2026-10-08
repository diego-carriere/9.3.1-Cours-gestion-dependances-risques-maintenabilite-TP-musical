using Microsoft.Extensions.Options;
using ReveilMusical.Domain.Abstractions;

namespace ReveilMusical.Infrastructure.Users;

/// <summary>
/// Un profil mal formé empêche l'application de démarrer, plutôt que d'échouer à 6 h du matin. Le
/// canal préféré doit être enregistré dans le conteneur, sans liste en dur : ajouter WhatsApp reste
/// un adaptateur et un enregistrement.
/// </summary>
internal sealed class UserDirectoryOptionsValidator : IValidateOptions<UserDirectoryOptions>
{
    private readonly INotificationChannelResolver _channels;

    public UserDirectoryOptionsValidator(INotificationChannelResolver channels) => _channels = channels;

    public ValidateOptionsResult Validate(string? name, UserDirectoryOptions options)
    {
        // La position plutôt que le seul identifiant : un identifiant vide doit rester localisable.
        var mapped = options.Users
            .Select((record, index) => (Index: index, Result: UserRecordMapper.Map(record)))
            .ToList();

        var failures = mapped
            .Where(user => user.Result.IsFailure)
            .Select(user => $"{UserDirectoryOptions.SectionName}:Users:{user.Index} — {user.Result.Error.Message}")
            .ToList();

        failures.AddRange(
            from user in mapped
            where user.Result.IsSuccess && _channels.Resolve(user.Result.Value.PreferredChannel) is null
            select $"{UserDirectoryOptions.SectionName}:Users:{user.Index} — aucun canal n'est enregistré sous " +
                   $"'{user.Result.Value.PreferredChannel}', le canal préféré.");

        failures.AddRange(
            from record in options.Users
            group record by record.Id.Trim() into same
            where same.Count() > 1
            select $"Utilisateur '{same.Key}' : identifiant en double.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
