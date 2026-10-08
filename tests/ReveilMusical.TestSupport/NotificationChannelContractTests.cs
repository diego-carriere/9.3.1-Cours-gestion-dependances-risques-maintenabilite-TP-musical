using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using Xunit;

namespace ReveilMusical.TestSupport;

/// <summary>
/// Contrat commun à tout <see cref="INotificationChannel"/> : chaque adaptateur ramène son SDK
/// (exception, code de statut, callback...) à ces trois comportements, sans jamais lever.
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
}
