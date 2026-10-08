using Microsoft.Extensions.Options;

namespace ReveilMusical.Infrastructure.Users;

/// <summary>Un profil mal formé empêche l'application de démarrer, plutôt que d'échouer à 6 h du matin.</summary>
internal sealed class UserDirectoryOptionsValidator : IValidateOptions<UserDirectoryOptions>
{
    public ValidateOptionsResult Validate(string? name, UserDirectoryOptions options)
    {
        // La position plutôt que le seul identifiant : un identifiant vide doit rester localisable.
        var failures = options.Users
            .Select((record, index) => (Index: index, Result: UserRecordMapper.Map(record)))
            .Where(mapped => mapped.Result.IsFailure)
            .Select(mapped => $"{UserDirectoryOptions.SectionName}:Users:{mapped.Index} — {mapped.Result.Error.Message}")
            .ToList();

        failures.AddRange(
            from record in options.Users
            group record by record.Id.Trim() into same
            where same.Count() > 1
            select $"Utilisateur '{same.Key}' : identifiant en double.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
