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
    protected static readonly TrackRequest SampleRequest = TrackRequest.Create("Soleil").Value;

    /// <summary>Une instance dont le fournisseur est injoignable.</summary>
    protected abstract IMusicCatalog CreateSutWhoseProviderIsDown();

    /// <summary>Une instance dont le fournisseur répond, mais sans aucun résultat.</summary>
    protected abstract IMusicCatalog CreateSutWithNoMatch();

    /// <summary>Une instance dont le fournisseur trouve <see cref="SampleRequest"/>.</summary>
    protected abstract IMusicCatalog CreateSutWithAMatch();

    [Fact]
    public async Task A_known_track_returns_at_least_one_track()
    {
        var result = await CreateSutWithAMatch().SearchAsync(SampleRequest, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value);
    }

    [Fact]
    public async Task A_cancellation_requested_by_the_caller_propagates_never_a_failure()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateSutWithAMatch().SearchAsync(SampleRequest, cancellation.Token));
    }

    [Fact]
    public async Task An_unreachable_provider_is_an_expected_failure_never_an_exception()
    {
        var result = await CreateSutWhoseProviderIsDown().SearchAsync(SampleRequest, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Error.Kind, new[] { ErrorKind.ProviderUnavailable, ErrorKind.Timeout });
    }

    [Fact]
    public async Task No_match_is_an_empty_success_not_a_failure()
    {
        var result = await CreateSutWithNoMatch().SearchAsync(SampleRequest, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
