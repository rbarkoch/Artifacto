using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Artifacto.FileStorage;

/// <summary>
/// Provides extension methods for registering file storage services with the dependency injection container.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// Adds the Artifacto file storage services to the specified service collection.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <param name="basePath">The base path where projects and artifacts will be stored.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddArtifactoFileStorage(this IServiceCollection services, string basePath)
    {
        services.AddSingleton<IArtifactoFileStorage>(serviceProvider =>
        {
            ILogger<ArtifactoFileStorage> logger = serviceProvider.GetRequiredService<ILogger<ArtifactoFileStorage>>();
            return new ArtifactoFileStorage(logger, basePath);
        });
        return services;
    }
}
