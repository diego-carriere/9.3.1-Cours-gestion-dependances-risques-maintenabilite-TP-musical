using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using Xunit;

namespace ReveilMusical.TestSupport;

/// <summary>
/// Contrat de substituabilité (Liskov) commun à toute implémentation d'<see cref="IMusicCatalog"/>,
/// réelle ou fake. Hérité par le test de chaque adaptateur et du fake : un fournisseur qui laisse
/// fuir une exception pour une panne, ou qui confond « aucun résultat » et « panne », le viole.
/// </summary>
public abstract class MusicCatalogContractTests
{
    protected static readonly Keyword SampleKeyword = Keyword.Create("soleil").Value;

    /// <summary>Une instance dont le fournisseur est injoignable.</summary>
    protected abstract IMusicCatalog CreateSutWhoseProviderIsDown();

    /// <summary>Une instance dont le fournisseur répond, mais sans aucun résultat.</summary>
    protected abstract IMusicCatalog CreateSutWithNoMatch();

    [Fact]
    public async Task An_unreachable_provider_is_an_expected_failure_never_an_exception()
    {
        var result = await CreateSutWhoseProviderIsDown().SearchAsync(SampleKeyword, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Error.Kind, new[] { ErrorKind.ProviderUnavailable, ErrorKind.Timeout });
    }

    [Fact]
    public async Task No_match_is_an_empty_success_not_a_failure()
    {
        var result = await CreateSutWithNoMatch().SearchAsync(SampleKeyword, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
