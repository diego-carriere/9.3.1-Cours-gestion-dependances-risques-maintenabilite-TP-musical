using System.Reflection;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Infrastructure.Tests;

/// <summary>
/// Les objets propres à chaque fournisseur ou SDK (DTO, trackViewUrl, types des SDK simulés) ne
/// sortent jamais de leur adaptateur, et l'hôte ne peut nommer aucun adaptateur : il ne peut donc
/// pas en instancier un avec <c>new</c>.
/// </summary>
public sealed class AdapterIsolationTests
{
    private static readonly Assembly InfrastructureAssembly = typeof(InfrastructureServiceCollectionExtensions).Assembly;

    /// <summary>Le seul vocabulaire que l'Infrastructure expose : la composition et ses options.</summary>
    private static readonly HashSet<string> AllowedPublicTypeNames = new(StringComparer.Ordinal)
    {
        "InfrastructureServiceCollectionExtensions",
        "ChannelResilienceOptions",
        "MusicProvidersOptions",
        "ITunesOptions",
        "MusicBrainzOptions",
        "MusicCacheOptions",
        "MusicResilienceOptions",
        "ProviderResilienceOptions",
        "UserDirectoryOptions",
        "UserRecord",
    };

    [Fact]
    public void Every_provider_DTO_is_internal_and_sealed()
    {
        var offending = InfrastructureAssembly.GetTypes()
            .Where(t => t.Name.EndsWith("Dto", StringComparison.Ordinal))
            .Where(t => t.IsVisible || !t.IsSealed)
            .Select(t => t.FullName)
            .ToList();

        Assert.True(offending.Count == 0, $"DTO non internal/sealed : {string.Join(", ", offending)}");
    }

    [Fact]
    public void The_public_surface_is_only_composition_and_options()
    {
        var offending = InfrastructureAssembly.GetExportedTypes()
            .Where(t => !AllowedPublicTypeNames.Contains(t.Name))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(offending.Count == 0, $"Type(s) publics en trop (un adaptateur fuit) : {string.Join(", ", offending)}");
    }

    [Fact]
    public void No_port_implementation_exposes_a_DTO_or_a_vendor_type()
    {
        var ports = new[] { typeof(IMusicCatalog), typeof(INotificationChannel), typeof(IUserProfileProvider), typeof(IFallbackPlaylist) };
        var vendorAssembly = typeof(FakeVendors.Mail.SmtpMailClient).Assembly;

        var offending =
            from type in InfrastructureAssembly.GetTypes()
            where ports.Any(port => port.IsAssignableFrom(type))
            from method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            from involved in method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType)
            where involved.Name.EndsWith("Dto", StringComparison.Ordinal) || involved.Assembly == vendorAssembly
            select $"{type.Name}.{method.Name} -> {involved.Name}";

        Assert.Empty(offending);
    }

    [Fact]
    public void Infrastructure_holds_no_mutable_static_state()
    {
        Assert.Empty(StaticStateInspector.FindMutableStaticFields(InfrastructureAssembly.GetTypes()));
    }
}
