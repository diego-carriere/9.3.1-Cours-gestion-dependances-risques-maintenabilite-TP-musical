using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ReveilMusical.Domain.Abstractions;

namespace ReveilMusical.Infrastructure.Users;

internal static class UsersServiceCollectionExtensions
{
    public static IServiceCollection AddUserDirectory(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<UserDirectoryOptions>()
            .Bind(configuration.GetSection(UserDirectoryOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<UserDirectoryOptions>, UserDirectoryOptionsValidator>());

        // Singleton : sans état mutable, ses profils sont construits une fois.
        services.TryAddSingleton<IUserProfileProvider, InMemoryUserProfileProvider>();

        return services;
    }
}
