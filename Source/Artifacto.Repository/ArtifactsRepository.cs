using Artifacto.Database;
using Artifacto.Database.Models;
using Artifacto.FileStorage;
using Artifacto.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using OneOf;
using OneOf.Types;

using Artifact = Artifacto.Models.Artifact;
using Version = Artifacto.Models.Version;

namespace Artifacto.Repository;

/// <summary>
/// Provides repository operations for managing artifacts in the Artifacto system.
/// This class handles CRUD operations for artifacts and coordinates between database and file storage.
/// </summary>
public class ArtifactsRepository
{
    private readonly ArtifactoDbContext _dbContext;
    private readonly IArtifactoFileStorage _fileStorage;
    private readonly ILogger<ArtifactsRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactsRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context for data operations.</param>
    /// <param name="fileStorage">The file storage service for managing artifact files.</param>
    /// <param name="logger">The logger for recording operations.</param>
    public ArtifactsRepository(ILogger<ArtifactsRepository> logger, ArtifactoDbContext dbContext, IArtifactoFileStorage fileStorage)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves an artifact by project key and version.
    /// </summary>
    /// <param name="projectKey">The unique key of the project containing the artifact.</param>
    /// <param name="version">The version of the artifact to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task"/> containing either the found artifact or a not found error.
    /// </returns>
    public async Task<OneOf<Artifact, NotFoundError>> GetArtifactAsync(string projectKey, Version version, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting artifact for project {ProjectKey} version {Version}", projectKey, version.ToString());
        
        Database.Models.Project? project = await _dbContext.Projects.FindByKeyAsync(projectKey, cancellationToken);
        if (project is null)
        {
            _logger.LogInformation("Project not found {ProjectKey}", projectKey);
            return new NotFoundError($"Project with key '{projectKey}' not found.");
        }

        Database.Models.Artifact? artifact = await _dbContext.Artifacts.FindByVersionAsync(project.ProjectId, version.ToString(), cancellationToken);
        if (artifact is null)
        {
            _logger.LogInformation("Artifact not found for project {ProjectKey} version {Version}", projectKey, version.ToString());
            return new NotFoundError($"Artifact with version '{version}' not found in project with key '{projectKey}'.");
        }

        _logger.LogDebug("Found artifact for project {ProjectKey} version {Version} filename {FileName}", projectKey, version.ToString(), artifact.FileName);
        return artifact.ToDomainModel();
    }

    /// <summary>
    /// Downloads the artifact file stream for the specified project key and version.
    /// This method first validates that the artifact exists in the database and then attempts to retrieve
    /// the file from the configured file storage provider.
    /// </summary>
    /// <param name="projectKey">The unique key of the project containing the artifact.</param>
    /// <param name="version">The version of the artifact to download.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task"/> containing a OneOf which will be the artifact <see cref="Stream"/> on success,
    /// or a <see cref="NotFoundError"/> if the artifact or file is not found.
    /// </returns>
    public async Task<OneOf<Stream, NotFoundError>> DownloadArtifactAsync(string projectKey, Version version, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Downloading artifact for project {ProjectKey} version {Version}", projectKey, version.ToString());
        
        // First check if artifact exists in database
        OneOf<Artifact, NotFoundError> artifactResponse = await GetArtifactAsync(projectKey, version, cancellationToken);
        if (!artifactResponse.TryPickT0(out Artifact _, out NotFoundError notFoundError))
        {
            return notFoundError;
        }

        // Download from file storage
        OneOf<Stream, BadRequestError, NotFoundError> fileResponse = await _fileStorage.DownloadArtifactAsync(projectKey, version.ToString(), cancellationToken);
        return fileResponse.Match<OneOf<Stream, NotFoundError>>(
            stream => 
            {
                _logger.LogDebug("Successfully downloaded artifact stream for project {ProjectKey} version {Version}", projectKey, version.ToString());
                return stream;
            },
            badRequestError => 
            {
                _logger.LogWarning("Bad request downloading artifact for project {ProjectKey} version {Version}: {Message}", projectKey, version.ToString(), badRequestError.Message);
                return new NotFoundError($"Artifact with version '{version}' not found in project with key '{projectKey}'.");
            },
            notFound => 
            {
                _logger.LogInformation("Artifact file not found for project {ProjectKey} version {Version}: {Message}", projectKey, version.ToString(), notFound.Message);
                return new NotFoundError($"Artifact with version '{version}' not found in project with key '{projectKey}'.");
            }
        );
    }

