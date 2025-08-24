using System.Net.Http;

namespace Artifacto.Client;

/// <summary>
/// The main client for interacting with the Artifacto Web API.
/// Provides access to projects and artifacts operations through specialized client interfaces.
/// </summary>
public class ArtifactoClient
{
    readonly ProjectsClient _projectsClient;
    
    /// <summary>
    /// Gets the client for project-related operations.
    /// </summary>
    public IProjectsClient Projects => _projectsClient;

    readonly ArtifactsClient _artifactsClient;
    
    /// <summary>
    /// Gets the client for artifact-related operations.
    /// </summary>
    public IArtifactsClient Artifacts => _artifactsClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactoClient"/> class.
    /// </summary>
    /// <param name="baseUrl">The base URL of the Artifacto Web API.</param>
    /// <param name="httpClient">The HTTP client to use for requests. If null, a new instance will be created.</param>
    public ArtifactoClient(string baseUrl, HttpClient? httpClient = null)
    {
        httpClient ??= new HttpClient();

        _projectsClient = new ProjectsClient(baseUrl, httpClient);
        _artifactsClient = new ArtifactsClient(baseUrl, httpClient);
    }
}
