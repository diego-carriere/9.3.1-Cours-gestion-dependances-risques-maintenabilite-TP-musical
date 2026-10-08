using ReveilMusical.Infrastructure.Users;

namespace ReveilMusical.Infrastructure.Tests.Users;

public sealed class UserDirectoryOptionsValidatorTests
{
    [Fact]
    public void A_valid_directory_passes()
    {
        Assert.True(Validate(InMemoryUserProfileProviderTests.Alice()).Succeeded);
    }

    public static TheoryData<string, Action<UserRecord>> Defects => new()
    {
        { "id vide", r => r.Id = " " },
        { "canal préféré invalide", r => r.PreferredChannel = "push notif" },
        { "canal de contact invalide", r => r.Contacts["sms!"] = "+33612345678" },
        { "contact vide", r => r.Contacts["sms"] = "" },
        { "météo inconnue", r => r.KeywordsByWeather["BROUILLARD"] = ["x"] },
        { "jour inconnu", r => r.KeywordsByDayAndWeather["MONDAY+PLUIE"] = ["x"] },
        { "clé jour+météo sans séparateur", r => r.KeywordsByDayAndWeather["LUNDI"] = ["x"] },
        { "mots-clés vides", r => r.KeywordsByWeather["SOLEIL"] = [] },
        { "secours vide", r => r.FallbackKeywords = [] },
    };

    [Theory]
    [MemberData(nameof(Defects))]
    public void A_malformed_record_prevents_startup(string defect, Action<UserRecord> corrupt)
    {
        var record = InMemoryUserProfileProviderTests.Alice();
        corrupt(record);

        var result = Validate(record);

        Assert.True(result.Failed, defect);
        Assert.Contains("UserService:Users:0", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_records_with_the_same_id_prevent_startup()
    {
        Assert.True(Validate(InMemoryUserProfileProviderTests.Alice(), InMemoryUserProfileProviderTests.Alice()).Failed);
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(params UserRecord[] users) =>
        new UserDirectoryOptionsValidator().Validate(null, new UserDirectoryOptions { Users = [.. users] });
}
