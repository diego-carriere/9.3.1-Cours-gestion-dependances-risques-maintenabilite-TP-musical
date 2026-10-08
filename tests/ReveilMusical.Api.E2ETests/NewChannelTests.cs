using Microsoft.Extensions.DependencyInjection;
using ReveilMusical.Infrastructure;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Api.E2ETests;

/// <summary>
/// Exigence « de nouveaux canaux (WhatsApp, appel vocal) » : un canal qui n'existe nulle part dans
/// le code de production est ajouté par ce seul test, avec un adaptateur et une ligne
/// d'enregistrement. Ni le Domaine, ni l'Application, ni l'hôte ne changent (OCP).
/// </summary>
public sealed class NewChannelTests
{
    [Fact]
    public async Task A_whatsapp_channel_is_one_adapter_and_one_registration()
    {
        await using var factory = new ReveilApiFactory(
            new Dictionary<string, string?>
            {
                ["UserService:Users:3:Id"] = "99",
                ["UserService:Users:3:DisplayName"] = "Dana",
                ["UserService:Users:3:PreferredChannel"] = "whatsapp",
                ["UserService:Users:3:Contacts:whatsapp"] = "+33700000000",
                ["UserService:Users:3:KeywordsByWeather:SOLEIL:0"] = "soleil",
                ["UserService:Users:3:FallbackKeywords:0"] = "wake up",
            },
            services => services.AddNotificationChannel<FakeNotificationChannel>("whatsapp"));
        factory.Upstream.FixtureFor(FakeUpstream.ITunesHost, "itunes-search-soleil.json");

        var json = await (await factory.CreateClient().WakeUpAsync("99", "JEUDI", "SOLEIL")).JsonAsync();

        Assert.Equal("whatsapp", json.Str("notification.deliveredOn"));
        Assert.Equal("false", json.Str("degraded"));
        var sent = Assert.Single(factory.Services.GetRequiredService<FakeNotificationChannel>().Sent);
        Assert.Equal("+33700000000", sent.Recipient.Value);
        Assert.StartsWith("Bonjour Dana ! Ce jeudi", sent.Message.Body, StringComparison.Ordinal);
    }
}