    public async Task<OneOf<List<Artifact>, NotFoundError>> GetArtifactsAsync(string projectKey, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting all artifacts for project {ProjectKey}", projectKey);
        
        Database.Models.Project? project = await _dbContext.Projects.FindByKeyAsync(projectKey, cancellationToken);
        if (project is null)
        {
            _logger.LogInformation("Project not found {ProjectKey}", projectKey);
            return new NotFoundError($"Project with key '{projectKey}' not found.");
        }

        List<Database.Models.Artifact> artifacts = await _dbContext.Artifacts
            .Where(a => a.ProjectId == project.ProjectId)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Found {Count} artifacts for project {ProjectKey}", artifacts.Count, projectKey);
        return artifacts.Select(a => a.ToDomainModel()).ToList();
    }

    /// <summary>
    /// Updates metadata for an existing artifact (version, retained, locked).
    /// Validates immutable fields (timestamp, file name, file size, hash) and ensures that when the version
    /// changes no conflicting artifact already exists. If the version changes, attempts to reversion the stored file.
    /// </summary>
    /// <param name="artifact">The domain model containing updated artifact metadata.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task"/> containing a OneOf with:
    /// - <see cref="Success"/> on successful update,
    /// - <see cref="BadRequestError"/> for invalid updates,
    /// - <see cref="NotFoundError"/> if the artifact does not exist,
    /// - <see cref="ConflictError"/> if a version conflict is detected.
    /// </returns>
    public async Task<OneOf<Success, BadRequestError, NotFoundError, ConflictError>> UpdateArtifactAsync(Artifact artifact, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating artifact metadata for artifact {ArtifactId} version {Version}", artifact.ArtifactId, artifact.Version.ToString());
        
        Database.Models.Artifact? existingArtifact = await _dbContext.Artifacts.FindAsync(artifact.ArtifactId, cancellationToken);

        // ----- Validation ---- //
        if (existingArtifact is null)
        {
            _logger.LogInformation("Artifact not found for update {ArtifactId}", artifact.ArtifactId);
            return new NotFoundError($"Artifact with ID '{artifact.ArtifactId}' not found.");
        }

        if (artifact.Timestamp != existingArtifact.Timestamp)
        {
            _logger.LogWarning("Cannot modify timestamp for artifact {ArtifactId}", artifact.ArtifactId);
            return new BadRequestError($"Cannot modify the timestamp of an artifact without uploading new data.");
        }

        if (artifact.FileName != existingArtifact.FileName)
        {
            _logger.LogWarning("Cannot modify filename for artifact {ArtifactId}", artifact.ArtifactId);
            return new BadRequestError($"Cannot modify the file name of an artifact without uploading new data.");
        }

        if (artifact.FileSizeBytes != existingArtifact.FileSizeBytes)
        {
            _logger.LogWarning("Cannot modify file size for artifact {ArtifactId}", artifact.ArtifactId);
            return new BadRequestError($"Cannot modify the file size of an artifact without uploading new data.");
        }

        if (artifact.Sha256Hash != existingArtifact.Sha256Hash)
        {
            _logger.LogWarning("Cannot modify hash for artifact {ArtifactId}", artifact.ArtifactId);
            return new BadRequestError($"Cannot modify the hash of an artifact without uploading new data.");
        }

        if (existingArtifact.Version != artifact.Version.ToString())
        {
            // Version is changing, we need to ensure the new version doesn't already exists.
            bool newVersionAlreadyExists = await _dbContext.Artifacts.AnyAsync(a => a.ProjectId == artifact.ProjectId && a.Version == artifact.Version.ToString(), cancellationToken);
            if (newVersionAlreadyExists)
            {
                _logger.LogWarning("Version conflict updating artifact {ArtifactId} to version {Version}", artifact.ArtifactId, artifact.Version.ToString());
                return new ConflictError($"Artifact with version '{artifact.Version}' already exists.");
            }
        }
        // --------------------- //

        // Update file store.
        if (existingArtifact.Version != artifact.Version.ToString())
        {
            _logger.LogDebug("Reversioning artifact in file storage from {OldVersion} to {NewVersion}", existingArtifact.Version, artifact.Version.ToString());
            OneOf<Success, BadRequestError, NotFoundError, ConflictError> reversionResult = await _fileStorage.ReversionArtifactAsync(existingArtifact.Project.Key, existingArtifact.Version, artifact.Version.ToString(), cancellationToken);
            if (!reversionResult.TryPickT0(out Success _, out OneOf<BadRequestError, NotFoundError, ConflictError> reversionError))
            {
                _logger.LogWarning("File storage reversion failed for artifact {ArtifactId}", artifact.ArtifactId);
                return reversionError.Match<OneOf<Success, BadRequestError, NotFoundError, ConflictError>>(
                    badRequest => badRequest,
                    notFound => notFound,
                    conflict => conflict
                );
            }
        }

        // Update the artifact metadata
        existingArtifact.Version = artifact.Version.ToString();
        existingArtifact.Retained = artifact.Retained;
        existingArtifact.Locked = artifact.Locked;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Updated artifact metadata for artifact {ArtifactId} version {Version}", artifact.ArtifactId, artifact.Version.ToString());
        return new Success();
    }

