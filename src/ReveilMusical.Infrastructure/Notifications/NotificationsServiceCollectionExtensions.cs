using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using ReveilMusical.Domain.Abstractions;
using ReveilMusical.Domain.Model;
using ReveilMusical.Domain.Results;
using ReveilMusical.FakeVendors.Mail;
using ReveilMusical.FakeVendors.Push;
using ReveilMusical.FakeVendors.Sms;

namespace ReveilMusical.Infrastructure.Notifications;

internal static class NotificationsServiceCollectionExtensions
{
    public const string VendorsSection = "Vendors";

    public static IServiceCollection AddNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ChannelResilienceOptions>().Bind(configuration.GetSection(ChannelResilienceOptions.SectionName));

        // Les SDK sont des singletons : ils détiennent le verrou de leur fichier de sortie.
        services.AddVendorSettings<SmtpMailSettings>(configuration, "Mail");
        services.AddVendorSettings<SmsGatewaySettings>(configuration, "Sms");
        services.AddVendorSettings<PushServiceSettings>(configuration, "Push");
        services.TryAddSingleton<SmtpMailClient>();
        services.TryAddSingleton<ISmsGatewayClient, SmsGatewayClient>();
        services.TryAddSingleton<IPushService, PushService>();

        services.AddNotificationChannel<EmailChannelAdapter>(ChannelKeys.Email);
        services.AddNotificationChannel<SmsChannelAdapter>(ChannelKeys.Sms);
        services.AddNotificationChannel<PushChannelAdapter>(ChannelKeys.Push);

        services.TryAddTransient<INotificationChannelResolver, KeyedNotificationChannelResolver>();
        services.TryAddSingleton<IOperatorAlerter, LoggingOperatorAlerter>();

        return services;
    }

    /// <summary>
    /// Enregistre un canal : son adaptateur, enveloppé par <see cref="ResilientNotificationChannel"/>,
    /// sous la clé <paramref name="channelId"/>, avec son propre pipeline (donc son propre disjoncteur).
    /// </summary>
    public static IServiceCollection AddChannel<TAdapter>(this IServiceCollection services, string channelId)
        where TAdapter : class, INotificationChannel
    {
        var id = ChannelId.Create(channelId);
        if (id.IsFailure)
        {
            throw new ArgumentException(id.Error.Message, nameof(channelId));
        }

        var key = id.Value.Value;
        var pipelineKey = $"channel:{key}";

        services.TryAddSingleton<TAdapter>();
        services.AddResiliencePipeline<string, Result<DeliveryReceipt>>(pipelineKey, static (builder, context) =>
            ChannelResiliencePipelines.Configure(
                builder, context.ServiceProvider.GetRequiredService<IOptions<ChannelResilienceOptions>>().Value));

        // ActivatorUtilities plutôt que `new` : le conteneur construit le décorateur, seuls
        // l'adaptateur, le pipeline et la clé lui sont passés explicitement.
        services.AddKeyedSingleton<INotificationChannel>(key, (provider, _) =>
            ActivatorUtilities.CreateInstance<ResilientNotificationChannel>(
                provider,
                provider.GetRequiredService<TAdapter>(),
                provider.GetRequiredService<ResiliencePipelineProvider<string>>().GetPipeline<Result<DeliveryReceipt>>(pipelineKey),
                id.Value));

        return services;
    }

    private static void AddVendorSettings<TSettings>(this IServiceCollection services, IConfiguration configuration, string vendor)
        where TSettings : class
    {
        services.AddOptions<TSettings>().Bind(configuration.GetSection($"{VendorsSection}:{vendor}"));
        services.TryAddSingleton(static provider => provider.GetRequiredService<IOptions<TSettings>>().Value);
    }
}
