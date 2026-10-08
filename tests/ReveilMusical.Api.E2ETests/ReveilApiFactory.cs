using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.TestSupport;

namespace ReveilMusical.Api.E2ETests;

/// <summary>
/// Héberge l'application complète en mémoire, avec son vrai <c>appsettings.json</c> (utilisateurs
/// de démonstration compris). Ne sont remplacés que le transport HTTP sortant
/// (<see cref="FakeUpstream"/>), le hasard (scripté : toujours le premier élément) et le dossier de
/// sortie des SDK simulés (temporaire).
/// </summary>
public sealed class ReveilApiFactory : WebApplicationFactory<Program>
{
    private readonly IReadOnlyDictionary<string, string?> _overrides;
    private readonly Action<IServiceCollection>? _extraServices;

    public ReveilApiFactory(IReadOnlyDictionary<string, string?>? overrides = null, Action<IServiceCollection>? extraServices = null)
    {
        _overrides = overrides ?? new Dictionary<string, string?>();
        _extraServices = extraServices;
        Directory.CreateDirectory(OutboxDirectory);
    }

    public FakeUpstream Upstream { get; } = new();

    public CapturingLoggerProvider Logs { get; } = new();

    public string OutboxDirectory { get; } = Path.Combine(Path.GetTempPath(), "reveil-e2e-" + Guid.NewGuid().ToString("N"));

    public string Outbox(string file)
    {
        var path = Path.Combine(OutboxDirectory, file);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Music:ITunes:BaseUrl"] = $"https://{FakeUpstream.ITunesHost}/",
                ["Music:MusicBrainz:BaseUrl"] = $"https://{FakeUpstream.MusicBrainzHost}/",
                ["Music:MusicBrainz:UserAgent"] = "ReveilMusical-E2E/1.0 ( https://example.test/contact )",
                ["Resilience:Music:ITunes:RetryBaseDelay"] = "00:00:00",
                ["Resilience:Music:MusicBrainz:RetryBaseDelay"] = "00:00:00",
                ["Resilience:Channels:RetryDelay"] = "00:00:00",
                ["Vendors:Mail:OutboxDirectory"] = OutboxDirectory,
                ["Vendors:Mail:WriteToConsole"] = "false",
                ["Vendors:Sms:OutboxDirectory"] = OutboxDirectory,
                ["Vendors:Sms:WriteToConsole"] = "false",
                ["Vendors:Push:OutboxDirectory"] = OutboxDirectory,
                ["Vendors:Push:WriteToConsole"] = "false",
            });
            configuration.AddInMemoryCollection(_overrides);
        });

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));

        builder.ConfigureTestServices(services =>
        {
            services.ConfigureHttpClientDefaults(http => http.ConfigurePrimaryHttpMessageHandler(Upstream.CreateHandler));
            services.Replace(ServiceDescriptor.Singleton<IRandom>(new FakeRandom()));
            _extraServices?.Invoke(services);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(OutboxDirectory))
        {
            Directory.Delete(OutboxDirectory, recursive: true);
        }
    }
}