    /// <summary>
    /// Updates an existing artifact by uploading new file data and updating metadata.
    /// Validates immutable fields, saves the new file version to storage, updates the artifact's hash,
    /// and persists changes to the database. If database persistence fails, the method attempts to remove
    /// the newly saved file to avoid orphaned storage.
    /// </summary>
    /// <param name="artifact">The domain model describing the artifact to update.</param>
    /// <param name="stream">A <see cref="Stream"/> containing the new artifact data to upload.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task"/> containing a OneOf with:
    /// - <see cref="Success"/> on successful update,
    /// - <see cref="BadRequestError"/> for invalid input or storage failures,
    /// - <see cref="NotFoundError"/> if the artifact or project cannot be found,
    /// - <see cref="ConflictError"/> if a version conflict is detected.
    /// </returns>
    /// <exception cref="Exception">
    /// Rethrows exceptions from the database save operation after attempting to clean up the stored file.
    /// </exception>
    public async Task<OneOf<Success, BadRequestError, NotFoundError, ConflictError>> UpdateArtifactAsync(Artifact artifact, Stream stream, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating artifact with new file for artifact {ArtifactId} version {Version}", artifact.ArtifactId, artifact.Version.ToString());
        
        Database.Models.Artifact? existingArtifact = await _dbContext.Artifacts.Include(a => a.Project).FirstOrDefaultAsync(a => a.ArtifactId == artifact.ArtifactId, cancellationToken);

        // ----- Validation ---- //
        if (existingArtifact is null)
        {
            _logger.LogInformation("Artifact not found for update {ArtifactId}", artifact.ArtifactId);
            return new NotFoundError($"Artifact with ID '{artifact.ArtifactId}' not found.");
        }

        if (artifact.Timestamp != existingArtifact.Timestamp)
        {
            _logger.LogWarning("Cannot modify timestamp for artifact {ArtifactId}", artifact.ArtifactId);
            return new BadRequestError($"Cannot modify the timestamp of an artifact without uploading new data.");
        }

        if (artifact.FileName != existingArtifact.FileName)
        {
            _logger.LogWarning("Cannot modify filename for artifact {ArtifactId}", artifact.ArtifactId);
            return new BadRequestError($"Cannot modify the file name of an artifact without uploading new data.");
        }

        if (artifact.FileSizeBytes != existingArtifact.FileSizeBytes)
        {
            _logger.LogWarning("Cannot modify file size for artifact {ArtifactId}", artifact.ArtifactId);
            return new BadRequestError($"Cannot modify the file size of an artifact without uploading new data.");
        }

        if (existingArtifact.Version != artifact.Version.ToString())
        {
            // Version is changing, we need to ensure the new version doesn't already exists.
            bool newVersionAlreadyExists = await _dbContext.Artifacts.AnyAsync(a => a.ProjectId == artifact.ProjectId && a.Version == artifact.Version.ToString(), cancellationToken);
            if (newVersionAlreadyExists)
            {
                _logger.LogWarning("Version conflict updating artifact {ArtifactId} to version {Version}", artifact.ArtifactId, artifact.Version.ToString());
                return new ConflictError($"Artifact with version '{artifact.Version}' already exists.");
            }
        }
        // --------------------- //

        // Update the artifact metadata
        existingArtifact.Version = artifact.Version.ToString();
        existingArtifact.Retained = artifact.Retained;
        existingArtifact.Locked = artifact.Locked;

        // Store the original version in case we need to rollback
        string originalVersion = existingArtifact.Project.Key;
        
        _logger.LogDebug("Saving artifact file to storage for project {ProjectKey} version {Version}", existingArtifact.Project.Key, existingArtifact.Version);
        OneOf<SaveArtifactSuccess, BadRequestError> saveResult = await _fileStorage.SaveArtifactAsync(existingArtifact.Project.Key, existingArtifact.Version, stream, cancellationToken);
        if (!saveResult.TryPickT0(out SaveArtifactSuccess saveSuccess, out BadRequestError saveError))
        {
            _logger.LogWarning("Failed to save artifact file for artifact {ArtifactId}: {Message}", artifact.ArtifactId, saveError.Message);
            return saveError;
        }

        // Update the hash with the new file
        existingArtifact.Sha256Hash = saveSuccess.Sha256Hash;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Updated artifact with new file for artifact {ArtifactId} version {Version}", artifact.ArtifactId, artifact.Version.ToString());
        }
        catch (Exception ex)
        {
            // Compensation: If database save fails, delete the new file version
            _logger.LogError(ex, "Database save failed for artifact {ArtifactId}, attempting file cleanup", artifact.ArtifactId);
            _ = await _fileStorage.DeleteArtifactAsync(existingArtifact.Project.Key, existingArtifact.Version, cancellationToken);
            throw; // Re-throw the original exception
        }

