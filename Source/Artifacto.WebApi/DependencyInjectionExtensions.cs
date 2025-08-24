using Artifacto.WebApi.ControllerImplementations;

using Microsoft.Extensions.DependencyInjection;

namespace Artifacto.WebApi;

/// <summary>
/// Provides extension methods for registering Artifacto Web API controllers with the dependency injection container.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// Registers the Artifacto controller implementations for dependency injection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the controllers to.</param>
    /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddArtifactoControllers(this IServiceCollection services)
    {
        services.AddScoped<IProjectsController, ProjectsControllerImplementation>();
        services.AddScoped<IArtifactsController, ArtifactsControllerImplementation>();
        return services;
    }
}
