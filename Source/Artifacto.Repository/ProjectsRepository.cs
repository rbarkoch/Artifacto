using Artifacto.Database;
using Artifacto.Database.Models;
using Artifacto.FileStorage;
using Artifacto.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using OneOf;
using OneOf.Types;

using Project = Artifacto.Models.Project;

namespace Artifacto.Repository;

/// <summary>
/// Provides repository operations for managing projects in the Artifacto system.
/// This class handles CRUD operations for projects and coordinates between database and file storage.
/// </summary>
public class ProjectsRepository
{
    private readonly ArtifactoDbContext _dbContext;
    private readonly IArtifactoFileStorage _fileStorage;
    private readonly ILogger<ProjectsRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectsRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context for data operations.</param>
    /// <param name="fileStorage">The file storage service for managing project directories.</param>
    /// <param name="logger">The logger for recording operations.</param>
    public ProjectsRepository(ILogger<ProjectsRepository> logger, ArtifactoDbContext dbContext, IArtifactoFileStorage fileStorage)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a project by its unique key.
    /// </summary>
    /// <param name="projectKey">The unique key of the project to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task"/> containing either the found project or a not found error.
    /// </returns>
    public async Task<OneOf<Project, NotFoundError>> GetProjectAsync(string projectKey, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting project {ProjectKey}", projectKey);
        
        var projectWithCount = await _dbContext.Projects
            .Where(p => p.Key == projectKey)
            .GroupJoin(
                _dbContext.Artifacts,
                project => project.ProjectId,
                artifact => artifact.ProjectId,
                (project, artifacts) => new
                {
                    Project = project,
                    ArtifactCount = artifacts.Count()
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (projectWithCount is null)
        {
            _logger.LogInformation("Project not found {ProjectKey}", projectKey);
            return new NotFoundError($"Project with key '{projectKey}' not found.");
        }

        string? latestStableVersion = await GetLatestStableVersionAsync(projectKey, cancellationToken);
        string? latestVersion = await GetLatestVersionAsync(projectKey, cancellationToken);
        DateTime? latestStableVersionDate = await GetLatestStableVersionUploadDateAsync(projectKey, cancellationToken);
        DateTime? latestVersionDate = await GetLatestVersionUploadDateAsync(projectKey, cancellationToken);

        _logger.LogDebug("Found project {ProjectKey} with {ArtifactCount} artifacts", projectKey, projectWithCount.ArtifactCount);
        return new Project(
            ProjectId: projectWithCount.Project.ProjectId,
            Key: projectWithCount.Project.Key,
            Name: projectWithCount.Project.Name,
            Description: projectWithCount.Project.Description,
            ArtifactCount: projectWithCount.ArtifactCount,
            LatestStableVersion: latestStableVersion,
            LatestVersion: latestVersion,
            LatestStableVersionUploadDate: latestStableVersionDate,
            LatestVersionUploadDate: latestVersionDate
        );
    }

    /// <summary>
    /// Gets the latest stable (non-prerelease) version for a project.
    /// </summary>
    /// <param name="projectKey">The unique key of the project.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The latest stable version string, or null if no stable versions exist.</returns>
    private async Task<string?> GetLatestStableVersionAsync(string projectKey, CancellationToken cancellationToken = default)
    {
        Database.Models.Project? project = await _dbContext.Projects.FirstOrDefaultAsync(p => p.Key == projectKey, cancellationToken);
        if (project == null)
        {
            return null;
        }

        List<string> versions = await _dbContext.Artifacts
            .Where(a => a.ProjectId == project.ProjectId)
            .Select(a => a.Version)
            .ToListAsync(cancellationToken);

        List<Models.Version> validVersions = [];
        foreach (string versionString in versions)
        {
            if (Models.Version.TryParse(versionString, out Models.Version version) && !version.IsPreRelease)
            {
                validVersions.Add(version);
            }
        }

        return validVersions.Count == 0 ? null : validVersions.Max().ToString();
    }

    /// <summary>
    /// Gets the latest version (including prerelease) for a project.
    /// </summary>
    /// <param name="projectKey">The unique key of the project.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The latest version string, or null if no versions exist.</returns>
    private async Task<string?> GetLatestVersionAsync(string projectKey, CancellationToken cancellationToken = default)
    {
        Database.Models.Project? project = await _dbContext.Projects.FirstOrDefaultAsync(p => p.Key == projectKey, cancellationToken);
        if (project == null)
        {
            return null;
        }

        List<string> versions = await _dbContext.Artifacts
            .Where(a => a.ProjectId == project.ProjectId)
            .Select(a => a.Version)
            .ToListAsync(cancellationToken);

        List<Models.Version> validVersions = [];
        foreach (string versionString in versions)
        {
            if (Models.Version.TryParse(versionString, out Models.Version version))
            {
                validVersions.Add(version);
            }
        }

        return validVersions.Count == 0 ? null : validVersions.Max().ToString();
    }

    /// <summary>
    /// Gets the upload date of the latest stable (non-prerelease) version for a project.
    /// </summary>
    /// <param name="projectKey">The unique key of the project.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The upload date of the latest stable version, or null if no stable versions exist.</returns>
    private async Task<DateTime?> GetLatestStableVersionUploadDateAsync(string projectKey, CancellationToken cancellationToken = default)
    {
        Database.Models.Project? project = await _dbContext.Projects.FirstOrDefaultAsync(p => p.Key == projectKey, cancellationToken);
        if (project == null)
        {
            return null;
        }

        List<Database.Models.Artifact> artifacts = await _dbContext.Artifacts
            .Where(a => a.ProjectId == project.ProjectId)
            .ToListAsync(cancellationToken);

        Database.Models.Artifact? latestStableArtifact = null;
        Models.Version? latestStableVersion = null;

        foreach (Database.Models.Artifact artifact in artifacts)
        {
            if (Models.Version.TryParse(artifact.Version, out Models.Version version) && !version.IsPreRelease)
            {
                if (latestStableVersion == null || version.CompareTo(latestStableVersion) > 0)
                {
                    latestStableVersion = version;
                    latestStableArtifact = artifact;
                }
            }
        }

        return latestStableArtifact?.Timestamp;
    }

    /// <summary>
    /// Gets the upload date of the latest version (including prerelease) for a project.
    /// </summary>
    /// <param name="projectKey">The unique key of the project.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The upload date of the latest version, or null if no versions exist.</returns>
    private async Task<DateTime?> GetLatestVersionUploadDateAsync(string projectKey, CancellationToken cancellationToken = default)
    {
        Database.Models.Project? project = await _dbContext.Projects.FirstOrDefaultAsync(p => p.Key == projectKey, cancellationToken);
        if (project == null)
        {
            return null;
        }

        List<Database.Models.Artifact> artifacts = await _dbContext.Artifacts
            .Where(a => a.ProjectId == project.ProjectId)
            .ToListAsync(cancellationToken);

        Database.Models.Artifact? latestArtifact = null;
        Models.Version? latestVersion = null;

        foreach (Database.Models.Artifact artifact in artifacts)
        {
            if (Models.Version.TryParse(artifact.Version, out Models.Version version))
            {
                if (latestVersion == null || version.CompareTo(latestVersion) > 0)
                {
                    latestVersion = version;
                    latestArtifact = artifact;
                }
            }
        }

        return latestArtifact?.Timestamp;
    }

    /// <summary>
    /// Retrieves all projects with their artifact counts.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task"/> containing a list of all projects in the system.
    /// </returns>
    public async Task<List<Project>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting all projects");
        
        var projectsWithCounts = await _dbContext.Projects
            .GroupJoin(
                _dbContext.Artifacts,
                project => project.ProjectId,
                artifact => artifact.ProjectId,
                (project, artifacts) => new
                {
                    Project = project,
                    ArtifactCount = artifacts.Count()
                })
            .ToListAsync(cancellationToken);

        List<Project> result = [];
        foreach (var item in projectsWithCounts)
        {
            string? latestStableVersion = await GetLatestStableVersionAsync(item.Project.Key, cancellationToken);
            string? latestVersion = await GetLatestVersionAsync(item.Project.Key, cancellationToken);
            DateTime? latestStableVersionDate = await GetLatestStableVersionUploadDateAsync(item.Project.Key, cancellationToken);
            DateTime? latestVersionDate = await GetLatestVersionUploadDateAsync(item.Project.Key, cancellationToken);
            
            result.Add(new Project(
                ProjectId: item.Project.ProjectId,
                Key: item.Project.Key,
                Name: item.Project.Name,
                Description: item.Project.Description,
                ArtifactCount: item.ArtifactCount,
                LatestStableVersion: latestStableVersion,
                LatestVersion: latestVersion,
                LatestStableVersionUploadDate: latestStableVersionDate,
                LatestVersionUploadDate: latestVersionDate
            ));
        }

        _logger.LogDebug("Found {Count} projects", result.Count);
        return result;
    }

    /// <summary>
    /// Updates an existing project's metadata and handles key changes.
    /// If the project key is changed, the corresponding directory in file storage is renamed.
    /// </summary>
    /// <param name="project">The project with updated information.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task"/> containing the result of the update operation.
    /// </returns>
    public async Task<OneOf<Success, BadRequestError, NotFoundError, ConflictError>> UpdateProjectAsync(Project project, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating project {ProjectId} key {ProjectKey} name {Name}", project.ProjectId, project.Key, project.Name);
        
        Database.Models.Project? existingProject = await _dbContext.Projects.FindAsync(project.ProjectId, cancellationToken);
        if (existingProject is null)
        {
            _logger.LogInformation("Project not found for update {ProjectId}", project.ProjectId);
            return new NotFoundError($"Project with ID '{project.ProjectId}' not found.");
        }

        if (existingProject.Key != project.Key)
        {
            _logger.LogDebug("Project key changing from {OldKey} to {NewKey}", existingProject.Key, project.Key);
            if (await _dbContext.Projects.AnyAsync(p => p.Key == project.Key, cancellationToken))
            {
                _logger.LogWarning("Project key conflict updating project {ProjectId} to key {ProjectKey}", project.ProjectId, project.Key);
                return new ConflictError($"Project with key '{project.Key}' already exists.");
            }

            // Rename the project directory in file storage
            _logger.LogDebug("Renaming project directory from {OldKey} to {NewKey}", existingProject.Key, project.Key);
            OneOf<Success, BadRequestError, NotFoundError> renameResult = await _fileStorage.RenameProject(existingProject.Key, project.Key, cancellationToken);
            if (!renameResult.TryPickT0(out Success _, out OneOf<BadRequestError, NotFoundError> renameError))
            {
                _logger.LogWarning("Failed to rename project directory from {OldKey} to {NewKey}", existingProject.Key, project.Key);
                return renameError.Match<OneOf<Success, BadRequestError, NotFoundError, ConflictError>>(
                    badRequest => badRequest,
                    notFound => notFound
                );
            }
        }

        // Validate and update the project
        existingProject.Key = project.Key;
        existingProject.Name = project.Name;
        existingProject.Description = project.Description;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Updated project {ProjectId} key {ProjectKey} name {Name}", project.ProjectId, project.Key, project.Name);
        return new Success();
    }

    /// <summary>
    /// Deletes a project and all its associated artifacts from both database and file storage.
    /// </summary>
    /// <param name="projectKey">The unique key of the project to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task"/> containing the result of the delete operation.
    /// </returns>
    public async Task<OneOf<Success, NotFoundError>> DeleteProjectAsync(string projectKey, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting project {ProjectKey}", projectKey);
        
        Database.Models.Project? existingProject = await _dbContext.Projects.FindByKeyAsync(projectKey, cancellationToken);
        if (existingProject is null)
        {
            _logger.LogInformation("Project not found for deletion {ProjectKey}", projectKey);
            return new NotFoundError($"Project with key '{projectKey}' not found.");
        }

        _dbContext.Projects.Remove(existingProject);

        // Delete the project directory from file storage
        _logger.LogDebug("Deleting project directory for {ProjectKey}", projectKey);
        OneOf<Success, BadRequestError, NotFoundError> deleteResult = await _fileStorage.DeleteProjectAsync(projectKey, cancellationToken);
        if (!deleteResult.TryPickT0(out Success _, out OneOf<BadRequestError, NotFoundError> deleteError))
        {
            string errorMessage = deleteError.Match(
                badRequest => badRequest.Message,
                notFound => notFound.Message
            );
            _logger.LogWarning("Failed to delete project directory for {ProjectKey}: {Message}", projectKey, errorMessage);
        }
        // Note: We continue even if file deletion fails, to allow cleanup of orphaned database records

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted project {ProjectKey}", projectKey);
        return new Success();
    }

    /// <summary>
    /// Creates a new project in both database and file storage.
    /// </summary>
    /// <param name="project">The project to create.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task"/> containing either the created project or a conflict error if the project already exists.
    /// </returns>
    public async Task<OneOf<Project, ConflictError>> NewProjectAsync(Project project, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating new project {ProjectKey} name {Name}", project.Key, project.Name);
        
        if (await _dbContext.Projects.AnyAsync(p => p.Key == project.Key, cancellationToken))
        {
            _logger.LogWarning("Project key conflict creating project {ProjectKey}", project.Key);
            return new ConflictError($"Project with key '{project.Key}' already exists.");
        }

        // Create the project directory in file storage
        _logger.LogDebug("Creating project directory for {ProjectKey}", project.Key);
        OneOf<Success, BadRequestError, ConflictError> createResult = await _fileStorage.NewProject(project.Key, cancellationToken);
        if (!createResult.TryPickT0(out Success _, out OneOf<BadRequestError, ConflictError> createError))
        {
            _logger.LogWarning("Failed to create project directory for {ProjectKey}", project.Key);
            return createError.Match<OneOf<Project, ConflictError>>(
                badRequest => new ConflictError($"Failed to create project directory: {badRequest.Message}"),
                conflict => conflict
            );
        }

        Database.Models.Project newProject = new()
        {
            Key = project.Key,
            Name = project.Name,
            Description = project.Description
        };

        _dbContext.Projects.Add(newProject);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created new project {ProjectKey} name {Name}", project.Key, project.Name);
        return newProject.ToDomainModel();
    }
}