        return new Success();
    }

    public async Task<OneOf<Success, NotFoundError>> DeleteArtifactAsync(string projectKey, Version version, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting artifact for project {ProjectKey} version {Version}", projectKey, version.ToString());
        
        Database.Models.Project? project = await _dbContext.Projects.FindByKeyAsync(projectKey, cancellationToken);
        if (project is null)
        {
            _logger.LogInformation("Project not found {ProjectKey}", projectKey);
            return new NotFoundError($"Project with key '{projectKey}' not found.");
        }

        Database.Models.Artifact? existingArtifact = await _dbContext.Artifacts.FindByVersionAsync(project.ProjectId, version.ToString(), cancellationToken);
        if (existingArtifact is null)
        {
            _logger.LogInformation("Artifact not found for deletion project {ProjectKey} version {Version}", projectKey, version.ToString());
            return new NotFoundError($"Artifact with version '{version}' not found in project with key '{projectKey}'.");
        }

        // Delete the artifact from the database first
        _dbContext.Artifacts.Remove(existingArtifact);

        // Then try to delete from file storage
        // Note: We continue even if file deletion fails, to allow cleanup of orphaned database records
        _logger.LogDebug("Deleting artifact file from storage for project {ProjectKey} version {Version}", projectKey, version.ToString());
        OneOf<Success, BadRequestError, NotFoundError> fileDeleteResult = await _fileStorage.DeleteArtifactAsync(projectKey, version.ToString(), cancellationToken);
        if (!fileDeleteResult.TryPickT0(out Success _, out OneOf<BadRequestError, NotFoundError> fileDeleteError))
        {
            string errorMessage = fileDeleteError.Match(
                badRequest => badRequest.Message,
                notFound => notFound.Message
            );
            _logger.LogWarning("Failed to delete artifact file for project {ProjectKey} version {Version}: {Message}", projectKey, version.ToString(), errorMessage);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted artifact for project {ProjectKey} version {Version}", projectKey, version.ToString());
        return new Success();
    }

    public async Task<OneOf<Artifact, NotFoundError, ConflictError>> NewArtifactAsync(Artifact artifact, Stream stream, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating new artifact for project {ProjectId} version {Version} filename {FileName}", artifact.ProjectId, artifact.Version.ToString(), artifact.FileName);
        
        // Find the project by key (from the controller context)
        Database.Models.Project? project = await _dbContext.Projects
            .FirstOrDefaultAsync(p => p.ProjectId == artifact.ProjectId, cancellationToken);

        if (project is null)
        {
            _logger.LogInformation("Project not found for artifact creation {ProjectId}", artifact.ProjectId);
            return new NotFoundError($"Project with ID '{artifact.ProjectId}' not found.");
        }

        // Check if artifact with same version already exists
        bool artifactExists = await _dbContext.Artifacts
            .AnyAsync(a => a.ProjectId == project.ProjectId && a.Version == artifact.Version.ToString(), cancellationToken);

        if (artifactExists)
        {
            _logger.LogWarning("Artifact version conflict for project {ProjectKey} version {Version}", project.Key, artifact.Version.ToString());
            return new ConflictError($"Artifact with version '{artifact.Version}' already exists in project with key '{project.Key}'.");
        }

        // Save to file storage first
        _logger.LogDebug("Saving new artifact file to storage for project {ProjectKey} version {Version}", project.Key, artifact.Version.ToString());
        OneOf<SaveArtifactSuccess, BadRequestError> saveResult = await _fileStorage.SaveArtifactAsync(
            project.Key,
            artifact.Version.ToString(),
            stream,
            cancellationToken);

        if (!saveResult.TryPickT0(out SaveArtifactSuccess saveSuccess, out BadRequestError badRequestError))
        {
            _logger.LogWarning("Failed to save artifact file for project {ProjectKey} version {Version}: {Message}", project.Key, artifact.Version.ToString(), badRequestError.Message);
            return new ConflictError($"Failed to save artifact file: {badRequestError.Message}");
        }

        // Create new artifact in database
        Database.Models.Artifact newArtifact = new()
        {
            ProjectId = project.ProjectId,
            Version = artifact.Version.ToString(),
            FileName = artifact.FileName,
            FileSizeBytes = artifact.FileSizeBytes,
            Sha256Hash = saveSuccess.Sha256Hash,
            Timestamp = DateTime.UtcNow,
            Retained = artifact.Retained,
            Locked = artifact.Locked
        };

        _dbContext.Artifacts.Add(newArtifact);
        
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Created new artifact for project {ProjectKey} version {Version} filename {FileName}", project.Key, artifact.Version.ToString(), artifact.FileName);
        }
        catch (Exception ex)
        {
            // Compensation: If database save fails, remove the orphaned file
            _logger.LogError(ex, "Database save failed for new artifact project {ProjectKey} version {Version}, attempting file cleanup", project.Key, artifact.Version.ToString());
            _ = await _fileStorage.DeleteArtifactAsync(project.Key, artifact.Version.ToString(), cancellationToken);
            throw; // Re-throw the original exception
        }

        return newArtifact.ToDomainModel();
    }
}
