namespace Artifacto.WebApi;

/// <summary>
/// Provides factory methods for generating standardized error responses for common project and artifact errors.
/// </summary>
public static class ErrorResponses
{
    /// <summary>
    /// Creates an error response indicating the specified project ID is invalid.
    /// </summary>
    /// <param name="projectId">The project ID that is invalid.</param>
    /// <returns>An <see cref="ErrorResponse"/> describing the invalid project ID.</returns>
    public static ErrorResponse InvalidProjectId(string? projectId) => new() { Message = $"Project ID '{projectId}' is invalid. Project ID's must non-empty and consist of only lowercase letters, numbers, and dashes." };

    /// <summary>
    /// Creates an error response indicating the specified artifact version is invalid.
    /// </summary>
    /// <param name="version">The artifact version that is invalid.</param>
    /// <returns>An <see cref="ErrorResponse"/> describing the invalid artifact version.</returns>
    public static ErrorResponse InvalidArtifactVersion(string? version) => new() { Message = $"Artifact version '{version}' is invalid. Artifact versions must be non-empty and consist of only lowercase letters, numbers, periods, and dashes." };

    /// <summary>
    /// Creates an error response indicating the specified project already exists.
    /// </summary>
    /// <param name="projectId">The project ID that already exists.</param>
    /// <returns>An <see cref="ErrorResponse"/> describing the project already exists error.</returns>
    public static ErrorResponse ProjectAlreadyExists(string? projectId) => new() { Message = $"Project with ID '{projectId}' already exists." };

    /// <summary>
    /// Creates an error response indicating the specified project was not found.
    /// </summary>
    /// <param name="projectId">The project ID that was not found.</param>
    /// <returns>An <see cref="ErrorResponse"/> describing the project not found error.</returns>
    public static ErrorResponse ProjectNotFound(string? projectId) => new() { Message = $"Project with ID '{projectId}' not found." };

    /// <summary>
    /// Creates an error response indicating the specified artifact was not found in the given project.
    /// </summary>
    /// <param name="projectId">The project ID in which the artifact was not found.</param>
    /// <param name="version">The version of the artifact that was not found.</param>
    /// <returns>An <see cref="ErrorResponse"/> describing the artifact not found error.</returns>
    public static ErrorResponse ArtifactNotFound(string? projectId, string? version) => new() { Message = $"Artifact with version '{version}' not found in project with ID '{projectId}'." };
}
