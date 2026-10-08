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

    /// <summary>Code météo (« SOLEIL ») → morceau choisi par l'utilisateur.</summary>
    public Dictionary<string, TrackRecord> TracksByWeather { get; set; } = [];

    /// <summary>
    /// « LUNDI+PLUIE » → morceau. Le « + » plutôt que « : », séparateur de sections de la
    /// configuration .NET, qui casserait la clé en deux.
    /// </summary>
    public Dictionary<string, TrackRecord> TracksByDayAndWeather { get; set; } = [];

    /// <summary>Le morceau de secours, pour les cas que l'utilisateur n'a pas couverts.</summary>
    public TrackRecord? FallbackTrack { get; set; }
}

public sealed class TrackRecord
{
    public string Title { get; set; } = string.Empty;

    public string? Artist { get; set; }
}
