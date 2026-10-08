using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.FakeVendors.Mail;
using ReveilMusical.Infrastructure.Notifications;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Infrastructure.Tests.Notifications;

public sealed class EmailChannelAdapterTests : NotificationChannelContractTests, IDisposable
{
    private readonly TempDirectory _outbox = new();

    protected override ContactAddress ValidContact => ContactAddress.Create("alice@example.com").Value;

    protected override ContactAddress InvalidContact => ContactAddress.Create("alice").Value;

    [Fact]
    public async Task The_mail_carries_the_track_in_its_subject_and_an_html_encoded_body()
    {
        var message = WakeUpMessage.Compose("Alice", new Track("Rock & Roll", "Led Zeppelin"), DayOfWeek.Friday, WeatherCondition.Cloudy);

        var receipt = await CreateWorkingSut().SendAsync(ValidContact, message, TestContext.Current.CancellationToken);

        var log = _outbox.Read("mail.log");
        Assert.Contains(receipt.Value.Reference, log, StringComparison.Ordinal);
        Assert.Contains("subject=\"Réveil musical : Rock & Roll\"", log, StringComparison.Ordinal);
        Assert.Contains("<p>Bonjour Alice ! Ce vendredi s&#39;annonce nuageux", log, StringComparison.Ordinal);
        Assert.Contains("Rock &amp; Roll", log, StringComparison.Ordinal);
        Assert.Contains("from=reveil@test.example", log, StringComparison.Ordinal);
    }

    public void Dispose() => _outbox.Dispose();

    protected override INotificationChannel CreateWorkingSut() => Create(simulateOutage: false);

    protected override INotificationChannel CreateSutWhoseServiceIsDown() => Create(simulateOutage: true);

    private EmailChannelAdapter Create(bool simulateOutage) => new(new SmtpMailClient(new SmtpMailSettings
    {
        OutboxDirectory = _outbox.Path,
        WriteToConsole = false,
        SimulateOutage = simulateOutage,
        FromAddress = "reveil@test.example",
    }));
}
