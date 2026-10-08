using System.Reflection;
using ReveilMusical.Domain.Model;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Domain.Tests;

/// <summary>
/// Rend exécutable la règle de Support J1 « chaque couche dépend de celle du dessous, jamais de
/// celle du dessus » : ces tests échouent dès qu'un import distrait fait fuiter une dépendance
/// technique, un fournisseur ou un SDK dans une couche interne.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(UserProfile).Assembly;
    private static readonly Assembly ApplicationAssembly = Assembly.Load("ReveilMusical.Application");

    [Fact]
    public void Domain_references_nothing_but_the_BCL()
    {
        var offending = DomainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(name => name is not null
                           && name != "netstandard"
                           && name != "System"
                           && !name.StartsWith("System.", StringComparison.Ordinal))
            .ToList();

        Assert.True(offending.Count == 0, $"ReveilMusical.Domain référence : {string.Join(", ", offending)}");
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Polly")]
    [InlineData("Microsoft.Extensions.Http")]
    [InlineData("Microsoft.Extensions.Caching")]
    [InlineData("ReveilMusical.Infrastructure")]
    [InlineData("ReveilMusical.FakeVendors")]
    public void Application_does_not_know_technical_details(string forbiddenPrefix)
    {
        var offending = ApplicationAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(name => name is not null && name.StartsWith(forbiddenPrefix, StringComparison.Ordinal))
            .ToList();

        Assert.True(offending.Count == 0, $"ReveilMusical.Application référence : {string.Join(", ", offending)}");
    }

    /// <summary>
    /// Aucun vocabulaire de fournisseur (DTO, iTunes, MusicBrainz, SMTP...) dans le métier : le
    /// brief cite trackViewUrl comme le champ qui ne doit pas fuiter.
    /// </summary>
    [Theory]
    [InlineData("Dto")]
    [InlineData("ITunes")]
    [InlineData("MusicBrainz")]
    [InlineData("Smtp")]
    [InlineData("TrackViewUrl")]
    public void Domain_and_Application_carry_no_provider_vocabulary(string word)
    {
        var offending = DomainAssembly.GetTypes().Concat(ApplicationAssembly.GetTypes())
            .SelectMany(t => new[] { t.Name }.Concat(t.GetMembers().Select(m => $"{t.Name}.{m.Name}")))
            .Where(name => name.Contains(word, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(offending.Count == 0, $"Vocabulaire fournisseur hors Infrastructure : {string.Join(", ", offending)}");
    }

    [Fact]
    public void Domain_and_Application_hold_no_mutable_static_state()
    {
        var offending = StaticStateInspector.FindMutableStaticFields(
            DomainAssembly.GetTypes().Concat(ApplicationAssembly.GetTypes()));

        Assert.True(offending.Count == 0, $"État statique mutable : {string.Join(", ", offending)}");
    }
}
