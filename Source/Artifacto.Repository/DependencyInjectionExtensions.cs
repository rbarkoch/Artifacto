using Microsoft.Extensions.DependencyInjection;

namespace Artifacto.Repository;

/// <summary>
/// Provides extension methods for registering repository services with the dependency injection container.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// Adds the Artifacto repository services to the specified service collection.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddArtifactoRepositories(this IServiceCollection services)
    {
        services.AddScoped<ProjectsRepository>();
        services.AddScoped<ArtifactsRepository>();

        return services;
    }
}
