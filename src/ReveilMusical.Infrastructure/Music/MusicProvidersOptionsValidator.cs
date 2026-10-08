using Microsoft.Extensions.Options;

namespace ReveilMusical.Infrastructure.Music;

/// <summary>Une clé de fournisseur inconnue, ou une liste vide, empêche l'application de démarrer.</summary>
internal sealed class MusicProvidersOptionsValidator : IValidateOptions<MusicProvidersOptions>
{
    public ValidateOptionsResult Validate(string? name, MusicProvidersOptions options)
    {
        if (options.Providers.Length == 0)
        {
            return ValidateOptionsResult.Fail("Music:Providers doit citer au moins un fournisseur.");
        }

        var unknown = options.Providers.Where(p => !ProviderKeys.All.Contains(ProviderKeys.Normalize(p))).ToList();

        return unknown.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Music:Providers : fournisseur(s) inconnu(s) '{string.Join("', '", unknown)}' (connus : {string.Join(", ", ProviderKeys.All)}).");
    }
}
