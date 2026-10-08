namespace ReveilMusical.Infrastructure.Users;

/// <summary>
/// Les données du service utilisateur simulé (section <see cref="SectionName"/>). Forme propre à ce
/// fournisseur : codes météo et jours en français, clé « JOUR+METEO » pour les surcharges.
/// </summary>
public sealed class UserDirectoryOptions
{
    public const string SectionName = "UserService";

    /// <summary>Simule une panne du service : toute lecture de profil échoue.</summary>
    public bool SimulateOutage { get; set; }

    public List<UserRecord> Users { get; set; } = [];
}

public sealed class UserRecord
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PreferredChannel { get; set; } = string.Empty;

    /// <summary>Canal → coordonnée (« sms » → « +33612345678 »).</summary>
    public Dictionary<string, string> Contacts { get; set; } = [];

    /// <summary>Code météo (« SOLEIL ») → mots-clés.</summary>
    public Dictionary<string, string[]> KeywordsByWeather { get; set; } = [];

    /// <summary>
    /// « LUNDI+PLUIE » → mots-clés. Le « + » plutôt que « : », séparateur de sections de la
    /// configuration .NET, qui casserait la clé en deux.
    /// </summary>
    public Dictionary<string, string[]> KeywordsByDayAndWeather { get; set; } = [];

    public string[] FallbackKeywords { get; set; } = [];
}
