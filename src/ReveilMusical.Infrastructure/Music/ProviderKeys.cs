using System.Collections.Frozen;

namespace ReveilMusical.Infrastructure.Music;

/// <summary>Les clés des fournisseurs musicaux, telles qu'écrites dans <c>Music:Providers</c>.</summary>
internal static class ProviderKeys
{
    public const string ITunes = "itunes";
    public const string MusicBrainz = "musicbrainz";

    public static readonly FrozenSet<string> All = FrozenSet.Create(StringComparer.Ordinal, ITunes, MusicBrainz);

    public static string Normalize(string? raw) => (raw ?? string.Empty).Trim().ToLowerInvariant();
}
