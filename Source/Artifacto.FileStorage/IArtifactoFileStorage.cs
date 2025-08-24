using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Artifacto.Models;

using OneOf;
using OneOf.Types;

namespace Artifacto.FileStorage;

/// <summary>
/// Represents a successful artifact save operation with hash information.
/// </summary>
/// <param name="Sha256Hash">The SHA256 hash of the saved artifact file.</param>
public record struct SaveArtifactSuccess(string Sha256Hash);

/// <summary>
/// Provides file storage operations for artifacts and projects in the Artifacto system.
/// This interface abstracts the file system operations for managing project directories and artifact files.
/// </summary>
public interface IArtifactoFileStorage
{
    /// <summary>
    /// Saves an artifact for the specified project and version.
    /// </summary>
    /// <param name="projectKey">The unique identifier (key) of the project.</param>
    /// <param name="artifactVersion">The version of the artifact to save.</param>
    /// <param name="artifactStream">The stream containing the artifact data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation, containing a <see cref="OneOf{SaveArtifactSuccess, BadRequestError}"/> 
    /// indicating either a successful save with hash information or a bad request error.
    /// </returns>
    Task<OneOf<SaveArtifactSuccess, BadRequestError>> SaveArtifactAsync(string projectKey, string artifactVersion, Stream artifactStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an artifact for the specified project and version.
    /// </summary>
    /// <param name="projectKey">The unique identifier (key) of the project.</param>
    /// <param name="artifactVersion">The version of the artifact to download.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation, containing a <see cref="OneOf{Stream, BadRequestError, NotFoundError}"/>
    /// indicating either the artifact stream, a bad request error, or a not found error.
    /// </returns>
    Task<OneOf<Stream, BadRequestError, NotFoundError>> DownloadArtifactAsync(string projectKey, string artifactVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an artifact for the specified project and version.
    /// </summary>
    /// <param name="projectKey">The unique identifier (key) of the project.</param>
    /// <param name="artifactVersion">The version of the artifact to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation, containing a <see cref="OneOf{Success, BadRequestError, NotFoundError}"/>
    /// indicating either a successful delete, a bad request error, or a not found error.
    /// </returns>
    Task<OneOf<Success, BadRequestError, NotFoundError>> DeleteArtifactAsync(string projectKey, string artifactVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new version of an artifact by copying an existing artifact.
    /// </summary>
    /// <param name="projectKey">The unique identifier (key) of the project.</param>
    /// <param name="sourceArtifactVersion">The version of the existing artifact to copy from.</param>
    /// <param name="targetArtifactVersion">The version of the new artifact to create.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation, containing a <see cref="OneOf{Success, BadRequestError, NotFoundError, ConflictError}"/>
    /// indicating either a successful reversion, a bad request error, a not found error, or a conflict error if the target version already exists.
    /// </returns>
    Task<OneOf<Success, BadRequestError, NotFoundError, ConflictError>> ReversionArtifactAsync(string projectKey, string sourceArtifactVersion, string targetArtifactVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new project directory in the file storage.
    /// </summary>
    /// <param name="projectKey">The unique identifier (key) of the project.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation, containing a <see cref="OneOf{Success, BadRequestError, ConflictError}"/>
    /// indicating either a successful creation, a bad request error, or a conflict error if the project already exists.
    /// </returns>
    Task<OneOf<Success, BadRequestError, ConflictError>> NewProject(string projectKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a project and all its artifacts from the file storage.
    /// </summary>
    /// <param name="projectKey">The unique identifier (key) of the project.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation, containing a <see cref="OneOf{Success, BadRequestError, NotFoundError}"/>
    /// indicating either a successful deletion, a bad request error, or a not found error.
    /// </returns>
    Task<OneOf<Success, BadRequestError, NotFoundError>> DeleteProjectAsync(string projectKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a project directory in the file storage.
    /// </summary>
    /// <param name="oldProjectKey">The current unique identifier (key) of the project.</param>
    /// <param name="newProjectKey">The new unique identifier (key) of the project.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation, containing a <see cref="OneOf{Success, BadRequestError, NotFoundError}"/>
    /// indicating either a successful rename, a bad request error, or a not found error.
    /// </returns>
    Task<OneOf<Success, BadRequestError, NotFoundError>> RenameProject(string oldProjectKey, string newProjectKey, CancellationToken cancellationToken = default);
}
