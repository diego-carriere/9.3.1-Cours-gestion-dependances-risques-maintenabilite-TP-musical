using System.ComponentModel.DataAnnotations;

namespace ReveilMusical.Infrastructure.Music;

/// <summary>Ordre de repli des fournisseurs (section <c>Music</c>, clé <c>Providers</c>).</summary>
public sealed class MusicProvidersOptions
{
    public const string SectionName = "Music";

    /// <summary>
    /// Relu à chaque recherche par <see cref="FailoverMusicCatalog"/> (bascule sans redémarrage) ;
    /// ce type sert à la validation au démarrage.
    /// </summary>
    public string[] Providers { get; set; } = [];
}

public sealed class ITunesOptions
{
    public const string SectionName = "Music:ITunes";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://itunes.apple.com/";

    /// <summary>Catalogue national interrogé (code ISO 3166-1 alpha-2).</summary>
    [Required]
    [RegularExpression("^[A-Z]{2}$")]
    public string Country { get; set; } = "FR";

    [Range(1, 200)]
    public int Limit { get; set; } = 5;

    /// <summary>Quota documenté d'iTunes Search : environ 20 requêtes par minute.</summary>
    [Range(1, 1000)]
    public int RequestsPerMinute { get; set; } = 20;
}

public sealed class MusicBrainzOptions
{
    public const string SectionName = "Music:MusicBrainz";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://musicbrainz.org/";

    /// <summary>
    /// Exigé par MusicBrainz : « Application/Version ( contact ) », sans quoi la requête est rejetée.
    /// Lu en configuration, jamais écrit en dur ; l'application refuse de démarrer sans lui.
    /// </summary>
    [Required]
    [RegularExpression(@"^\S+/\S+ \(.+\)$", ErrorMessage = "Music:MusicBrainz:UserAgent doit avoir la forme 'Application/Version ( contact )'.")]
    public string UserAgent { get; set; } = string.Empty;

    [Range(1, 100)]
    public int Limit { get; set; } = 5;

    /// <summary>Politique d'usage de MusicBrainz : une requête par seconde au plus.</summary>
    [Range(1, 10)]
    public int RequestsPerSecond { get; set; } = 1;
}

public sealed class MusicCacheOptions
{
    public const string SectionName = "Music:Cache";

    /// <summary>Durée pendant laquelle une recherche est resservie sans rappeler le fournisseur.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "30.00:00:00", ParseLimitsInInvariantCulture = true)]
    public TimeSpan Freshness { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Durée pendant laquelle une recherche périmée reste utilisable si le fournisseur tombe.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "30.00:00:00", ParseLimitsInInvariantCulture = true)]
    public TimeSpan StaleRetention { get; set; } = TimeSpan.FromDays(7);
}
