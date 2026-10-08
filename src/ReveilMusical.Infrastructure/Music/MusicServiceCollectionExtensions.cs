using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ReveilMusical.Domain.Abstractions;

namespace ReveilMusical.Infrastructure.Music;

internal static class MusicServiceCollectionExtensions
{
    public static IServiceCollection AddMusic(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MusicProvidersOptions>()
            .Bind(configuration.GetSection(MusicProvidersOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MusicProvidersOptions>, MusicProvidersOptionsValidator>());

        services.AddOptions<ITunesOptions>()
            .Bind(configuration.GetSection(ITunesOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<MusicBrainzOptions>()
            .Bind(configuration.GetSection(MusicBrainzOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart(); // L'application refuse de démarrer sans User-Agent MusicBrainz identifiable.
        services.AddOptions<MusicCacheOptions>()
            .Bind(configuration.GetSection(MusicCacheOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<MusicResilienceOptions>()
            .Bind(configuration.GetSection(MusicResilienceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Singleton : un cache par requête ne cache rien.
        services.AddMemoryCache();

        // Clients typés (transients par conception d'IHttpClientFactory, handlers mutualisés), chacun
        // avec son pipeline et son limiteur, créés une fois par pipeline : le quota est partagé.
        services.AddHttpClient<ITunesCatalog>(static (provider, client) =>
                client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<ITunesOptions>>().Value.BaseUrl))
            .AddResilienceHandler(ProviderKeys.ITunes, static (builder, context) =>
            {
                var limiter = HttpResiliencePipelines.ITunesLimiter(
                    context.ServiceProvider.GetRequiredService<IOptions<ITunesOptions>>().Value.RequestsPerMinute);
                context.OnPipelineDisposed(limiter.Dispose);
                HttpResiliencePipelines.Configure(
                    builder, context.ServiceProvider.GetRequiredService<IOptions<MusicResilienceOptions>>().Value.ITunes, limiter);
            });

        services.AddHttpClient<MusicBrainzCatalog>(static (provider, client) =>
                client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<MusicBrainzOptions>>().Value.BaseUrl))
            .AddResilienceHandler(ProviderKeys.MusicBrainz, static (builder, context) =>
            {
                var limiter = HttpResiliencePipelines.MusicBrainzLimiter(
                    context.ServiceProvider.GetRequiredService<IOptions<MusicBrainzOptions>>().Value.RequestsPerSecond);
                context.OnPipelineDisposed(limiter.Dispose);
                HttpResiliencePipelines.Configure(
                    builder, context.ServiceProvider.GetRequiredService<IOptions<MusicResilienceOptions>>().Value.MusicBrainz, limiter);
            });

        // Chaque fournisseur est exposé sous sa clé, déjà enveloppé par son cache (Decorator).
        services.AddKeyedTransient<IMusicCatalog>(ProviderKeys.ITunes, static (provider, _) =>
            ActivatorUtilities.CreateInstance<CachedMusicCatalog>(provider, provider.GetRequiredService<ITunesCatalog>(), ProviderKeys.ITunes));
        services.AddKeyedTransient<IMusicCatalog>(ProviderKeys.MusicBrainz, static (provider, _) =>
            ActivatorUtilities.CreateInstance<CachedMusicCatalog>(provider, provider.GetRequiredService<MusicBrainzCatalog>(), ProviderKeys.MusicBrainz));

        // Le port lui-même : le failover (Composite), seul IMusicCatalog non keyed.
        services.TryAddTransient<IMusicCatalog, FailoverMusicCatalog>();
        services.TryAddSingleton<IFallbackPlaylist, BuiltInFallbackPlaylist>();

        return services;
    }
}
