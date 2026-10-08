using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using Xunit;

namespace ReveilMusical.TestSupport;

/// <summary>
/// Contrat commun à tout <see cref="INotificationChannel"/> : chaque adaptateur ramène son SDK
/// (exception, code de statut, callback...) à ces comportements, sans jamais lever pour un échec
/// attendu. Seule l'annulation demandée par l'appelant se propage.
/// </summary>
public abstract class NotificationChannelContractTests
{
    protected static readonly WakeUpMessage SampleMessage =
        WakeUpMessage.Compose("Alice", new Track("Here Comes the Sun", "The Beatles"), DayOfWeek.Monday, WeatherCondition.Sunny);

    /// <summary>Une coordonnée que le canal accepte.</summary>
    protected abstract ContactAddress ValidContact { get; }

    /// <summary>Une coordonnée que le canal refuse (mauvais format).</summary>
    protected abstract ContactAddress InvalidContact { get; }

    protected abstract INotificationChannel CreateWorkingSut();

    protected abstract INotificationChannel CreateSutWhoseServiceIsDown();

    [Fact]
    public async Task A_working_channel_returns_a_receipt()
    {
        var result = await CreateWorkingSut().SendAsync(ValidContact, SampleMessage, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Reference));
    }

    [Fact]
    public async Task A_service_outage_is_an_expected_failure_never_an_exception()
    {
        var result = await CreateSutWhoseServiceIsDown().SendAsync(ValidContact, SampleMessage, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Error.Kind, new[] { ErrorKind.ChannelUnavailable, ErrorKind.Timeout });
    }

    [Fact]
    public async Task A_malformed_contact_is_rejected_as_invalid_never_an_exception()
    {
        var result = await CreateWorkingSut().SendAsync(InvalidContact, SampleMessage, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.InvalidContact, result.Error.Kind);
    }

    /// <summary>
    /// RGPD : le message d'erreur part dans les journaux, dans l'alerte opérateur et dans la réponse
    /// HTTP. Il ne répète donc pas la coordonnée (numéro, adresse, jeton).
    /// </summary>
    [Fact]
    public async Task A_rejected_contact_is_not_repeated_in_the_error_message()
    {
        var result = await CreateWorkingSut().SendAsync(InvalidContact, SampleMessage, CancellationToken.None);

        Assert.DoesNotContain(InvalidContact.Value, result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_cancellation_requested_by_the_caller_propagates_never_a_failure()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateWorkingSut().SendAsync(ValidContact, SampleMessage, cancellation.Token));
    }
}
